using System;
using System.IO;

namespace TypeIt4Me.Services
{
    public static class Constants
    {
        public const string AppName = "TypeIt4Me";
        public const string SnippetsFileName = "snippets.json";
        public const string SettingsFileName = "settings.json";

        /// <summary>
        /// Cached application-data folder. Only the path resolution is memoized;
        /// <see cref="Environment.GetFolderPath"/> is a shell API call and the location cannot
        /// change for the lifetime of the process.
        /// </summary>
        private static string? _appDataFolder;

        public static string GetAppDataPath(string fileName)
        {
            string folder = _appDataFolder ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName);

            // Deliberately NOT cached alongside the path. CreateDirectory is idempotent and cheap
            // when the folder already exists, and re-running it on every call is what lets the app
            // self-heal if the folder is deleted or quarantined mid-run. Caching the whole result
            // would leave every later save and log throwing DirectoryNotFoundException for the
            // rest of the process lifetime.
            Directory.CreateDirectory(folder);

            return Path.Combine(folder, fileName);
        }
    }
}
