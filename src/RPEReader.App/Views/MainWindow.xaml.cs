using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RPEReader.App.ViewModels;
using RPEReader.Core.Services;

namespace RPEReader.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(
            new RpeFileService(logger: App.Logger),
            App.Logger);

        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }

    public Task OpenFileAsync(string path) => _viewModel.OpenAsync(path);

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is NodeViewModel node)
        {
            _viewModel.SelectedNode = node;
        }
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (_viewModel.SearchCommand.CanExecute(null))
        {
            _viewModel.SearchCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (!TryGetDroppedFile(e, out var path))
        {
            return;
        }

        await _viewModel.OpenAsync(path!).ConfigureAwait(true);
    }

    /// <summary>
    /// Accepts a single existing file. Directories and multi-file drops are
    /// ignored rather than guessed at.
    /// </summary>
    private static bool TryGetDroppedFile(DragEventArgs e, out string? path)
    {
        path = null;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } files)
        {
            return false;
        }

        if (!File.Exists(files[0]))
        {
            return false;
        }

        path = files[0];
        return true;
    }

    /// <summary>
    /// Cancels the edit before it starts for a field this build will not write:
    /// a declared .NET type name, a value that was shortened for display, or a
    /// derived value with no location in the file.
    /// </summary>
    private void OnDetailsBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is FieldRowViewModel row && !row.CanEditValue)
        {
            e.Cancel = true;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnAboutClick(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void OnHelpClick(object sender, RoutedEventArgs e) =>
        new HelpWindow { Owner = this }.ShowDialog();

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e)
    {
        var folder = App.Logger.LogDirectory;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show(
                "No log folder is available. Logging is disabled because no writable location was found.",
                "RPE Reader",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            // Explorer is launched on the folder itself; nothing from a .rpe
            // file ever reaches this call.
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            App.Logger.Error("Could not open the log folder.", ex);
            MessageBox.Show($"The log folder could not be opened.\n\n{ex.Message}",
                "RPE Reader", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
