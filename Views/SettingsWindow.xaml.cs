using System.Windows;

namespace TypeIt4Me.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();
    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}
