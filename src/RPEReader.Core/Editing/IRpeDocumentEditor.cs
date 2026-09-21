using RPEReader.Core.Models;

namespace RPEReader.Core.Editing;

/// <summary>Outcome of writing a document back out.</summary>
public sealed class SaveResult
{
    public required bool Success { get; init; }

    public required IReadOnlyList<ParseDiagnostic> Diagnostics { get; init; }

    /// <summary>Path written, when the save targeted a file.</summary>
    public string? Path { get; init; }

    /// <summary>SHA-256 of the file that was written.</summary>
    public string? Sha256 { get; init; }

    public long BytesWritten { get; init; }

    /// <summary>Path of the backup taken before an in-place overwrite.</summary>
    public string? BackupPath { get; init; }

    /// <summary>
    /// True when the written file was re-opened and re-parsed successfully,
    /// and every recorded edit was found in it.
    /// </summary>
    public bool VerifiedByReparse { get; init; }
}

/// <summary>
/// The editing surface for a document whose format this build can also write.
/// Formats that are read-only expose no editor at all.
/// </summary>
public interface IRpeDocumentEditor
{
    /// <summary>Format the editor writes, matching <see cref="RpeDocument.FormatId"/>.</summary>
    string FormatId { get; }

    bool IsDirty { get; }

    /// <summary>
    /// True when a save with no edits reproduces the file's payload byte for
    /// byte, so an exported file differs only where the user changed something.
    /// False when the source uses a form the reader cannot reproduce — most
    /// often carriage returns inside values, which XML readers are required to
    /// normalise. Editing is still allowed; the difference is reported when the
    /// file is opened.
    /// </summary>
    bool RoundTripsExactly { get; }

    IReadOnlyList<RpeEdit> PendingEdits { get; }

    /// <summary>
    /// Checks a candidate value without applying it. Never throws.
    /// </summary>
    EditValidationResult Validate(RpeField field, string? newValue);

    /// <summary>
    /// Applies a value to a field and records the change. Returns the
    /// validation outcome; nothing is applied when that outcome is an error.
    /// </summary>
    EditValidationResult SetValue(RpeField field, string? newValue, string path);

    /// <summary>Undoes one recorded edit.</summary>
    bool Revert(RpeEdit edit);

    /// <summary>Undoes every recorded edit, returning the document to what was read.</summary>
    void RevertAll();

    /// <summary>
    /// Re-baselines every edited field after a successful save, so the document
    /// is no longer dirty. Unlike <see cref="RevertAll"/> the values are kept:
    /// they are what the saved file now holds.
    /// </summary>
    void AcceptChanges();

    /// <summary>
    /// Writes a complete, importable file. <paramref name="path"/> must not be
    /// the file the document was read from unless <paramref name="allowOverwrite"/>
    /// is set, in which case a timestamped backup is taken first.
    /// </summary>
    SaveResult Save(string path, bool allowOverwrite = false, CancellationToken cancellationToken = default);

    /// <summary>Writes the document to a stream. Used by tests and by Save.</summary>
    void WriteTo(Stream destination, CancellationToken cancellationToken = default);
}
