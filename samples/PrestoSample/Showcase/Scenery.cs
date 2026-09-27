using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Showcase;

/// <summary>A little landscape drawn in a fixed 200×130 frame, so a thumbnail and a hero show the same picture at two scales.</summary>
public sealed class Scenery : Viewbox
{
    public static readonly StyledProperty<double> HueProperty = AvaloniaProperty.Register<Scenery, double>(nameof(Hue));

    /// <summary>Places the sun and the far peak, so neighbouring places with close hues still differ.</summary>
    public static readonly StyledProperty<int> IndexProperty = AvaloniaProperty.Register<Scenery, int>(nameof(Index));

    public Scenery()
    {
        Stretch = Stretch.UniformToFill;
        Child = Draw(Hue, Index);
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
        if (change.Property == HueProperty || change.Property == IndexProperty) Child = Draw(Hue, Index);
    }

    private static Canvas Draw(double hue, int index)
    {
        var far = new ImmutableSolidColorBrush(HsvColor.ToRgb((hue + 200) % 360, .45, .45));
        var near = new ImmutableSolidColorBrush(HsvColor.ToRgb((hue + 190) % 360, .55, .25));
        var sun = new ImmutableSolidColorBrush(HsvColor.ToRgb((hue + 40) % 360, .3, 1));
        var peak = 40 + index * 23 % 40;
        return new Canvas
        {
            Width = 200,
            Height = 130,
            Background = Palette.HueGradient(hue),
            Children =
            {
                new Ellipse { Width = 34, Height = 34, Fill = sun, [Canvas.LeftProperty] = 20d + index * 47 % 140, [Canvas.TopProperty] = 18d },
                new Path { Fill = far, Opacity = .8, Data = Geometry.Parse(string.Create(CultureInfo.InvariantCulture, $"M0,95 L40,{peak + 22} L75,85 L115,{peak} L160,82 L200,62 L200,130 L0,130 Z")) },
                new Path { Fill = near, Data = Geometry.Parse("M0,110 C50,88 90,120 140,100 C170,90 185,96 200,92 L200,130 L0,130 Z") },
            },
        };
    }
}
