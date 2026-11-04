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
        private readonly ILogger? _logger;

        public IntPtr LastExternalWindowHandle { get; private set; }

        internal FocusTracker(Func<IntPtr>? getForegroundWindow = null, Func<IntPtr, bool>? isOwnWindow = null, ILogger? logger = null)
        {
            _getForegroundWindow = getForegroundWindow ?? NativeMethods.GetForegroundWindow;
            _isOwnWindow = isOwnWindow ?? (getForegroundWindow != null ? _ => false : IsOwnProcessWindow);
            _logger = logger;
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
                while (!token.IsCancellationRequested)
                {
                    IntPtr foreground = _getForegroundWindow();
                    if (foreground != IntPtr.Zero && foreground != _myWindowHandle && !_isOwnWindow(foreground))
                    {
                        LastExternalWindowHandle = foreground;
                    }
                    await Task.Delay(200, token);
                }
            }
            catch (TaskCanceledException ex)
            {
                // Expected during shutdown
                _logger?.LogInfo($"Focus tracking loop canceled: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Unexpected error in focus tracking loop", ex);
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
