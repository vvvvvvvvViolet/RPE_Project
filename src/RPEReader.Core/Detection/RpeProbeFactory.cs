using System.IO.Compression;
using System.Text;
using RPEReader.Core.Abstractions;
using RPEReader.Core.Models;

namespace RPEReader.Core.Detection;

/// <summary>
/// Builds the <see cref="RpeProbe"/> handed to every parser. Reads at most a
/// few kilobytes plus, for ZIP containers, the central directory listing.
/// </summary>
public static class RpeProbeFactory
{
    public const int HeaderSampleBytes = 4096;

    public static RpeProbe Create(Stream stream, string fileName, long fileSizeBytes, RpeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(limits);

        var headerLength = (int)Math.Min(HeaderSampleBytes, Math.Max(0, fileSizeBytes));
        var header = new byte[headerLength];
        if (headerLength > 0)
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.ReadExactly(header, 0, headerLength);
        }

        var isZip = FileSignatures.StartsWith(header, FileSignatures.ZipLocalFileHeader)
                    || FileSignatures.StartsWith(header, FileSignatures.ZipEmptyArchive)
                    || FileSignatures.StartsWith(header, FileSignatures.ZipSpanned);

        var entries = isZip ? ListEntries(stream, limits) : Array.Empty<string>();
        var headerText = DecodeHeaderText(header);

        return new RpeProbe
        {
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            Header = header,
            IsZipContainer = isZip,
            ContainerEntryNames = entries,
            LooksLikeXml = LooksLikeXml(headerText),
            HeaderText = headerText
        };
    }

    private static IReadOnlyList<string> ListEntries(Stream stream, RpeLimits limits)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var names = new List<string>();
            foreach (var entry in archive.Entries)
            {
                names.Add(entry.FullName);
                if (names.Count >= limits.MaxEntryCount)
                {
                    break;
                }
            }

            return names;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ObjectDisposedException)
        {
            // A damaged or truncated container is still a legitimate file to
            // inspect with the generic parser; detection just learns nothing.
            return Array.Empty<string>();
        }
    }

    private static string DecodeHeaderText(byte[] header)
    {
        if (header.Length == 0)
        {
            return string.Empty;
        }

        if (FileSignatures.StartsWith(header, FileSignatures.Utf8Bom))
        {
            return new UTF8Encoding(false).GetString(header, 3, header.Length - 3);
        }

        if (FileSignatures.StartsWith(header, FileSignatures.Utf16LeBom))
        {
            return Encoding.Unicode.GetString(header, 2, header.Length - 2);
        }

        if (FileSignatures.StartsWith(header, FileSignatures.Utf16BeBom))
        {
            return Encoding.BigEndianUnicode.GetString(header, 2, header.Length - 2);
        }

        return new UTF8Encoding(false).GetString(header);
    }

    private static bool LooksLikeXml(string headerText)
    {
        var trimmed = headerText.TrimStart('﻿', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
               || (trimmed.StartsWith('<') && trimmed.Length > 1 && (char.IsLetter(trimmed[1]) || trimmed[1] == '!'));
    }
}
