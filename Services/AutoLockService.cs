using System;
using System.Windows.Threading;

namespace TypeIt4Me.Services
{
    public class AutoLockService : IDisposable, IAutoLockService
    {
        private readonly DispatcherTimer _timer;
        private DateTime _lastActivity;
        private readonly ISettingsManager _settingsManager;

        public event Action OnLockTriggered;

        public AutoLockService(ISettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _timer = new DispatcherTimer();
            _timer.Tick += Timer_Tick;
            
            // Check every 5 seconds
            _timer.Interval = TimeSpan.FromSeconds(5);
            UpdateLastActivity();
            
            // Listen to settings changes if needed, or just poll property
            // We'll update the timer start/stop based on usage
        }

        public void UpdateLastActivity()
        {
            _lastActivity = DateTime.Now;
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
                if ((DateTime.Now - _lastActivity).TotalMinutes >= _settingsManager.Settings.AutoLockMinutes)
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
