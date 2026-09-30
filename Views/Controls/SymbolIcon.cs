using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TypeIt4Me.Views.Controls;

/// <summary>A small, resolution-independent icon from our shared stroke geometry family.</summary>
public sealed class SymbolIcon : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(SymbolIcon), new PropertyMetadata(null));

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
