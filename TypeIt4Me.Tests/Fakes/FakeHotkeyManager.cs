using System;
using System.Windows.Input;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeHotkeyManager : IHotkeyManager
    {
        public void Initialize(IntPtr windowHandle) { }
        public void ClearRegistrations() { }
        public int Register(Key key, ModifierKeys modifiers, Action callback, Guid snippetId = default) => 1;
        public void UnregisterBySnippetId(Guid snippetId) { }
        public void Dispose() { }
    }
}
