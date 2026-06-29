using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TypeIt4Me.Services
{
    public class FocusTracker : IDisposable, IFocusTracker
    {
        private CancellationTokenSource? _cts;
        private IntPtr _myWindowHandle;
        private readonly Func<IntPtr> _getForegroundWindow;
        
        public IntPtr LastExternalWindowHandle { get; private set; }

        public FocusTracker() : this(NativeMethods.GetForegroundWindow)
        {
        }

        internal FocusTracker(Func<IntPtr> getForegroundWindow)
        {
            _getForegroundWindow = getForegroundWindow;
        }

        public void Start(IntPtr myWindowHandle)
        {
            _myWindowHandle = myWindowHandle;
            _cts = new CancellationTokenSource();
            _ = TrackFocusLoop(_cts.Token);
        }

        private async Task TrackFocusLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    IntPtr foreground = _getForegroundWindow();
                    if (foreground != IntPtr.Zero && foreground != _myWindowHandle)
                    {
                        LastExternalWindowHandle = foreground;
                    }
                    await Task.Delay(200, token);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected during shutdown
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
