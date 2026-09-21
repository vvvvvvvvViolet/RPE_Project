using RPEReader.Core.Services;

namespace RPEReader.App.ViewModels;

/// <summary>Row shown in the search-results grid.</summary>
public sealed class SearchHitViewModel
{
    public SearchHitViewModel(SearchHit hit)
    {
        Hit = hit ?? throw new ArgumentNullException(nameof(hit));
    }

    public SearchHit Hit { get; }

    public string Kind => Hit.Kind;

    public string Location => Hit.Location;

    public string Name => Hit.Name;

    public string Value => Shorten(Hit.Value);

    public string OffsetText => Hit.Offset is null ? string.Empty : $"0x{Hit.Offset:X8}";

    private static string Shorten(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var single = value.Replace("\r", " ").Replace("\n", " ");
        return single.Length <= 200 ? single : single[..200] + "…";
    }
}
