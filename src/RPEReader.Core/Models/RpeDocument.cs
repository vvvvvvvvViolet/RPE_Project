namespace RPEReader.Core.Models;

/// <summary>The parsed, in-memory view of one .rpe file.</summary>
public sealed class RpeDocument
{
    public required string FormatId { get; init; }

    public required string FormatDisplayName { get; init; }

    /// <summary>Root of the logical tree. Never <c>null</c>.</summary>
    public required RpeNode Root { get; init; }

    /// <summary>Top-level facts shown in the summary pane.</summary>
    public IReadOnlyList<RpeField> Summary { get; init; } = Array.Empty<RpeField>();

    /// <summary>
    /// Text extracted from the file for the Raw Text viewer, already capped to
    /// <see cref="RpeLimits.MaxRawTextChars"/>.
    /// </summary>
    public string RawText { get; init; } = string.Empty;

    public bool RawTextTruncated { get; init; }

    /// <summary>True when any limit prevented the document from being fully read.</summary>
    public bool Incomplete { get; init; }
}
