using System;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeFocusTracker : IFocusTracker
    {
        public IntPtr LastExternalWindowHandle => IntPtr.Zero;
        public void Start(IntPtr myWindowHandle) { }
        public void Dispose() { }
    }
}
