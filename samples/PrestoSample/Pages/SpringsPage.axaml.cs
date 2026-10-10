using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>What a spring is made of: one <see cref="MonacoLabs.Presto.Spring"/> set physically or by bounce and duration, plotted and dropped.</summary>
public partial class SpringsPage : ShowcasePage
{
    private const int Samples = 241;
    private const double PlotPadding = 24;
    private const double Drop = 120;
    private const double Anchor = 10;
    private const double WeightTop = 40;
    private const int CoilTurns = 8;
    private const double CoilWidth = 14;
    private const double CoilLead = 6;
    private const double RaceDistance = 300;
    private const double TweenDuration = 600;
    private const int TraceSamples = 180;
    private const double TraceScale = 40;
    private const double ArrowScale = 60;
    private const double ArrowHead = 10;
    private const int RowDots = 6;
    private const double RowDrop = 150;
    private const double RowRest = 22;
    private const double ToggleTravel = 26;
    private const double TabWidth = 64;
    private const double SheetClosed = 140;
    private const double SheetOpen = 50;
    private const double ScrimShown = .5;
    private const int ChainDots = 10;

    public static readonly StyledProperty<double> SheetTopProperty =
        AvaloniaProperty.Register<SpringsPage, double>(nameof(SheetTop), SheetClosed);

    public static readonly StyledProperty<double> ScrimOpacityProperty =
        AvaloniaProperty.Register<SpringsPage, double>(nameof(ScrimOpacity));

    private static readonly string[] RegimeUses = ["playful UI feedback", "the default for UI moving to rest", "heavy, sluggish settling"];

    private static readonly (double Bounce, double Duration)[] ChainSprings = [(0, 300), (.5, 350), (0, 80)];

    private readonly Spring _spring = new();
    private bool _designer;
    private bool _syncing;

    #region snippet SpringRegimes
    private readonly Spring[] _regimes =
    [
        new Spring(stiffness: 100, damping: 6),
        new Spring(stiffness: 100, damping: 20),
        new Spring(stiffness: 100, damping: 40),
    ];
    #endregion

    private readonly MotionValue _targetX;
    private readonly MotionValue _targetY;
    private readonly MotionValue _followerX;
    private readonly double[] _tweenTrace = new double[TraceSamples];
    private readonly double[] _springTrace = new double[TraceSamples];
    private int _traceHead;
    private double _sampledAt = double.NaN;
    private double _tweenX;
    private double _springX;

    private readonly Animatable _throw;
    private readonly VelocityTracker _trackX = new();
    private readonly VelocityTracker _trackY = new();
    private Vector _grab;

    private readonly Spring _rowSpring = new(stiffness: 120, damping: 10);
    private readonly MotionValue _rowTarget;
    private bool _easeDown;

    private bool _toggled;
    private int _unread = 1;

    private readonly Spring _chainSpring = new(bounce: ChainSprings[0].Bounce, duration: ChainSprings[0].Duration);
    private readonly MotionValue _pointerX;
    private readonly MotionValue _pointerY;

    public SpringsPage()
    {
        InitializeComponent();
        Refresh();

        TextBlock[] captions = [UnderCaption, CriticalCaption, OverCaption];
        for (var i = 0; i < _regimes.Length; i++)
        {
            var ratio = _regimes[i].DampingRatio;
            captions[i].Text = string.Create(CultureInfo.InvariantCulture, $"DampingRatio {ratio:0.0#} · {Regime(ratio)} · {RegimeUses[i]}");
        }
        RegimesCode.Code = $"{Snippets.SpringRegimes}\n\n{Snippets.SpringRace}";

        #region snippet SpringFollowerSetup
        var spring = new Spring(stiffness: 40, damping: 9);
        _targetX = Presto.CreateMotionValue();
        _targetY = Presto.CreateMotionValue();
        _followerX = _targetX.Spring(spring);
        Follow.SetX(SpringPuck, _followerX);
        Follow.SetY(SpringPuck, _targetY.Spring(spring));
        #endregion
        VelocityCode.Code = $"{Snippets.SpringFollowerSetup}\n\n{Snippets.SpringRetarget}";

        #region snippet SpringThrowChase
        _throw = ThrowPuck.CreateAnimatable(new()
        {
            ["X"] = new(),
            ["Y"] = new(),
            Ease = new Spring(stiffness: 400, damping: 32),
            PreserveVelocity = true,
        });
        #endregion
        ThrowCode.Code = $"{Snippets.SpringThrowChase}\n\n{Snippets.SpringThrowMove}\n\n{Snippets.SpringThrowRelease}";

        for (var i = 0; i < RowDots; i++)
        {
            EaseRow.Children.Add(RowDot(i));
            FollowerRow.Children.Add(RowDot(i));
        }
        #region snippet SpringFollowerRow
        _rowTarget = Presto.CreateMotionValue();
        foreach (var dot in FollowerRow.Children) Follow.SetY(dot, _rowTarget.Spring(_rowSpring));
        #endregion
        EaseFollowerCode.Code = $"{Snippets.SpringEaseRow}\n\n{Snippets.SpringFollowerRow}\n\n{Snippets.SpringFollowerRetarget}";

        PresetsCode.CSharp = $"{Snippets.SpringPresetToggle}\n\n{Snippets.SpringPresetTabs}\n\n{Snippets.SpringPresetBadge}\n\n{Snippets.SpringPresetSheetOpen}";

        var chain = new Border[ChainDots];
        for (var i = 0; i < ChainDots; i++)
        {
            var size = 26 - (1.5 * i);
            chain[i] = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = Palette.Aqua,
                Opacity = 1 - (.07 * i),
                [Canvas.LeftProperty] = -size / 2,
                [Canvas.TopProperty] = -size / 2,
            };
            ChainStage.Children.Insert(0, chain[i]);
        }
        #region snippet SpringChain
        _pointerX = Presto.CreateMotionValue(ChainStage.Width / 2);
        _pointerY = Presto.CreateMotionValue(ChainStage.Height / 2);
        MotionValue x = _pointerX, y = _pointerY;
        foreach (var dot in chain)
        {
            x = x.Spring(_chainSpring);
            y = y.Spring(_chainSpring);
            Follow.SetX(dot, x);
            Follow.SetY(dot, y);
        }
        #endregion
        ChainCode.Code = $"{Snippets.SpringChain}\n\n{Snippets.SpringChainMove}\n\n{Snippets.SpringChainSwap}";
    }

    public double SheetTop
    {
        get => GetValue(SheetTopProperty);
        set => SetValue(SheetTopProperty, value);
    }

    public double ScrimOpacity
    {
        get => GetValue(ScrimOpacityProperty);
        set => SetValue(ScrimOpacityProperty, value);
    }

    /// <summary>The horizontal velocity of the tween puck and of the spring follower, in px/ms, as last sampled for the trace.</summary>
    public (double Tween, double Spring) HorizontalVelocity => (_tweenTrace[_traceHead], _springTrace[_traceHead]);

    protected override void OnAttached() => Presto.CreateTimer(new TimerParams { OnUpdate = _ => SampleVelocity() });

    private void StiffnessChanged(object? sender, RangeBaseValueChangedEventArgs e) => Set(false, () => _spring.Stiffness = e.NewValue);

    private void DampingChanged(object? sender, RangeBaseValueChangedEventArgs e) => Set(false, () => _spring.Damping = e.NewValue);

    private void MassChanged(object? sender, RangeBaseValueChangedEventArgs e) => Set(false, () => _spring.Mass = e.NewValue);

    private void VelocityChanged(object? sender, RangeBaseValueChangedEventArgs e) => Set(false, () => _spring.Velocity = e.NewValue);

    private void BounceChanged(object? sender, RangeBaseValueChangedEventArgs e) => Set(true, () => _spring.Bounce = e.NewValue);

    private void DurationChanged(object? sender, RangeBaseValueChangedEventArgs e) => Set(true, () => _spring.Duration = e.NewValue);

    private void Set(bool designer, Action apply)
    {
        if (_syncing) return;
        apply();
        _designer = designer;
        Refresh();
    }

    private void DropWeight(object? sender, RoutedEventArgs e) => Scope.Execute(_ =>
    {
        var spring = _spring;
        #region snippet SpringLabDrop
        Weight.Animate(new()
        {
            ["Y"] = TweenValue.FromTo(0, Drop),
            Ease = spring,
            OnUpdate = _ => DrawCoil((double)Presto.Get(Weight, "Y")!),
        });
        #endregion
    });

    private void Refresh()
    {
        _syncing = true;
        StiffnessSlider.Value = _spring.Stiffness;
        DampingSlider.Value = _spring.Damping;
        MassSlider.Value = _spring.Mass;
        VelocitySlider.Value = _spring.Velocity;
        BounceSlider.Value = _spring.Bounce;
        DurationSlider.Value = _spring.Duration;
        _syncing = false;

        var ratio = _spring.DampingRatio;
        RatioText.Text = string.Create(CultureInfo.InvariantCulture, $"DampingRatio {ratio:0.000} · {Regime(ratio)}");
        SettlingText.Text = string.Create(CultureInfo.InvariantCulture, $"SettlingDuration {_spring.SettlingDuration:0} ms");
        DurationText.Text = string.Create(CultureInfo.InvariantCulture, $"Duration {_spring.Duration:0} ms");

        var call = _designer
            ? string.Create(CultureInfo.InvariantCulture, $"Eases.Spring(bounce: {_spring.Bounce:0.###}, duration: {_spring.Duration:0.###})")
            : string.Create(CultureInfo.InvariantCulture,
                $"Eases.Spring(mass: {_spring.Mass:0.###}, stiffness: {_spring.Stiffness:0.###}, damping: {_spring.Damping:0.###}, velocity: {_spring.Velocity:0.###})");
        LabCode.Code = $"var spring = {call};\n\n{Snippets.SpringLabDrop}";

        Plot();
        DrawCoil(Presto.Get(Weight, "Y") is double y ? y : 0);
    }

    private static string Regime(double ratio) => Math.Abs(ratio - 1) < 1e-3 ? "critically damped" : ratio < 1 ? "underdamped" : "overdamped";

    private void RaceClicked(object? sender, RoutedEventArgs e) => Scope.Execute(_ =>
    {
        #region snippet SpringRace
        Border[] dots = [UnderDot, CriticalDot, OverDot];
        for (var i = 0; i < dots.Length; i++)
            dots[i].Animate(new() { ["X"] = TweenValue.FromTo(0, RaceDistance), Ease = _regimes[i] });
        #endregion
    });

    private void VelocityStagePressed(object? sender, PointerPressedEventArgs e) => Scope.Execute(_ =>
    {
        var click = e.GetPosition(VelocityStage);
        var x = click.X - Canvas.GetLeft(TweenPuck) - (TweenPuck.Width / 2);
        var y = click.Y - Canvas.GetTop(TweenPuck) - (TweenPuck.Height / 2);
        #region snippet SpringRetarget
        TweenPuck.Animate(new() { ["X"] = x, ["Y"] = y, Duration = TweenDuration, Ease = Eases.OutQuad });
        _targetX.Set(x);
        _targetY.Set(y);
        #endregion
    });

    private void ThrowPressed(object? sender, PointerPressedEventArgs e)
    {
        _grab = e.GetPosition(ThrowStage) - new Point(_throw.Get("X"), _throw.Get("Y"));
        _trackX.Reset();
        _trackY.Reset();
        ThrowArrow.Points = [];
        e.Pointer.Capture(ThrowPuck);
    }

    private void ThrowMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Captured != ThrowPuck) return;
        var (x, y) = e.GetPosition(ThrowStage) - _grab;
        #region snippet SpringThrowMove
        _trackX.Add(x, Presto.Engine.Now);
        _trackY.Add(y, Presto.Engine.Now);
        _throw.Set("X", x).Set("Y", y);
        #endregion
    }

    private void ThrowReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured != ThrowPuck) return;
        e.Pointer.Capture(null);
        var from = new Point(Canvas.GetLeft(ThrowPuck) + (ThrowPuck.Width / 2) + _throw.Get("X"), Canvas.GetTop(ThrowPuck) + (ThrowPuck.Height / 2) + _throw.Get("Y"));
        var released = new Vector(_trackX.Velocity, _trackY.Velocity);
        #region snippet SpringThrowRelease
        void Launch(string axis, double velocity)
        {
            var distance = -_throw.Get(axis);
            // The Ease rather than the Spring: Set would relaunch a Spring at the chase's velocity, not the pointer's.
            var spring = new Spring(stiffness: 120, damping: 8, velocity: distance == 0 ? 0 : velocity * 1000 / distance);
            _throw.Set(axis, 0, spring.SettlingDuration, spring.Ease);
        }
        Launch("X", _trackX.Velocity);
        Launch("Y", _trackY.Velocity);
        #endregion
        DrawArrow(from, released);
        ThrowText.Text = string.Create(CultureInfo.InvariantCulture, $"Release velocity ({released.X:0.00}, {released.Y:0.00}) px/ms");
    }

    private void DrawArrow(Point from, Vector velocity)
    {
        if (velocity.Length == 0) return;
        var tip = from + (velocity * ArrowScale);
        var back = -velocity.Normalize() * ArrowHead;
        Point Barb(double angle)
        {
            var (sin, cos) = Math.SinCos(angle);
            return tip + new Vector((back.X * cos) - (back.Y * sin), (back.X * sin) + (back.Y * cos));
        }
        ThrowArrow.Points = [from, tip, Barb(.5), tip, Barb(-.5)];
    }

    private Border RowDot(int index) => new()
    {
        Width = 20,
        Height = 20,
        CornerRadius = new CornerRadius(10),
        Background = Palette.Categorical[index],
    };

    private void EaseRowClicked(object? sender, RoutedEventArgs e) => Scope.Execute(_ =>
    {
        #region snippet SpringEaseRow
        _easeDown = !_easeDown;
        Presto.Animate(EaseRow.Children, new()
        {
            ["Y"] = _easeDown ? RowDrop : 0,
            Ease = _rowSpring,
            Delay = Presto.Stagger(60),
        });
        #endregion
    });

    private void FollowerTargetChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        #region snippet SpringFollowerRetarget
        _rowTarget.Set(e.NewValue);
        #endregion
        FollowerTargetLine.StartPoint = new Point(0, RowRest + e.NewValue);
        FollowerTargetLine.EndPoint = new Point(FollowerTargetLine.EndPoint.X, RowRest + e.NewValue);
    }

    private void ToggleClicked(object? sender, PointerPressedEventArgs e) => Scope.Execute(_ =>
    {
        #region snippet SpringPresetToggle
        _toggled = !_toggled;
        ToggleThumb.Animate(new() { ["X"] = _toggled ? ToggleTravel : 0, Ease = Springs.Stiff });
        #endregion
        ToggleTrack.Background = _toggled ? Palette.Accent : Palette.SurfaceHigh;
    });

    private void TabClicked(object? sender, RoutedEventArgs e) => Scope.Execute(_ =>
    {
        #region snippet SpringPresetTabs
        var index = Tabs.Children.IndexOf((Control)sender!);
        TabIndicator.Animate(new() { ["X"] = index * TabWidth, Ease = Springs.Snappy });
        #endregion
    });

    private void BadgeClicked(object? sender, RoutedEventArgs e) => Scope.Execute(_ =>
    {
        #region snippet SpringPresetBadge
        BadgeCount.Text = (++_unread).ToString(CultureInfo.InvariantCulture);
        Badge.Animate(new() { ["Scale"] = TweenValue.FromTo(.4, 1), Ease = Springs.Bouncy });
        #endregion
    });

    private void SheetClicked(object? sender, RoutedEventArgs e)
    {
        var open = SheetTop != SheetOpen;
        #region snippet SpringPresetSheetOpen
        SheetTop = open ? SheetOpen : SheetClosed;
        ScrimOpacity = open ? ScrimShown : 0;
        #endregion
        SheetButton.Content = open ? "Close sheet" : "Open sheet";
    }

    private void ChainMoved(object? sender, PointerEventArgs e)
    {
        #region snippet SpringChainMove
        var (x, y) = e.GetPosition(ChainStage);
        _pointerX.Set(x);
        _pointerY.Set(y);
        #endregion
    }

    private void ChainPickerChanged(object? sender, SelectionChangedEventArgs e)
    {
        #region snippet SpringChainSwap
        var (bounce, duration) = ChainSprings[((ComboBox)sender!).SelectedIndex];
        _chainSpring.Bounce = bounce;
        _chainSpring.Duration = duration;
        #endregion
    }

    // Both velocities are frame differences: VelocityOf peak-holds over its sample window, which would step the spring's curve.
    private void SampleVelocity()
    {
        var now = Presto.Engine.Now;
        var tweenX = (double)Presto.Get(TweenPuck, "X")!;
        var springX = _followerX.Value;
        if (now > _sampledAt)
        {
            _traceHead = (_traceHead + 1) % TraceSamples;
            _tweenTrace[_traceHead] = (tweenX - _tweenX) / (now - _sampledAt);
            _springTrace[_traceHead] = (springX - _springX) / (now - _sampledAt);
        }
        (_sampledAt, _tweenX, _springX) = (now, tweenX, springX);
        Trace(TweenTrace, _tweenTrace);
        Trace(SpringTrace, _springTrace);
    }

    private void Trace(Polyline line, double[] samples)
    {
        var middle = TraceCanvas.Height / 2;
        var points = new List<Point>(TraceSamples);
        for (var age = TraceSamples - 1; age >= 0; age--)
        {
            var velocity = samples[(_traceHead - age + TraceSamples) % TraceSamples];
            points.Add(new Point((TraceSamples - 1 - age) * TraceCanvas.Width / (TraceSamples - 1),
                Math.Clamp(middle - (velocity * TraceScale), 0, TraceCanvas.Height)));
        }
        line.Points = points;
    }

    private void Plot()
    {
        var settling = _spring.SettlingDuration;
        var span = Math.Max(settling, _spring.Duration) * 1.1;
        var samples = new double[Samples];
        double lowest = 0, highest = 1;
        for (var i = 0; i < Samples; i++)
        {
            var time = (double)i / (Samples - 1) * span;
            samples[i] = time >= settling ? 1 : _spring.PureEase(time / settling);
            lowest = Math.Min(lowest, samples[i]);
            highest = Math.Max(highest, samples[i]);
        }

        var width = PlotCanvas.Width;
        double PlotY(double value) => PlotPadding + ((highest - value) / (highest - lowest) * (PlotCanvas.Height - (2 * PlotPadding)));
        double PlotX(double time) => time / span * width;

        var points = new List<Point>(Samples);
        for (var i = 0; i < Samples; i++) points.Add(new Point((double)i / (Samples - 1) * width, PlotY(samples[i])));
        Curve.Points = points;

        ZeroLine.StartPoint = new Point(0, PlotY(0));
        ZeroLine.EndPoint = new Point(width, PlotY(0));
        OneLine.StartPoint = new Point(0, PlotY(1));
        OneLine.EndPoint = new Point(width, PlotY(1));
        Mark(PerceivedLine, PerceivedLabel, PlotX(_spring.Duration));
        Mark(SettlingLine, SettlingLabel, PlotX(settling));
    }

    private void Mark(Line line, TextBlock label, double x)
    {
        line.StartPoint = new Point(x, 0);
        line.EndPoint = new Point(x, PlotCanvas.Height);
        var right = x > PlotCanvas.Width / 2;
        Canvas.SetLeft(label, right ? double.NaN : x + 4);
        Canvas.SetRight(label, right ? PlotCanvas.Width - x + 4 : double.NaN);
    }

    private void DrawCoil(double y)
    {
        var center = Canvas.GetLeft(Weight) + (Weight.Width / 2);
        var bottom = Math.Max(WeightTop + y, Anchor + 20);
        var step = (bottom - Anchor - (2 * CoilLead)) / (2 * CoilTurns);
        var points = new List<Point>((2 * CoilTurns) + 4) { new(center, Anchor), new(center, Anchor + CoilLead) };
        for (var i = 0; i < 2 * CoilTurns; i++)
            points.Add(new Point(center + (i % 2 == 0 ? CoilWidth : -CoilWidth), Anchor + CoilLead + ((i + .5) * step)));
        points.Add(new Point(center, bottom - CoilLead));
        points.Add(new Point(center, bottom));
        Coil.Points = points;
    }
}
