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
        private readonly Func<IntPtr, bool> _isOwnWindow;

        public IntPtr LastExternalWindowHandle { get; private set; }

        internal FocusTracker(Func<IntPtr>? getForegroundWindow = null, Func<IntPtr, bool>? isOwnWindow = null)
        {
            _getForegroundWindow = getForegroundWindow ?? NativeMethods.GetForegroundWindow;
            _isOwnWindow = isOwnWindow ?? (getForegroundWindow != null ? _ => false : IsOwnProcessWindow);
        }

        private static bool IsOwnProcessWindow(IntPtr window)
        {
            NativeMethods.GetWindowThreadProcessId(window, out uint processId);
            return processId == Environment.ProcessId;
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
                while (true)
                {
                    IntPtr foreground = _getForegroundWindow();
                    if (foreground != IntPtr.Zero && foreground != _myWindowHandle && !_isOwnWindow(foreground))
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
