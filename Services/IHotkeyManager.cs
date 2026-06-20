using System;

namespace TypeIt4Me.Services
{
    public interface IHotkeyManager : IDisposable
    {
        void UnregisterBySnippetId(Guid snippetId);
        void Initialize(IntPtr windowHandle);
        void ClearRegistrations();
        int Register(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers, Action callback, Guid snippetId = default);
    }
}
