using System;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeAutoLockService : IAutoLockService
    {
        public bool UpdateLastActivityCalled { get; private set; } = false;

        public event Action OnLockTriggered;

        public void UpdateLastActivity()
        {
            UpdateLastActivityCalled = true;
        }

        public void EvaluateTimerState() { }
        public void Dispose() { }
    }
}
