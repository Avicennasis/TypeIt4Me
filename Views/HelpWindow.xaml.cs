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
        try { Process.Start(new ProcessStartInfo("https://github.com/Avicennasis/TypeIt4Me") { UseShellExecute = true }); }
        catch (Exception) { new DialogService().ShowInformation("Windows could not open the browser. Visit github.com/Avicennasis/TypeIt4Me to view the source.", "Open source repository"); }
    }
    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}
