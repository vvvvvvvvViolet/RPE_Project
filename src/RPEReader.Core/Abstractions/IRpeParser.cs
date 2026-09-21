using RPEReader.Core.Models;

namespace RPEReader.Core.Abstractions;

/// <summary>
/// Contract every .rpe format reader implements. Adding support for a future
/// RPE revision means adding one class and registering it; nothing else changes.
/// </summary>
public interface IRpeParser
{
    /// <summary>Stable identifier, e.g. "opcrouter4.zip".</summary>
    string FormatId { get; }

    /// <summary>Human-readable format name shown in the UI.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Higher wins when several parsers accept the same file. The generic
    /// fallback parser uses <see cref="int.MinValue"/>.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Cheap check against a probe of the file header and container listing.
    /// Must not throw and must not read the whole file.
    /// </summary>
    bool CanParse(RpeProbe probe);

    /// <summary>
    /// True when this parser can also write its format back out, in which case
    /// the documents it produces carry an editor.
    /// </summary>
    bool SupportsWriting => false;

    /// <summary>
    /// Reads the document. Implementations report problems through the returned
    /// <see cref="ParseResult"/> rather than throwing.
    /// </summary>
    /// <param name="stream">A readable, seekable, read-only stream over the file.</param>
    ParseResult Parse(Stream stream, RpeParseContext context);
}
