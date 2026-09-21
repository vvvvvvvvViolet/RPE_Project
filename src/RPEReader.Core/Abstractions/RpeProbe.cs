namespace RPEReader.Core.Abstractions;

/// <summary>
/// The evidence a parser is allowed to base <see cref="IRpeParser.CanParse"/> on:
/// a small header sample plus, for container formats, the entry names.
/// </summary>
public sealed class RpeProbe
{
    public required string FileName { get; init; }

    public required long FileSizeBytes { get; init; }

    /// <summary>First bytes of the file (up to 4 KiB).</summary>
    public required IReadOnlyList<byte> Header { get; init; }

    /// <summary>True when the file starts with a local ZIP file header.</summary>
    public required bool IsZipContainer { get; init; }

    /// <summary>Entry names found in the container, empty for non-containers.</summary>
    public IReadOnlyList<string> ContainerEntryNames { get; init; } = Array.Empty<string>();

    /// <summary>True when the decoded header text begins an XML declaration or element.</summary>
    public required bool LooksLikeXml { get; init; }

    /// <summary>Best-effort text decoding of <see cref="Header"/> for sniffing.</summary>
    public required string HeaderText { get; init; }

    public bool HasEntry(string name) =>
        ContainerEntryNames.Any(e => string.Equals(e, name, StringComparison.OrdinalIgnoreCase));

    public bool HeaderStartsWith(ReadOnlySpan<byte> signature)
    {
        if (Header.Count < signature.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (Header[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }
}
