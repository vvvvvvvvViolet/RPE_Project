using System.IO.Compression;
using System.Text;
using System.Xml;
using RPEReader.Core.Abstractions;
using RPEReader.Core.Detection;
using RPEReader.Core.Models;
using RPEReader.Core.Parsing.OpcRouter4;

namespace RPEReader.Core.Parsing;

/// <summary>
/// Reads .rpe files produced by Inray OPC Router 4: a ZIP container holding a
/// single <c>OpcRouter4.xml</c> export document.
/// </summary>
public sealed class OpcRouter4Parser : IRpeParser
{
    public string FormatId => "opcrouter4.zip";

    public string DisplayName => "Inray OPC Router 4 project export (ZIP + OpcRouter4.xml)";

    public int Priority => 100;

    public bool CanParse(RpeProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        return probe.IsZipContainer && probe.HasEntry(OpcRouter4Schema.ContainerEntryName);
    }

    public ParseResult Parse(Stream stream, RpeParseContext context)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(context);

        var limits = context.Limits;
        var diagnostics = new List<ParseDiagnostic>();

        byte[] xmlBytes;
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            var inspection = SafeZipReader.Inspect(archive, limits);
            diagnostics.AddRange(inspection.Diagnostics);

            var entry = archive.GetEntry(OpcRouter4Schema.ContainerEntryName)
                        ?? archive.Entries.FirstOrDefault(e =>
                            string.Equals(e.Name, OpcRouter4Schema.ContainerEntryName, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return ParseResult
                    .Failed($"The container does not hold '{OpcRouter4Schema.ContainerEntryName}'.")
                    .AddRange(diagnostics);
            }

            if (!SafeZipReader.IsSafeEntryName(entry.FullName))
            {
                return ParseResult
                    .Failed("The export entry name is not a safe relative path; the file was rejected.")
                    .AddRange(diagnostics);
            }

            foreach (var other in archive.Entries.Where(e => e != entry))
            {
                diagnostics.Add(ParseDiagnostic.Info(
                    $"Additional container entry '{other.FullName}' ({RpeFileInfo.FormatSize(other.Length)}) was listed but not parsed."));
            }

            xmlBytes = SafeZipReader.ReadEntry(entry, limits.MaxEntryUncompressedBytes, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            return ParseResult.Failed($"The container is damaged or exceeds a safety limit: {ex.Message}").AddRange(diagnostics);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ObjectDisposedException)
        {
            return ParseResult.Failed($"The container could not be read: {ex.Message}").AddRange(diagnostics);
        }

        return ParseXml(xmlBytes, context, diagnostics);
    }

    private ParseResult ParseXml(byte[] xmlBytes, RpeParseContext context, List<ParseDiagnostic> diagnostics)
    {
        var limits = context.Limits;

        XmlDocument document;
        try
        {
            // DTDs are prohibited and no resolver is supplied, so external
            // entities and entity-expansion attacks cannot reach the parser.
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = false,
                IgnoreWhitespace = true,
                MaxCharactersInDocument = limits.MaxEntryUncompressedBytes,
                MaxCharactersFromEntities = 0,
                CloseInput = true
            };

            using var memory = new MemoryStream(xmlBytes, writable: false);
            using var reader = XmlReader.Create(memory, settings);

            document = new XmlDocument { XmlResolver = null };
            document.Load(reader);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (XmlException ex)
        {
            return ParseResult
                .Failed($"The export XML is not well formed (line {ex.LineNumber}, position {ex.LinePosition}): {ex.Message}")
                .AddRange(diagnostics);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or OutOfMemoryException)
        {
            return ParseResult.Failed($"The export XML could not be read: {ex.Message}").AddRange(diagnostics);
        }

        var root = document.DocumentElement;
        if (root is null || !string.Equals(root.LocalName, OpcRouter4Schema.RootElement, StringComparison.Ordinal))
        {
            return ParseResult
                .Failed($"Expected a <{OpcRouter4Schema.RootElement}> root element but found <{root?.LocalName ?? "nothing"}>.")
                .AddRange(diagnostics);
        }

        var depth = MeasureDepth(root, limits.MaxXmlDepth);
        if (depth >= limits.MaxXmlDepth)
        {
            diagnostics.Add(ParseDiagnostic.Warning(
                $"Element nesting reaches the {limits.MaxXmlDepth}-level limit; deeper content was not expanded."));
        }

        var mapper = new XmlToNodeMapper(limits, diagnostics, context.CancellationToken);
        var treeRoot = new RpeNode(
            $"OPC Router 4 export — {root.GetAttribute("DisplayName")}".TrimEnd(' ', '—'),
            "Document");

        var summary = new List<RpeField>();
        foreach (var attribute in OpcRouter4Schema.RootAttributes)
        {
            var value = root.GetAttribute(attribute);
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var note = attribute switch
            {
                "EncryptedFieldHandling" when value.Equals("Skip", StringComparison.OrdinalIgnoreCase)
                    => "Encrypted fields were omitted by the exporter, so no secrets are present.",
                "FileVersion" => "Export schema revision.",
                "Version" => "OPC Router version that wrote the export.",
                _ => null
            };

            summary.Add(new RpeField(attribute, value, note: note));
            treeRoot.AddField(attribute, value, note: note);
        }

        foreach (var sectionName in OpcRouter4Schema.TopLevelSections)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var section = root.ChildNodes
                .OfType<XmlElement>()
                .FirstOrDefault(e => string.Equals(e.LocalName, sectionName, StringComparison.Ordinal));

            if (section is null)
            {
                continue;
            }

            var childCount = section.ChildNodes.OfType<XmlElement>().Count();
            var sectionNode = new RpeNode(sectionName, "Section")
            {
                Value = childCount == 0 ? "empty" : $"{childCount:N0} item{(childCount == 1 ? string.Empty : "s")}"
            };

            foreach (var child in section.ChildNodes.OfType<XmlElement>())
            {
                sectionNode.AddChild(mapper.Map(child));
            }

            treeRoot.AddChild(sectionNode);
        }

        // Any section the exporter added that this reader does not know about is
        // still surfaced rather than silently dropped.
        foreach (var unknown in root.ChildNodes.OfType<XmlElement>()
                     .Where(e => !OpcRouter4Schema.TopLevelSections.Contains(e.LocalName, StringComparer.Ordinal)))
        {
            diagnostics.Add(ParseDiagnostic.Info(
                $"Unrecognised top-level section <{unknown.LocalName}> was read with the generic element mapping."));
            treeRoot.AddChild(mapper.Map(unknown, "Section (unrecognised)"));
        }

        AddDerivedSummary(root, summary);

        var rawText = DecodeRawText(xmlBytes, limits, out var rawTruncated);

        var doc = new RpeDocument
        {
            FormatId = FormatId,
            FormatDisplayName = DisplayName,
            Root = treeRoot,
            Summary = summary,
            RawText = rawText,
            RawTextTruncated = rawTruncated,
            Incomplete = mapper.LimitHit || diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning)
        };

        return ParseResult.Ok(doc).AddRange(diagnostics);
    }

    /// <summary>Adds the counts a user of OPC Router would expect to see first.</summary>
    private static void AddDerivedSummary(XmlElement root, List<RpeField> summary)
    {
        var connections = root.GetElementsByTagName("Connection").Count;
        var groups = root.GetElementsByTagName("ConnectionGroup").Count;
        var plugins = root.ChildNodes.OfType<XmlElement>()
            .FirstOrDefault(e => e.LocalName == "Plugins")?.ChildNodes.OfType<XmlElement>().Count() ?? 0;
        var lines = root.GetElementsByTagName("ConnectionLine").Count;
        var templateVariables = root.GetElementsByTagName("TemplateVariable").Count;

        summary.Add(new RpeField("Plug-in configurations", plugins.ToString("N0")));
        summary.Add(new RpeField("Connection groups", groups.ToString("N0")));
        summary.Add(new RpeField("Connections", connections.ToString("N0")));
        summary.Add(new RpeField("Connection lines", lines.ToString("N0"), note: "Wires between transfer-object items."));
        summary.Add(new RpeField("Template variables", templateVariables.ToString("N0")));
    }

    private static string DecodeRawText(byte[] xmlBytes, RpeLimits limits, out bool truncated)
    {
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            .GetString(xmlBytes);

        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        truncated = text.Length > limits.MaxRawTextChars;
        return truncated ? text[..limits.MaxRawTextChars] : text;
    }

    private static int MeasureDepth(XmlElement element, int cap)
    {
        var max = 1;
        var stack = new Stack<(XmlElement Element, int Depth)>();
        stack.Push((element, 1));

        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();
            if (depth > max)
            {
                max = depth;
            }

            if (depth >= cap)
            {
                return cap;
            }

            foreach (var child in current.ChildNodes.OfType<XmlElement>())
            {
                stack.Push((child, depth + 1));
            }
        }

        return max;
    }
}
