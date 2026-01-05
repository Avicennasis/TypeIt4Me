using CommunityToolkit.Mvvm.ComponentModel;

namespace TypeIt4Me.Models
{
    public class AppSettings : ObservableObject
    {
        private bool _alwaysOnTop = true;
        private string _pinHash = string.Empty;
        private string _pinSalt = string.Empty;
        private bool _isMiniMode = false;
        private bool _minimizeToTray = true;
        private bool _isDarkMode = false;

        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set => SetProperty(ref _alwaysOnTop, value);
        }

        public string PinHash
        {
            get => _pinHash;
            set => SetProperty(ref _pinHash, value);
        }

        public string PinSalt
        {
            get => _pinSalt;
            set => SetProperty(ref _pinSalt, value);
        }

        public bool IsMiniMode
        {
            get => _isMiniMode;
            set => SetProperty(ref _isMiniMode, value);
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set => SetProperty(ref _minimizeToTray, value);
        }

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set => SetProperty(ref _isDarkMode, value);
        }

        private int _autoLockMinutes = 0;
        public int AutoLockMinutes
        {
             get => _autoLockMinutes;
             set => SetProperty(ref _autoLockMinutes, value);
        }

        private bool _lockOnRestore = false;
        public bool LockOnRestore
        {
             get => _lockOnRestore;
             set => SetProperty(ref _lockOnRestore, value);
        }
    }
}
