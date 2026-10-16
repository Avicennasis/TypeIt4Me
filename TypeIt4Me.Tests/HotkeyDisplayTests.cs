using System.Windows.Input;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests;

public class HotkeyDisplayTests
{
    [Fact]
    public void Format_KeyNone_ReturnsEmptyString()
    {
        var result = HotkeyDisplay.Format(Key.None, ModifierKeys.None);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(Key.A, ModifierKeys.None, "A")]
    [InlineData(Key.Enter, ModifierKeys.None, "Enter")]
    [InlineData(Key.Space, ModifierKeys.None, "Space")]
    public void Format_NoModifiers_ReturnsKeyName(Key key, ModifierKeys modifiers, string expected)
    {
        var result = HotkeyDisplay.Format(key, modifiers);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.D0, "0")]
    [InlineData(Key.D5, "5")]
    [InlineData(Key.D9, "9")]
    public void Format_DigitKeys_ReturnsDigitOnly(Key key, string expected)
    {
        var result = HotkeyDisplay.Format(key, ModifierKeys.None);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ModifierKeys.Control, Key.C, "Ctrl + C")]
    [InlineData(ModifierKeys.Alt, Key.Tab, "Alt + Tab")]
    [InlineData(ModifierKeys.Shift, Key.A, "Shift + A")]
    [InlineData(ModifierKeys.Windows, Key.D, "Win + D")]
    public void Format_SingleModifier_ReturnsModifierAndKey(ModifierKeys modifiers, Key key, string expected)
    {
        var result = HotkeyDisplay.Format(key, modifiers);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt, Key.Delete, "Ctrl + Alt + Delete")]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.Escape, "Ctrl + Shift + Escape")]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.K, "Ctrl + Alt + Shift + K")]
    [InlineData(ModifierKeys.Windows | ModifierKeys.Shift, Key.S, "Shift + Win + S")]
    public void Format_MultipleModifiers_ReturnsAllModifiersAndKey(ModifierKeys modifiers, Key key, string expected)
    {
        var result = HotkeyDisplay.Format(key, modifiers);
        Assert.Equal(expected, result);
    }
}
