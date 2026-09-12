using System;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeAutoLockService : IAutoLockService
    {
        public event Action? OnLockTriggered;
        public bool UpdateLastActivityCalled { get; private set; }

        public void UpdateLastActivity()
        {
            UpdateLastActivityCalled = true;
        }

        public void EvaluateTimerState() { }
        public void Dispose() { }
    }
}
