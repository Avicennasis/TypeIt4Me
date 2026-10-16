using System.Windows;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests;

public class WindowPlacementTests
{
    [Fact]
    public void Clamp_FullyInsideWorkArea_ReturnsOriginalBounds()
    {
        var bounds = new Rect(100, 100, 200, 200);
        var workArea = new Rect(0, 0, 1920, 1080);
        var result = WindowPlacement.Clamp(bounds, workArea);
        Assert.Equal(bounds, result);
    }

    [Fact]
    public void Clamp_OutsideWorkAreaTopLeft_MovesInside()
    {
        var bounds = new Rect(-100, -100, 200, 200);
        var workArea = new Rect(0, 0, 1920, 1080);
        var result = WindowPlacement.Clamp(bounds, workArea);
        Assert.Equal(new Rect(0, 0, 200, 200), result);
    }

    [Fact]
    public void Clamp_OutsideWorkAreaBottomRight_MovesInside()
    {
        var bounds = new Rect(1800, 1000, 200, 200);
        var workArea = new Rect(0, 0, 1920, 1080);
        var result = WindowPlacement.Clamp(bounds, workArea);
        Assert.Equal(new Rect(1720, 880, 200, 200), result);
    }

    [Fact]
    public void Clamp_WiderThanWorkArea_ClampsWidthAndCenters()
    {
        var bounds = new Rect(-100, 100, 2500, 200);
        var workArea = new Rect(0, 0, 1920, 1080);
        var result = WindowPlacement.Clamp(bounds, workArea);
        Assert.Equal(new Rect(0, 100, 1920, 200), result);
    }

    [Fact]
    public void Clamp_TallerThanWorkArea_ClampsHeightAndCenters()
    {
        var bounds = new Rect(100, -100, 200, 1500);
        var workArea = new Rect(0, 0, 1920, 1080);
        var result = WindowPlacement.Clamp(bounds, workArea);
        Assert.Equal(new Rect(100, 0, 200, 1080), result);
    }

    [Fact]
    public void Clamp_LargerThanWorkAreaBothDimensions_ClampsToWorkAreaSizeAndPosition()
    {
        var bounds = new Rect(-100, -100, 2500, 1500);
        var workArea = new Rect(0, 0, 1920, 1080);
        var result = WindowPlacement.Clamp(bounds, workArea);
        Assert.Equal(new Rect(0, 0, 1920, 1080), result);
    }

    [Fact]
    public void Clamp_NonZeroWorkAreaOrigin_ClampsCorrectly()
    {
        var bounds = new Rect(50, 50, 200, 200);
        var workArea = new Rect(100, 100, 800, 600);
        var result = WindowPlacement.Clamp(bounds, workArea);
        Assert.Equal(new Rect(100, 100, 200, 200), result);
    }
}
