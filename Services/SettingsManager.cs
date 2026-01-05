using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TypeIt4Me.Models;

namespace TypeIt4Me.Services
{
    public class SettingsManager
    {

        private readonly System.Threading.SemaphoreSlim _fileLock = new System.Threading.SemaphoreSlim(1, 1);

        public AppSettings Settings { get; private set; } = new AppSettings();

        private string GetFilePath()
        {
            return Constants.GetAppDataPath(Constants.SettingsFileName);
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                string path = GetFilePath();
                if (File.Exists(path))
                {
                    await _fileLock.WaitAsync();
                    try
                    {
                        using FileStream stream = File.OpenRead(path);
                        var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream);
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
                    finally
                    {
                        _fileLock.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
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
                using (FileStream stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, Settings);
                }
                
                // Move is atomic on same volume
                File.Move(tempPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
