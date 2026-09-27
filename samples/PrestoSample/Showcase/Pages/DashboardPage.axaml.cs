using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Path = Avalonia.Controls.Shapes.Path;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

public partial class DashboardPage : ShowcasePage
{
    private const double FeedInterval = 800;
    private const double PushDuration = 900;
    private const double BarWidth = 30;
    private const double BarHeight = 168;
    private const double ArcThickness = 20;
    // The space between two slices, as a fraction of the ring's contour.
    private const double SliceGap = 0.012;

    private const string RingData = "M 90,18 A 72,72 0 0 1 90,162 A 72,72 0 0 1 90,18";

    private readonly TextBlock[] _counters = new TextBlock[Datasets.Metrics.Count];
    private readonly Border[] _bars = new Border[Datasets.Bars[0].Count];
    private readonly Path[] _arcs = new Path[Datasets.Categories.Count];

    private int _round;

    public DashboardPage() => InitializeComponent();

    /// <summary>The timer behind the live switch.</summary>
    public Timer? Feed { get; private set; }

    protected override void OnAttached()
    {
        BuildTiles();
        BuildBars();
        BuildDonut();

        Spark.Data = SparkGeometry(0);
        PrestoAvalonia.CreateDrawable(Spark);

        Bar.Timer = Reveal();
        Feed = Presto.CreateTimer(new TimerParams
        {
            Duration = FeedInterval,
            Loop = LoopCount.Infinite,
            Autoplay = false,
            OnLoop = _ => Push(),
        });
    }

    // The contour fraction each slice starts at, cumulative, with one entry more than there are slices.
    private static double[] Spans(IReadOnlyList<double> weights)
    {
        var total = weights.Sum();
        var spans = new double[weights.Count + 1];
        for (var i = 0; i < weights.Count; i++) spans[i + 1] = spans[i] + (weights[i] / total);
        return spans;
    }

    private static string Slice(double[] spans, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{spans[index] + SliceGap:0.#####} {spans[index + 1]:0.#####}");

    private Geometry SparkGeometry(int round)
    {
        var readings = Datasets.Sparklines[round];
        var data = new StringBuilder();
        for (var i = 0; i < readings.Count; i++)
        {
            var x = i * Plot.Width / (readings.Count - 1);
            var y = Plot.Height - (readings[i] / 100 * Plot.Height);
            data.Append(CultureInfo.InvariantCulture, $"{(i == 0 ? "M" : "L")} {x:0.##},{y:0.##} ");
        }
        return Geometry.Parse(data.ToString());
    }

    private void BuildTiles()
    {
        for (var i = 0; i < _counters.Length; i++)
        {
            var metric = Datasets.Metrics[i];
            _counters[i] = new TextBlock { Classes = { "readout" }, Text = Utils.RoundPad(0, metric.Decimals) };
            var tile = new Border
            {
                Classes = { "card", "showcase" },
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Classes = { "eyebrow", "showcase" }, Text = metric.Label.ToUpperInvariant() },
                        _counters[i],
                        new TextBlock { Classes = { "caption" }, Text = metric.Unit },
                    },
                },
            };
            Grid.SetColumn(tile, i);
            Tiles.Children.Add(tile);
        }
    }

    private void BuildBars()
    {
        for (var i = 0; i < _bars.Length; i++)
        {
            _bars[i] = new Border
            {
                Width = BarWidth,
                Height = 0,
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Background = Palette.AccentBrushRamp[1 + (i * 3 / _bars.Length)],
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            BarField.Children.Add(_bars[i]);
        }
    }

    private void BuildDonut()
    {
        var spans = Spans(Datasets.Slices[0]);
        for (var i = 0; i < _arcs.Length; i++)
        {
            var brush = Palette.AccentBrushRamp[i + 1];
            _arcs[i] = new Path
            {
                Data = Geometry.Parse(RingData),
                Stretch = Stretch.None,
                Stroke = brush,
                StrokeThickness = ArcThickness,
                StrokeLineCap = PenLineCap.Round,
            };
            PrestoAvalonia.CreateDrawable(_arcs[i], spans[i], spans[i]);
            DonutStage.Children.Add(_arcs[i]);

            Legend.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new Border { Classes = { "dot" }, Background = brush },
                    new TextBlock { Classes = { "caption" }, Text = Datasets.Categories[i], VerticalAlignment = VerticalAlignment.Center },
                },
            });
        }
    }

    private Timeline Reveal()
    {
        var timeline = Presto.CreateTimeline();
        for (var i = 0; i < _counters.Length; i++) timeline.Add(_counters[i], CounterTo(i, 1600), 0);
        return timeline
            .Add(_bars, BarsTo(1100), 0)
            .Add(_arcs, ArcsTo(1200), 0)
            .Add(Spark, new AvaloniaAnimationParams { ["Draw"] = "0 1", Duration = 1400, Ease = Eases.InOutQuad }, 0);
    }

    private AvaloniaAnimationParams CounterTo(int index, double duration)
    {
        var metric = Datasets.Metrics[index];
        return new AvaloniaAnimationParams
        {
            ["Text"] = new TweenParams
            {
                To = metric.Readings[_round % metric.Readings.Count],
                TextModifier = value => Utils.RoundPad(value, metric.Decimals),
            },
            Duration = duration,
            Ease = Eases.OutQuad,
        };
    }

    private AvaloniaAnimationParams BarsTo(double duration)
    {
        var values = Datasets.Bars[_round % Datasets.Bars.Count];
        return new AvaloniaAnimationParams
        {
            ["Height"] = (FunctionValue)((_, i, _) => values[i] / 100 * BarHeight),
            Duration = duration,
            Ease = Eases.OutQuad,
            Delay = Presto.Stagger(26),
        };
    }

    private AvaloniaAnimationParams ArcsTo(double duration)
    {
        var spans = Spans(Datasets.Slices[_round % Datasets.Slices.Count]);
        return new AvaloniaAnimationParams
        {
            ["Draw"] = (FunctionValue)((_, i, _) => Slice(spans, i)),
            Duration = duration,
            Ease = Eases.OutQuad,
            Delay = Presto.Stagger(70),
        };
    }

    private AvaloniaAnimationParams SparkTo() => new()
    {
        ["Data"] = PrestoAvalonia.MorphTo(SparkGeometry(_round % Datasets.Sparklines.Count)),
        Duration = PushDuration,
        Ease = Eases.OutQuad,
    };

    private void Push()
    {
        _round++;
        Scope.Execute(_ =>
        {
            for (var i = 0; i < _counters.Length; i++) Presto.Animate(_counters[i], CounterTo(i, PushDuration));
            Presto.Animate(_bars, BarsTo(PushDuration));
            Presto.Animate(_arcs, ArcsTo(PushDuration));
            Presto.Animate(Spark, SparkTo());
        });
        if (Feed is { } feed)
        {
            FeedText.Text = string.Create(CultureInfo.InvariantCulture, $"round {_round} · feed {feed.CurrentTime:0} ms");
        }
    }

    private void PushRound(object? sender, RoutedEventArgs e) => Push();

    private void LiveChanged(object? sender, RoutedEventArgs e)
    {
        if (Feed is not { } feed) return;
        if (Live.IsChecked == true) feed.Play(); else feed.Pause();
    }
}
