using System;
using System.Windows;
using System.Windows.Interop;
using TypeIt4Me.Services;
using TypeIt4Me.ViewModels;
using TypeIt4Me.Views;

namespace TypeIt4Me
{
    public partial class App : Application
    {
        private ILogger? _logger;
        private ISnippetManager? _snippetManager;
        private IHotkeyManager? _hotkeyManager;
        private IInputInjector? _inputInjector;
        private IFocusTracker? _focusTracker;
        private ISettingsManager? _settingsManager;
        private IAutoLockService? _autoLockService;
        private IThemeService? _themeService;

        private MainViewModel? _mainViewModel;
        private MainWindow? _mainWindow;

        public App()
        {
            // Global Exception Handling
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Security: Show generic error to user, log detailed info to file only
            string userMessage = $"An unexpected error occurred: {e.Exception.Message}\n\nPlease check the error log for details.";

            e.Handled = true; // Prevent crash if possible

            MessageBox.Show(userMessage, "TypeIt4Me Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Log detailed information to file (secure location)
            _logger?.LogError("Unhandled Dispatcher Exception", e.Exception);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1. Initialize Services
                _logger = new FileLogger();
                _snippetManager = new SnippetManager(_logger);
                _hotkeyManager = new HotkeyManager();
                _inputInjector = new InputInjector();
                _focusTracker = new FocusTracker();
                _settingsManager = new SettingsManager(_logger);
                _themeService = new ThemeService();

                // 2. Load Data
                await _settingsManager.LoadSettingsAsync();
                await _snippetManager.LoadSnippetsAsync();

                // AutoLock needs settings loaded
                _autoLockService = new AutoLockService(_settingsManager);

                // 3. Initialize ViewModel (Inject Services)
                _mainViewModel = new MainViewModel(_snippetManager, _hotkeyManager, _inputInjector,
                                                 _focusTracker, _settingsManager,
                                                 _autoLockService, _themeService);

                _mainViewModel.RequestSnippetEditor += MainViewModel_RequestSnippetEditor;
                _mainViewModel.RequestPinSet += MainViewModel_RequestPinSet;

                _mainViewModel.RequestPinInput += MainViewModel_RequestPinInput;
                _mainViewModel.RequestLockState += MainViewModel_RequestLockState;
                _mainViewModel.RequestUnlock += MainViewModel_RequestUnlock;
                _mainViewModel.RequestInput += MainViewModel_RequestInput;
                _mainViewModel.RequestShowHelp += MainViewModel_RequestShowHelp;

                // 4. Initialize Window
                _mainWindow = new MainWindow
                {
                    DataContext = _mainViewModel
                };

                // Check PIN before showing (V3 only - requires salt)
                if (!string.IsNullOrEmpty(_settingsManager.Settings.PinHash))
                {
                    // Validate that salt exists (V3 requirement)
                    if (string.IsNullOrEmpty(_settingsManager.Settings.PinSalt))
                    {
                        MessageBox.Show("PIN configuration is invalid. Please reset your PIN.", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        // Clear invalid PIN data
                        _settingsManager.Settings.PinHash = string.Empty;
                        _settingsManager.Settings.PinSalt = string.Empty;
                        await _settingsManager.SaveSettingsAsync();
                    }
                    else
                    {
                        bool unlocked = false;
                        while (!unlocked)
                        {
                            var pinWin = new PinEntryWindow("Unlock TypeIt4Me");
                            if (pinWin.ShowDialog() == true)
                            {
                                // Validate PIN using salted hash (V3 only)
                                string hash = Services.CryptoService.HashPin(pinWin.Pin.AsSpan(), _settingsManager.Settings.PinSalt);
                                if (hash == _settingsManager.Settings.PinHash)
                                {
                                    unlocked = true;
                                    // Set PIN in Manager for Decryption
                                    _snippetManager.SetPin(pinWin.Pin);
                                    // CRITICAL: Re-load snippets now that we have the PIN/Key
                                    await _snippetManager.LoadSnippetsAsync();
                                }
                                else
                                {
                                    MessageBox.Show("Invalid PIN. Please try again.", "Security", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                            else
                            {
                                // User cancelled the unlock (e.g. hit Cancel or Close on PIN dialog).
                                // We should NOT Shutdown the app here, just remain locked/hidden.
                                return; // App starts up but hidden
                            }
                        }
                    }
                }

                 // Register hotkeys AFTER loading (or re-loading) snippets
                _mainWindow.SourceInitialized += MainWindow_SourceInitialized;
                _mainWindow.Show();

                // If we re-loaded after SourceInitialized/Show, we might need to manually trigger hotkey registration if logic was there.
                // SourceInitialized calls RegisterSnippetHotkey loop.
                // So if we await LoadSnippetsAsync BEFORE SourceInitialized, it should be fine.
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Critical Error during startup: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 try
                 {
                     Shutdown();
                 }
                 catch { }
                 finally
                 {
                     Environment.Exit(1);
                 }
            }
        }

        private void MainViewModel_RequestPinSet()
        {
            if (_settingsManager == null) return;

            var pinWin = new PinEntryWindow("Set New PIN (Minimum 4 characters)");
            if (pinWin.ShowDialog() == true)
            {
                 // Security: Enforce minimum PIN length
                 if (string.IsNullOrEmpty(pinWin.Pin) || pinWin.Pin.Length < 4)
                 {
                     MessageBox.Show("PIN must be at least 4 characters long.", "Invalid PIN", MessageBoxButton.OK, MessageBoxImage.Warning);
                     return;
                 }

                 // Recommendation for strong PINs
                 if (pinWin.Pin.Length < 6)
                 {
                     var result = MessageBox.Show(
                         "Your PIN is short. For better security, we recommend using at least 6 characters.\n\nDo you want to continue with this PIN?",
                         "Security Recommendation",
                         MessageBoxButton.YesNo,
                         MessageBoxImage.Question);
                     if (result == MessageBoxResult.No)
                     {
                         return;
                     }
                 }

                 // Generate Salt and Hash
                 string salt = Services.CryptoService.GenerateSalt();
                 string hash = Services.CryptoService.HashPin(pinWin.Pin.AsSpan(), salt);

                 _settingsManager.Settings.PinSalt = salt;
                 _settingsManager.Settings.PinHash = hash;
                 _settingsManager.SaveSettingsAsync();

                 // Set PIN in manager and Save (this triggers encryption)
                 _snippetManager!.SetPin(pinWin.Pin);
                 _snippetManager.SaveSnippetsAsync();

                 MessageBox.Show("PIN Set Successfully! Your snippets are now encrypted with V3 (AES-256 + HMAC-SHA256).", "Security", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            if (_mainWindow == null) return;

            var helper = new WindowInteropHelper(_mainWindow);
            var handle = helper.Handle;

            // Initialize services that need HWND
            _hotkeyManager?.Initialize(handle);
            _focusTracker?.Start(handle);

            // Register existing hotkeys
            if (_snippetManager != null && _hotkeyManager != null)
            {
                var failedSnippets = new System.Collections.Generic.List<string>();
                foreach (var snippet in _snippetManager.Snippets)
                {
                    if (!RegisterSnippetHotkey(snippet))
                    {
                        failedSnippets.Add(snippet.Name);
                    }
                }

                if (failedSnippets.Count > 0)
                {
                    string msg = "Failed to register hotkeys for the following snippets (likely conflicts):\n\n" +
                                 string.Join("\n", failedSnippets);
                    MessageBox.Show(msg, "Hotkey Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void MainViewModel_RequestSnippetEditor(Models.Snippet snippet)
        {
            // If new snippet, snippet object is empty or pre-filled.
            // If editing, it's the existing reference.

            // We clone logic if we want cancel support (MVVM pattern), but for MVP modifying directly is risky but simple.
            // Better: use a clone/copy, then update if Save=true.
            // But SnippetEditorViewModel logic updates the object on Save.

            var vm = new SnippetEditorViewModel(snippet);
            var win = new SnippetEditorWindow
            {
                DataContext = vm,
                Owner = _mainWindow
            };

            vm.RequestClose += (result) =>
            {
                if (result)
                {
                    if (!_snippetManager!.Snippets.Contains(vm.CurrentSnippet))
                    {
                         _snippetManager.AddSnippet(vm.CurrentSnippet);
                         if (!RegisterSnippetHotkey(vm.CurrentSnippet))
                         {
                             MessageBox.Show("Failed to register hotkey for this snippet. Key combination may be in use.", "Hotkey Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                         }
                    }
                    else
                    {
                        _snippetManager.SaveSnippetsAsync();
                        // For MVP: Simplest way to update hotkeys is to re-register everything or just this one.
                        // Since we don't track IDs easily yet, let's just unregister all and re-register all.
                        // Efficient? No. Reliable? Yes.
                        ReloadHotkeys();
                    }
                }
                win.Close();
            };

            win.ShowDialog();
        }

        private void ReloadHotkeys()
        {
            if (_hotkeyManager == null || _snippetManager == null) return;

            _hotkeyManager.ClearRegistrations();

            var failedSnippets = new System.Collections.Generic.List<string>();
            foreach (var snippet in _snippetManager.Snippets)
            {
                if (!RegisterSnippetHotkey(snippet))
                {
                     failedSnippets.Add(snippet.Name);
                }
            }

            // Only warn if this was a manual reload or bulk op; for individual add, we handle separately
            if (failedSnippets.Count > 0)
            {
                 MessageBox.Show($"Failed to register {failedSnippets.Count} hotkeys.", "Hotkey Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool RegisterSnippetHotkey(Models.Snippet snippet)
        {
             if (_hotkeyManager != null && snippet.TriggerKey != System.Windows.Input.Key.None)
             {
                 int id = _hotkeyManager.Register(snippet.TriggerKey, snippet.TriggerModifiers, async () =>
                 {
                      if (_mainViewModel != null)
                      {
                          await Application.Current.Dispatcher.InvokeAsync(async () =>
                          {
                               await _mainViewModel.TriggerSnippetCommand.ExecuteAsync(snippet);
                          });
                      }
                 }, snippet.Id);

                 return id != 0;
             }
             return true; // No hotkey to register count as success
        }

        private string? MainViewModel_RequestPinInput()
        {
            var pinWin = new PinEntryWindow("Enter PIN for Import");
            if (pinWin.ShowDialog() == true)
            {
                return pinWin.Pin;
            }
            return null;
        }

        private void MainViewModel_RequestLockState(bool isLocked)
        {
            if (isLocked)
            {
                _mainWindow?.Hide();
                // We should ensure Tray Icon is visible (it usually is)
            }
            else
            {
                _mainWindow?.Show();
            }
        }

        private void MainViewModel_RequestUnlock()
        {
             // Prompt for PIN to unlock
             if (!string.IsNullOrEmpty(_settingsManager.Settings.PinHash))
             {
                 bool authenticated = false;

                 while (!authenticated)
                 {
                     var pinWin = new PinEntryWindow("Unlock TypeIt4Me");
                     if (pinWin.ShowDialog() == true)
                     {
                         // Use Salted Check
                         string hash = Services.CryptoService.HashPin(pinWin.Pin.AsSpan(), _settingsManager.Settings.PinSalt);
                         if (hash == _settingsManager.Settings.PinHash)
                         {
                             authenticated = true;
                             _mainViewModel.UnlockApp();
                             // Ensure PIN is set in manager (for decryption if needed, though usually set on startup)
                             _snippetManager.SetPin(pinWin.Pin);
                         }
                         else
                         {
                             MessageBox.Show("Invalid PIN.", "Security", MessageBoxButton.OK, MessageBoxImage.Warning);
                         }
                     }
                     else
                     {
                         // User cancelled unlock. Keep locked? Or Exit?
                         // If called from Restore, just keep hidden/locked.
                         break;
                     }
                 }
             }
             else
             {
                 // No PIN set? Just unlock.
                 _mainViewModel.UnlockApp();
             }
        }

        private string? MainViewModel_RequestInput(string message, string defaultVal)
        {
             var inputWin = new Views.InputWindow(message, defaultVal);
             if (inputWin.ShowDialog() == true)
             {
                 return inputWin.Result;
             }
             return null;
        }

        private void MainViewModel_RequestShowHelp()
        {
             var helpWin = new Views.HelpWindow();
             helpWin.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hotkeyManager?.Dispose();
            _focusTracker?.Dispose();
            _autoLockService?.Dispose();
            base.OnExit(e);
        }
    }
}
