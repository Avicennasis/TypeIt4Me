using System;

namespace TypeIt4Me.Services
{
    public interface ILogger
    {
        void LogInfo(string message);
        void LogError(string message, Exception? ex = null);
    }
}
