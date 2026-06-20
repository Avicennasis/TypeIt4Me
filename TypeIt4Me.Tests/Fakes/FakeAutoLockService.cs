using System;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeAutoLockService : IAutoLockService
    {
        public event Action OnLockTriggered;
        public void UpdateLastActivity() { }
        public void EvaluateTimerState() { }
        public void Dispose() { }
    }
}
