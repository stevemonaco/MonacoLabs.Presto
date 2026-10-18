using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MonacoLabs.Presto.Lottie;

namespace MonacoLabs.Presto.Tests.Lottie;

/// <summary>A <see cref="LottieView"/> with <see cref="LottieView.RenderThread"/> set, played by the render loop rather than by the UI-thread engine.</summary>
public sealed class LottieRenderThreadTests : CompositorTestBase
{
    private static LottieView Playing()
    {
        var view = new LottieView { Composition = LottieViewTests.Bar(), RenderThread = true };
        view.PlayOnRenderThread();
        return LottieViewTests.Place(view);
    }

    private static double Left(LottieView view) => Extent(view, Colors.Red)?.Left ?? -1;

    private static double Moved(LottieView view)
    {
        Assert.True(Pump(() => Left(view) >= 20), "render-thread playback never advanced");
        return Left(view);
    }

    private static double Holds(LottieView view)
    {
        // A message sent from Render reaches the handler with the next frame's batch.
        Tick();
        Tick();
        var left = Left(view);
        TickFor(100);
        Tick();
        Tick();
        Assert.Equal(left, Left(view));
        return left;
    }

    [AvaloniaFact]
    public void FramesAdvanceWithoutTheUIThreadEngine()
    {
        var view = Playing();

        var first = Moved(view);
        Assert.True(Pump(() => Left(view) != first), "render-thread playback stopped advancing");

        Assert.Equal(0, view.Frame);
    }

    [AvaloniaFact]
    public void StopHoldsTheFrame()
    {
        var view = Playing();
        Moved(view);

        view.StopRenderThread();

        Assert.InRange(Holds(view), 1, 80);
    }

    [AvaloniaFact]
    public void WritingFrameSeeksAndStopsPlayback()
    {
        var view = Playing();
        Moved(view);

        view.Frame = 5;

        Assert.Equal(40, Holds(view));
    }

    [AvaloniaFact]
    public void TogglingOffReturnsToFrameDrivenDrawing()
    {
        var view = Playing();
        Moved(view);

        view.RenderThread = false;
        Assert.Equal(0, Holds(view));

        view.Frame = 5;
        Tick();
        Assert.Equal(40, Left(view));
    }

    [AvaloniaFact]
    public void TheKeptRequestStartsWhenRenderThreadTurnsOnAndAgainOnReattach()
    {
        var view = new LottieView { Composition = LottieViewTests.Bar() };
        view.PlayOnRenderThread();
        LottieViewTests.Place(view);
        Assert.Equal(0, Holds(view));

        view.RenderThread = true;
        Moved(view);

        var window = WindowOf(view);
        window.Content = null;
        Tick();
        window.Content = view;
        Tick();
        var first = Left(view);
        Assert.True(Pump(() => Left(view) != first), "render-thread playback did not restart on reattach");
    }

    [AvaloniaTheory]
    [InlineData("Frame", 40)]
    [InlineData("Progress", 40)]
    [InlineData("Composition", 0)]
    public void WritingFrameProgressOrCompositionDropsARequestNotYetStarted(string property, double left)
    {
        var view = new LottieView { Composition = LottieViewTests.Bar(), RenderThread = true };
        view.PlayOnRenderThread();

        if (property == "Frame") view.Frame = 5;
        else if (property == "Progress") view.Progress = 0.5;
        else view.Composition = LottieViewTests.Bar();
        LottieViewTests.Place(view);

        Assert.Equal(left, Holds(view));
    }

    [AvaloniaFact]
    public void DetachingReleasesTheRenderThreadPlayer()
    {
        var before = LottiePlayerHandle.Live;
        var view = Playing();
        Moved(view);
        Assert.Equal(before + 1, LottiePlayerHandle.Live);

        WindowOf(view).Content = null;
        Tick();
        Tick();

        Assert.Equal(before, LottiePlayerHandle.Live);
    }
}
