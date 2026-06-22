using System;
using System.Windows;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TypeIt4Me.Tests")]

namespace TypeIt4Me.Services
{
    public class ThemeService : IThemeService
    {
        private readonly Action<Uri> _applyThemeAction;

        public ThemeService()
        {
            _applyThemeAction = ApplyThemeToApplication;
        }

        internal ThemeService(Action<Uri> applyThemeAction)
        {
            _applyThemeAction = applyThemeAction;
        }

        public void SetTheme(bool isDark)
        {
            string uriPath = isDark ?
                "pack://application:,,,/TypeIt4Me;component/Views/DarkTheme.xaml" :
                "pack://application:,,,/TypeIt4Me;component/Views/LightTheme.xaml";

            _applyThemeAction(new Uri(uriPath, UriKind.RelativeOrAbsolute));
        }

        private void ApplyThemeToApplication(Uri themeUri)
        {
            // Must run on UI thread
            Application.Current.Dispatcher.Invoke(() => 
            {
                var dict = new ResourceDictionary();
                dict.Source = themeUri;
                
                // Clear old theme if present - simpler to just clear and add
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);
            });
        }
    }
}
