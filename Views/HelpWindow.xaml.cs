using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using TypeIt4Me.Services;

namespace TypeIt4Me.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        VersionText.Text = "Version " + (typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown");
    }
    private void Source_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var uri = new Uri("https://github.com/Avicennasis/TypeIt4Me");
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = uri.AbsoluteUri,
                UseShellExecute = false
            });
        }
        catch (Exception) { new DialogService().ShowInformation("Windows could not open the browser. Visit github.com/Avicennasis/TypeIt4Me to view the source.", "Open source repository"); }
    }
    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}
