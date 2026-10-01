using System.Linq;
using System.Windows;
using TypeIt4Me.Views;

namespace TypeIt4Me.Services;

public class DialogService : IDialogService
{
    public static Window? ActiveOwner => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);

    public string? OpenFileDialog(string filter, string defaultExt)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter, DefaultExt = defaultExt, Title = "Import snippets" };
        var owner = ActiveOwner;
        return (owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true ? dialog.FileName : null;
    }

    public string? SaveFileDialog(string filter, string defaultExt, string fileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = filter, DefaultExt = defaultExt, FileName = fileName, Title = "Export snippets", OverwritePrompt = true };
        var owner = ActiveOwner;
        return (owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true ? dialog.FileName : null;
    }

    public bool ShowConfirmation(string message, string title) =>
        new MessageWindow(message, title, confirmation: true, destructive: title is "Delete snippet" or "Remove PIN") { Owner = ActiveOwner }.ShowDialog() == true;

    public void ShowInformation(string message, string title) =>
        new MessageWindow(message, title) { Owner = ActiveOwner }.ShowDialog();
}
