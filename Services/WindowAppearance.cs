using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TypeIt4Me.Services;

/// <summary>Optional DWM treatment. The opaque WPF surface remains the fallback.</summary>
public static class WindowAppearance
{
    public static ILogger? Logger { get; set; }

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

        var background = window.TryFindResource("CanvasBrush") as SolidColorBrush;
        int dark = background != null && background.Color.R < 128 ? 1 : 0;
        int corners = SystemParameters.HighContrast ? 1 : 2;

        SetDwmAttributes(handle, dark, corners);
    }

    internal static void SetDwmAttributes(IntPtr handle, int dark, int corners)
    {
        try
        {
            // Unsupported attributes return an HRESULT and do not affect rendering.
            _dwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            _dwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
        }
        catch (DllNotFoundException ex)
        {
            Logger?.LogInfo($"DWM not supported: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            Logger?.LogInfo($"DWM attribute not supported: {ex.Message}");
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    internal delegate int DwmSetWindowAttributeDelegate(IntPtr hwnd, int attribute, ref int value, int size);
    internal static DwmSetWindowAttributeDelegate _dwmSetWindowAttribute = DwmSetWindowAttribute;
}
