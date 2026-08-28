using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TypeIt4Me.Models;

namespace TypeIt4Me.Services
{
    public class SettingsManager : ISettingsManager
    {
        private readonly ILogger _logger;
        private readonly System.Threading.SemaphoreSlim _fileLock = new System.Threading.SemaphoreSlim(1, 1);
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { MaxDepth = 3 };

        public AppSettings Settings { get; private set; } = new AppSettings();

        public SettingsManager(ILogger logger)
        {
            _logger = logger;
        }

        protected virtual string GetFilePath()
        {
            return Constants.GetAppDataPath(Constants.SettingsFileName);
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                string path = GetFilePath();

                await _fileLock.WaitAsync();
                try
                {
                    using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                    var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions);
                    if (loaded != null)
                    {
                        Settings.AlwaysOnTop = loaded.AlwaysOnTop;
                        Settings.PinHash = loaded.PinHash;
                        Settings.PinSalt = loaded.PinSalt;
                        Settings.IsMiniMode = loaded.IsMiniMode;
                        Settings.MinimizeToTray = loaded.MinimizeToTray;
                        Settings.IsDarkMode = loaded.IsDarkMode;
                        Settings.AutoLockMinutes = loaded.AutoLockMinutes;
                        Settings.LockOnRestore = loaded.LockOnRestore;
                    }
                }
                catch (FileNotFoundException)
                {
                    // Ignore, first run
                }
                catch (DirectoryNotFoundException)
                {
                    // Ignore, first run
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error loading settings", ex);
            }
        }
        public async Task SaveSettingsAsync()
        {
            try
            {
                string path = GetFilePath();
                string tempPath = path + ".tmp";
                
                await _fileLock.WaitAsync();
                
                // Atomic Save: Write to .tmp, then Move to .json
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await JsonSerializer.SerializeAsync(stream, Settings, _jsonOptions);
                }
                
                // Move is atomic on same volume
                File.Move(tempPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                 _logger.LogError("Error saving settings", ex);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
