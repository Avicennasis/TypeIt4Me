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
        
        public IntPtr LastExternalWindowHandle { get; private set; }

        public void Start(IntPtr myWindowHandle)
        {
            _myWindowHandle = myWindowHandle;
            _cts = new CancellationTokenSource();
            _ = TrackFocusLoop(_cts.Token);
        }

        private async Task TrackFocusLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                IntPtr foreground = NativeMethods.GetForegroundWindow();
                if (foreground != IntPtr.Zero && foreground != _myWindowHandle)
                {
                    LastExternalWindowHandle = foreground;
                }
                await Task.Delay(200, token);
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
