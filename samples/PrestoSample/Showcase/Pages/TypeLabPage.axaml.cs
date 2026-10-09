using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Controls;
using Monaco.Presto.AvaloniaBridge.Draggables;
using Monaco.Presto.AvaloniaBridge.Text;

namespace PrestoSample.Showcase.Pages;

public partial class TypeLabPage : ShowcasePage
{
    private const double HandleRadius = 8;
    private const double DepartureInterval = 5000;
    private const double Gain = .6;

    private static readonly Point[] Wave = [new(40, 170), new(220, 30), new(420, 250), new(600, 110)];
    private static readonly Point[] Step = [new(40, 210), new(330, 210), new(310, 50), new(600, 50)];
    private static readonly Geometry Circle = Geometry.Parse("M 210,130 A 110,110 0 1 1 430,130 A 110,110 0 1 1 210,130 Z");
    private static readonly string[] Words = ["SHATTER", "THREATS", "HEARTS", "HEARTH", "EARTH"];

    private static readonly string[] Departures =
    [
        "PR 214  LISBON    08:40",
        "KL 1022 AMSTERDAM 08:55",
        "AZ 611  ROME      09:10",
        "LH 482  FRANKFURT 09:25",
        "IB 3107 MADRID    09:40",
        "SK 905  OSLO      09:55",
        "OS 118  VIENNA    10:15",
        "AY 1334 HELSINKI  10:30",
        "TP 750  PORTO     10:45",
    ];

    private readonly Point[] _points = [.. Wave];
    private readonly Ellipse[] _handles;
    private readonly SplitFlap[] _boards;
    private Point[] _next = Step;
    private Ellipse? _dragged;
    private Timeline? _morph;
    private Animation? _flow;
    private int _word;
    private int _departure;

    public TypeLabPage()
    {
        InitializeComponent();
        _handles = [.. Handles.Children.OfType<Ellipse>()];
        _boards = [.. Board.Children.OfType<SplitFlap>()];
        HowCode.Xaml = string.Join("\n\n", Snippets.TypeLabRibbon, Snippets.TypeLabBoard);
        HowCode.CSharp = string.Join("\n\n", Snippets.TypeLabMorph, Snippets.TypeLabWeightWave, Snippets.TypeLabShatter,
            Snippets.TypeLabDepartures, Snippets.TypeLabElastic);
    }

    protected override void OnAttached()
    {
        _morph = null;
        Shape(_points);
        FlowToggle.IsChecked = false;
        FlowToggle.IsEnabled = !Presto.Defaults.ReducedMotion;
        SplitWaves();
        RollDepartures();
        PrestoAvalonia.SplitText(ElasticWord, new SplitTextParams { Words = null, Chars = new SplitPieceParams() }).AddEffect(Elastic);
    }

    #region snippet TypeLabWeightWave
    private void SplitWaves()
    {
        PrestoAvalonia.SplitText(WeightLine, new SplitTextParams { Words = null, Chars = new SplitPieceParams() })
            .AddEffect(split => Ripple(split, "FontWeight", 100, 900));
        PrestoAvalonia.SplitText(StretchLine, new SplitTextParams { Words = null, Chars = new SplitPieceParams() })
            .AddEffect(split => Ripple(split, "FontStretch", (int)FontStretch.ExtraCondensed, (int)FontStretch.Expanded));
    }

    private static Action Ripple(TextSplitter split, string property, double from, double to)
    {
        var animation = Presto.Animate(split.Chars, new AnimationParams
        {
            [property] = TweenValue.FromTo(from, to),
            Duration = 1200,
            Ease = Eases.InOutSine,
            Delay = Presto.Stagger(90),
            Loop = true,
            Alternate = true,
            WhenReduced = new() { [property] = Presto.Stagger(new[] { from, to }), Duration = 0, Delay = 0, Loop = false, Alternate = false },
        });
        return () => animation.Revert();
    }
    #endregion

    #region snippet TypeLabShatter
    private void Shatter(object? sender, TappedEventArgs e)
    {
        _word = (_word + 1) % Words.Length;
        var explode = ExplodeToggle.IsChecked == true;
        // MorphText cuts vector shards only on the outline split that shape makes.
        var shape = ShapeToggle.IsChecked == true || (explode && ShardsToggle.IsChecked == true);
        Recipes.MorphText(ShatterWord, Words[_word], explode, shape: shape);
    }
    #endregion

    #region snippet TypeLabDepartures
    private void RollDepartures()
    {
        ShowDepartures();
        Presto.CreateTimer(new TimerParams
        {
            Duration = DepartureInterval,
            Loop = true,
            OnLoop = _ =>
            {
                _departure++;
                ShowDepartures();
            },
        });
    }

    private void ShowDepartures()
    {
        for (var row = 0; row < _boards.Length; row++) _boards[row].Text = Departures[(_departure + row) % Departures.Length];
    }
    #endregion

    #region snippet TypeLabElastic
    // A draggable's own X would hide a followed one, so each letter only triggers the drag of an invisible proxy.
    private Action Elastic(TextSplitter split)
    {
        var letters = split.Chars;
        var reduced = Presto.Defaults.ReducedMotion;
        var drags = new Draggable[letters.Count];
        var xs = new MotionValue[letters.Count][];
        var ys = new MotionValue[letters.Count][];
        for (var i = 0; i < letters.Count; i++)
        {
            var proxy = new Border();
            ElasticProxies.Children.Add(proxy);
            drags[i] = PrestoAvalonia.CreateDraggable(proxy, new DraggableParams
            {
                Trigger = letters[i],
                Snap = _ => 0,
                ReleaseEase = Springs.Bouncy,
                OnRelease = reduced ? drag => drag.Reset() : null,
            });
            if (reduced)
            {
                Follow.SetX(letters[i], drags[i].XValue);
                Follow.SetY(letters[i], drags[i].YValue);
            }
            else
            {
                xs[i] = Hops(drags[i].XValue, letters.Count);
                ys[i] = Hops(drags[i].YValue, letters.Count);
            }
        }
        if (!reduced)
        {
            for (var i = 0; i < letters.Count; i++)
            {
                Follow.SetX(letters[i], Felt(xs, i));
                Follow.SetY(letters[i], Felt(ys, i));
            }
        }
        return () =>
        {
            foreach (var drag in drags) drag.Revert();
            ElasticProxies.Children.Clear();
        };
    }

    // Hop k carries a letter's pull to the letters k positions away, a spring later and Gain^k as strong.
    private static MotionValue[] Hops(MotionValue pull, int count) =>
        [pull, .. pull.Trail(count - 1, Springs.Snappy).Select((hop, k) => hop.Map(v => v * Math.Pow(Gain, k + 1)))];

    // Every letter's pull reaches every other, so grabbing a new letter mid-settle needs no rewiring.
    private static MotionValue Felt(MotionValue[][] hops, int letter) =>
        MotionValue.Combine([.. hops.Select((pull, i) => pull[Math.Abs(i - letter)])], pulls => pulls.Sum());
    #endregion

    private void Morph(object? sender, RoutedEventArgs e)
    {
        if (_morph is { Completed: false }) return;
        _morph?.Revert();
        var next = _next;
        _next = next == Step ? Wave : Step;
        if (Presto.Defaults.ReducedMotion) Shape(next);
        else _morph = Scope.Execute(_ => MorphThrough(next));
    }

    private void Flow(object? sender, RoutedEventArgs e)
    {
        if (FlowToggle.IsChecked == true) _flow = Scope.Execute(_ => FlowAlong());
        else _flow?.Revert();
    }

    #region snippet TypeLabMorph
    // The text takes no part: TextPath re-places it in every frame the Data changes.
    private Timeline MorphThrough(Point[] next) => Presto.CreateTimeline()
        .Add(Handles, new() { ["Opacity"] = 0, Duration = 200 })
        .Add(Curve, new() { ["Data"] = PrestoAvalonia.MorphTo(Circle), Duration = 900, Ease = Eases.InOutCubic }, 0)
        .Add(Curve, new() { ["Data"] = PrestoAvalonia.MorphTo(Bezier(next)), Duration = 900, Ease = Eases.InOutCubic }, "+=250")
        .Call(_ => Shape(next))
        .Add(Handles, new() { ["Opacity"] = 1, Duration = 200 });

    private Animation FlowAlong() => Ribbon.Animate(new()
    {
        [TextPath.OffsetProperty] = TweenValue.FromTo(0, 1),
        Duration = 6000,
        Ease = Eases.None,
        Loop = true,
    });
    #endregion

    private void Shape(Point[] points)
    {
        points.CopyTo(_points, 0);
        Curve.Data = Bezier(points);
        for (var i = 0; i < _handles.Length; i++)
        {
            Canvas.SetLeft(_handles[i], points[i].X - HandleRadius);
            Canvas.SetTop(_handles[i], points[i].Y - HandleRadius);
        }
        (StartArm.StartPoint, StartArm.EndPoint) = (points[0], points[1]);
        (EndArm.StartPoint, EndArm.EndPoint) = (points[3], points[2]);
    }

    private static Geometry Bezier(Point[] p) =>
        Geometry.Parse(FormattableString.Invariant($"M {p[0].X},{p[0].Y} C {p[1].X},{p[1].Y} {p[2].X},{p[2].Y} {p[3].X},{p[3].Y}"));

    // The morph holds Data and the handles' Opacity once it ends, so a drag reverts it to reach the local values underneath.
    private void GrabHandle(object? sender, PointerPressedEventArgs e)
    {
        _morph?.Revert();
        _dragged = (Ellipse)sender!;
        e.Pointer.Capture(_dragged);
        e.Handled = true;
    }

    private void DragHandle(object? sender, PointerEventArgs e)
    {
        if (sender != _dragged) return;
        var at = e.GetPosition(PathStage);
        _points[Array.IndexOf(_handles, _dragged)] = new Point(Math.Clamp(at.X, 0, PathStage.Width), Math.Clamp(at.Y, 0, PathStage.Height));
        Shape(_points);
    }

    private void DropHandle(object? sender, PointerReleasedEventArgs e) => _dragged = null;
}
