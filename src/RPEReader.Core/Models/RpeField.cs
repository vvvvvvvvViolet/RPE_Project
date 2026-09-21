namespace RPEReader.Core.Models;

/// <summary>
/// A single name/value pair belonging to an <see cref="RpeNode"/>.
/// Values are always carried as text; the reader never converts a declared
/// .NET type name into a real <see cref="Type"/> and never instantiates one.
/// </summary>
public sealed class RpeField
{
    public RpeField(
        string name,
        string? value,
        string? typeHint = null,
        string? note = null,
        object? source = null,
        bool editable = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
        OriginalValue = value;
        TypeHint = typeHint;
        Note = note;
        Source = source;
        Editable = editable;
    }

    /// <summary>Field name as written in the source document.</summary>
    public string Name { get; }

    /// <summary>
    /// Current textual value, or <c>null</c> when the element was empty.
    /// Changed only through <see cref="Editing.IRpeDocumentEditor"/>, so that
    /// every change is recorded and can be reverted.
    /// </summary>
    public string? Value { get; private set; }

    /// <summary>
    /// The baseline this field is compared against: what was read from the
    /// file, or what was last successfully written to one.
    /// </summary>
    public string? OriginalValue { get; private set; }

    /// <summary>True when <see cref="Value"/> differs from <see cref="OriginalValue"/>.</summary>
    public bool IsModified => !string.Equals(Value, OriginalValue, StringComparison.Ordinal);

    /// <summary>
    /// The value of a <c>Type="..."</c> attribute, if the source carried one.
    /// Treated strictly as an opaque label for display purposes.
    /// </summary>
    public string? TypeHint { get; }

    /// <summary>
    /// Optional decoded or explanatory text, for example a decoded timestamp.
    /// Recomputed by the editor when the value changes, so it never describes
    /// a value the field no longer holds.
    /// </summary>
    public string? Note { get; private set; }

    /// <summary>
    /// The node this field belongs to, set when it is added. Lets the editor
    /// refresh a node label that was derived from this field's value.
    /// </summary>
    public RpeNode? Owner { get; internal set; }

    /// <summary>
    /// Opaque handle to whatever the parser read this field from, used by the
    /// matching writer to put an edit back in the right place. Formats that
    /// cannot be written leave this <c>null</c>.
    /// </summary>
    public object? Source { get; }

    /// <summary>
    /// True when this build is willing to let the field be edited. False for
    /// derived or structural values, and for declared .NET type names.
    /// </summary>
    public bool Editable { get; }

    /// <summary>
    /// Applies a new value. Internal on purpose: callers go through the
    /// document editor so the change is validated and recorded.
    /// </summary>
    internal void SetValueInternal(string? value) => Value = value;

    /// <summary>
    /// Re-baselines the field after a successful save, so the current value
    /// stops counting as a pending modification.
    /// </summary>
    internal void AcceptValueInternal() => OriginalValue = Value;

    internal void SetNoteInternal(string? note) => Note = note;

    public override string ToString() => $"{Name} = {Value}";
}
