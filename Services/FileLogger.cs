using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public sealed class FileLogger : ILogger, IDisposable
    {
        private readonly string _logPath;
        private readonly Channel<string> _logChannel;
        private readonly Task _backgroundTask;

        public FileLogger()
        {
            _logPath = Constants.GetAppDataPath("error.log");

            // Use an unbounded channel to never block the caller.
            // SingleReader is true because only one background task will write to the file.
            _logChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            _backgroundTask = Task.Run(ProcessLogQueueAsync);
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

        private void LogAsync(string level, string message)
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}\n" +
                                 "--------------------------------------------------\n";

                // Enqueue the log entry instead of writing directly to the file.
                // This avoids blocking the caller thread.
                _logChannel.Writer.TryWrite(logEntry);
            }
            catch
            {
                // In a desktop app, we avoid crashing the app because logging failed.
            }
        }

        private async Task ProcessLogQueueAsync()
        {
            try
            {
                await foreach (var logEntry in _logChannel.Reader.ReadAllAsync())
                {
                    try
                    {
                        // Because this is running in a single reader background task,
                        // we no longer need a lock for appending text.
                        File.AppendAllText(_logPath, logEntry);
                    }
                    catch
                    {
                        // Ignore file system errors for individual entries
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore channel read cancellations
            }
            catch
            {
                // Ignore other background errors
            }
        }

        public void Dispose()
        {
            // Complete the channel and wait briefly for the queue to flush
            _logChannel.Writer.TryComplete();
            try
            {
                _backgroundTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Ignore wait errors
            }
        }
    }
}
