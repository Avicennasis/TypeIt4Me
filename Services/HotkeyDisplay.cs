using System.Collections.Generic;
using System.Windows.Input;

namespace TypeIt4Me.Services;

public static class HotkeyDisplay
{
    public static string Format(Key key, ModifierKeys modifiers)
    {
        if (key == Key.None) return string.Empty;
        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key >= Key.D0 && key <= Key.D9 ? ((int)key - (int)Key.D0).ToString() : key.ToString());
        return string.Join(" + ", parts);
    }
}
