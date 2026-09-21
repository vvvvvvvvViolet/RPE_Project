using System.IO.Compression;
using RPEReader.Core.Models;

namespace RPEReader.Core.Detection;

/// <summary>Description of one entry after the container has been vetted.</summary>
public sealed record SafeZipEntry(string Name, long CompressedLength, long Length);

/// <summary>Result of inspecting a ZIP container against the configured limits.</summary>
public sealed class SafeZipInspection
{
    public required bool Accepted { get; init; }

    public required IReadOnlyList<SafeZipEntry> Entries { get; init; }

    public required IReadOnlyList<ParseDiagnostic> Diagnostics { get; init; }

    public long TotalUncompressedBytes { get; init; }
}

/// <summary>
/// ZIP handling with the checks a parser must not forget: entry-count and size
/// caps, a compression-ratio ceiling to stop decompression bombs, and rejection
/// of entry names that attempt path traversal.
/// </summary>
/// <remarks>
/// Entries are only ever read into memory under a cap; nothing is written to
/// disk, so traversal-shaped names are rejected as a defence-in-depth signal
/// rather than as the only barrier.
/// </remarks>
public static class SafeZipReader
{
    public static SafeZipInspection Inspect(ZipArchive archive, RpeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(limits);

        var diagnostics = new List<ParseDiagnostic>();
        var entries = new List<SafeZipEntry>();
        long totalUncompressed = 0;
        long totalCompressed = 0;
        var accepted = true;

        var count = 0;
        foreach (var entry in archive.Entries)
        {
            count++;
            if (count > limits.MaxEntryCount)
            {
                diagnostics.Add(ParseDiagnostic.Warning(
                    $"Container holds more than the permitted {limits.MaxEntryCount:N0} entries; the remainder was ignored."));
                accepted = false;
                break;
            }

            if (!IsSafeEntryName(entry.FullName))
            {
                diagnostics.Add(ParseDiagnostic.Warning(
                    $"Entry #{count} was skipped: its name is absolute or contains a parent-directory segment."));
                continue;
            }

            if (entry.Length > limits.MaxEntryUncompressedBytes)
            {
                diagnostics.Add(ParseDiagnostic.Warning(
                    $"Entry '{entry.FullName}' declares {RpeFileInfo.FormatSize(entry.Length)} uncompressed, above the " +
                    $"{RpeFileInfo.FormatSize(limits.MaxEntryUncompressedBytes)} per-entry limit; it was skipped."));
                accepted = false;
                continue;
            }

            totalUncompressed += entry.Length;
            totalCompressed += entry.CompressedLength;

            if (totalUncompressed > limits.MaxTotalUncompressedBytes)
            {
                diagnostics.Add(ParseDiagnostic.Warning(
                    $"Container expands past the {RpeFileInfo.FormatSize(limits.MaxTotalUncompressedBytes)} total limit; " +
                    "reading stopped early."));
                accepted = false;
                break;
            }

            entries.Add(new SafeZipEntry(entry.FullName, entry.CompressedLength, entry.Length));
        }

        if (totalCompressed > 0 && totalUncompressed / Math.Max(1, totalCompressed) > limits.MaxCompressionRatio)
        {
            diagnostics.Add(ParseDiagnostic.Warning(
                $"Container compression ratio is about {totalUncompressed / Math.Max(1, totalCompressed):N0}:1, above the " +
                $"{limits.MaxCompressionRatio}:1 threshold. It is being treated as a possible decompression bomb."));
            accepted = false;
        }

        return new SafeZipInspection
        {
            Accepted = accepted,
            Entries = entries,
            Diagnostics = diagnostics,
            TotalUncompressedBytes = totalUncompressed
        };
    }

    /// <summary>
    /// Rejects absolute paths, drive-qualified paths, and any parent-directory
    /// segment, in either slash style.
    /// </summary>
    public static bool IsSafeEntryName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        var normalised = name.Replace('\\', '/');

        if (normalised.StartsWith('/'))
        {
            return false;
        }

        // "C:/..." or "C:..." style rooted names.
        if (normalised.Length >= 2 && normalised[1] == ':')
        {
            return false;
        }

        var segments = normalised.Split('/');
        return segments.All(segment => segment != "..");
    }

    /// <summary>
    /// Reads an entry into memory, refusing to exceed <paramref name="maxBytes"/>
    /// even when the entry's declared length understates the real payload.
    /// </summary>
    public static byte[] ReadEntry(ZipArchiveEntry entry, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var source = entry.Open();
        using var buffer = new MemoryStream(capacity: (int)Math.Min(entry.Length <= 0 ? 8192 : entry.Length, 1024 * 1024));

        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException(
                    $"Entry '{entry.FullName}' produced more than the permitted {RpeFileInfo.FormatSize(maxBytes)} of data.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
