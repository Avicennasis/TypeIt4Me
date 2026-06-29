using System;
using System.Windows;

namespace TypeIt4Me.Services
{
    public class ThemeService : IThemeService
    {
        private readonly Action<Action> _invokeOnUI;
        private readonly Action<string> _applyTheme;

        public ThemeService()
        {
            _invokeOnUI = (action) => Application.Current.Dispatcher.Invoke(action);
            _applyTheme = (uriPath) =>
            {
                var dict = new ResourceDictionary { Source = new Uri(uriPath) };
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);
            };
        }

        internal ThemeService(Action<Action> invokeOnUI, Action<string> applyTheme)
        {
            _invokeOnUI = invokeOnUI;
            _applyTheme = applyTheme;
        }

        public void SetTheme(bool isDark)
        {
            // Must run on UI thread
            _invokeOnUI(() =>
            {
                string uriPath = isDark ? 
                    "pack://application:,,,/TypeIt4Me;component/Views/DarkTheme.xaml" : 
                    "pack://application:,,,/TypeIt4Me;component/Views/LightTheme.xaml";
                
                _applyTheme(uriPath);
            });
        }
    }
}
