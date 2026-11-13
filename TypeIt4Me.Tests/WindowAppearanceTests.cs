using System;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests;

public class WindowAppearanceTests
{
    [Fact]
    public void SetDwmAttributes_SwallowsDllNotFoundException()
    {
        // Arrange
        WindowAppearance._dwmSetWindowAttribute = (IntPtr hwnd, int attribute, ref int value, int size) =>
        {
            throw new DllNotFoundException();
        };

        // Act
        // Should not throw
        WindowAppearance.SetDwmAttributes(IntPtr.Zero, 0, 0);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void SetDwmAttributes_SwallowsEntryPointNotFoundException()
    {
        // Arrange
        WindowAppearance._dwmSetWindowAttribute = (IntPtr hwnd, int attribute, ref int value, int size) =>
        {
            throw new EntryPointNotFoundException();
        };

        // Act
        // Should not throw
        WindowAppearance.SetDwmAttributes(IntPtr.Zero, 0, 0);

        // Assert
        Assert.True(true);
    }
}
