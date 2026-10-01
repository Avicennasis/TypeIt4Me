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
        private readonly ILogger _logger;
        private readonly IDialogService _dialogService;

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
        private string _statusMessage = "Select a text field in another app, then insert a snippet.";

        [ObservableProperty]
        private bool _statusIsError;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UseSelectedSnippetCommand))]
        [NotifyCanExecuteChangedFor(nameof(EditSelectedSnippetCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteSelectedSnippetCommand))]
        private Snippet? _selectedSnippet;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private bool _isDialogOpen;

        partial void OnIsDialogOpenChanged(bool value) => NotifyDataCommands();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ApplyAutoLockCommand))]
        [NotifyPropertyChangedFor(nameof(AutoLockValidation))]
        private string _autoLockDraft = "0";

        public string AutoLockValidation => CanApplyAutoLock() ? string.Empty : "Enter a whole number from 0 to 1440.";
        private bool CanApplyAutoLock() => int.TryParse(AutoLockDraft, out int minutes) && minutes >= 0 && minutes <= 1440;

        [RelayCommand(CanExecute = nameof(CanApplyAutoLock))]
        private void ApplyAutoLock()
        {
            if (int.TryParse(AutoLockDraft, out int minutes) && CanApplyAutoLock())
            {
                AutoLockMinutes = minutes;
                ReportStatus(minutes == 0 ? "Auto-lock is off." : $"Auto-lock set to {minutes} minutes.");
            }
        }

        [RelayCommand] private void SetLightTheme() => IsDarkMode = false;
        [RelayCommand] private void SetDarkTheme() => IsDarkMode = true;

        private bool _applyingSettings;
        private int _dataOperations;
        private bool _clearPinAfterOperation;

        [ObservableProperty]
        private bool _isInserting;

        public IDisposable BeginDataOperation()
        {
            _dataOperations++;
            NotifyDataCommands();
            return new DataOperation(() =>
            {
                _dataOperations--;
                if (_dataOperations == 0 && _clearPinAfterOperation)
                {
                    _clearPinAfterOperation = false;
                    _snippetManager.SetPin(ReadOnlySpan<char>.Empty);
                }
                NotifyDataCommands();
            });
        }

        private sealed class DataOperation : IDisposable
        {
            private Action? _end;
            public DataOperation(Action end) => _end = end;
            public void Dispose() => Interlocked.Exchange(ref _end, null)?.Invoke();
        }

        private void NotifyDataCommands()
        {
            OnPropertyChanged(nameof(IsWorking));
            OnPropertyChanged(nameof(ResultSummary));
            AddSnippetCommand.NotifyCanExecuteChanged();
            EditSnippetCommand.NotifyCanExecuteChanged();
            DeleteSnippetCommand.NotifyCanExecuteChanged();
            ImportSnippetsCommand.NotifyCanExecuteChanged();
            ExportSnippetsCommand.NotifyCanExecuteChanged();
            SetPinCommand.NotifyCanExecuteChanged();
            RemovePinCommand.NotifyCanExecuteChanged();
            TriggerSnippetCommand.NotifyCanExecuteChanged();
            UseSelectedSnippetCommand.NotifyCanExecuteChanged();
            EditSelectedSnippetCommand.NotifyCanExecuteChanged();
            DeleteSelectedSnippetCommand.NotifyCanExecuteChanged();
            UnlockCommand.NotifyCanExecuteChanged();
        }
        public Task Initialization { get; }
        public bool IsWorking => _dataOperations > 0 || IsInserting;
        public bool IsUnlocked => !IsLocked;
        public bool HasPin => !string.IsNullOrEmpty(_settingsManager.Settings.PinHash);
        public bool IsLightMode => !IsDarkMode;
        public bool HasResults => !IsLocked && FilteredSnippets.Count > 0;
        public bool IsFirstRun => !IsLocked && _snippetManager.Snippets.Count == 0;
        public bool HasNoResults => !IsLocked && !IsFirstRun && !HasResults;
        public string ResultSummary => IsLocked ? "Locked" : IsWorking ? "Working…" : IsSearching ? "Searching…" :
            string.IsNullOrWhiteSpace(SearchText) ? $"{FilteredSnippets.Count} snippet{(FilteredSnippets.Count == 1 ? "" : "s")}" :
            $"{FilteredSnippets.Count} of {_snippetManager.Snippets.Count} snippets";
        public string SecuritySummary => HasPin ? "PIN protection is on" : "No PIN · stored as plain text";

        public void RefreshSecurityState()
        {
            OnPropertyChanged(nameof(HasPin));
            OnPropertyChanged(nameof(SecuritySummary));
            LockCommand.NotifyCanExecuteChanged();
            RemovePinCommand.NotifyCanExecuteChanged();
        }

        public void ReportStatus(string message, bool isError = false)
        {
            StatusMessage = message;
            StatusIsError = isError;
        }

        private void NotifyResults()
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(IsFirstRun));
            OnPropertyChanged(nameof(HasNoResults));
            OnPropertyChanged(nameof(ResultSummary));
            if (SelectedSnippet == null || !FilteredSnippets.Contains(SelectedSnippet))
                SelectedSnippet = FilteredSnippets.FirstOrDefault();
        }

        partial void OnIsSearchingChanged(bool value)
        {
            OnPropertyChanged(nameof(ResultSummary));
            UseSelectedSnippetCommand.NotifyCanExecuteChanged();
            EditSelectedSnippetCommand.NotifyCanExecuteChanged();
            DeleteSelectedSnippetCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsInsertingChanged(bool value) => NotifyDataCommands();

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
                    AutoLockDraft = validatedValue.ToString();
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
                             IAutoLockService autoLockService, IThemeService themeService, ILogger logger,
                             IDialogService? dialogService = null)
        {
            _snippetManager = snippetManager;
            _hotkeyManager = hotkeyManager;
            _inputInjector = inputInjector;
            _focusTracker = focusTracker;
            _settingsManager = settingsManager;
            _autoLockService = autoLockService;
            _themeService = themeService;
            _logger = logger;
            _dialogService = dialogService ?? new DialogService();

            _autoLockService.OnLockTriggered += LockApp;

            Initialization = LoadSettings();

            _snippetManager.Snippets.CollectionChanged += Snippets_CollectionChanged;
            FilteredSnippets.CollectionChanged += (_, _) => NotifyResults();
            RefreshSnippets();
        }

        private async Task LoadSettings()
        {
            await _settingsManager.LoadSettingsAsync();
            _applyingSettings = true;
            IsAlwaysOnTop = _settingsManager.Settings.AlwaysOnTop;
            IsMiniMode = _settingsManager.Settings.IsMiniMode;
            MinimizeToTray = _settingsManager.Settings.MinimizeToTray;
            IsDarkMode = _settingsManager.Settings.IsDarkMode;
            AutoLockDraft = AutoLockMinutes.ToString();
            _applyingSettings = false;

            // Notify Security Props
            OnPropertyChanged(nameof(LockOnRestore));
            OnPropertyChanged(nameof(AutoLockMinutes));
            OnPropertyChanged(nameof(IsAutoLockOff));
            OnPropertyChanged(nameof(IsAutoLock1Min));
            OnPropertyChanged(nameof(IsAutoLock5Min));

            // Apply the initial theme
            _themeService.SetTheme(IsDarkMode);

            // Start AutoLock Timer based on loaded settings
            _autoLockService.EvaluateTimerState();
            RefreshSecurityState();
        }

        partial void OnIsDarkModeChanged(bool value)
        {
             _settingsManager.Settings.IsDarkMode = value;
             if (!_applyingSettings) _ = _settingsManager.SaveSettingsAsync();
             _themeService.SetTheme(value);
             OnPropertyChanged(nameof(IsLightMode));
        }

        partial void OnIsAlwaysOnTopChanged(bool value)
        {
             _settingsManager.Settings.AlwaysOnTop = value;
             if (!_applyingSettings) _ = _settingsManager.SaveSettingsAsync();
        }

        partial void OnMinimizeToTrayChanged(bool value)
        {
             _settingsManager.Settings.MinimizeToTray = value;
             if (!_applyingSettings) _ = _settingsManager.SaveSettingsAsync();
        }

        partial void OnIsMiniModeChanged(bool value)
        {
             _settingsManager.Settings.IsMiniMode = value;
             if (!_applyingSettings) _ = _settingsManager.SaveSettingsAsync();
             RequestWindowResize?.Invoke(value);
        }

        public event Action<bool>? RequestWindowResize;

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
                try { cts.Cancel(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error cancelling search: {ex.GetType().FullName}"); }
                cts.Dispose();
            }
        }

        private async void DebounceSearch()
        {
            IsSearching = true;
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _searchCts, newCts);
            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error cancelling search: {ex.GetType().FullName}"); }
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
                    FilteredSnippets.ReplaceAll(IsLocked ? Array.Empty<Snippet>() : results);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore - this is expected when search is cancelled
            }
            catch (Exception ex)
            {
                _logger.LogError("Search error", ex);
            }
            finally
            {
                if (Interlocked.CompareExchange(ref _searchCts, null, newCts) == newCts) IsSearching = false;
                newCts.Dispose();
            }
        }

        private IEnumerable<Snippet> PerformFiltering(string filter, IEnumerable<Snippet> source)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return source.ToList();
            }

            filter = filter.Trim();
            int filterLength = filter.Length;
            var result = new List<Snippet>(source is ICollection<Snippet> col ? col.Count : 100);
            foreach (var s in source)
            {
                if (s == null) continue;

                string name = s.Name;
                if (name != null && name.Length >= filterLength && name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(s);
                    continue;
                }

                string category = s.Category;
                if (category != null && category.Length >= filterLength && category.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(s);
                    continue;
                }

                if (s.Content?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
                    s.HotkeyLabel.Contains(filter, StringComparison.OrdinalIgnoreCase)) result.Add(s);
            }
            return result;
        }

        private void Snippets_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RefreshSnippets();
        }

        private void RefreshSnippets()
        {
            CancelPendingSearch();
            IsSearching = false;
            FilteredSnippets.ReplaceAll(IsLocked ? Array.Empty<Snippet>() : PerformFiltering(SearchText, _snippetManager.Snippets));
        }

        public void RefreshAfterEdit() => RefreshSnippets();

        [RelayCommand]
        private void ClearSearch() => SearchText = string.Empty;

        [RelayCommand]
        private void ShowSettings() => RequestShowSettings?.Invoke();

        public event Action? RequestShowSettings;

        private bool CanManageSnippets() => !IsLocked && !IsDialogOpen && !IsInserting && _dataOperations == 0;
        private bool CanUseSnippet(Snippet? snippet) => CanManageSnippets() && snippet != null;
        private bool CanUseSelectedSnippet() => !IsSearching && CanUseSnippet(SelectedSnippet);

        [RelayCommand(CanExecute = nameof(CanUseSelectedSnippet))]
        private Task UseSelectedSnippet() => !CanUseSelectedSnippet() || SelectedSnippet == null ? Task.CompletedTask : TriggerSnippet(SelectedSnippet);

        [RelayCommand(CanExecute = nameof(CanUseSelectedSnippet))]
        private void EditSelectedSnippet()
        {
            if (CanUseSelectedSnippet() && SelectedSnippet != null) RequestSnippetEditor?.Invoke(SelectedSnippet);
        }

        [RelayCommand(CanExecute = nameof(CanUseSelectedSnippet))]
        private Task DeleteSelectedSnippet() => !CanUseSelectedSnippet() || SelectedSnippet == null ? Task.CompletedTask : DeleteSnippet(SelectedSnippet);

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

        [RelayCommand(CanExecute = nameof(CanUseSnippet))]
        private async Task TriggerSnippet(Snippet? snippet)
        {
            if (snippet == null || !CanUseSnippet(snippet)) return;
            if (FilteredSnippets.Contains(snippet)) SelectedSnippet = snippet;
            IsInserting = true;
            try
            {

                // Return to the last external target before sending input.
                IntPtr target = _focusTracker.LastExternalWindowHandle;
                if (target == IntPtr.Zero)
                {
                    ReportStatus("Choose a text field in another app, then try again.", true);
                    return;
                }

                if (NativeMethods.GetForegroundWindow() != target)
                {
                     if (!NativeMethods.SetForegroundWindow(target))
                     {
                         ReportStatus("Could not focus the target app. Select its text field and try again.", true);
                         return;
                     }
                     // Allow time for focus switch
                     await Task.Delay(200);
                }

                if (IsLocked || NativeMethods.GetForegroundWindow() != target) return;
                try
                {
                    _autoLockService.UpdateLastActivity();
                    await _inputInjector.TypeTextAsync(snippet.Content);
                    ReportStatus($"Sent “{snippet.Name}” to the active app.");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not insert snippet", ex);
                    ReportStatus("Could not insert this snippet. Check its length and the target app.", true);
                }
            }
            finally { IsInserting = false; }
        }

        [RelayCommand(CanExecute = nameof(CanUseSnippet))]
        private async Task DeleteSnippet(Snippet snippet)
        {
            if (snippet == null || !CanManageSnippets()) return;
            if (!_dialogService.ShowConfirmation($"Delete “{snippet.Name}”? This permanently removes the snippet from this device.", "Delete snippet") || IsLocked) return;
            using var operation = BeginDataOperation();
            int index = _snippetManager.Snippets.IndexOf(snippet);
            _hotkeyManager.UnregisterBySnippetId(snippet.Id);
            try
            {
                await _snippetManager.RemoveSnippet(snippet);
                ReportStatus("Snippet deleted.");
            }
            catch (Exception ex)
            {
                if (index >= 0 && !_snippetManager.Snippets.Contains(snippet))
                    _snippetManager.Snippets.Insert(Math.Min(index, _snippetManager.Snippets.Count), snippet);
                _logger.LogError("Could not delete snippet", ex);
                ReportStatus("Could not save the deletion. The snippet is still available; check the data folder and try again.", true);
            }
        }

        [RelayCommand(CanExecute = nameof(CanManageSnippets))]
        private async Task AddSnippet()
        {
            // Ask the view to open the editor for a brand-new snippet.
            // The view (App.xaml.cs) subscribes to RequestSnippetEditor and is
            // the only layer that knows about WPF windows — this keeps the
            // ViewModel free of UI dependencies and unit-testable in isolation.
            if (CanManageSnippets()) RequestSnippetEditor?.Invoke(new Snippet());
            await Task.CompletedTask;
        }

        [RelayCommand(CanExecute = nameof(CanUseSnippet))]
        private async Task EditSnippet(Snippet snippet)
        {
            if (snippet == null || !CanManageSnippets()) return;
            RequestSnippetEditor?.Invoke(snippet);
            await Task.CompletedTask;
        }

        [RelayCommand(CanExecute = nameof(CanManageSnippets))]
        private async Task ExportSnippets()
        {
            if (!CanManageSnippets()) return;
            using var operation = BeginDataOperation();
            string? filePath = _dialogService.SaveFileDialog("JSON Files (*.json)|*.json", ".json", "snippets_export");
            if (!string.IsNullOrEmpty(filePath) && !IsLocked)
            {
                await _snippetManager.ExportSnippetsAsync(filePath);
                ReportStatus(HasPin ? "Encrypted export saved." : "Export saved as plain text.");
            }
        }

        [RelayCommand(CanExecute = nameof(CanManageSnippets))]
        private void SetPin()
        {
            if (CanManageSnippets()) RequestPinSet?.Invoke();
        }

        private bool CanRemovePin() => HasPin && CanManageSnippets();

        [RelayCommand(CanExecute = nameof(CanRemovePin))]
        private async Task RemovePin()
        {
            if (!CanRemovePin() || !_dialogService.ShowConfirmation("Removing the PIN saves your snippets as plain text. Enter your current PIN to confirm.", "Remove PIN") || IsLocked) return;
            char[]? pin = RequestPinCopy();
            if (pin == null || pin.Length == 0) return;
            using var operation = BeginDataOperation();
            try
            {
                if (IsLocked) return;
                string hash = await Task.Run(() => CryptoService.HashPin(pin, _settingsManager.Settings.PinSalt));
                if (IsLocked) return;
                if (!PinHashComparison.Equal(hash, _settingsManager.Settings.PinHash))
                {
                    _dialogService.ShowInformation("The PIN was incorrect. PIN protection is still on.", "Could not remove PIN");
                    return;
                }
                _snippetManager.SetPin(ReadOnlySpan<char>.Empty);
                try { await _snippetManager.SaveSnippetsAsync(); }
                catch { _snippetManager.SetPin(pin); throw; }
                _settingsManager.Settings.PinHash = string.Empty;
                _settingsManager.Settings.PinSalt = string.Empty;
                await _settingsManager.SaveSettingsAsync();
                RefreshSecurityState();
                ReportStatus("PIN removed. Snippets are now stored as plain text.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Could not remove PIN", ex);
                _dialogService.ShowInformation("Could not remove PIN protection. Check the error log before trying again.", "Could not remove PIN");
            }
            finally { Array.Clear(pin); }
        }

        public event Action? RequestPinSet;
        public event Action<Snippet>? RequestSnippetEditor;
        public event Action? RequestUnlock;
        public event Action<bool>? RequestLockState;

        // Lock State
        private bool _isLocked = false;
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (!SetProperty(ref _isLocked, value)) return;
                OnPropertyChanged(nameof(IsUnlocked));
                RefreshSnippets();
                AddSnippetCommand.NotifyCanExecuteChanged();
                EditSnippetCommand.NotifyCanExecuteChanged();
                DeleteSnippetCommand.NotifyCanExecuteChanged();
                TriggerSnippetCommand.NotifyCanExecuteChanged();
                UseSelectedSnippetCommand.NotifyCanExecuteChanged();
                EditSelectedSnippetCommand.NotifyCanExecuteChanged();
                DeleteSelectedSnippetCommand.NotifyCanExecuteChanged();
                ImportSnippetsCommand.NotifyCanExecuteChanged();
                ExportSnippetsCommand.NotifyCanExecuteChanged();
                SetPinCommand.NotifyCanExecuteChanged();
                RefreshSecurityState();
                UnlockCommand.NotifyCanExecuteChanged();
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
             RequestInput?.Invoke("Enter Auto-Lock timeout in minutes:", AutoLockMinutes.ToString(), (input) =>
             {
                 if (int.TryParse(input, out int result) && result >= 0 && result <= 1440)
                 {
                     AutoLockMinutes = result;
                 }
             });
        }

        public event Action<string, string, Action<string>>? RequestInput;

        [RelayCommand]
        private void ShowHelp()
        {
            RequestShowHelp?.Invoke();
        }

        public event Action? RequestShowHelp;

        private bool CanLock() => HasPin && !IsLocked;

        [RelayCommand(CanExecute = nameof(CanLock))]
        private void Lock()
        {
            if (CanLock()) LockApp();
        }

        private bool CanUnlock() => IsLocked && _dataOperations == 0;

        [RelayCommand(CanExecute = nameof(CanUnlock))]
        private void Unlock()
        {
            if (CanUnlock()) RequestUnlock?.Invoke();
        }

        public void LockApp()
        {
            if (IsLocked) return;
            IsLocked = true;
            ReportStatus("Snippets locked. Enter your PIN to continue.");
            // Redact immediately, but let an active encrypted save finish with its
            // existing key. Clearing it mid-save can accidentally write plaintext.
            if (_dataOperations == 0) _snippetManager.SetPin(ReadOnlySpan<char>.Empty);
            else _clearPinAfterOperation = true;
        }

        public bool UnlockApp() // Called by View when PIN is entered
        {
            if (_dataOperations != 0) return false;
            IsLocked = false;
            _autoLockService.UpdateLastActivity();
            ReportStatus("Unlocked. Your snippets are ready.");
            return true;
        }

        [RelayCommand(CanExecute = nameof(CanManageSnippets))]
        private async Task ImportSnippets()
        {
            if (!CanManageSnippets()) return;
            using var operation = BeginDataOperation();
            string? filePath = _dialogService.OpenFileDialog("JSON Files (*.json)|*.json", ".json");
            if (string.IsNullOrEmpty(filePath) || IsLocked)
                return;

            bool success = await _snippetManager.ImportSnippetsAsync(filePath);
            if (success)
            {
                ShowImportSuccessfulMessage();
                return;
            }

            await TryImportWithPinAsync(filePath);
        }

        private async Task TryImportWithPinAsync(string fileName)
        {
            bool success = false;
            while (!success)
            {
                if (IsLocked) break;
                string message = "Failed to decrypt snippets. Do you want to try entering a PIN?";
                string caption = "Import Failed";
                bool userWantsToTryPin = _dialogService.ShowConfirmation(message, caption);

                if (!userWantsToTryPin)
                    break;

                char[]? inputPin = RequestPinCopy();

                if (inputPin == null || inputPin.Length == 0)
                    break;

                try
                {
                    if (IsLocked) break;
                    success = await _snippetManager.ImportSnippetsAsync(fileName, inputPin);
                    if (success)
                    {
                        ShowImportSuccessfulMessage();
                    }
                }
                finally
                {
                    Array.Clear(inputPin, 0, inputPin.Length);
                }
            }
        }

        private void ShowImportSuccessfulMessage()
        {
            _dialogService.ShowInformation("Import Successful!", "Import");
        }

        public event Action<Action<char[]?>>? RequestPinInput;

        private char[]? RequestPinCopy()
        {
            char[]? result = null;
            // The view clears its buffer as soon as the callback returns. Own a copy
            // for asynchronous import/removal, then clear that copy in our finally.
            RequestPinInput?.Invoke(pin => result = pin?.ToArray());
            return result;
        }

        [RelayCommand]
        private void RestoreFromTray()
        {
            if (_dataOperations > 0 && (IsLocked || (_settingsManager.Settings.LockOnRestore && HasPin)))
            {
                LockApp();
                RequestLockState?.Invoke(false);
                ReportStatus("Finishing the current data operation. Unlock will be available when it completes.");
                return;
            }
            // if LockOnRestore is true and PIN is set, we treat it as "Locked" even if IsLocked was false.
            if (_settingsManager.Settings.LockOnRestore && !string.IsNullOrEmpty(_settingsManager.Settings.PinHash))
            {
                 LockApp();
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
