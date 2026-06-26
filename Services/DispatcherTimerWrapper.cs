using System;
using System.Windows.Threading;

namespace TypeIt4Me.Services
{
    public class DispatcherTimerWrapper : ITimer
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public bool IsEnabled => _timer.IsEnabled;

        public event EventHandler? Tick
        {
            add => _timer.Tick += value;
            remove => _timer.Tick -= value;
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();
    }
}
