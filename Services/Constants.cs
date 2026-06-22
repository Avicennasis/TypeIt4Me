using System;
using System.IO;

namespace TypeIt4Me.Services
{
    public static class Constants
    {
        public const string AppName = "TypeIt4Me";
        public const string SnippetsFileName = "snippets.json";
        public const string SettingsFileName = "settings.json";
        public const string LogFileName = "error.log";

        public static string GetAppDataPath(string fileName)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, AppName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, fileName);
        }
    }
}
