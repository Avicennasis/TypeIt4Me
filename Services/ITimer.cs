using System;

namespace TypeIt4Me.Services
{
    public interface ITimer
    {
        TimeSpan Interval { get; set; }
        bool IsEnabled { get; }
        event EventHandler Tick;
        void Start();
        void Stop();
    }
}
