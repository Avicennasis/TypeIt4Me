using System;
using System.Windows;

namespace TypeIt4Me.Views;

public partial class InputWindow : Window
{
    public string Result { get; private set; } = string.Empty;
    private readonly Func<string, string?>? _validate;

    public InputWindow(string message, string defaultValue = "", Func<string, string?>? validate = null)
    {
        InitializeComponent(); MessageText.Content = message; InputBox.Text = defaultValue; _validate = validate;
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        string? error = _validate?.Invoke(InputBox.Text);
        if (error != null) { ValidationText.Text = error; InputBox.Focus(); return; }
        Result = InputBox.Text; DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
