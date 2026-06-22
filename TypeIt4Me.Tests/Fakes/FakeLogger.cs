using System;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeLogger : ILogger
    {
        public void LogInfo(string message) { }
        public void LogError(string message, Exception? ex = null) { }
    }
}
