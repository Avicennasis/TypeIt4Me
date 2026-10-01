using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TypeIt4Me.Services;

public static class WindowPlacement
{
    public static Rect Clamp(Rect bounds, Rect workArea)
    {
        double width = Math.Min(bounds.Width, workArea.Width);
        double height = Math.Min(bounds.Height, workArea.Height);
        return new Rect(Math.Clamp(bounds.X, workArea.Left, workArea.Right - width),
            Math.Clamp(bounds.Y, workArea.Top, workArea.Bottom - height), width, height);
    }

    public static void EnsureVisible(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref info)) return;
        var source = HwndSource.FromHwnd(handle);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(info.Work.Left, info.Work.Top));
        var bottomRight = transform.Transform(new Point(info.Work.Right, info.Work.Bottom));
        var workArea = new Rect(topLeft, bottomRight);
        window.MinWidth = Math.Min(window.MinWidth, workArea.Width);
        window.MinHeight = Math.Min(window.MinHeight, workArea.Height);
        window.MaxWidth = workArea.Width; window.MaxHeight = workArea.Height;
        double width = double.IsFinite(window.Width) ? window.Width : window.ActualWidth;
        double height = double.IsFinite(window.Height) ? window.Height : window.ActualHeight;
        var bounds = Clamp(new Rect(window.Left, window.Top, width, height), workArea);
        window.Left = bounds.Left; window.Top = bounds.Top;
        window.Width = bounds.Width;
        if (window.SizeToContent == SizeToContent.Manual || bounds.Height < height) window.Height = bounds.Height;
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
