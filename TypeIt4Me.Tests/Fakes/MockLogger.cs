using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class MockLogger : ILogger
    {
        public List<string> InfoLogs { get; } = new List<string>();
        public List<(string Message, Exception? Exception)> ErrorLogs { get; } = new List<(string, Exception?)>();

        // Used to await background tasks that log errors
        public TaskCompletionSource<bool>? ErrorLoggedCompletionSource { get; set; }

        public void LogInfo(string message)
        {
            InfoLogs.Add(message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            ErrorLogs.Add((message, ex));
            ErrorLoggedCompletionSource?.TrySetResult(true);
        }
    }
}
