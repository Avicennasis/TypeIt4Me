using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.ViewModels;
using TypeIt4Me.Views;

namespace TypeIt4Me;

public partial class App : Application
{
    private ILogger? _logger;
    private ISnippetManager? _snippetManager;
    private IHotkeyManager? _hotkeyManager;
    private IFocusTracker? _focusTracker;
    private ISettingsManager? _settingsManager;
    private IAutoLockService? _autoLockService;
    private IThemeService? _themeService;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private HelpWindow? _helpWindow;
    private bool _unlocking;

    public App()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, e) =>
        {
            _logger?.LogError("Unhandled UI exception", e.Exception);
            e.Handled = true;
            // Do not put snippet text, paths, or exception messages into error dialogs.
            new DialogService().ShowInformation("Something went wrong. Your saved collection has not been reset. Check error.log in the TypeIt4Me data folder for details.", "TypeIt4Me could not complete that action");
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _logger = new FileLogger();
            _snippetManager = new SnippetManager(_logger);
            _hotkeyManager = new HotkeyManager();
            _focusTracker = new FocusTracker();
            _settingsManager = new SettingsManager(_logger);
            _themeService = new ThemeService();
            await _settingsManager.LoadSettingsAsync();
            _themeService.SetTheme(_settingsManager.Settings.IsDarkMode);
            if (string.IsNullOrEmpty(_settingsManager.Settings.PinHash)) await _snippetManager.LoadSnippetsAsync();
            _autoLockService = new AutoLockService(_settingsManager);
            _mainViewModel = new MainViewModel(_snippetManager, _hotkeyManager, new InputInjector(new WindowsInputSender()),
                _focusTracker, _settingsManager, _autoLockService, _themeService, _logger);
            await _mainViewModel.Initialization;
            _mainWindow = new MainWindow { DataContext = _mainViewModel };
            MainWindow = _mainWindow;
            _mainWindow.RestorePlacement(_settingsManager.Settings, () => _ = _settingsManager.SaveSettingsAsync());
            _mainViewModel.RequestSnippetEditor += EditSnippet;
            _mainViewModel.RequestPinSet += SetPin;
            _mainViewModel.RequestPinInput += RequestPinInput;
            _mainViewModel.RequestLockState += ApplyLockState;
            _mainViewModel.RequestUnlock += Unlock;
            _mainViewModel.RequestInput += RequestInput;
            _mainViewModel.RequestShowHelp += ShowHelp;
            _mainViewModel.RequestShowSettings += ShowSettings;
            _snippetManager.Snippets.CollectionChanged += (_, _) => ReloadHotkeys();
            _mainWindow.SourceInitialized += (_, _) =>
            {
                var handle = new WindowInteropHelper(_mainWindow).Handle;
                _hotkeyManager.Initialize(handle); _focusTracker.Start(handle); ReloadHotkeys();
            };
            InputManager.Current.PreProcessInput += TrackActivity;
            if (_mainViewModel.HasPin) _mainViewModel.LockApp();
            _mainWindow.Show();
            if (_mainViewModel.IsLocked) Unlock();
        }
        catch (Exception ex)
        {
            _logger?.LogError("Startup failed", ex);
            // Native fallback is intentional if the resource system itself cannot start.
            MessageBox.Show("TypeIt4Me could not start. Check the error log in %AppData%\\TypeIt4Me. No data has been reset.", "TypeIt4Me", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void TrackActivity(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is KeyEventArgs or MouseButtonEventArgs or MouseWheelEventArgs) _autoLockService?.UpdateLastActivity();
    }

    private async void EditSnippet(Snippet snippet)
    {
        if (_mainViewModel == null || _snippetManager == null || _mainViewModel.IsLocked) return;
        var vm = new SnippetEditorViewModel(snippet);
        var window = new SnippetEditorWindow { DataContext = vm, Owner = DialogService.ActiveOwner ?? _mainWindow };
        _mainViewModel.IsDialogOpen = true;
        try
        {
            window.ShowDialog();
            if (!window.Saved || _mainViewModel.IsLocked) return;
            using var operation = _mainViewModel.BeginDataOperation();
            if (!_snippetManager.Snippets.Contains(snippet)) await _snippetManager.AddSnippet(snippet);
            else await _snippetManager.SaveSnippetsAsync();
            _mainViewModel.RefreshAfterEdit(); ReloadHotkeys();
            _mainViewModel.ReportStatus("Snippet saved.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Snippet save failed", ex);
            _mainViewModel.RefreshAfterEdit();
            _mainViewModel.ReportStatus("Could not save changes. Keep TypeIt4Me open and try saving again.", true);
            new DialogService().ShowInformation("Could not save the snippet. Check the data folder and error log before closing TypeIt4Me.", "Could not save snippet");
        }
        finally { _mainViewModel.IsDialogOpen = false; }
    }

    private void ReloadHotkeys()
    {
        if (_hotkeyManager == null || _snippetManager == null || _mainViewModel == null || _mainWindow == null) return;
        if (new WindowInteropHelper(_mainWindow).Handle == IntPtr.Zero) return;
        _hotkeyManager.ClearRegistrations();
        if (_mainViewModel.IsLocked) return;
        int failed = 0;
        foreach (var snippet in _snippetManager.Snippets)
        {
            if (snippet.TriggerKey == Key.None) continue;
            int id = _hotkeyManager.Register(snippet.TriggerKey, snippet.TriggerModifiers,
                () => { if (_mainViewModel.TriggerSnippetCommand.CanExecute(snippet)) _mainViewModel.TriggerSnippetCommand.Execute(snippet); }, snippet.Id);
            if (id == 0) failed++;
        }
        if (failed > 0) _mainViewModel.ReportStatus($"{failed} hotkey{(failed == 1 ? " is" : "s are")} unavailable. Edit the shortcuts to resolve conflicts.", true);
    }

    private async Task<string?> ValidateCurrentPin(char[] pin)
    {
        if (_settingsManager == null) return "PIN settings are unavailable.";
        if (string.IsNullOrEmpty(_settingsManager.Settings.PinSalt)) return "PIN metadata is incomplete. Restore settings.json from a backup; your data has not been reset.";
        string hash = await Task.Run(() => CryptoService.HashPin(pin, _settingsManager.Settings.PinSalt));
        return PinHashComparison.Equal(hash, _settingsManager.Settings.PinHash) ? null : "That PIN did not match. Please try again.";
    }

    private void Unlock()
    {
        if (_unlocking || _mainViewModel == null || _settingsManager == null || _snippetManager == null) return;
        if (!_mainViewModel.HasPin) { _mainViewModel.UnlockApp(); return; }
        if (!_mainViewModel.UnlockCommand.CanExecute(null)) return;
        _unlocking = true; _mainViewModel.IsDialogOpen = true;
        try
        {
            var window = new PinEntryWindow("Unlock TypeIt4Me", validate: async pin =>
            {
                _autoLockService?.UpdateLastActivity();
                string? error = await ValidateCurrentPin(pin);
                if (error != null) return error;
                _snippetManager.SetPin(pin);
                await _snippetManager.LoadSnippetsAsync();
                return null;
            }) { Owner = _mainWindow };
            if (window.ShowDialog() == true)
            {
                window.SecurePin?.Dispose(); _mainViewModel.UnlockApp(); ReloadHotkeys();
                _mainViewModel.ReportStatus("Unlocked. Your snippets are ready.");
            }
        }
        finally { _mainViewModel.IsDialogOpen = false; _unlocking = false; }
    }

    private void SetPin()
    {
        if (_settingsManager == null || _snippetManager == null || _mainViewModel == null || _mainViewModel.IsLocked) return;
        char[]? previousPin = null;
        using var operation = _mainViewModel.BeginDataOperation();
        _mainViewModel.IsDialogOpen = true;
        try
        {
            if (_mainViewModel.HasPin)
            {
                var current = new PinEntryWindow("Confirm current PIN", validate: ValidateCurrentPin) { Owner = DialogService.ActiveOwner };
                if (current.ShowDialog() != true) return;
                using var secure = current.SecurePin;
                previousPin = CryptoService.SecureStringToCharArray(secure);
            }
            var window = new PinEntryWindow("Protect your snippets", creating: true, validate: async pin =>
            {
                if (_mainViewModel.IsLocked) return "TypeIt4Me locked while this dialog was open. Cancel and unlock before setting a PIN.";
                try
                {
                    var salt = CryptoService.GenerateSalt();
                    var hash = await Task.Run(() => CryptoService.HashPin(pin, salt));
                    _snippetManager.SetPin(pin);
                    await _snippetManager.SaveSnippetsAsync();
                    _settingsManager.Settings.PinSalt = salt; _settingsManager.Settings.PinHash = hash;
                    await _settingsManager.SaveSettingsAsync();
                    return null;
                }
                catch (Exception ex)
                {
                    _snippetManager.SetPin(previousPin);
                    _logger?.LogError("PIN save failed", ex);
                    return "Could not save PIN protection. Check the data folder and try again.";
                }
            }) { Owner = DialogService.ActiveOwner };
            if (window.ShowDialog() == true)
            {
                window.SecurePin?.Dispose(); _mainViewModel.RefreshSecurityState();
                _mainViewModel.ReportStatus("PIN protection is on. Snippets and exports are encrypted.");
            }
        }
        finally
        {
            if (previousPin != null) Array.Clear(previousPin);
            _mainViewModel.IsDialogOpen = false;
        }
    }

    private void RequestPinInput(Action<char[]?> callback)
    {
        var window = new PinEntryWindow("Enter PIN") { Owner = DialogService.ActiveOwner };
        char[]? pin = null;
        bool wasDialogOpen = _mainViewModel?.IsDialogOpen == true;
        if (_mainViewModel != null) _mainViewModel.IsDialogOpen = true;
        try
        {
            if (window.ShowDialog() == true)
            {
                using var secure = window.SecurePin;
                pin = CryptoService.SecureStringToCharArray(secure); callback(pin);
            }
            else callback(null);
        }
        finally
        {
            if (pin != null) Array.Clear(pin);
            if (_mainViewModel != null) _mainViewModel.IsDialogOpen = wasDialogOpen;
        }
    }

    private void ApplyLockState(bool locked)
    {
        if (locked)
        {
            foreach (Window window in Windows.OfType<Window>().ToArray())
                if (window is SnippetEditorWindow editor) editor.CloseForLock();
                else if (window is SettingsWindow or HelpWindow) window.Close();
            _hotkeyManager?.ClearRegistrations();
        }
        else
        {
            _mainWindow?.Show();
            if (_mainWindow != null) { _mainWindow.WindowState = WindowState.Normal; WindowPlacement.EnsureVisible(_mainWindow); _mainWindow.Activate(); }
        }
    }

    private void RequestInput(string message, string defaultValue, Action<string> callback)
    {
        var window = new InputWindow(message, defaultValue, value => int.TryParse(value, out int minutes) && minutes >= 0 && minutes <= 1440 ? null : "Enter a whole number from 0 to 1440.") { Owner = DialogService.ActiveOwner };
        if (window.ShowDialog() == true) callback(window.Result);
    }

    private void ShowSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow { Owner = _mainWindow, DataContext = _mainViewModel };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else _settingsWindow.Activate();
    }

    private void ShowHelp()
    {
        if (_helpWindow == null)
        {
            _helpWindow = new HelpWindow { Owner = DialogService.ActiveOwner ?? _mainWindow };
            _helpWindow.Closed += (_, _) => _helpWindow = null;
            _helpWindow.Show();
        }
        else _helpWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        InputManager.Current.PreProcessInput -= TrackActivity;
        _hotkeyManager?.Dispose(); _focusTracker?.Dispose(); _autoLockService?.Dispose(); _snippetManager?.Dispose();
        (_themeService as IDisposable)?.Dispose(); (_logger as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
