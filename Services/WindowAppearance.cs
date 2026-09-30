using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TypeIt4Me.Services;

/// <summary>Optional DWM treatment. The opaque WPF surface remains the fallback.</summary>
public static class WindowAppearance
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(WindowAppearance), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject value) => (bool)value.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject value, bool enabled) => value.SetValue(EnabledProperty, enabled);

    private static void OnEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Window window && (bool)e.NewValue)
        {
            window.SourceInitialized += (_, _) => Apply(window);
            window.Loaded += (_, _) => WindowPlacement.EnsureVisible(window);
            window.DpiChanged += (_, _) => WindowPlacement.EnsureVisible(window);
            if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Apply(window);
        }
    }

    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindows()) return;
        try
        {
            var background = window.TryFindResource("CanvasBrush") as SolidColorBrush;
            int dark = background != null && background.Color.R < 128 ? 1 : 0;
            int corners = SystemParameters.HighContrast ? 1 : 2;
            // Unsupported attributes return an HRESULT and do not affect rendering.
            DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
