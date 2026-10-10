using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Values;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// Color tweens expanded along CSS Color 4 paths by <see cref="TweenKeyframesBuilder.Color"/>: an OKLCH hue cycle beside
/// RGB keyframes, each drawn as its whole path and lightness curve under a shared playhead, and one status flip played in
/// every space, one row each.
/// </summary>
public partial class ColorSpacesPage : ShowcasePage
{
    private const double Cycle = 8000;
    private const double TrackWidth = 560;
    private const double StripHeight = 28;
    private const double PlotTop = 38;
    private const double PlotHeight = 72;
    private const double MinLightness = 0.3;

    private static readonly (string Name, string Color)[] Statuses = [("Syncing", "#2563eb"), ("Warning", "#f59e0b"), ("Error", "#ef4444")];
    private static readonly ColorSpace?[] Spaces = [null, ColorSpace.Srgb, ColorSpace.LinearSrgb, ColorSpace.Oklab, ColorSpace.Oklch];

    private readonly List<Border> _pills = [];
    private readonly List<Border> _paths = [];
    private readonly Animation[] _flips = new Animation[Spaces.Length];
    private Track? _rgbTrack;
    private Track? _oklchTrack;
    private int _status;

    public ColorSpacesPage()
    {
        InitializeComponent();
        foreach (var space in Spaces)
        {
            var label = new TextBlock { Classes = { "mono" }, Text = space?.ToString() ?? "Default (≈ LinearSrgb)" };
            var pill = new Border { Classes = { "pill" }, Child = new TextBlock() };
            var path = new Border { Classes = { "path" } };
            Grid.SetColumn(pill, 1);
            Grid.SetColumn(path, 2);
            _pills.Add(pill);
            _paths.Add(path);
            PillRows.Children.Add(new Grid { ColumnDefinitions = new("190,150,*"), Children = { label, pill, path } });
        }
    }

    public Animation? RgbCycle { get; private set; }

    public Animation? OklchCycle { get; private set; }

    public IReadOnlyList<Border> Pills => _pills;

    public IReadOnlyList<Animation> StatusFlips => _flips;

    protected override void OnAttached()
    {
        var (rgb, rgbCode) = Snippet.Of(Presto.Build()
            .Background(k => k.To("#ffff00").To("#00ff00").To("#00ffff").To("#0000ff").To("#ff00ff").To("#ff0000"))
            .Duration(8000)
            .Ease(Eases.None));
        var (oklch, oklchCode) = Snippet.Of(Presto.Build()
            .Background(k => k.Color("#ff0000", "#ff0000", ColorSpace.Oklch, HueInterpolation.Increasing, duration: 8000, ease: Eases.None)));
        HueCode.Code = $"var rgb = {rgbCode};\n\nvar oklch = {oklchCode};\n\nRgbGlow.Animate(rgb.Loop());\nOklchGlow.Animate(oklch.Loop());";

        _rgbTrack = BuildTrack(RgbTrack, Sample(rgb, RgbGlow.Background, Cycle, 160));
        _oklchTrack = BuildTrack(OklchTrack, Sample(oklch, null, Cycle, 160));
        RgbCycle = RgbGlow.Animate(rgb.Clone().Loop().WhenReduced(reduced => reduced.Loop(false)));
        OklchCycle = OklchGlow.Animate(oklch.Clone().Loop().WhenReduced(reduced => reduced.Loop(false)).OnUpdate(UpdateTracks));

        _status = 1;
        Flip();
    }

    private void NextStatus(object? sender, RoutedEventArgs e)
    {
        _status = (_status + 1) % Statuses.Length;
        Flip();
    }

    private void Flip()
    {
        var from = Statuses[(_status + Statuses.Length - 1) % Statuses.Length].Color;
        var to = Statuses[_status].Color;
        string defaultCode = "", colorCode = "";
        for (var i = 0; i < Spaces.Length; i++)
        {
            AnimationBuilder builder;
            if (Spaces[i] is { } space)
                (builder, colorCode) = Snippet.Of(Presto.Build().Background(k => k.Color(from, to, space, duration: 1500, ease: Eases.OutCubic)));
            else
                (builder, defaultCode) = Snippet.Of(Presto.Build().Background(to, from: from, duration: 1500, ease: Eases.OutCubic));
            _paths[i].Background = Gradient(Sample(builder, null, 1500, 48));
            ((TextBlock)_pills[i].Child!).Text = Statuses[_status].Name;
            _flips[i] = _pills[i].Animate(builder);
        }
        StatusCode.Code = $"var (from, to) = (\"{from}\", \"{to}\");\n\n// Default\n{defaultCode};\n\nforeach (var space in new[] {{ ColorSpace.Srgb, ColorSpace.LinearSrgb, ColorSpace.Oklab, ColorSpace.Oklch }})\n    {colorCode};";
    }

    private void UpdateTracks(MonacoLabs.Presto.Timer cycle)
    {
        var x = cycle.IterationProgress * TrackWidth;
        Move(_rgbTrack!, x, Read(RgbGlow, RgbReadout));
        Move(_oklchTrack!, x, Read(OklchGlow, OklchReadout));
    }

    private static void Move(Track track, double x, double lightness)
    {
        Canvas.SetLeft(track.Head, x - 1);
        Canvas.SetLeft(track.Dot, x - 5);
        Canvas.SetTop(track.Dot, PlotY(lightness) - 5);
    }

    private static Color[] Sample(AnimationBuilder builder, IBrush? start, double duration, int count)
    {
        var probe = new Border { Background = start };
        var animation = builder.Clone().Autoplay(false).Animate(probe);
        var colors = new Color[count];
        for (var i = 0; i < count; i++)
        {
            animation.Seek(duration * i / (count - 1));
            colors[i] = ((ISolidColorBrush)probe.Background!).Color;
        }
        animation.Cancel();
        return colors;
    }

    private static LinearGradientBrush Gradient(Color[] colors)
    {
        var brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative) };
        for (var i = 0; i < colors.Length; i++) brush.GradientStops.Add(new GradientStop(colors[i], (double)i / (colors.Length - 1)));
        return brush;
    }

    private static Track BuildTrack(Canvas canvas, Color[] path)
    {
        canvas.Width = TrackWidth;
        canvas.Height = PlotTop + PlotHeight;
        canvas.Children.Add(new Border { Width = TrackWidth, Height = StripHeight, CornerRadius = new CornerRadius(6), Background = Gradient(path) });
        var plot = new Border { Width = TrackWidth, Height = PlotHeight, CornerRadius = new CornerRadius(6), Background = Palette.SurfaceHigh };
        Canvas.SetTop(plot, PlotTop);
        canvas.Children.Add(plot);
        foreach (var level in new[] { 0.5, 0.7, 0.9 })
        {
            var y = PlotY(level);
            canvas.Children.Add(new Line { StartPoint = new Point(0, y), EndPoint = new Point(TrackWidth, y), Stroke = Palette.Border, StrokeThickness = 1 });
            var label = new TextBlock { Text = $"L {level:0.0}", FontSize = 10, Foreground = Palette.InkDim };
            Canvas.SetLeft(label, 6);
            Canvas.SetTop(label, y - 13);
            canvas.Children.Add(label);
        }
        var points = new List<Point>(path.Length);
        for (var i = 0; i < path.Length; i++) points.Add(new Point(TrackWidth * i / (path.Length - 1), PlotY(path[i].ToPresto().ToOklch().L)));
        canvas.Children.Add(new Polyline { Points = points, Stroke = Palette.Ink, StrokeThickness = 2 });
        var head = new Rectangle { Width = 2, Height = PlotTop + PlotHeight, Fill = Brushes.White };
        var dot = new Ellipse { Width = 10, Height = 10, Fill = Brushes.White, Stroke = Palette.Background, StrokeThickness = 2 };
        canvas.Children.Add(head);
        canvas.Children.Add(dot);
        return new Track(head, dot);
    }

    private static double PlotY(double lightness) =>
        PlotTop + 4 + ((1 - ((Math.Clamp(lightness, MinLightness, 1) - MinLightness) / (1 - MinLightness))) * (PlotHeight - 8));

    private static double Read(Border glow, TextBlock readout)
    {
        if (glow.Background is not ISolidColorBrush { Color: var color }) return 0;
        var luminance = 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var lightness = color.ToPresto().ToOklch().L;
        readout.Text = $"L {lightness:0.00}   {1.05 / (luminance + 0.05):0.0}:1 vs white";
        return lightness;
    }

    private static double Linear(byte channel)
    {
        var c = channel / 255d;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private sealed record Track(Rectangle Head, Ellipse Dot);
}
