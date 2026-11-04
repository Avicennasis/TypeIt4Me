using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace TypeIt4Me.ViewModels
{
    public partial class MainViewModel
    {
        [ObservableProperty]
        private bool _isAlwaysOnTop = true;

        [ObservableProperty]
        private bool _minimizeToTray = true;

        [ObservableProperty]
        private bool _isMiniMode = false;

        [ObservableProperty]
        private bool _isDarkMode = false;

        [RelayCommand] private void SetLightTheme() => IsDarkMode = false;
        [RelayCommand] private void SetDarkTheme() => IsDarkMode = true;

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
        private void ShowSettings() => RequestShowSettings?.Invoke();

        public event Action? RequestShowSettings;

        [RelayCommand]
        private void ShowHelp()
        {
            RequestShowHelp?.Invoke();
        }

        public event Action? RequestShowHelp;

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
    }
}
