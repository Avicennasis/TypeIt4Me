using System;
using System.Linq;
using System.Windows;

namespace TypeIt4Me.Services
{
    public class ThemeService : IThemeService, IDisposable
    {
        private readonly Action<Action> _invokeOnUI;
        private readonly Action<string> _applyTheme;
        private readonly Func<bool> _isHighContrast;
        private bool _isDark;
        private readonly bool _watchSystem;

        public ThemeService()
        {
            _isHighContrast = () => SystemParameters.HighContrast;
            _watchSystem = true;
            _invokeOnUI = (action) => Application.Current.Dispatcher.Invoke(action);
            _applyTheme = (uriPath) =>
            {
                var dict = new ResourceDictionary { Source = new Uri(uriPath) };
                var dictionaries = Application.Current.Resources.MergedDictionaries;
                // Keep component styles and icon resources alive when changing colors.
                for (int i = dictionaries.Count - 1; i >= 0; i--)
                    if (dictionaries[i].Source?.OriginalString.Contains("Theme.xaml", StringComparison.Ordinal) == true)
                        dictionaries.RemoveAt(i);
                dictionaries.Insert(0, dict);
                // Shared mutable brushes also update hidden/tray windows immediately.
                // A deferred template can retain a brush from an earlier palette;
                // replacing only its dictionary leaves that existing visual stale.
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (entry.Value is not System.Windows.Media.SolidColorBrush incoming) continue;
                    if (Application.Current.Resources.Keys.Cast<object>().Contains(entry.Key) &&
                        Application.Current.Resources[entry.Key] is System.Windows.Media.SolidColorBrush current && !current.IsFrozen)
                        current.Color = incoming.Color;
                    else Application.Current.Resources[entry.Key] = incoming.CloneCurrentValue();
                }
                foreach (Window window in Application.Current.Windows) WindowAppearance.Apply(window);
            };
            SystemParameters.StaticPropertyChanged += SystemParametersChanged;
        }

        internal ThemeService(Action<Action> invokeOnUI, Action<string> applyTheme, Func<bool>? isHighContrast = null)
        {
            _invokeOnUI = invokeOnUI;
            _applyTheme = applyTheme;
            _isHighContrast = isHighContrast ?? (() => false);
        }

        public void SetTheme(bool isDark)
        {
            _isDark = isDark;
            // Must run on UI thread
            _invokeOnUI(() =>
            {
                string theme = _isHighContrast() ? "HighContrast" : isDark ? "Dark" : "Light";
                string uriPath = $"pack://application:,,,/TypeIt4Me;component/Views/{theme}Theme.xaml";

                _applyTheme(uriPath);
            });
        }

        private void SystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast)) SetTheme(_isDark);
        }

        public void Dispose()
        {
            if (_watchSystem) SystemParameters.StaticPropertyChanged -= SystemParametersChanged;
        }
    }
}
