using System;

namespace TypeIt4Me.Services
{
    public interface IAutoLockService : IDisposable
    {
        event Action OnLockTriggered;
        void UpdateLastActivity();
        void EvaluateTimerState();
    }
}
