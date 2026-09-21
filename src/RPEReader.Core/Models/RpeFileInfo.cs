namespace RPEReader.Core.Models;

/// <summary>Read-only facts about the file on disk.</summary>
public sealed class RpeFileInfo
{
    public required string FileName { get; init; }

    public required string FullPath { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTime LastWriteTimeUtc { get; init; }

    public required DateTime CreationTimeUtc { get; init; }

    /// <summary>Lower-case hexadecimal SHA-256 of the whole file.</summary>
    public required string Sha256 { get; init; }

    public string SizeDisplay => FormatSize(SizeBytes);

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes:N0} {units[unit]}"
            : $"{value:N2} {units[unit]} ({bytes:N0} bytes)";
    }
}
