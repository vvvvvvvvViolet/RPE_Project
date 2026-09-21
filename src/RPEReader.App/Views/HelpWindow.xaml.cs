using System.Windows;

namespace RPEReader.App.Views;

public partial class HelpWindow : Window
{
    public HelpWindow() => InitializeComponent();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
