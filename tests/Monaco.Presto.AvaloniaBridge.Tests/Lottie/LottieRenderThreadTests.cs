using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Lottie;

namespace Monaco.Presto.AvaloniaBridge.Tests.Lottie;

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

    private static async Task<double> Moved(LottieView view)
    {
        Assert.True(await Pump(() => Left(view) >= 20), "render-thread playback never advanced");
        return Left(view);
    }

    private static async Task<double> Holds(LottieView view)
    {
        // A message sent from Render reaches the handler with the next frame's batch.
        Tick();
        Tick();
        var left = Left(view);
        await Task.Delay(100);
        Tick();
        Tick();
        Assert.Equal(left, Left(view));
        return left;
    }

    [AvaloniaFact]
    public async Task FramesAdvanceWithoutTheUIThreadEngine()
    {
        var view = Playing();

        var first = await Moved(view);
        Assert.True(await Pump(() => Left(view) != first), "render-thread playback stopped advancing");

        Assert.Equal(0, view.Frame);
    }

    [AvaloniaFact]
    public async Task StopHoldsTheFrame()
    {
        var view = Playing();
        await Moved(view);

        view.StopRenderThread();

        Assert.InRange(await Holds(view), 1, 80);
    }

    [AvaloniaFact]
    public async Task WritingFrameSeeksAndStopsPlayback()
    {
        var view = Playing();
        await Moved(view);

        view.Frame = 5;

        Assert.Equal(40, await Holds(view));
    }

    [AvaloniaFact]
    public async Task TogglingOffReturnsToFrameDrivenDrawing()
    {
        var view = Playing();
        await Moved(view);

        view.RenderThread = false;
        Assert.Equal(0, await Holds(view));

        view.Frame = 5;
        Tick();
        Assert.Equal(40, Left(view));
    }

    [AvaloniaFact]
    public async Task DetachingReleasesTheRenderThreadPlayer()
    {
        var before = LottiePlayerHandle.Live;
        var view = Playing();
        await Moved(view);
        Assert.Equal(before + 1, LottiePlayerHandle.Live);

        WindowOf(view).Content = null;
        Tick();
        Tick();

        Assert.Equal(before, LottiePlayerHandle.Live);
    }
}
