using System;
using System.Linq;
using System.Windows;
using TypeIt4Me.Views;

namespace TypeIt4Me.Services;

public class DialogService : IDialogService
{
    public static Window? ActiveOwner => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);

    internal Func<Window?> GetActiveOwner = () => ActiveOwner;

    internal Func<string, string, string, Window?, (bool? Result, string FileName)> OpenFileDialogFunc = (filter, defaultExt, title, owner) =>
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter, DefaultExt = defaultExt, Title = title };
        return (owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner), dialog.FileName);
    };

    internal Func<string, string, string, string, Window?, (bool? Result, string FileName)> SaveFileDialogFunc = (filter, defaultExt, fileName, title, owner) =>
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = filter, DefaultExt = defaultExt, FileName = fileName, Title = title, OverwritePrompt = true };
        return (owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner), dialog.FileName);
    };

    internal Func<string, string, bool, bool, Window?, bool?> ShowMessageWindowFunc = (message, title, confirmation, destructive, owner) =>
        new MessageWindow(message, title, confirmation: confirmation, destructive: destructive) { Owner = owner }.ShowDialog();

    public string? OpenFileDialog(string filter, string defaultExt)
    {
        var owner = GetActiveOwner();
        var result = OpenFileDialogFunc(filter, defaultExt, "Import snippets", owner);
        return result.Result == true ? result.FileName : null;
    }

    public string? SaveFileDialog(string filter, string defaultExt, string fileName)
    {
        var owner = GetActiveOwner();
        var result = SaveFileDialogFunc(filter, defaultExt, fileName, "Export snippets", owner);
        return result.Result == true ? result.FileName : null;
    }

    public bool ShowConfirmation(string message, string title) =>
        ShowMessageWindowFunc(message, title, true, title is "Delete snippet" or "Remove PIN", GetActiveOwner()) == true;

    public void ShowInformation(string message, string title) =>
        ShowMessageWindowFunc(message, title, false, false, GetActiveOwner());
}
