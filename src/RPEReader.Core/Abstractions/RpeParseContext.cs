using RPEReader.Core.Models;

namespace RPEReader.Core.Abstractions;

/// <summary>Everything a parser is given besides the stream itself.</summary>
public sealed class RpeParseContext
{
    public required RpeProbe Probe { get; init; }

    public RpeLimits Limits { get; init; } = RpeLimits.Default;

    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    /// <summary>
    /// Full path of the file being read, when it came from disk. The editor
    /// uses it to refuse an accidental in-place overwrite.
    /// </summary>
    public string? SourcePath { get; init; }
}
