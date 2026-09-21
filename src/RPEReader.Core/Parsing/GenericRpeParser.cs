using System.IO.Compression;
using System.Text;
using RPEReader.Core.Abstractions;
using RPEReader.Core.Detection;
using RPEReader.Core.Models;
using RPEReader.Core.Services;

namespace RPEReader.Core.Parsing;

/// <summary>
/// The fallback used for any .rpe whose layout this build does not recognise.
/// It makes no claims about structure: it reports what can be measured —
/// signature, encoding, entropy, container listing, printable strings — so an
/// unknown revision is still inspectable rather than rejected.
/// </summary>
public sealed class GenericRpeParser : IRpeParser
{
    /// <summary>Bytes sampled for entropy, encoding and string extraction.</summary>
    public const int InspectionSampleBytes = 4 * 1024 * 1024;

    public string FormatId => "generic";

    public string DisplayName => "Generic RPE inspector (structure not recognised)";

    public int Priority => int.MinValue;

    /// <summary>Accepts everything; it is the last parser the registry tries.</summary>
    public bool CanParse(RpeProbe probe) => true;

    public ParseResult Parse(Stream stream, RpeParseContext context)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(context);

        var probe = context.Probe;
        var limits = context.Limits;
        var diagnostics = new List<ParseDiagnostic>
        {
            ParseDiagnostic.Info(
                "No structural parser matched this file. The generic inspector reports measured facts only; " +
                "no field layout has been inferred.")
        };

        var sampleLength = (int)Math.Min(InspectionSampleBytes, probe.FileSizeBytes);
        var sample = new byte[sampleLength];
        if (sampleLength > 0)
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.ReadExactly(sample, 0, sampleLength);
        }

        var root = new RpeNode("Generic RPE inspection", "Document");
        var summary = new List<RpeField>();

        // --- Signature -------------------------------------------------------
        var signatureNode = new RpeNode("File signature", "Section");
        var magic = DescribeMagic(sample);
        signatureNode.AddField("Detected container", magic);
        signatureNode.AddField("First 16 bytes (hex)", HexDumpService.ToHexString(sample.AsSpan(0, Math.Min(16, sample.Length))));
        signatureNode.AddField("Looks like XML", probe.LooksLikeXml ? "yes" : "no");
        summary.Add(new RpeField("Detected container", magic));
        root.AddChild(signatureNode);

        // --- Encoding --------------------------------------------------------
        var encoding = EncodingSniffer.Sniff(sample);
        var encodingNode = new RpeNode("Encoding", "Section");
        encodingNode.AddField("Best guess", encoding.Name, note: encoding.Evidence);
        summary.Add(new RpeField("Encoding (guess)", encoding.Name, note: encoding.Evidence));
        root.AddChild(encodingNode);

        // --- Entropy ---------------------------------------------------------
        var entropy = EntropyService.Calculate(sample);
        var entropyNode = new RpeNode("Entropy", "Section");
        entropyNode.AddField("Shannon entropy", $"{entropy:F3} bits/byte", note: EntropyService.Describe(entropy));
        entropyNode.AddField("Sample size", RpeFileInfo.FormatSize(sampleLength),
            note: sampleLength < probe.FileSizeBytes ? "Measured over the leading sample, not the whole file." : null);
        summary.Add(new RpeField("Entropy", $"{entropy:F3} bits/byte", note: EntropyService.Describe(entropy)));
        root.AddChild(entropyNode);

        // --- Container listing ----------------------------------------------
        if (probe.IsZipContainer)
        {
            root.AddChild(BuildContainerNode(stream, limits, diagnostics));
        }

        // --- Strings ---------------------------------------------------------
        var strings = StringsExtractor.Extract(sample, minimumLength: 4, maxResults: Math.Min(limits.MaxDisplayRows, 20_000));
        var stringsNode = new RpeNode("Printable strings", "Section")
        {
            Value = $"{strings.Count:N0} found"
        };

        foreach (var s in strings.Take(limits.MaxDisplayRows))
        {
            stringsNode.AddField($"0x{s.Offset:X8}", s.Value, s.Kind);
            if (stringsNode.Fields.Count >= limits.MaxFieldsPerNode)
            {
                stringsNode.Truncated = true;
                diagnostics.Add(ParseDiagnostic.Info(
                    $"Only the first {limits.MaxFieldsPerNode:N0} extracted strings are listed."));
                break;
            }
        }

        root.AddChild(stringsNode);

        var rawText = BuildRawText(sample, encoding.Encoding, limits, out var truncated);

        var document = new RpeDocument
        {
            FormatId = FormatId,
            FormatDisplayName = DisplayName,
            Root = root,
            Summary = summary,
            RawText = rawText,
            RawTextTruncated = truncated || sampleLength < probe.FileSizeBytes,
            Incomplete = true
        };

        return ParseResult.Ok(document).AddRange(diagnostics);
    }

    private static RpeNode BuildContainerNode(Stream stream, RpeLimits limits, List<ParseDiagnostic> diagnostics)
    {
        var node = new RpeNode("ZIP container", "Section");
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var inspection = SafeZipReader.Inspect(archive, limits);
            diagnostics.AddRange(inspection.Diagnostics);

            node.Value = $"{inspection.Entries.Count:N0} entries";
            node.AddField("Total uncompressed", RpeFileInfo.FormatSize(inspection.TotalUncompressedBytes));
            node.AddField("Passed safety checks", inspection.Accepted ? "yes" : "no");

            foreach (var entry in inspection.Entries)
            {
                node.AddField(
                    entry.Name,
                    $"{RpeFileInfo.FormatSize(entry.Length)} (stored {RpeFileInfo.FormatSize(entry.CompressedLength)})");
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException)
        {
            node.AddField("Error", "The container index could not be read.");
            diagnostics.Add(ParseDiagnostic.Warning($"ZIP directory unreadable: {ex.Message}"));
        }

        return node;
    }

    private static string BuildRawText(byte[] sample, Encoding encoding, RpeLimits limits, out bool truncated)
    {
        string text;
        try
        {
            text = encoding.GetString(sample);
        }
        catch (ArgumentException)
        {
            text = new UTF8Encoding(false).GetString(sample);
        }

        // Control characters would render as boxes; replace them so the viewer
        // shows something legible.
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c is '\r' or '\n' or '\t' || !char.IsControl(c) ? c : '.');
        }

        text = builder.ToString();
        truncated = text.Length > limits.MaxRawTextChars;
        return truncated ? text[..limits.MaxRawTextChars] : text;
    }

    private static string DescribeMagic(ReadOnlySpan<byte> sample)
    {
        if (FileSignatures.StartsWith(sample, FileSignatures.ZipLocalFileHeader))
        {
            return "ZIP archive (PK\\x03\\x04)";
        }

        if (FileSignatures.StartsWith(sample, FileSignatures.ZipEmptyArchive))
        {
            return "empty ZIP archive (PK\\x05\\x06)";
        }

        if (FileSignatures.StartsWith(sample, FileSignatures.ZipSpanned))
        {
            return "spanned ZIP archive (PK\\x07\\x08)";
        }

        if (FileSignatures.StartsWith(sample, FileSignatures.GZip))
        {
            return "gzip stream (1F 8B)";
        }

        if (FileSignatures.StartsWith(sample, FileSignatures.SQLite))
        {
            return "SQLite database";
        }

        if (FileSignatures.StartsWith(sample, FileSignatures.MzExecutable))
        {
            return "DOS/Windows executable (MZ) — not opened or executed";
        }

        if (FileSignatures.StartsWith(sample, FileSignatures.Utf8Bom))
        {
            return "text with UTF-8 BOM";
        }

        return "no recognised signature";
    }
}
