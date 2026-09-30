using System;
using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using TypeIt4Me.Services;

namespace TypeIt4Me.Views;

public partial class PinEntryWindow : Window
{
    private readonly bool _creating;
    private readonly Func<char[], Task<string?>>? _validate;
    private bool _working;
    public SecureString? SecurePin { get; private set; }

    public PinEntryWindow(string title = "Enter PIN", bool creating = false, Func<char[], Task<string?>>? validate = null)
    {
        InitializeComponent();
        _creating = creating; _validate = validate;
        Title = title + " · TypeIt4Me";
        HeadingText.Text = title;
        if (creating)
        {
            DescriptionText.Text = "Protect stored snippets on this device with a PIN.";
            ConfirmationPanel.Visibility = Visibility.Visible;
            SubmitButton.Content = "_Save PIN";
        }
        else if (!title.StartsWith("Unlock", StringComparison.Ordinal)) SubmitButton.Content = "_Continue";
        Loaded += (_, _) => PinBox.Focus();
        Closed += (_, _) => { PinBox.Clear(); ConfirmBox.Clear(); };
    }

    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ValidationText != null) ValidationText.Visibility = Visibility.Collapsed;
    }

    public void ShowValidation(string message)
    {
        ValidationText.Text = message; ValidationText.Visibility = Visibility.Visible;
        (UIElementAutomationPeer.FromElement(ValidationText) ?? UIElementAutomationPeer.CreatePeerForElement(ValidationText))?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        PinBox.Focus(); PinBox.SelectAll();
    }

    private async void OK_Click(object sender, RoutedEventArgs e)
    {
        if (_working) return;
        using var secure = PinBox.SecurePassword;
        char[] pin = CryptoService.SecureStringToCharArray(secure) ?? Array.Empty<char>();
        char[]? confirmation = null;
        try
        {
            if (pin.Length == 0) { ShowValidation("Enter your PIN to continue."); return; }
            if (_creating)
            {
                if (pin.Length < 4) { ShowValidation("Use at least 4 characters for your PIN."); return; }
                using var confirmSecure = ConfirmBox.SecurePassword;
                confirmation = CryptoService.SecureStringToCharArray(confirmSecure);
                if (confirmation == null || !pin.AsSpan().SequenceEqual(confirmation)) { ShowValidation("The PINs do not match. Please try again."); return; }
            }
            _working = true; SubmitButton.IsEnabled = false; CancelButton.IsEnabled = false;
            PinBox.IsEnabled = false; ConfirmBox.IsEnabled = false;
            if (_validate != null)
            {
                string? error = await _validate(pin);
                if (error != null) { ShowValidation(error); return; }
            }
            SecurePin = secure.Copy();
            DialogResult = true;
        }
        finally
        {
            Array.Clear(pin);
            if (confirmation != null) Array.Clear(confirmation);
            _working = false; SubmitButton.IsEnabled = true; CancelButton.IsEnabled = true;
            PinBox.IsEnabled = true; ConfirmBox.IsEnabled = true;
            if (IsVisible) PinBox.Focus();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_working && DialogResult != true) e.Cancel = true;
        base.OnClosing(e);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
