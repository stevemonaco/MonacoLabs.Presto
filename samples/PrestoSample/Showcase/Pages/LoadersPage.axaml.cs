using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

/// <summary>Nine loading indicators, each shown with the UI-thread call that builds it; the <c>*Code</c> strings must match those calls.</summary>
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
    private readonly List<CompositorAnimation> _onCompositor = [];

    public LoadersPage()
    {
        InitializeComponent();
        for (var i = 0; i < 25; i++) Squares.Children.Add(new Border());
    }

    /// <summary>The loaders running on the UI thread: all nine, or the three with no composition property while <c>RenderThread</c> is on.</summary>
    public IReadOnlyList<Timer> Loaders => _loaders;

    /// <summary>The six loaders <c>RenderThread</c> moves onto the compositor; empty while it is off.</summary>
    public IReadOnlyList<CompositorAnimation> CompositorLoaders => _onCompositor;

    protected override void OnAttached() => Scope.Add(_ => Build());

    private void Build()
    {
        _loaders.Clear();
        _onCompositor.Clear();
        if (RenderThread.IsChecked == true)
        {
            _onCompositor.Add(PrestoAvalonia.AnimateOnCompositor(Bars.Children, new() {
                ["ScaleY"] = TweenValue.FromTo(.2, 1),
                Delay = Presto.Stagger(90), Duration = 480,
                Ease = Eases.InOutSine, Loop = true, Alternate = true }));

            _onCompositor.Add(PrestoAvalonia.AnimateOnCompositor(Orbit.Children, new() {
                ["Rotate"] = 360,
                Delay = Presto.Stagger(70), Duration = 1400,
                Ease = Eases.InOutCubic, Loop = true, LoopDelay = 280 }));

            _onCompositor.Add(PrestoAvalonia.AnimateOnCompositor(Squares.Children, new() {
                ["Scale"] = .2, ["Opacity"] = .3,
                Delay = Presto.Stagger(80, new() { Grid = [5, 5], From = StaggerFrom.Center }),
                Duration = 560, Ease = Eases.InOutQuad, Loop = true, Alternate = true }));

            _onCompositor.Add(Ball.AnimateOnCompositor(new() {
                ["Y"] = new TweenParams[] { new() { To = -56, Ease = Eases.OutQuad }, new() { To = 0, Ease = Eases.InQuad }, new() { Duration = 90 }, new() { Duration = 160 } },
                ["ScaleX"] = new TweenParams[] { new() { To = .9, Ease = Eases.OutQuad }, new(), new() { To = 1.4, Duration = 90 }, new() { To = 1, Duration = 160 } },
                ["ScaleY"] = new TweenParams[] { new() { To = 1.1, Ease = Eases.OutQuad }, new(), new() { To = .6, Duration = 90 }, new() { To = 1, Duration = 160 } },
                Duration = 1000, Loop = true }));

            _onCompositor.Add(PrestoAvalonia.AnimateOnCompositor(Typing.Children, new() {
                ["Y"] = TweenValue.Keyframes(0, -7, 0),
                ["Opacity"] = TweenValue.Keyframes(.4, 1, .4),
                Delay = Presto.Stagger(150), Duration = 600,
                Ease = Eases.InOutSine, Loop = true, LoopDelay = 450 }));

            _onCompositor.Add(PrestoAvalonia.AnimateOnCompositor(Ripples.Children, new() {
                ["Scale"] = TweenValue.FromTo(.15, 1), ["Opacity"] = TweenValue.FromTo(1, 0),
                Delay = Presto.Stagger(600), Duration = 1800, Ease = Eases.OutQuad, Loop = true }));
        }
        else
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
        }

        _loaders.Add(Blob.Animate(new() {
            ["Data"] = PrestoAvalonia.MorphTo(Splat), ["Rotate"] = 180,
            Duration = 1300, Ease = Eases.InOutSine, Loop = true, Alternate = true }));

        _loaders.Add(PrestoAvalonia.CreateDrawable(Ring).Animate(new() {
            ["Draw"] = TweenValue.Keyframes("0 0", "0 1", "1 1"),
            Duration = 1800, Ease = Eases.InOutQuad, Loop = true }));

        _loaders.Add(Presto.CreateTimeline(new() { Loop = true, LoopDelay = 500 })
            .Add(Percent, new() {
                ["Text"] = new TweenParams { From = 0, To = 100, TextModifier = v => $"{v:0}%" },
                Duration = 2400, Ease = Eases.InOutQuad })
            .Add(Meter, new() {
                ["Value"] = TweenValue.FromTo(0, 100),
                Duration = 2400, Ease = Eases.InOutQuad }, "<<"));

        Retune();
    }

    private void SpeedChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        SpeedLabel.Text = $"{e.NewValue:0.00}×";
        Retune();
    }

    private void PauseAllChanged(object? sender, RoutedEventArgs e) => Retune();

    private void Retune()
    {
        var paused = PauseAll.IsChecked == true;
        foreach (var loader in _loaders)
        {
            loader.Speed = Speed.Value;
            if (paused) loader.Pause();
            else loader.Resume();
        }
        foreach (var loader in _onCompositor)
        {
            // Setting the speed recompiles every composition animation, even to the same value.
            if (loader.Speed != Speed.Value) loader.Speed = Speed.Value;
            if (paused) loader.Pause();
            else loader.Resume();
        }
    }

    private void RenderThreadChanged(object? sender, RoutedEventArgs e) => Scope.Refresh();

    // Posted below Render priority so the button paints its pressed state before the dispatcher stops answering.
    private void Freeze(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() => Thread.Sleep(2000), DispatcherPriority.Background);
}