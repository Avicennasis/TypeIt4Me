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

        public event Action<string>? ErrorLogged;

        public void LogInfo(string message)
        {
            InfoLogs.Add(message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            ErrorLogs.Add((message, ex));
            ErrorLogged?.Invoke(message);
        }
    }
}
