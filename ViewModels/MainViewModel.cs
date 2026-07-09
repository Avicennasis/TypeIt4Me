using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TypeIt4Me.Models;
using TypeIt4Me.Services;

namespace TypeIt4Me.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISnippetManager _snippetManager;
        private readonly IHotkeyManager _hotkeyManager;
        private readonly IInputInjector _inputInjector;
        private readonly IFocusTracker _focusTracker;
        private readonly ISettingsManager _settingsManager;
        private readonly IAutoLockService _autoLockService;
        private readonly IThemeService _themeService;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isAlwaysOnTop = true;

        [ObservableProperty]
        private bool _minimizeToTray = true;

        [ObservableProperty]
        private bool _isMiniMode = false;
        
        [ObservableProperty]
        private bool _isDarkMode = false;
        
        [ObservableProperty]
        private string _miniModeButtonContent = "Expand";

        // Settings Wrappers
        public bool LockOnRestore
        {
            get => _settingsManager.Settings.LockOnRestore;
            set
            {
                if (_settingsManager.Settings.LockOnRestore != value)
                {
                    _settingsManager.Settings.LockOnRestore = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettingsAsync();
                }
            }
        }

        public int AutoLockMinutes
        {
            get => _settingsManager.Settings.AutoLockMinutes;
            set
            {
                // Security: Validate bounds (0 = disabled, max 24 hours = 1440 minutes)
                int validatedValue = Math.Max(0, Math.Min(value, 1440));

                if (_settingsManager.Settings.AutoLockMinutes != validatedValue)
                {
                    _settingsManager.Settings.AutoLockMinutes = validatedValue;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAutoLockOff));
                    OnPropertyChanged(nameof(IsAutoLock1Min));
                    OnPropertyChanged(nameof(IsAutoLock5Min));
                    _settingsManager.SaveSettingsAsync();
                    _autoLockService.EvaluateTimerState();
                }
            }
        }

        // UI Helpers for Menu Checkmarks
        public bool IsAutoLockOff => AutoLockMinutes == 0;
        public bool IsAutoLock1Min => AutoLockMinutes == 1;
        public bool IsAutoLock5Min => AutoLockMinutes == 5;

        public BulkObservableCollection<Snippet> FilteredSnippets { get; } = new BulkObservableCollection<Snippet>();

        public MainViewModel(ISnippetManager snippetManager, IHotkeyManager hotkeyManager, IInputInjector inputInjector, 
                             IFocusTracker focusTracker, ISettingsManager settingsManager, 
                             IAutoLockService autoLockService, IThemeService themeService)
        {
            _snippetManager = snippetManager;
            _hotkeyManager = hotkeyManager;
            _inputInjector = inputInjector;
            _focusTracker = focusTracker;
            _settingsManager = settingsManager;
            _autoLockService = autoLockService;
            _themeService = themeService;
            
            _autoLockService.OnLockTriggered += LockApp;

            Task.Run(LoadSettings);
            
            _snippetManager.Snippets.CollectionChanged += Snippets_CollectionChanged;
            RefreshSnippets();
        }

        private async Task LoadSettings()
        {
            await _settingsManager.LoadSettingsAsync();
            IsAlwaysOnTop = _settingsManager.Settings.AlwaysOnTop;
            IsMiniMode = _settingsManager.Settings.IsMiniMode;
            MinimizeToTray = _settingsManager.Settings.MinimizeToTray;
            IsDarkMode = _settingsManager.Settings.IsDarkMode;
            
            // Notify Security Props
            OnPropertyChanged(nameof(LockOnRestore));
            OnPropertyChanged(nameof(AutoLockMinutes));
            OnPropertyChanged(nameof(IsAutoLockOff));
            OnPropertyChanged(nameof(IsAutoLock1Min));
            OnPropertyChanged(nameof(IsAutoLock5Min));
            
            // Apply Initial Theme
            _themeService.SetTheme(IsDarkMode);
            
            // Start AutoLock Timer based on loaded settings
            _autoLockService.EvaluateTimerState();
        }
        
        partial void OnIsDarkModeChanged(bool value)
        {
             _settingsManager.Settings.IsDarkMode = value;
             _settingsManager.SaveSettingsAsync();
             _themeService.SetTheme(value);
        }
        
        partial void OnIsAlwaysOnTopChanged(bool value)
        {
             _settingsManager.Settings.AlwaysOnTop = value;
             _settingsManager.SaveSettingsAsync();
        }
        
        partial void OnMinimizeToTrayChanged(bool value)
        {
             _settingsManager.Settings.MinimizeToTray = value;
             _settingsManager.SaveSettingsAsync();
        }

        partial void OnIsMiniModeChanged(bool value)
        {
             _settingsManager.Settings.IsMiniMode = value;
             _settingsManager.SaveSettingsAsync();
             RequestWindowResize?.Invoke(value);
        }

        public event Action<bool> RequestWindowResize;

        partial void OnSearchTextChanged(string value)
        {
             // Simple Debounce: Cancel previous, start new delay
             // Using Task.Delay is simple but assumes single threading logic for UI updates which is true here.
             DebounceSearch();
        }

        private CancellationTokenSource? _searchCts;

        private void CancelPendingSearch()
        {
            var cts = Interlocked.Exchange(ref _searchCts, null);
            if (cts != null)
            {
                try { cts.Cancel(); } catch { }
                cts.Dispose();
            }
        }

        private async void DebounceSearch()
        {
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _searchCts, newCts);
            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch { }
                oldCts.Dispose();
            }

            var token = newCts.Token;

            try
            {
                await Task.Delay(300, token); // 300ms delay

                string filter = SearchText;
                // Snapshot the collection (shallow copy of references) to avoid InvalidOperationException
                // if the underlying collection is modified during background enumeration.
                // Using an array allocation is significantly faster and uses less memory than .ToList().
                Snippet[] source = _snippetManager.Snippets.ToArray();

                var results = await Task.Run(() => PerformFiltering(filter, source), token);

                if (!token.IsCancellationRequested)
                {
                    FilteredSnippets.ReplaceAll(results);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore - this is expected when search is cancelled
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.GetType().FullName}");
            }
            finally
            {
                Interlocked.CompareExchange(ref _searchCts, null, newCts);
                newCts.Dispose();
            }
        }

        private IEnumerable<Snippet> PerformFiltering(string filter, IEnumerable<Snippet> source)
        {
            var query = source.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                         (s.Category != null && s.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)));
            }
            return query.ToList();
        }

        private void Snippets_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RefreshSnippets();
        }

        private void RefreshSnippets()
        {
            CancelPendingSearch();
            FilteredSnippets.ReplaceAll(PerformFiltering(SearchText, _snippetManager.Snippets));

            // Re-register hotkeys? 
            // In a real app we'd diff and update. For MVP, we can treat this separately or just register all on Load.
            // But HotkeyManager needs the Window Handle which we might not have immediately in VM constructor.
        }

        [RelayCommand]
        private void ToggleAlwaysOnTop()
        {
            IsAlwaysOnTop = !IsAlwaysOnTop;
        }
        
        [RelayCommand]
        private void ToggleMinimizeToTray()
        {
            MinimizeToTray = !MinimizeToTray;
        }

        [RelayCommand]
        private void ToggleDarkMode()
        {
            IsDarkMode = !IsDarkMode;
        }

        [RelayCommand]
        private void ToggleMiniMode()
        {
            IsMiniMode = !IsMiniMode;
        }

        [RelayCommand]
        private async Task TriggerSnippet(Snippet snippet)
        {
            if (snippet == null) return;

            // If we are currently the active window, switch back to the last external window
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            // We need to know our own handle to compare, but logic is: if we are foreground, switch.
            // Assuming the UI click meant we are foreground.
            
            if (_focusTracker.LastExternalWindowHandle != IntPtr.Zero)
            {
                 NativeMethods.SetForegroundWindow(_focusTracker.LastExternalWindowHandle);
                 // Allow time for focus switch
                 await Task.Delay(200); 
            }

            await _inputInjector.TypeTextAsync(snippet.Content);
        }

        [RelayCommand]
        private async Task DeleteSnippet(Snippet snippet)
        {
            if (snippet == null) return;
            
            _hotkeyManager.UnregisterBySnippetId(snippet.Id);

            _snippetManager.RemoveSnippet(snippet);
            await _snippetManager.SaveSnippetsAsync();
        }

        [RelayCommand]
        private async Task AddSnippet()
        {
            // Ask the view to open the editor for a brand-new snippet.
            // The view (App.xaml.cs) subscribes to RequestSnippetEditor and is
            // the only layer that knows about WPF windows — this keeps the
            // ViewModel free of UI dependencies and unit-testable in isolation.
            RequestSnippetEditor?.Invoke(new Snippet());
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task EditSnippet(Snippet snippet)
        {
            if (snippet == null) return;
            RequestSnippetEditor?.Invoke(snippet);
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task ExportSnippets()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                DefaultExt = ".json",
                FileName = "snippets_export"
            };

            if (dialog.ShowDialog() == true)
            {
                await _snippetManager.ExportSnippetsAsync(dialog.FileName);
            }
        }

        [RelayCommand]
        private void SetPin()
        {
             RequestPinSet?.Invoke();
        }

        [RelayCommand]
        private void RemovePin()
        {
             _settingsManager.Settings.PinHash = string.Empty;
             _settingsManager.Settings.PinSalt = string.Empty;
             _settingsManager.SaveSettingsAsync();
             
             // Disable encryption and save as plain text
             _snippetManager.SetPin(ReadOnlySpan<char>.Empty);
             _snippetManager.SaveSnippetsAsync();
             
             MessageBox.Show(
                 "PIN Removed. Snippets are now stored in plain text.",
                 "Security",
                 MessageBoxButton.OK,
                 MessageBoxImage.Information);
        }

        public event Action RequestPinSet;
        public event Action<Snippet> RequestSnippetEditor;
        public event Action RequestUnlock; // Event to ask View/App to show PIN dialog
        public event Action<bool> RequestLockState; // True = Lock (Hide window), False = Unlock (Show window logic)

        // Lock State
        private bool _isLocked = false;
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                SetProperty(ref _isLocked, value);
                RequestLockState?.Invoke(value);
            }
        }


        [RelayCommand]
        private void SetAutoLock(string minutes)
        {
            if (int.TryParse(minutes, out int result))
            {
                AutoLockMinutes = result;
            }
        }
        
        [RelayCommand]
        private void SetCustomAutoLock()
        {
             // Simple input using Interaction.InputBox (VB) or a custom dialog.
             // Since we have PinEntryWindow, we can make a lightweight InputWindow or just re-use InputBox pattern 
             // if we reference Microsoft.VisualBasic, OR implemented a simple "RequestInput" event.
             
             RequestPinInput?.Invoke((result) => 
             {
                 // We re-use the RequestPinInput event mechanism but the View needs to know it's not a PIN...
                 // Actually RequestPinInput shows "PinEntryWindow" which masks input. That's bad for "Minutes".
                 // Let's create a specific Event for Generic Input.
             });
             
             RequestInput?.Invoke("Enter Auto-Lock timeout in minutes:", AutoLockMinutes.ToString(), (input) =>
             {
                 if (int.TryParse(input, out int result) && result >= 0)
                 {
                     AutoLockMinutes = result;
                 }
             });
        }
        
        public event Action<string, string, Action<string>> RequestInput;

        [RelayCommand]
        private void ShowHelp()
        {
             // We can fire an event or just let the View handle it via binding to this command?
             // Since it's a new Window, View-agnostic approach is tricky.
             // We'll use an event again.
             RequestShowHelp?.Invoke();
        }
        
        public event Action RequestShowHelp;

        public void LockApp()
        {
            IsLocked = true;
            // Clear PIN from memory on lock
            _snippetManager.SetPin(ReadOnlySpan<char>.Empty);
            // Clear snippets from memory? MVP: No, just hide UI.
        }

        public bool UnlockApp() // Called by View when PIN is entered
        {
            IsLocked = false;
            _autoLockService.UpdateLastActivity();
            return true;
        }

        [RelayCommand]
        private async Task ImportSnippets()
        {
             var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                bool success = await _snippetManager.ImportSnippetsAsync(dialog.FileName);
                if (!success)
                {
                    // Prompt for PIN
                    while (!success)
                    {
                         // We need a simple input dialog. 
                         // Check status: failed? 
                         string message = "Failed to decrypt snippets. Do you want to try entering a PIN?";
                         string caption = "Import Failed";
                         var result = MessageBox.Show(
                             message,
                             caption,
                             MessageBoxButton.YesNo,
                             MessageBoxImage.Question);
                         if (result == MessageBoxResult.No) break;
                         
                         // Request PIN from View
                         // We can reuse RequestPinSet or create a new event.
                         // Let's create a generic RequestPinInput event that returns a string (via args or callback).
                         // Simplified: We'll misuse the SettingsManager flow or just add a direct callback action.
                         
                         char[]? inputPin = null;
                         RequestPinInput?.Invoke((pin) => inputPin = pin);
                         
                         if (inputPin != null && inputPin.Length > 0)
                         {
                             try
                             {
                                 success = await _snippetManager.ImportSnippetsAsync(dialog.FileName, inputPin);
                                 if (success)
                                 {
                                     MessageBox.Show(
                                         "Import Successful!",
                                         "Import",
                                         MessageBoxButton.OK,
                                         MessageBoxImage.Information);
                                 }
                             }
                             finally
                             {
                                 Array.Clear(inputPin, 0, inputPin.Length);
                             }
                         }
                         else
                         {
                             break;
                         }
                    }
                }
                else
                {
                     MessageBox.Show(
                         "Import Successful!",
                         "Import",
                         MessageBoxButton.OK,
                         MessageBoxImage.Information);
                }
            }
        }
        
        public event Action<Action<char[]?>> RequestPinInput;

        [RelayCommand]
        private void RestoreFromTray()
        {
            // if LockOnRestore is true and PIN is set, we treat it as "Locked" even if IsLocked was false.
            if (_settingsManager.Settings.LockOnRestore && !string.IsNullOrEmpty(_settingsManager.Settings.PinHash))
            {
                 // Check if already locked?
                 // If not locked, we need to prompt.
                 // We can reuse RequestUnlock event.
                 RequestUnlock?.Invoke();
            }
            else if (IsLocked)
            {
                 // If auto-locked, we also need to prompt
                 RequestUnlock?.Invoke();
            }
            else
            {
                 // Just show window
                 RequestLockState?.Invoke(false); // False = Undo Lock / Show Window
            }
        }
    }
}
