using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

/// <summary>Nine loading indicators, each shown with the call that builds it; the <c>*Code</c> strings must match those calls.</summary>
public partial class LoadersPage : ShowcasePage
{
    public const string EqualiserCode = """
        Presto.Animate(Bars.Children, new() {
            ["ScaleY"] = TweenValue.FromTo(.2, 1),
            Delay = Presto.Stagger(90), Duration = 480,
            Ease = Eases.InOutSine, Loop = true, Alternate = true })
        """;

    public const string OrbitCode = """
        Presto.Animate(Orbit.Children, new() {
            ["Rotate"] = 360,
            Delay = Presto.Stagger(70), Duration = 1400,
            Ease = Eases.InOutCubic, Loop = true, LoopDelay = 280 })
        """;

    public const string GridCode = """
        Presto.Animate(Squares.Children, new() {
            ["Scale"] = .2, ["Opacity"] = .3,
            Delay = Presto.Stagger(80, new() { Grid = [5, 5], From = StaggerFrom.Center }),
            Duration = 560, Ease = Eases.InOutQuad, Loop = true, Alternate = true })
        """;

    public const string BlobCode = """
        Blob.Animate(new() {
            ["Data"] = PrestoAvalonia.MorphTo(Splat), ["Rotate"] = 180,
            Duration = 1300, Ease = Eases.InOutSine, Loop = true, Alternate = true })
        """;

    public const string RingCode = """
        PrestoAvalonia.CreateDrawable(Ring).Animate(new() {
            ["Draw"] = TweenValue.Keyframes("0 0", "0 1", "1 1"),
            Duration = 1800, Ease = Eases.InOutQuad, Loop = true })
        """;

    public const string BallCode = """
        Ball.Animate(new() {
            Keyframes = new Keyframe[] {
                new() { ["Y"] = -56, ["ScaleX"] = .9, ["ScaleY"] = 1.1, Ease = Eases.OutQuad },
                new() { ["Y"] = 0, Ease = Eases.InQuad },
                new() { ["ScaleX"] = 1.4, ["ScaleY"] = .6, Duration = 90 },
                new() { ["ScaleX"] = 1, ["ScaleY"] = 1, Duration = 160 } },
            Duration = 1000, Loop = true })
        """;

    public const string TypingCode = """
        Presto.Animate(Typing.Children, new() {
            ["Y"] = TweenValue.Keyframes(0, -7, 0),
            ["Opacity"] = TweenValue.Keyframes(.4, 1, .4),
            Delay = Presto.Stagger(150), Duration = 600,
            Ease = Eases.InOutSine, Loop = true, LoopDelay = 450 })
        """;

    public const string RippleCode = """
        Presto.Animate(Ripples.Children, new() {
            ["Scale"] = TweenValue.FromTo(.15, 1), ["Opacity"] = TweenValue.FromTo(1, 0),
            Delay = Presto.Stagger(600), Duration = 1800, Ease = Eases.OutQuad, Loop = true })
        """;

    public const string CounterCode = """
        Presto.CreateTimeline(new() { Loop = true, LoopDelay = 500 })
            .Add(Percent, new() {
                ["Text"] = new TweenParams { From = 0, To = 100, TextModifier = v => $"{v:0}%" },
                Duration = 2400, Ease = Eases.InOutQuad })
            .Add(Meter, new() {
                ["Value"] = TweenValue.FromTo(0, 100),
                Duration = 2400, Ease = Eases.InOutQuad }, "<<")
        """;

    private static readonly Geometry Splat = Geometry.Parse("M30,10 C48,2 60,22 50,34 C42,46 40,58 26,54 C10,50 0,40 8,26 C14,14 18,14 30,10 Z");

    private readonly List<Timer> _loaders = [];

    public LoadersPage()
    {
        InitializeComponent();
        for (var i = 0; i < 25; i++) Squares.Children.Add(new Border());
    }

    /// <summary>The nine loaders, each its own animation or timeline so the page can set its speed alone.</summary>
    public IReadOnlyList<Timer> Loaders => _loaders;

    protected override void OnAttached()
    {
        _loaders.Add(Presto.Animate(Bars.Children, new() {
            ["ScaleY"] = TweenValue.FromTo(.2, 1),
            Delay = Presto.Stagger(90), Duration = 480,
            Ease = Eases.InOutSine, Loop = true, Alternate = true }));

        _loaders.Add(Presto.Animate(Orbit.Children, new() {
            ["Rotate"] = 360,
            Delay = Presto.Stagger(70), Duration = 1400,
            Ease = Eases.InOutCubic, Loop = true, LoopDelay = 280 }));

        _loaders.Add(Presto.Animate(Squares.Children, new() {
            ["Scale"] = .2, ["Opacity"] = .3,
            Delay = Presto.Stagger(80, new() { Grid = [5, 5], From = StaggerFrom.Center }),
            Duration = 560, Ease = Eases.InOutQuad, Loop = true, Alternate = true }));

        _loaders.Add(Blob.Animate(new() {
            ["Data"] = PrestoAvalonia.MorphTo(Splat), ["Rotate"] = 180,
            Duration = 1300, Ease = Eases.InOutSine, Loop = true, Alternate = true }));

        _loaders.Add(PrestoAvalonia.CreateDrawable(Ring).Animate(new() {
            ["Draw"] = TweenValue.Keyframes("0 0", "0 1", "1 1"),
            Duration = 1800, Ease = Eases.InOutQuad, Loop = true }));

        _loaders.Add(Ball.Animate(new() {
            Keyframes = new Keyframe[] {
                new() { ["Y"] = -56, ["ScaleX"] = .9, ["ScaleY"] = 1.1, Ease = Eases.OutQuad },
                new() { ["Y"] = 0, Ease = Eases.InQuad },
                new() { ["ScaleX"] = 1.4, ["ScaleY"] = .6, Duration = 90 },
                new() { ["ScaleX"] = 1, ["ScaleY"] = 1, Duration = 160 } },
            Duration = 1000, Loop = true }));

        _loaders.Add(Presto.Animate(Typing.Children, new() {
            ["Y"] = TweenValue.Keyframes(0, -7, 0),
            ["Opacity"] = TweenValue.Keyframes(.4, 1, .4),
            Delay = Presto.Stagger(150), Duration = 600,
            Ease = Eases.InOutSine, Loop = true, LoopDelay = 450 }));

        _loaders.Add(Presto.Animate(Ripples.Children, new() {
            ["Scale"] = TweenValue.FromTo(.15, 1), ["Opacity"] = TweenValue.FromTo(1, 0),
            Delay = Presto.Stagger(600), Duration = 1800, Ease = Eases.OutQuad, Loop = true }));

        _loaders.Add(Presto.CreateTimeline(new() { Loop = true, LoopDelay = 500 })
            .Add(Percent, new() {
                ["Text"] = new TweenParams { From = 0, To = 100, TextModifier = v => $"{v:0}%" },
                Duration = 2400, Ease = Eases.InOutQuad })
            .Add(Meter, new() {
                ["Value"] = TweenValue.FromTo(0, 100),
                Duration = 2400, Ease = Eases.InOutQuad }, "<<"));
    }

    private void SpeedChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        SpeedLabel.Text = $"{e.NewValue:0.00}×";
        foreach (var loader in _loaders) loader.Speed = e.NewValue;
    }

    private void PauseAllChanged(object? sender, RoutedEventArgs e)
    {
        foreach (var loader in _loaders)
        {
            if (PauseAll.IsChecked == true) loader.Pause();
            else loader.Resume();
        }
    }
}
