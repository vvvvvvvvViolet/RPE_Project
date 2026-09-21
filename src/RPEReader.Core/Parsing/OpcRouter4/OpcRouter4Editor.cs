using System.Security.Cryptography;
using System.Xml;
using RPEReader.Core.Editing;
using RPEReader.Core.Models;
using RPEReader.Core.Services;

namespace RPEReader.Core.Parsing.OpcRouter4;

/// <summary>
/// Applies edits to the in-memory export document and writes it back out.
/// </summary>
/// <remarks>
/// Edits are applied to the original <see cref="XmlDocument"/> that was parsed,
/// never to a document rebuilt from the display tree. That is what keeps an
/// unmodified save byte-identical and a modified save a minimal diff: every
/// element, attribute and value the reader did not touch is carried through
/// exactly as it arrived.
/// </remarks>
public sealed class OpcRouter4Editor : IRpeDocumentEditor
{
    private readonly XmlDocument _document;
    private readonly RpeLimits _limits;
    private readonly List<RpeEdit> _edits = new();
    private readonly string? _sourcePath;

    public OpcRouter4Editor(XmlDocument document, RpeLimits limits, string? sourcePath, bool roundTripsExactly = true)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _sourcePath = sourcePath;
        RoundTripsExactly = roundTripsExactly;
    }

    /// <summary>
    /// True when a save with no edits reproduces the file's payload byte for
    /// byte. Measured when the document was opened, not assumed.
    /// </summary>
    public bool RoundTripsExactly { get; }

    public string FormatId => "opcrouter4.zip";

    public bool IsDirty => _edits.Count > 0;

    public IReadOnlyList<RpeEdit> PendingEdits => _edits;

    // ---------------------------------------------------------------- validate

    public EditValidationResult Validate(RpeField field, string? newValue)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (!field.Editable)
        {
            return EditValidationResult.Error(
                field.Name.StartsWith("@Type", StringComparison.Ordinal)
                    ? "A declared .NET type name cannot be edited. Changing it would misdescribe the data to OPC Router."
                    : $"'{field.Name}' is a derived or structural value and is not editable.");
        }

        if (field.Source is not (XmlElement or XmlAttribute))
        {
            return EditValidationResult.Error("This field cannot be traced back to a location in the file.");
        }

        if (newValue is not null && newValue.Length > _limits.MaxFieldValueChars)
        {
            return EditValidationResult.Error(
                $"The value is longer than the {_limits.MaxFieldValueChars:N0}-character limit.");
        }

        if (newValue is not null)
        {
            var offending = FindIllegalXmlCodePoint(newValue);
            if (offending is not null)
            {
                return EditValidationResult.Error(
                    $"The value contains U+{offending:X4}, which XML 1.0 cannot represent. " +
                    "The file would not be readable by OPC Router.");
            }
        }

        var originalShape = ValueShapeClassifier.Classify(field.OriginalValue, StripPrefix(field.Name));
        var newShape = ValueShapeClassifier.Classify(newValue, StripPrefix(field.Name));

        if (ValueShapeClassifier.IsNotableChange(originalShape, newShape))
        {
            return EditValidationResult.Warning(
                $"'{field.Name}' originally held {ValueShapeClassifier.Describe(originalShape)}; " +
                $"the new value is {ValueShapeClassifier.Describe(newShape)}. " +
                "This build has no schema for the format, so the change is allowed — check it is what OPC Router expects.");
        }

        return EditValidationResult.Ok;
    }

    /// <summary>
    /// Returns the first code point that XML 1.0 cannot carry, or <c>null</c>.
    /// Tab, line feed and carriage return are the only control characters
    /// allowed.
    /// </summary>
    /// <remarks>
    /// Iterates code points, not UTF-16 units. A character outside the basic
    /// multilingual plane — an emoji, or an astral CJK ideograph — is stored as
    /// a surrogate pair, and each half on its own falls in the reserved
    /// D800..DFFF range. Checking unit by unit would reject every such
    /// character as illegal when it is perfectly valid. An *unpaired*
    /// surrogate genuinely is illegal, and is still caught.
    /// </remarks>
    internal static int? FindIllegalXmlCodePoint(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    // A well-formed pair: every code point it can encode
                    // (U+10000..U+10FFFF) is legal in XML 1.0.
                    i++;
                    continue;
                }

                return c;
            }

            if (char.IsLowSurrogate(c))
            {
                // A low surrogate reached here has no high surrogate before it.
                return c;
            }

            var legal = c is '\t' or '\n' or '\r'
                        || (c >= 0x20 && c <= 0xD7FF)
                        || (c >= 0xE000 && c <= 0xFFFD);

            if (!legal)
            {
                return c;
            }
        }

        return null;
    }

    private static string StripPrefix(string fieldName) =>
        fieldName.StartsWith('@') ? fieldName[1..] : fieldName;

    // ------------------------------------------------------------------- apply

    public EditValidationResult SetValue(RpeField field, string? newValue, string path)
    {
        ArgumentNullException.ThrowIfNull(field);

        var validation = Validate(field, newValue);
        if (!validation.IsAccepted)
        {
            return validation;
        }

        var previous = field.Value;
        if (string.Equals(previous, newValue, StringComparison.Ordinal))
        {
            return validation;
        }

        ApplyToXml(field, newValue);
        field.SetValueInternal(newValue);
        RefreshDerivedText(field);

        // Collapse repeated edits of the same field into one entry, so the
        // pending-changes list shows the net effect rather than keystrokes.
        var existing = _edits.FindIndex(e => ReferenceEquals(e.Field, field));
        if (existing >= 0)
        {
            var original = _edits[existing].OldValue;
            _edits.RemoveAt(existing);

            if (!string.Equals(original, newValue, StringComparison.Ordinal))
            {
                _edits.Insert(existing, new RpeEdit(path, field, original, newValue));
            }
        }
        else
        {
            _edits.Add(new RpeEdit(path, field, previous, newValue));
        }

        return validation;
    }

    /// <summary>
    /// Recomputes anything the reader derived from this field's value: the
    /// explanatory note, and the owning node's label when that label was taken
    /// from this field. Without this a changed timestamp keeps showing the old
    /// decoded date and a renamed element keeps its old label in the tree —
    /// both of which state something that is no longer true.
    /// </summary>
    private static void RefreshDerivedText(RpeField field)
    {
        if (field.Source is XmlElement element)
        {
            field.SetNoteInternal(XmlToNodeMapper.DeriveNote(element.LocalName, field.TypeHint, field.Value));
        }

        var owner = field.Owner;
        if (owner?.Source is XmlElement ownerElement)
        {
            owner.Name = XmlToNodeMapper.DisplayNameFor(ownerElement);
        }
    }

    private static void ApplyToXml(RpeField field, string? newValue)
    {
        switch (field.Source)
        {
            case XmlAttribute attribute:
                attribute.Value = newValue ?? string.Empty;
                break;

            case XmlElement element:
                if (string.IsNullOrEmpty(newValue))
                {
                    // Setting InnerText to "" would leave an empty text node
                    // behind and serialise as <Name></Name>. The product writes
                    // an empty element as <Name />, so drop the children and
                    // mark the element empty to match it exactly.
                    element.IsEmpty = true;
                }
                else
                {
                    // Assigning InnerText replaces the children with a single
                    // text node, which is the shape every leaf element has.
                    element.InnerText = newValue;
                }

                break;

            default:
                throw new InvalidOperationException("The field has no writable source.");
        }
    }

    // ------------------------------------------------------------------ revert

    public bool Revert(RpeEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var index = _edits.FindIndex(e => ReferenceEquals(e, edit));
        if (index < 0)
        {
            return false;
        }

        ApplyToXml(edit.Field, edit.OldValue);
        edit.Field.SetValueInternal(edit.OldValue);
        RefreshDerivedText(edit.Field);
        _edits.RemoveAt(index);
        return true;
    }

    public void AcceptChanges()
    {
        // Called after a successful save. The values in the document are now
        // what the file holds, so they become the new baseline: nothing is
        // pending and nothing is marked as modified.
        foreach (var edit in _edits)
        {
            edit.Field.AcceptValueInternal();
        }

        _edits.Clear();
    }

    public void RevertAll()
    {
        // Reverse order so that a field edited more than once ends on its
        // original value even if the collapse above ever misses a case.
        for (var i = _edits.Count - 1; i >= 0; i--)
        {
            var edit = _edits[i];
            ApplyToXml(edit.Field, edit.OldValue);
            edit.Field.SetValueInternal(edit.OldValue);
            RefreshDerivedText(edit.Field);
        }

        _edits.Clear();
    }

    // -------------------------------------------------------------------- save

    public void WriteTo(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        OpcRouter4Writer.WriteContainer(_document, destination, cancellationToken);
    }

    public SaveResult Save(string path, bool allowOverwrite = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var diagnostics = new List<ParseDiagnostic>();
        string? backupPath = null;

        try
        {
            var full = Path.GetFullPath(path);

            var targetsSource = _sourcePath is not null
                                && string.Equals(Path.GetFullPath(_sourcePath), full, StringComparison.OrdinalIgnoreCase);

            if (targetsSource && !allowOverwrite)
            {
                return Failed(diagnostics,
                    "Writing over the file that is open is not allowed unless overwriting was explicitly requested.");
            }

            if (targetsSource)
            {
                backupPath = CreateBackup(full, diagnostics);
                if (backupPath is null)
                {
                    return Failed(diagnostics,
                        "The original could not be backed up, so it was left untouched. Use Save As instead.");
                }
            }

            // Write to a temporary file in the same folder, then move it into
            // place. A failure part-way through therefore cannot leave a
            // truncated .rpe where a valid one used to be.
            var directory = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(directory))
            {
                return Failed(diagnostics, "The destination folder could not be determined.");
            }

            Directory.CreateDirectory(directory);
            var temp = Path.Combine(directory, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    WriteTo(stream, cancellationToken);
                }

                var verification = Verify(temp, diagnostics);
                if (!verification)
                {
                    TryDelete(temp);
                    return Failed(diagnostics,
                        "The file written did not pass its own re-read check, so it was discarded. Nothing was saved.",
                        backupPath);
                }

                File.Move(temp, full, overwrite: true);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }

            var info = new FileInfo(full);
            var sha = ComputeSha256(full);

            diagnostics.Add(ParseDiagnostic.Info(
                $"Wrote {info.Name} ({RpeFileInfo.FormatSize(info.Length)}) with {_edits.Count:N0} change(s) applied."));
            diagnostics.Add(ParseDiagnostic.Info(
                "The file was re-opened, re-parsed and compared with the document in memory; they match exactly."));

            return new SaveResult
            {
                Success = true,
                Diagnostics = diagnostics,
                Path = full,
                Sha256 = sha,
                BytesWritten = info.Length,
                BackupPath = backupPath,
                VerifiedByReparse = true
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return Failed(diagnostics, "Access to the destination was denied. Check the folder permissions.", backupPath);
        }
        catch (IOException ex)
        {
            return Failed(diagnostics, $"The file could not be written: {ex.Message}", backupPath);
        }
        catch (XmlException ex)
        {
            return Failed(diagnostics, $"The document could not be serialised: {ex.Message}", backupPath);
        }
    }

    /// <summary>
    /// Re-opens the file just written and confirms it parses and carries every
    /// recorded change. A save that cannot survive this is not reported as one.
    /// </summary>
    private bool Verify(string path, List<ParseDiagnostic> diagnostics)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

            var entry = archive.GetEntry(OpcRouter4Schema.ContainerEntryName);
            if (entry is null)
            {
                diagnostics.Add(ParseDiagnostic.Error(
                    $"The written container has no '{OpcRouter4Schema.ContainerEntryName}' entry."));
                return false;
            }

            var bytes = Detection.SafeZipReader.ReadEntry(entry, _limits.MaxEntryUncompressedBytes, CancellationToken.None);
            var reloaded = OpcRouter4Writer.ReadXml(bytes, _limits.MaxEntryUncompressedBytes);

            if (reloaded.DocumentElement is null
                || !string.Equals(reloaded.DocumentElement.LocalName, OpcRouter4Schema.RootElement, StringComparison.Ordinal))
            {
                diagnostics.Add(ParseDiagnostic.Error("The written document does not have the expected root element."));
                return false;
            }

            // Serialising the reloaded document must reproduce the same bytes;
            // if it does not, the file is not stable across a round trip.
            var reserialised = OpcRouter4Writer.SerialiseXml(reloaded);
            if (!reserialised.AsSpan().SequenceEqual(bytes))
            {
                diagnostics.Add(ParseDiagnostic.Error(
                    "The written document does not round-trip to identical bytes, so it was not trusted."));
                return false;
            }

            // The decisive check: what came back has to be exactly what this
            // document would serialise to. Comparing whole payloads proves
            // every edit reached the file and that nothing else moved, which
            // checking the edited values one by one would not.
            var expected = OpcRouter4Writer.SerialiseXml(_document);
            if (!expected.AsSpan().SequenceEqual(bytes))
            {
                diagnostics.Add(ParseDiagnostic.Error(
                    "The file that was written does not match the document in memory, so it was discarded."));
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException or NotSupportedException)
        {
            diagnostics.Add(ParseDiagnostic.Error($"The written file could not be verified: {ex.Message}"));
            return false;
        }
    }

    private static string? CreateBackup(string path, List<ParseDiagnostic> diagnostics)
    {
        try
        {
            var backup = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            var attempt = 0;
            while (File.Exists(backup) && attempt < 100)
            {
                attempt++;
                backup = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}-{attempt}.bak";
            }

            File.Copy(path, backup, overwrite: false);
            diagnostics.Add(ParseDiagnostic.Info($"The original was copied to {Path.GetFileName(backup)} before overwriting."));
            return backup;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(ParseDiagnostic.Error($"The backup could not be created: {ex.Message}"));
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // A leftover temporary file is not worth surfacing as a save failure.
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// Reports a failed save. <paramref name="backupPath"/> is carried through
    /// so a backup taken before the failure is disclosed rather than left
    /// sitting unmentioned beside the original.
    /// </summary>
    private static SaveResult Failed(List<ParseDiagnostic> diagnostics, string message, string? backupPath = null)
    {
        diagnostics.Add(ParseDiagnostic.Error(message));

        if (backupPath is not null)
        {
            diagnostics.Add(ParseDiagnostic.Warning(
                $"A backup taken before the attempt is still on disk: {backupPath}"));
        }

        return new SaveResult
        {
            Success = false,
            Diagnostics = diagnostics,
            BackupPath = backupPath,
            VerifiedByReparse = false
        };
    }
}
