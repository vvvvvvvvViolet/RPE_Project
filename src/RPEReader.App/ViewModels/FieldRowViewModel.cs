using RPEReader.App.Mvvm;
using RPEReader.Core.Editing;
using RPEReader.Core.Models;

namespace RPEReader.App.ViewModels;

/// <summary>
/// Row shown in the property grid for the selected node. When the document
/// carries an editor and edit mode is on, the Value column writes through it.
/// </summary>
public sealed class FieldRowViewModel : ObservableObject
{
    private readonly RpeField _field;
    private readonly IRpeDocumentEditor? _editor;
    private readonly string _path;
    private readonly Action<FieldRowViewModel, EditValidationResult>? _onEdited;
    private bool _editModeEnabled;
    private string? _lastMessage;

    public FieldRowViewModel(
        RpeField field,
        IRpeDocumentEditor? editor = null,
        string path = "",
        Action<FieldRowViewModel, EditValidationResult>? onEdited = null)
    {
        _field = field ?? throw new ArgumentNullException(nameof(field));
        _editor = editor;
        _path = path;
        _onEdited = onEdited;
    }

    public RpeField Field => _field;

    public string Name => _field.Name;

    public string DeclaredType => _field.TypeHint ?? string.Empty;

    /// <summary>
    /// The validation message from the last edit if there was one, otherwise
    /// whatever the reader derived — a decoded timestamp, for instance.
    /// </summary>
    public string Note => _lastMessage ?? _field.Note ?? string.Empty;

    public string OriginalValue => _field.OriginalValue ?? string.Empty;

    public bool IsModified => _field.IsModified;

    /// <summary>The node label may be re-derived when this field changes.</summary>
    public string OwnerName => _field.Owner?.Name ?? string.Empty;

    /// <summary>Shown as a marker column so changed rows are obvious at a glance.</summary>
    public string ChangeMarker => _field.IsModified ? "●" : string.Empty;

    /// <summary>True when this particular field may be edited right now.</summary>
    public bool CanEditValue => _editModeEnabled && _editor is not null && _field.Editable;

    public bool EditModeEnabled
    {
        get => _editModeEnabled;
        set
        {
            if (SetProperty(ref _editModeEnabled, value))
            {
                OnPropertyChanged(nameof(CanEditValue));
            }
        }
    }

    public string Value
    {
        get => _field.Value ?? string.Empty;
        set
        {
            if (_editor is null || !CanEditValue)
            {
                // Re-raise so the grid discards whatever was typed.
                OnPropertyChanged();
                return;
            }

            var candidate = value;
            if (string.Equals(_field.Value ?? string.Empty, candidate, StringComparison.Ordinal))
            {
                return;
            }

            var result = _editor.SetValue(_field, candidate, _path);

            _lastMessage = result.Level switch
            {
                EditValidationLevel.Error => $"Rejected: {result.Message}",
                EditValidationLevel.Warning => $"Warning: {result.Message}",
                _ => null
            };

            // The field is the source of truth: on rejection it still holds the
            // old value, and raising the change puts that back in the cell.
            OnPropertyChanged();
            OnPropertyChanged(nameof(Note));
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(ChangeMarker));

            _onEdited?.Invoke(this, result);
        }
    }

    /// <summary>
    /// Redraws the row after a change made elsewhere — a revert, or an edit to
    /// the twin row that shares this field.
    /// </summary>
    /// <param name="keepMessage">
    /// True to leave any validation message in place. The row that raised the
    /// edit passes true, or refreshing would erase the very message the edit
    /// just produced.
    /// </param>
    public void Refresh(bool keepMessage = false)
    {
        if (!keepMessage)
        {
            _lastMessage = null;
        }

        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Note));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(ChangeMarker));
    }
}
