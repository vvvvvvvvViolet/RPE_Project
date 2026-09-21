namespace RPEReader.Core.Models;

/// <summary>
/// A single name/value pair belonging to an <see cref="RpeNode"/>.
/// Values are always carried as text; the reader never converts a declared
/// .NET type name into a real <see cref="Type"/> and never instantiates one.
/// </summary>
public sealed class RpeField
{
    public RpeField(string name, string? value, string? typeHint = null, string? note = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
        TypeHint = typeHint;
        Note = note;
    }

    /// <summary>Field name as written in the source document.</summary>
    public string Name { get; }

    /// <summary>Raw textual value, or <c>null</c> when the element was empty.</summary>
    public string? Value { get; }

    /// <summary>
    /// The value of a <c>Type="..."</c> attribute, if the source carried one.
    /// Treated strictly as an opaque label for display purposes.
    /// </summary>
    public string? TypeHint { get; }

    /// <summary>Optional decoded/explanatory text, e.g. a decoded timestamp.</summary>
    public string? Note { get; }

    public override string ToString() => $"{Name} = {Value}";
}
