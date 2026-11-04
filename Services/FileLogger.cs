using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public sealed class FileLogger : ILogger, IDisposable
    {
        /// <summary>
        /// Size at which the log rolls over to <c>&lt;name&gt;.1</c>. One generation of history is
        /// kept, so the crash trail survives the rollover instead of being discarded.
        /// </summary>
        private const long MaxLogSizeBytes = 1024 * 1024;

        private readonly string _logPath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly object _syncLock = new object();
        private Task _pendingWrite = Task.CompletedTask;

        internal FileLogger(string logPath)
        {
            _logPath = logPath;
        }

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
                fullMessage += $"\nException: {ex.GetType().FullName}";
            }
            LogAsync("ERROR", fullMessage);
        }

        private void LogAsync(string level, string message, bool skipRotation = false)
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
                    _pendingWrite = LogInternalAsync(logEntry, skipRotation);
                }
            }
            catch
            {
                // In a desktop app, we avoid crashing the app because logging failed.
            }
        }

        private async Task LogInternalAsync(string logEntry, bool skipRotation)
        {
             try
             {
                 // We use ConfigureAwait(false) to avoid deadlocking with UI threads
                 // during shutdown when Dispose() is called.
                 await _semaphore.WaitAsync().ConfigureAwait(false);
                 try
                 {
                     if (!skipRotation)
                     {
                         await RotateIfOversizedAsync().ConfigureAwait(false);
                     }
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

        private void LogRotationError(string message, Exception ex)
        {
            string fullMessage = $"{message}\nException: {ex.ToString()}";
            LogAsync("ERROR", fullMessage, skipRotation: true);
        }

        /// <summary>
        /// Rolls an oversized log over to <c>&lt;name&gt;.1</c> so diagnostic history is preserved
        /// rather than truncated away. Must be called while the semaphore is held.
        /// </summary>
        /// <remarks>
        /// If rotation fails, we log the failure but do not attempt rotation recursively,
        /// which prevents infinite loops while ensuring diagnostic details aren't swallowed.
        /// </remarks>
        private async Task RotateIfOversizedAsync()
        {
            try
            {
                // One FileInfo snapshot answers both questions. Using File.Exists() followed by a
                // separate new FileInfo().Length would reintroduce a TOCTOU window in which the
                // file can vanish between the two calls and throw FileNotFoundException.
                FileInfo info = new FileInfo(_logPath);
                if (info.Exists && info.Length > MaxLogSizeBytes)
                {
                    await Task.Run(() => File.Move(_logPath, _logPath + ".1", overwrite: true)).ConfigureAwait(false);
                }
            }
            catch (IOException ex)
            {
                LogRotationError("Failed to rotate log file.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogRotationError("Failed to rotate log file due to unauthorized access.", ex);
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
