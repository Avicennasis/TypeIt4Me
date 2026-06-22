using System;
using System.Collections.Generic;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeLogger : ILogger
    {
        public List<string> InfoLogs { get; } = new List<string>();
        public List<string> ErrorLogs { get; } = new List<string>();

        public void LogInfo(string message)
        {
            InfoLogs.Add(message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            ErrorLogs.Add($"{message} - {ex?.Message}");
        }
    }
}
