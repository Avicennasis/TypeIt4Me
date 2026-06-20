using System;

namespace TypeIt4Me.Services
{
    public interface IFocusTracker : IDisposable
    {
        IntPtr LastExternalWindowHandle { get; }
        void Start(IntPtr myWindowHandle);
    }
}
