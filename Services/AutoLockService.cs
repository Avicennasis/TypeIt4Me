using System;
using System.Windows.Threading;

namespace TypeIt4Me.Services
{
    public class AutoLockService : IDisposable, IAutoLockService
    {
        private readonly ITimer _timer;
        private DateTime _lastActivity;
        private readonly ISettingsManager _settingsManager;
        private readonly Func<DateTime> _nowProvider;

        public event Action? OnLockTriggered;

        public AutoLockService(ISettingsManager settingsManager)
            : this(settingsManager, new DispatcherTimerWrapper(), () => DateTime.Now)
        {
        }

        internal AutoLockService(ISettingsManager settingsManager, ITimer timer, Func<DateTime> nowProvider)
        {
            _settingsManager = settingsManager;
            _timer = timer;
            _nowProvider = nowProvider;
            _timer.Tick += Timer_Tick;
            
            // Check every 5 seconds
            _timer.Interval = TimeSpan.FromSeconds(5);
            UpdateLastActivity();
            
            // Listen to settings changes if needed, or just poll property
            // We'll update the timer start/stop based on usage
        }

        public void UpdateLastActivity()
        {
            _lastActivity = _nowProvider();
        }

        public void EvaluateTimerState()
        {
            if (_settingsManager.Settings.AutoLockMinutes > 0)
            {
                if (!_timer.IsEnabled) _timer.Start();
            }
            else
            {
                if (_timer.IsEnabled) _timer.Stop();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_settingsManager.Settings.AutoLockMinutes > 0)
            {
                if ((_nowProvider() - _lastActivity).TotalMinutes >= _settingsManager.Settings.AutoLockMinutes)
                {
                     // Only lock if PIN is set
                     if (!string.IsNullOrEmpty(_settingsManager.Settings.PinHash))
                     {
                         OnLockTriggered?.Invoke();
                     }
                }
            }
        }
        
        public void Stop()
        {
            _timer.Stop();
        }

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
