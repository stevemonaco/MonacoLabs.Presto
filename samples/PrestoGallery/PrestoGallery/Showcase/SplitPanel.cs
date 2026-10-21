using Avalonia;
using Avalonia.Controls;

namespace PrestoGallery.Showcase;

/// <summary>
/// Lays its children out side by side, sized like grid columns by <see cref="Widths"/>, once it is at least
/// <see cref="Breakpoint"/> wide, and stacks them at full width below that, so a two-pane demo still reads on a phone.
/// </summary>
public sealed class SplitPanel : Panel
{
    public static readonly StyledProperty<string> WidthsProperty =
        AvaloniaProperty.Register<SplitPanel, string>(nameof(Widths), "*,*");

    public static readonly StyledProperty<double> BreakpointProperty =
        AvaloniaProperty.Register<SplitPanel, double>(nameof(Breakpoint), 560);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<SplitPanel, double>(nameof(Spacing), 12);

    private IReadOnlyList<GridLength> _widths = [.. GridLength.ParseLengths("*,*")];

    static SplitPanel() => AffectsMeasure<SplitPanel>(WidthsProperty, BreakpointProperty, SpacingProperty);

    /// <summary>The children's widths side by side, as a grid's column definitions spell them; a child past the list takes <c>*</c>.</summary>
    public string Widths
    {
        get => GetValue(WidthsProperty);
        set => SetValue(WidthsProperty, value);
    }

    /// <summary>The narrowest width at which the children sit side by side.</summary>
    public double Breakpoint
    {
        get => GetValue(BreakpointProperty);
        set => SetValue(BreakpointProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WidthsProperty) _widths = [.. GridLength.ParseLengths(Widths)];
    }

    private bool IsSplit(double width) => double.IsInfinity(width) || width >= Breakpoint;

    private GridLength WidthOf(int index) => index < _widths.Count ? _widths[index] : GridLength.Star;

    protected override Size MeasureOverride(Size availableSize)
    {
        var shown = Children.Where(child => child.IsVisible).ToList();
        if (!IsSplit(availableSize.Width))
        {
            var height = 0.0;
            foreach (var child in shown)
            {
                child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
                height += child.DesiredSize.Height;
            }
            return new Size(availableSize.Width, height + Spacing * Math.Max(0, shown.Count - 1));
        }

        var columns = Columns(availableSize.Width, shown);
        var tallest = 0.0;
        for (var i = 0; i < shown.Count; i++)
        {
            shown[i].Measure(new Size(columns[i], availableSize.Height));
            tallest = Math.Max(tallest, shown[i].DesiredSize.Height);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? columns.Sum() + Spacing * (shown.Count - 1) : availableSize.Width, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var shown = Children.Where(child => child.IsVisible).ToList();
        if (!IsSplit(finalSize.Width))
        {
            var y = 0.0;
            foreach (var child in shown)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + Spacing;
            }
            return finalSize;
        }

        var columns = Columns(finalSize.Width, shown);
        var x = 0.0;
        for (var i = 0; i < shown.Count; i++)
        {
            shown[i].Arrange(new Rect(x, 0, columns[i], finalSize.Height));
            x += columns[i] + Spacing;
        }
        return finalSize;
    }

    // Auto columns take their child's unconstrained width, as stars do with no width to share; otherwise stars share what is left.
    private double[] Columns(double width, List<Control> shown)
    {
        var columns = new double[shown.Count];
        var stars = 0.0;
        var used = Spacing * Math.Max(0, shown.Count - 1);
        for (var i = 0; i < shown.Count; i++)
        {
            var length = WidthOf(i);
            if (length.IsStar && !double.IsInfinity(width))
            {
                stars += length.Value;
                continue;
            }
            if (length.IsAbsolute) columns[i] = length.Value;
            else
            {
                shown[i].Measure(Size.Infinity);
                columns[i] = shown[i].DesiredSize.Width;
            }
            used += columns[i];
        }
        var left = Math.Max(0, width - used);
        for (var i = 0; i < shown.Count; i++)
        {
            if (stars > 0 && WidthOf(i).IsStar) columns[i] = left * WidthOf(i).Value / stars;
        }
        return columns;
    }
}
