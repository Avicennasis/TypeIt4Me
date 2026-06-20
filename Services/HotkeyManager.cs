using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace TypeIt4Me.Services
{
    public class HotkeyManager : IDisposable, IHotkeyManager
    {
        private IntPtr _windowHandle;
        private HwndSource _source;
        private int _currentId;
        private readonly Dictionary<int, Action> _callbacks = new Dictionary<int, Action>();
        private readonly Dictionary<Guid, int> _snippetMap = new Dictionary<Guid, int>();

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(WndProc);
        }

        public int Register(Key key, ModifierKeys modifiers, Action callback)
        {
            _currentId++;
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            uint fsModifiers = 0;

            if ((modifiers & ModifierKeys.Alt) != 0) fsModifiers |= NativeMethods.MOD_ALT;
            if ((modifiers & ModifierKeys.Control) != 0) fsModifiers |= NativeMethods.MOD_CONTROL;
            if ((modifiers & ModifierKeys.Shift) != 0) fsModifiers |= NativeMethods.MOD_SHIFT;
            if ((modifiers & ModifierKeys.Windows) != 0) fsModifiers |= NativeMethods.MOD_WIN;

            // NoRepeat to prevent spamming while holding down
            fsModifiers |= NativeMethods.MOD_NOREPEAT;

            if (NativeMethods.RegisterHotKey(_windowHandle, _currentId, fsModifiers, vk))
            {
                _callbacks[_currentId] = callback;
                return _currentId;
            }

            return 0; // Failed
        }

        public int Register(Key key, ModifierKeys modifiers, Action callback, Guid snippetId)
        {
            int id = Register(key, modifiers, callback);
            if (id != 0)
                _snippetMap[snippetId] = id;
            return id;
        }

        public void UnregisterBySnippetId(Guid snippetId)
        {
            if (_snippetMap.TryGetValue(snippetId, out int id))
            {
                Unregister(id);
                _snippetMap.Remove(snippetId);
            }
        }

        public void Unregister(int id)
        {
            if (_callbacks.ContainsKey(id))
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
                _callbacks.Remove(id);
            }
        }
        
        public void ClearRegistrations()
        {
            foreach (var id in _callbacks.Keys)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
            }
            _callbacks.Clear();
            _snippetMap.Clear();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_callbacks.TryGetValue(id, out var callback))
                {
                    callback?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            ClearRegistrations();
            _source?.RemoveHook(WndProc);
        }
    }
}
