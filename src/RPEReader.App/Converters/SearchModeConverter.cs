using System.Globalization;
using System.Windows.Data;
using RPEReader.Core.Services;

namespace RPEReader.App.Converters;

/// <summary>Maps <see cref="SearchMode"/> to the combo box's selected index.</summary>
public sealed class SearchModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SearchMode mode ? (int)mode : 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index && Enum.IsDefined(typeof(SearchMode), index) ? (SearchMode)index : SearchMode.Text;
}
