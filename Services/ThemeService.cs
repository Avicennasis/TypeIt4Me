using System;
using System.Windows;

namespace TypeIt4Me.Services
{
    public class ThemeService : IThemeService
    {
        public void SetTheme(bool isDark)
        {
            // Must run on UI thread
            Application.Current.Dispatcher.Invoke(() => 
            {
                var dict = new ResourceDictionary();
                string uriPath = isDark ? 
                    "pack://application:,,,/TypeIt4Me;component/Views/DarkTheme.xaml" : 
                    "pack://application:,,,/TypeIt4Me;component/Views/LightTheme.xaml";
                
                dict.Source = new Uri(uriPath);
                
                // Clear old theme if present - simpler to just clear and add
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);
            });
        }
    }
}
