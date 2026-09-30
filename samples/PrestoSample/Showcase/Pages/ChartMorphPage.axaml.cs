using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Line = Avalonia.Controls.Shapes.Line;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Showcase.Pages;

public partial class ChartMorphPage : ShowcasePage
{
    /// <summary>The donut's outer radius around the centre of the plot.</summary>
    public const double DonutRadius = 132;

    private const double DonutHole = 80;
    private const double WedgeGap = 1.5;
    private const double BarWidth = 34;
    private const double BarRadius = 6;
    private const double MarkerRadius = 7;
    private const double ScatterInset = 30;
    private const double MorphDuration = 720;
    private const double UndrawDuration = 240;
    private const int TickCount = 5;

    private static readonly MotionStates Lift =
    [
        new MotionState
        {
            When = ":pointerover",
            Motion = Presto.Build().Scale(1.08).Duration(380).Ease(Eases.OutBack()),
            Leave = Presto.Build().Duration(260).Ease(Eases.OutQuad),
        },
    ];

    private readonly Path[] _points = new Path[Datasets.ChartPoints[0].Count];
    private readonly TextBlock[] _ticks = new TextBlock[TickCount];
    private ChartView _view;
    private int _round;
    private Timeline? _transition;

    public ChartMorphPage()
    {
        InitializeComponent();
        var column = Plot.Width / _points.Length;
        for (var i = 0; i < _points.Length; i++)
        {
            var index = i;
            _points[i] = new Path
            {
                Fill = Palette.Categorical[i],
                Data = ChartGeometry.Bar(Column(i), Plot.Height - 1, Plot.Height, BarWidth, BarRadius),
            };
            Motion.SetStates(_points[i], Lift);
            _points[i].PropertyChanged += (_, e) =>
            {
                if (e.Property == IsPointerOverProperty) Scope.Execute(_ => ShowTip(index, e.GetNewValue<bool>()));
            };
            Plot.Children.Insert(Plot.Children.IndexOf(Tip), _points[i]);

            var label = new TextBlock { Text = Points[i].Label, Width = column, TextAlignment = TextAlignment.Center };
            Canvas.SetLeft(label, i * column);
            Canvas.SetTop(label, 6);
            Categories.Children.Add(label);
        }

        for (var i = 0; i < TickCount; i++)
        {
            var y = Plot.Height * (1 - ((double)i / (TickCount - 1)));
            Axes.Children.Add(new Line
            {
                StartPoint = new Point(0, y),
                EndPoint = new Point(Plot.Width, y),
                Stroke = i == 0 ? Palette.InkDim : Palette.Border,
                StrokeThickness = 1,
            });
            _ticks[i] = new TextBlock { Text = "0", Width = 32, TextAlignment = TextAlignment.Right };
            Canvas.SetTop(_ticks[i], y - 8);
            Ticks.Children.Add(_ticks[i]);
        }
        Axes.Children.Add(new Line { EndPoint = new Point(0, Plot.Height), Stroke = Palette.InkDim, StrokeThickness = 1 });

        LineStroke.Data = ChartGeometry.Line(Markers(Points));
    }

    private enum ChartView
    {
        Bars,
        Donut,
        Scatter,
        Line,
    }

    private IReadOnlyList<Datasets.ChartPoint> Points => Datasets.ChartPoints[_round % Datasets.ChartPoints.Count];

    protected override void OnAttached()
    {
        PrestoAvalonia.CreateDrawable(LineStroke);
        Show(ChartView.Bars);
    }

    private static double NiceMax(double value) => Math.Ceiling(value / 20) * 20;

    private static AvaloniaAnimationParams Fade(double opacity) =>
        new() { ["Opacity"] = opacity, Duration = 360, Ease = Eases.OutQuad };

    private void Show(ChartView view)
    {
        // Replace composition would retarget the points alone; the last switch's stroke Call and draw-in must not fire later.
        _transition?.Cancel();
        _view = view;
        var points = Points;
        var shapes = Shapes(view, points);
        var max = NiceMax(view == ChartView.Scatter ? points.Max(p => p.Secondary) : points.Max(p => p.Value));
        var draw = (double[])PrestoAvalonia.Get(LineStroke, "Draw")!;
        var start = draw[1] > draw[0] ? UndrawDuration : 0;
        Unit.Text = view == ChartView.Scatter ? "% converted" : "k visits";

        var timeline = Presto.CreateTimeline();
        if (start > 0)
        {
            timeline.Add(LineStroke, new AvaloniaAnimationParams { ["Draw"] = "0 0", Duration = UndrawDuration, Ease = Eases.InQuad }, 0);
        }
        timeline
            .Add(_points, new AvaloniaAnimationParams
            {
                ["Data"] = (ChainedFunctionValue)((target, i, total, previous) => PrestoAvalonia.MorphTo(shapes[i])(target, i, total, previous)),
                Duration = MorphDuration,
                Ease = Eases.OutQuart,
                Delay = Presto.Stagger(45, new StaggerParams { From = view == ChartView.Donut ? StaggerFrom.First : StaggerFrom.Center }),
            }, start)
            .Add(_ticks, new AvaloniaAnimationParams
            {
                ["Text"] = new TweenParams
                {
                    To = (FunctionValue)((_, i, _) => max * i / (TickCount - 1)),
                    TextModifier = value => Utils.RoundPad(value, 0),
                },
                Duration = MorphDuration,
                Ease = Eases.OutQuart,
            }, start)
            .Add(new Visual[] { Axes, Ticks, Unit }, Fade(view == ChartView.Donut ? 0 : 1), start)
            .Add(Categories, Fade(view is ChartView.Bars or ChartView.Line ? 1 : 0), start);
        if (view == ChartView.Line)
        {
            var line = ChartGeometry.Line(Markers(points));
            timeline
                .Call(_ => LineStroke.Data = line, start)
                .Add(LineStroke, new AvaloniaAnimationParams { ["Draw"] = TweenValue.FromTo("0 0", "0 1"), Duration = 520, Ease = Eases.InOutQuad });
        }
        _transition = timeline;
    }

    private Geometry[] Shapes(ChartView view, IReadOnlyList<Datasets.ChartPoint> points)
    {
        var valueMax = NiceMax(points.Max(p => p.Value));
        switch (view)
        {
            case ChartView.Bars:
                return [.. points.Select((p, i) => ChartGeometry.Bar(Column(i), Y(p.Value, valueMax), Plot.Height, BarWidth, BarRadius))];
            case ChartView.Donut:
                var centre = new Point(Plot.Width / 2, Plot.Height / 2);
                return
                [
                    .. ChartGeometry.Sweeps([.. points.Select(p => p.Value)])
                        .Select(s => ChartGeometry.Wedge(centre, DonutHole, DonutRadius, s.Start + (WedgeGap / 2), s.Sweep - WedgeGap)),
                ];
            case ChartView.Scatter:
                var secondaryMax = NiceMax(points.Max(p => p.Secondary));
                var span = Plot.Width - (2 * ScatterInset);
                return
                [
                    .. points.Select(p => ChartGeometry.Circle(
                        new Point(ScatterInset + (p.Value / valueMax * span), Y(p.Secondary, secondaryMax)),
                        6 + (p.Weight * 3.5))),
                ];
            default:
                return [.. Markers(points).Select(marker => ChartGeometry.Circle(marker, MarkerRadius))];
        }
    }

    private double Column(int index) => (index + .5) * Plot.Width / _points.Length;

    private double Y(double value, double max) => Plot.Height * (1 - (value / max));

    private Point[] Markers(IReadOnlyList<Datasets.ChartPoint> points)
    {
        var max = NiceMax(points.Max(p => p.Value));
        return [.. points.Select((p, i) => new Point(Column(i), Y(p.Value, max)))];
    }

    private void ShowTip(int index, bool shown)
    {
        if (shown)
        {
            var point = Points[index];
            var box = _points[index].Data!.Bounds;
            _points[index].RenderTransformOrigin = new RelativePoint(box.Center, RelativeUnit.Absolute);
            TipText.Text = $"{point.Label} · {point.Value}k visits · {point.Secondary}%";
            Canvas.SetLeft(Tip, Math.Clamp(box.Center.X - (Tip.Width / 2), 0, Plot.Width - Tip.Width));
            Canvas.SetTop(Tip, Math.Max(0, box.Top - 36));
        }
        Tip.Animate(new() { ["Opacity"] = shown ? 1 : 0, Duration = 160, Ease = Eases.OutQuad });
    }

    private void ViewChanged(object? sender, SelectionChangedEventArgs e)
    {
        var view = (ChartView)((TabStrip)sender!).SelectedIndex;
        if (view != _view) Scope.Execute(_ => Show(view));
    }

    private void ShuffleClicked(object? sender, RoutedEventArgs e)
    {
        _round++;
        Scope.Execute(_ => Show(_view));
    }
}
