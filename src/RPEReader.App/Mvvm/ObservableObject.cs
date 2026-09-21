using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RPEReader.App.Mvvm;

/// <summary>
/// Minimal INotifyPropertyChanged base class.
/// </summary>
/// <remarks>
/// Hand-written rather than taken from an MVVM package: the application has no
/// third-party runtime dependencies, which keeps the supply-chain surface and
/// the licence inventory to nothing.
/// </remarks>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        RaisePropertyChanged(propertyName);

    /// <summary>
    /// Raises the change notification for another object in the same view model
    /// graph — a parent whose computed value depends on this one, for instance.
    /// </summary>
    internal void RaisePropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
