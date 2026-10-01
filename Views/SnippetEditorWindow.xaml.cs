using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TypeIt4Me.ViewModels;

namespace TypeIt4Me.Views;

public partial class SnippetEditorWindow : Window
{
    private bool _discardConfirmed;
    private IInputElement? _focusBeforeDiscard;
    public bool Saved { get; private set; }

    public SnippetEditorWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && DiscardPanel.Visibility == Visibility.Visible)
            {
                KeepEditing_Click(this, new RoutedEventArgs()); e.Handled = true;
            }
        };
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SnippetEditorViewModel old) old.RequestClose -= RequestClose;
            if (e.NewValue is SnippetEditorViewModel vm) vm.RequestClose += RequestClose;
        };
    }

    private void RequestClose(bool save)
    {
        Saved = save;
        Close();
    }

    public void CloseForLock()
    {
        _discardConfirmed = true;
        Saved = false;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!Saved && !_discardConfirmed && DataContext is SnippetEditorViewModel { HasChanges: true })
        {
            e.Cancel = true;
            _focusBeforeDiscard = Keyboard.FocusedElement;
            EditorActions.Visibility = Visibility.Collapsed;
            DiscardPanel.Visibility = Visibility.Visible;
            SaveButton.IsDefault = false;
            KeepEditingButton.IsDefault = true;
            KeepEditingButton.Focus();
        }
        base.OnClosing(e);
    }

    private void KeepEditing_Click(object sender, RoutedEventArgs e)
    {
        DiscardPanel.Visibility = Visibility.Collapsed;
        EditorActions.Visibility = Visibility.Visible;
        KeepEditingButton.IsDefault = false;
        SaveButton.IsDefault = true;
        if (_focusBeforeDiscard != null) Keyboard.Focus(_focusBeforeDiscard);
        else ContentBox.Focus();
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        _discardConfirmed = true;
        Saved = false;
        Close();
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Tab and Escape retain their normal navigation/cancellation behavior.
        if (e.Key is Key.Tab or Key.Escape) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (DataContext is SnippetEditorViewModel vm) vm.UpdateHotkey(key, Keyboard.Modifiers);
    }
}
