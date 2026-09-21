using RPEReader.Core.Models;

namespace RPEReader.App.ViewModels;

/// <summary>Row shown in the property grid for the selected node.</summary>
public sealed class FieldRowViewModel
{
    public FieldRowViewModel(RpeField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        Name = field.Name;
        Value = field.Value ?? string.Empty;
        DeclaredType = field.TypeHint ?? string.Empty;
        Note = field.Note ?? string.Empty;
    }

    public string Name { get; }

    public string Value { get; }

    public string DeclaredType { get; }

    public string Note { get; }
}
