using System.Reflection;
using System.Windows;

namespace RPEReader.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational ?? assembly.GetName().Version?.ToString() ?? "1.0.0";

        VersionText.Text =
            $"Version {version}  ·  .NET {Environment.Version}  ·  {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
