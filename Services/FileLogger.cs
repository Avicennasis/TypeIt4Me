using System;
using System.IO;

namespace TypeIt4Me.Services
{
    public class FileLogger : ILogger
    {
        private readonly string _logPath;
        private readonly object _lock = new object();

        public FileLogger()
        {
            _logPath = Constants.GetAppDataPath("error.log");
        }

        public void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $"\nException: {ex.GetType().FullName}\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
            }
            Log("ERROR", fullMessage);
        }

        private void Log(string level, string message)
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}\n" +
                                 "--------------------------------------------------\n";

                lock (_lock)
                {
                    File.AppendAllText(_logPath, logEntry);
                }
            }
            catch
            {
                // Last resort: if logging fails, we can't do much.
                // In a desktop app, we avoid crashing the app because logging failed.
            }
        }
    }
}
