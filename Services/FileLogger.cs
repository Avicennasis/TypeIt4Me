using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public sealed class FileLogger : ILogger, IDisposable
    {
        private readonly string _logPath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly object _syncLock = new object();
        private Task _pendingWrite = Task.CompletedTask;

        public FileLogger()
        {
            _logPath = Constants.GetAppDataPath("error.log");
        }

        public void LogInfo(string message)
        {
            LogAsync("INFO", message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $"\nException: {ex.GetType().FullName}\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
            }
            LogAsync("ERROR", fullMessage);
        }

        private void LogAsync(string level, string message)
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}\n" +
                                 "--------------------------------------------------\n";

                // Fire and forget, but keep a reference to the latest task so we can await it during shutdown.
                // Since this uses an async lock, tasks will queue up sequentially.
                // We lock the update to the pending write task itself to avoid race conditions during assignment.
                lock (_syncLock)
                {
                    _pendingWrite = LogInternalAsync(logEntry);
                }
            }
            catch
            {
                // In a desktop app, we avoid crashing the app because logging failed.
            }
        }

        private async Task LogInternalAsync(string logEntry)
        {
             try
             {
                 // We use ConfigureAwait(false) to avoid deadlocking with UI threads
                 // during shutdown when Dispose() is called.
                 await _semaphore.WaitAsync().ConfigureAwait(false);
                 try
                 {
                     await File.AppendAllTextAsync(_logPath, logEntry).ConfigureAwait(false);
                 }
                 finally
                 {
                     _semaphore.Release();
                 }
             }
             catch
             {
                 // Ignore
             }
        }

        public void Dispose()
        {
            try
            {
                _pendingWrite.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Ignore wait errors
            }
        }
    }
}
