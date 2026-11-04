using TypeIt4Me.Services;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace TypeIt4Me.ViewModels
{
    public partial class MainViewModel
    {
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

        public bool HasPin => !string.IsNullOrEmpty(_settingsManager.Settings.PinHash);
        public string SecuritySummary => HasPin ? "PIN protection is on" : "No PIN · stored as plain text";

        public void RefreshSecurityState()
        {
            OnPropertyChanged(nameof(HasPin));
            OnPropertyChanged(nameof(SecuritySummary));
            LockCommand.NotifyCanExecuteChanged();
            RemovePinCommand.NotifyCanExecuteChanged();
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

        public bool IsUnlocked => !IsLocked;

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
