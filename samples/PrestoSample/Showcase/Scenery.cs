using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace PrestoSample.Showcase;

/// <summary>A little landscape drawn in a fixed 200×130 frame, so a thumbnail and a hero show the same picture at two scales.</summary>
public sealed class Scenery : Viewbox
{
    public static readonly StyledProperty<double> HueProperty = AvaloniaProperty.Register<Scenery, double>(nameof(Hue));

    /// <summary>Places the sun and the far peak, so neighboring places with close hues still differ.</summary>
    public static readonly StyledProperty<int> IndexProperty = AvaloniaProperty.Register<Scenery, int>(nameof(Index));

    public Scenery()
    {
        Stretch = Stretch.UniformToFill;
        Child = new Image { Source = Picture(Hue, Index) };
    }

    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    /// <inheritdoc cref="IndexProperty"/>
    public int Index
    {
        get => GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HueProperty || change.Property == IndexProperty) ((Image)Child!).Source = Picture(Hue, Index);
    }

    /// <summary>The landscape for <paramref name="hue"/> and <paramref name="index"/> as a 200×130 image.</summary>
    public static DrawingImage Picture(double hue, int index)
    {
        var far = new ImmutableSolidColorBrush(HsvColor.ToRgb((hue + 200) % 360, .45, .45), .8);
        var near = new ImmutableSolidColorBrush(HsvColor.ToRgb((hue + 190) % 360, .55, .25));
        var sun = new ImmutableSolidColorBrush(HsvColor.ToRgb((hue + 40) % 360, .3, 1));
        var peak = 40 + index * 23 % 40;
        return new DrawingImage(new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = Palette.HueGradient(hue), Geometry = new RectangleGeometry(new Rect(0, 0, 200, 130)) },
                new GeometryDrawing { Brush = sun, Geometry = new EllipseGeometry(new Rect(20d + index * 47 % 140, 18, 34, 34)) },
                new GeometryDrawing { Brush = far, Geometry = Geometry.Parse(string.Create(CultureInfo.InvariantCulture, $"M0,95 L40,{peak + 22} L75,85 L115,{peak} L160,82 L200,62 L200,130 L0,130 Z")) },
                new GeometryDrawing { Brush = near, Geometry = Geometry.Parse("M0,110 C50,88 90,120 140,100 C170,90 185,96 200,92 L200,130 L0,130 Z") },
            },
        });
    }
}
