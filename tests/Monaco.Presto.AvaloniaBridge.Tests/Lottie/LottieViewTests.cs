using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Events;
using Monaco.Presto.AvaloniaBridge.Lottie;

namespace Monaco.Presto.AvaloniaBridge.Tests.Lottie;

/// <summary>A <see cref="LottieView"/> drawing the composition frame its <see cref="LottieView.Frame"/> or <see cref="LottieView.Progress"/> selects.</summary>
public sealed class LottieViewTests : CompositorTestBase
{
    // A red 20x100 bar on a 100x100 composition, its left edge moving linearly from 0 at the in point to 80 at the out point.
    internal static LottieComposition Bar(double inPoint = 0) => LottieComposition.FromJson($$$"""
        {"v":"5.7.0","fr":10,"ip":{{{inPoint}}},"op":{{{inPoint + 10}}},"w":100,"h":100,"layers":[
        {"ty":4,"ind":1,"ip":{{{inPoint}}},"op":{{{inPoint + 20}}},"st":0,
        "ks":{"o":{"a":0,"k":100},"r":{"a":0,"k":0},"p":{"a":0,"k":[0,0]},"a":{"a":0,"k":[0,0]},"s":{"a":0,"k":[100,100]}},
        "shapes":[
        {"ty":"rc","d":1,"s":{"a":0,"k":[20,100]},"r":{"a":0,"k":0},"p":{"a":1,"k":[
        {"t":{{{inPoint}}},"s":[10,50],"o":{"x":[0],"y":[0]},"i":{"x":[1],"y":[1]}},{"t":{{{inPoint + 10}}},"s":[90,50]}]}},
        {"ty":"fl","c":{"a":0,"k":[1,0,0,1]},"o":{"a":0,"k":100}}]}]}
        """);

    internal static LottieView Place(LottieView view, double width = 100, double height = 100)
    {
        view.Width = width;
        view.Height = height;
        view.HorizontalAlignment = HorizontalAlignment.Left;
        view.VerticalAlignment = VerticalAlignment.Top;
        new Window { Content = view, Background = Brushes.Black, Width = 100, Height = 100 }.Show();
        Tick();
        return view;
    }

    private static LottieView Shown(double frame = 0)
    {
        var view = Place(new LottieView { Composition = Bar() });
        view.Frame = frame;
        Tick();
        return view;
    }

    private static void AssertBarAt(Visual view, int left)
    {
        var row = Row(view, 50);
        if (left > 0) Assert.Equal(Colors.Black, row[left - 1]);
        Assert.Equal(Colors.Red, row[left]);
        Assert.Equal(Colors.Red, row[left + 19]);
        if (left < 80) Assert.Equal(Colors.Black, row[left + 20]);
    }

    [AvaloniaFact]
    public void FractionalFramesAreDrawnAsGivenNotFloored()
    {
        var view = Shown();
        AssertBarAt(view, 0);

        view.Frame = 5;
        Tick();
        AssertBarAt(view, 40);

        view.Frame = 5.5;
        Tick();
        AssertBarAt(view, 44);

        view.Frame = 10;
        Tick();
        Assert.Equal(Colors.Black, Row(view, 50)[78]);
        Assert.Equal(Colors.Red, Row(view, 50)[85]);
    }

    [AvaloniaFact]
    public void FrameAndProgressFollowEachOther()
    {
        var view = new LottieView { Composition = Bar() };

        view.Frame = 2.5;
        Assert.Equal(0.25, view.Progress);

        view.Progress = 0.75;
        Assert.Equal(7.5, view.Frame);
    }

    [AvaloniaFact]
    public void FrameAndProgressClampToTheComposition()
    {
        var view = new LottieView { Composition = Bar() };

        view.Frame = -3;
        Assert.Equal((0d, 0d), (view.Frame, view.Progress));

        view.Frame = 15;
        Assert.Equal((10d, 1d), (view.Frame, view.Progress));

        view.Progress = -1;
        Assert.Equal((0d, 0d), (view.Frame, view.Progress));
    }

    [AvaloniaFact]
    public void ChangingTheCompositionResetsFrameToItsInPointAndSeeksFromIt()
    {
        var view = Shown(7);

        view.Composition = Bar(inPoint: 5);
        Tick();

        Assert.Equal((5d, 0d), (view.Frame, view.Progress));
        AssertBarAt(view, 0);

        view.Frame = 10;
        Tick();
        AssertBarAt(view, 40);
    }

    [AvaloniaTheory]
    [InlineData(Stretch.Uniform, 10)]
    [InlineData(Stretch.Fill, 20)]
    [InlineData(Stretch.UniformToFill, 20)]
    public void StretchScalesTheComposition(Stretch stretch, int barWidth)
    {
        var view = Place(new LottieView { Composition = Bar(), Stretch = stretch }, height: 50);
        var row = Row(view, 25);

        Assert.Equal(Colors.Red, row[0]);
        Assert.Equal(Colors.Red, row[barWidth - 1]);
        Assert.Equal(Colors.Black, row[barWidth]);
        Assert.Equal(Colors.Black, Row(view, 50)[0]);
    }

    [AvaloniaFact]
    public void GrayscaleAppliesToTheLottieOutput()
    {
        var view = Place(new LottieView { Composition = Bar(), Grayscale = 1 });

        AssertNear(Color.FromRgb(54, 54, 54), Row(view, 50)[10]);
        Assert.Equal(Colors.Black, Row(view, 50)[30]);
    }

    [AvaloniaFact]
    public void TintAppliesToTheLottieOutput()
    {
        var view = Place(new LottieView { Composition = Bar(), Tint = 1, TintColor = Colors.Blue });

        AssertNear(Color.FromRgb(0, 0, 54), Row(view, 50)[10]);
    }

    [AvaloniaFact]
    public void AnAnimationSeeksPlaysAndReversesTheFrame()
    {
        var view = Shown();
        var animation = Presto.Animate(view, new() { ["Frame"] = 10, Duration = 100, Ease = "linear", Autoplay = false });

        animation.Seek(55);
        Tick();
        Assert.Equal(5.5, view.Frame, 6);
        AssertBarAt(view, 44);

        animation.Play();
        Frames.Advance(100, 10);
        Tick();
        Assert.Equal(10, view.Frame);
        Assert.Equal(Colors.Red, Row(view, 50)[85]);

        animation.Reverse();
        Frames.Advance(200, 10);
        Tick();
        Assert.Equal(0, view.Frame);
        AssertBarAt(view, 0);
    }

    [AvaloniaFact]
    public void ATimelineChildDrivesTheFrame()
    {
        var view = Shown();
        var timeline = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(view, new AnimationParams { ["Frame"] = 10, Duration = 100, Ease = "linear" }, 50);

        timeline.Seek(100);
        Tick();

        Assert.Equal(5, view.Frame, 6);
        AssertBarAt(view, 40);
    }

    [AvaloniaFact]
    public void AScrollSyncedAnimationDrivesTheFrame()
    {
        var view = new LottieView { Composition = Bar(), Width = 100, Height = 100, HorizontalAlignment = HorizontalAlignment.Left };
        // The view enters at offset 0 and leaves at 200, so 100 is halfway.
        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = new StackPanel { Children = { new Border { Height = 100 }, view, new Border { Height = 100 } } },
        };
        new Window { Content = scroller, Background = Brushes.Black, Width = 100, Height = 100 }.Show();
        Tick();
        var animation = Presto.Animate(view, new() { ["Frame"] = 10, Duration = 100, Ease = "linear", Autoplay = false });
        PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = view, Sync = true }).Link(animation);
        Frames.Advance(16);

        scroller.Offset = new Vector(0, 100);
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(40, 20);
        Tick();
        Tick();

        Assert.Equal((5d, 0.5), (Math.Round(view.Frame, 6), Math.Round(view.Progress, 6)));
        AssertBarAt(view, 40);
    }

    [AvaloniaFact]
    public void AnExitGhostSnapshotsTheFrameWithoutKeepingAPlayer()
    {
        var before = LottiePlayerHandle.Live;
        var view = Shown(5);
        var window = WindowOf(view);

        Assert.NotNull(Presence.Ghost(view, window, default, HeldMove.Opacity(from: 1)));
        window.Content = null;
        Tick();
        Tick();

        AssertBarAt(window, 40);
        Assert.Equal(before, LottiePlayerHandle.Live);
    }

    [AvaloniaFact]
    public void AMotionValueDrivesTheFrame()
    {
        var view = Shown();
        var source = Presto.CreateMotionValue();
        Follow.Bind(view, LottieView.FrameProperty, source);

        source.Set(5);
        Frames.Advance(16);
        Tick();

        Assert.Equal((5d, 0.5), (view.Frame, view.Progress));
        AssertBarAt(view, 40);
    }

    [AvaloniaFact]
    public void DetachingReleasesTheOnlyPlayerTheViewEverHeld()
    {
        var before = LottiePlayerHandle.Live;
        var view = Shown();
        var window = WindowOf(view);

        for (var i = 0; i < 100; i++)
        {
            view.Frame = i % 10 + 0.5;
            Tick();
        }
        Assert.Equal(before + 1, LottiePlayerHandle.Live);

        window.Content = null;
        Tick();
        Tick();

        Assert.Equal(before, LottiePlayerHandle.Live);
    }
}
