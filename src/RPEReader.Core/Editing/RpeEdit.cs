using RPEReader.Core.Models;

namespace RPEReader.Core.Editing;

/// <summary>
/// One recorded change to a field. The list of these is the complete,
/// reviewable description of what a save will alter — which matters when the
/// file is a configuration that will be imported back into a running system.
/// </summary>
public sealed class RpeEdit
{
    public RpeEdit(string path, RpeField field, string? oldValue, string? newValue)
    {
        Path = path ?? string.Empty;
        Field = field ?? throw new ArgumentNullException(nameof(field));
        OldValue = oldValue;
        NewValue = newValue;
        RecordedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Human-readable location of the field in the document tree.</summary>
    public string Path { get; }

    public RpeField Field { get; }

    public string FieldName => Field.Name;

    public string? OldValue { get; }

    public string? NewValue { get; }

    public DateTime RecordedAtUtc { get; }

    public override string ToString() => $"{Path} / {FieldName}: '{OldValue}' -> '{NewValue}'";
}
