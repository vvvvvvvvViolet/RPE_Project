using RPEReader.Core.Editing;

namespace RPEReader.App.ViewModels;

/// <summary>Row in the pending-changes list: one recorded edit.</summary>
public sealed class PendingEditViewModel
{
    public PendingEditViewModel(RpeEdit edit)
    {
        Edit = edit ?? throw new ArgumentNullException(nameof(edit));
    }

    public RpeEdit Edit { get; }

    public string Path => Edit.Path;

    public string Field => Edit.FieldName;

    public string OldValue => Shorten(Edit.OldValue);

    public string NewValue => Shorten(Edit.NewValue);

    public string RecordedAt => Edit.RecordedAtUtc.ToLocalTime().ToString("HH:mm:ss");

    private static string Shorten(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(empty)";
        }

        var single = value.Replace("\r", " ").Replace("\n", " ");
        return single.Length <= 160 ? single : single[..160] + "…";
    }
}
