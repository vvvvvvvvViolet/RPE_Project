using System.IO;
using System.Windows;
using System.Windows.Threading;
using RPEReader.Core.Logging;

namespace RPEReader.App;

/// <summary>
/// Application entry point. Installs the last-chance exception handlers and
/// forwards a command-line path, which is how a double-clicked .rpe arrives
/// once the file association is registered.
/// </summary>
public partial class App : Application
{
    internal static FileLogger Logger { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Logger.Info($"RPE Reader 1.0.0 starting. Log folder: {Logger.LogDirectory ?? "(disabled)"}");

        var window = new Views.MainWindow();
        MainWindow = window;
        window.Show();

        var path = e.Args.FirstOrDefault(a => !a.StartsWith('-') && !a.StartsWith('/'));
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            _ = window.OpenFileAsync(path);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("RPE Reader exiting.");
        Logger.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled exception on the UI thread.", e.Exception);

        MessageBox.Show(
            "RPE Reader hit an unexpected error but is still running.\n\n" +
            $"{e.Exception.GetType().Name}: {e.Exception.Message}\n\n" +
            $"Details were written to:\n{Logger.CurrentLogFile ?? "(logging disabled)"}",
            "RPE Reader",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Keeping the application alive is the right call for a read-only
        // viewer: nothing is being written, so there is no state to corrupt.
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Error("Unhandled exception on a background thread.", ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }
}
