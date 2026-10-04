using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge.Lottie;

namespace Monaco.Presto.AvaloniaBridge.Tests.Lottie;

/// <summary>Marker segments of a <see cref="LottieComposition"/> and the <see cref="LottieView"/> verbs that play them.</summary>
public sealed class LottieSegmentTests : BridgeTestBase
{
    // 10 fps over frames 0..60: "intro" 0-20, "idle" names frame 20 and runs to "outro" at 40, which runs to the out point.
    private const string Markers = """
        ,"markers":[{"tm":0,"cm":"intro","dr":20},{"tm":40,"cm":"{\"name\":\"outro\"}","dr":0},{"tm":20,"cm":"idle","dr":0}]
        """;

    internal static LottieComposition Composition(double frameRate = 10, double outPoint = 60, string markers = Markers) =>
        LottieComposition.FromJson($$"""{"v":"5.7.0","fr":{{frameRate}},"ip":0,"op":{{outPoint}},"w":100,"h":100,"layers":[]{{markers}}}""");

    private static LottieView View() => new() { Composition = Composition() };

    [Fact]
    public void MarkersBecomeSegmentsAndZeroDurationMarkersRunToTheNextMarkerOrTheOutPoint() =>
        Assert.Equal(
            [new LottieSegment("intro", 0, 20), new LottieSegment("outro", 40, 60), new LottieSegment("idle", 20, 40)],
            Composition().Segments);

    [Fact]
    public void MissingMarkersGiveNoSegments() => Assert.Empty(Composition(markers: "").Segments);

    [Fact]
    public void TryGetSegmentIgnoresCase()
    {
        var composition = Composition();

        Assert.True(composition.TryGetSegment("OUTRO", out var outro));
        Assert.Equal(new LottieSegment("outro", 40, 60), outro);
        Assert.False(composition.TryGetSegment("missing", out _));
    }

    [AvaloniaFact]
    public void PlayRunsTheWholeCompositionAtItsFrameRate()
    {
        var view = View();

        var animation = view.Play();
        Assert.Equal(6000, animation.Duration, 6);

        Frames.Advance(3000, 10);
        Assert.Equal(30, view.Frame, tolerance: 0.5);
        Frames.Advance(3100, 10);
        Assert.Equal(60, view.Frame);
        Assert.True(animation.Completed);
    }

    [AvaloniaFact]
    public void PlayAMarkerRunsItsSegmentAndSpeedScalesTheDuration()
    {
        var view = View();

        var animation = view.Play("Idle", speed: 2);
        Assert.Equal(1000, animation.Duration, 6);

        Frames.Advance(500, 10);
        Assert.Equal(30, view.Frame, tolerance: 0.5);
        Frames.Advance(600, 10);
        Assert.Equal(40, view.Frame);
    }

    [AvaloniaFact]
    public void ReversePlaysTheSegmentFromItsEnd()
    {
        var view = View();

        view.Play("intro", reverse: true);
        Frames.Advance(16);
        Assert.True(view.Frame is > 19 and < 20, $"{view.Frame}");

        Frames.Advance(2100, 10);
        Assert.Equal(0, view.Frame);
    }

    [AvaloniaFact]
    public void LoopRestartsTheSegment()
    {
        var view = View();

        var animation = view.Play("intro", loop: true);
        Frames.Advance(2500, 10);

        Assert.False(animation.Completed);
        Assert.Equal(5, view.Frame, tolerance: 0.5);
    }

    [AvaloniaFact]
    public void PlaySegmentWithToBelowFromPlaysBackwards()
    {
        var view = View();

        var animation = view.PlaySegment(50, 10);
        Assert.Equal(4000, animation.Duration, 6);

        Frames.Advance(1000, 10);
        Assert.Equal(40, view.Frame, tolerance: 0.5);
        Frames.Advance(3100, 10);
        Assert.Equal(10, view.Frame);
    }

    [AvaloniaFact]
    public void StartingAnotherPlayReplacesTheFirst()
    {
        var view = View();
        var first = view.Play(loop: true);

        view.Play("outro");
        Frames.Advance(2100, 10);

        Assert.True(first.Cancelled);
        Assert.Equal(60, view.Frame);
    }

    [AvaloniaFact]
    public void AnUnknownMarkerThrowsNamingTheMarkers()
    {
        var error = Assert.Throws<ArgumentException>(() => View().Play("missing"));

        Assert.Contains("intro, outro, idle", error.Message);
    }
}
