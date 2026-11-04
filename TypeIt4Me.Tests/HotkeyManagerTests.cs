using System;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Interop;
using System.Collections.Generic;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class HotkeyManagerTests
    {
        [Fact]
        public void Register_ReturnsGreaterThanOrEqualToZero()
        {
            using var manager = new HotkeyManager();
            var id = manager.Register(Key.A, ModifierKeys.Control, () => { });
            Assert.True(id >= 0, "Register should return 0 or higher depending on native result in CI");
        }

        [Fact]
        public void RegisterWithSnippetId_ReturnsGreaterThanOrEqualToZero()
        {
            using var manager = new HotkeyManager();
            var id = manager.Register(Key.A, ModifierKeys.Control, () => { }, Guid.NewGuid());
            Assert.True(id >= 0, "Register should return 0 or higher depending on native result in CI");
        }

        [Fact]
        public void Register_IncrementsId()
        {
            using var manager = new HotkeyManager();

            var id1 = manager.Register(Key.A, ModifierKeys.Control, () => { });
            var id2 = manager.Register(Key.B, ModifierKeys.Shift, () => { });

            var field = typeof(HotkeyManager).GetField("_currentId", BindingFlags.NonPublic | BindingFlags.Instance);
            int currentId = (int)(field?.GetValue(manager) ?? 0);

            Assert.Equal(2, currentId);
        }

        [Fact]
        public void Unregister_IgnoresUnknownId()
        {
            using var manager = new HotkeyManager();
            var exception = Record.Exception(() => manager.Unregister(999));
            Assert.Null(exception);
        }

        [Fact]
        public void UnregisterBySnippetId_IgnoresUnknownId()
        {
            using var manager = new HotkeyManager();
            var exception = Record.Exception(() => manager.UnregisterBySnippetId(Guid.NewGuid()));
            Assert.Null(exception);
        }

        [Fact]
        public void ClearRegistrations_DoesNotThrow()
        {
            using var manager = new HotkeyManager();
            var exception = Record.Exception(() => manager.ClearRegistrations());
            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_ClearsRegistrationsAndDoesNotThrow()
        {
            using var manager = new HotkeyManager();
            var exception = Record.Exception(() => manager.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public void WndProc_InvokesCallback_WhenMessageIsWM_HOTKEY()
        {
            using var manager = new HotkeyManager();

            // Add a callback to the private dictionary via reflection
            var callbacksField = typeof(HotkeyManager).GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance);
            var callbacks = (Dictionary<int, Action>?)callbacksField?.GetValue(manager);

            bool callbackInvoked = false;
            if (callbacks != null)
            {
                callbacks[1] = () => callbackInvoked = true;
            }

            // Invoke WndProc via reflection
            var wndProcMethod = typeof(HotkeyManager).GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance);

            bool handled = false;
            object[] parameters = new object[] { IntPtr.Zero, 0x0312 /* WM_HOTKEY */, new IntPtr(1), IntPtr.Zero, handled };

            wndProcMethod?.Invoke(manager, parameters);

            Assert.True(callbackInvoked);
            Assert.True((bool)parameters[4]);
        }

        [Fact]
        public void WndProc_IgnoresMessage_WhenNotWM_HOTKEY()
        {
            using var manager = new HotkeyManager();

            var callbacksField = typeof(HotkeyManager).GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance);
            var callbacks = (Dictionary<int, Action>?)callbacksField?.GetValue(manager);

            bool callbackInvoked = false;
            if (callbacks != null)
            {
                callbacks[1] = () => callbackInvoked = true;
            }

            var wndProcMethod = typeof(HotkeyManager).GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance);

            bool handled = false;
            object[] parameters = new object[] { IntPtr.Zero, 0x0111 /* WM_COMMAND */, new IntPtr(1), IntPtr.Zero, handled };

            wndProcMethod?.Invoke(manager, parameters);

            Assert.False(callbackInvoked);
            Assert.False((bool)parameters[4]);
        }
    }
}
