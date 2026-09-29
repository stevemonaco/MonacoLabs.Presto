using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>What the reveals leave on screen, frame by frame: the clip and mask are mutated in place, so each frame must reach the renderer.</summary>
public sealed class RevealRenderTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData("ClipInset")]
    [InlineData("ClipCircle")]
    [InlineData("MaskSweep")]
    public void EachFrameOfARevealReachesTheScreen(string shorthand)
    {
        var bar = ShowBar();
        var animation = Seeked(bar, new() { [shorthand] = shorthand == "ClipInset" ? TweenValue.FromTo("50%", "0%") : TweenValue.FromTo(0, 1) }, 0.25);
        Tick();
        var quarter = Extent(bar, Colors.Red);

        animation.Seek(animation.Duration * 0.75);
        Tick();
        var threeQuarters = Extent(bar, Colors.Red);

        Assert.NotNull(threeQuarters);
        Assert.True(quarter is null || quarter.Value.Width < threeQuarters.Value.Width, $"{quarter} then {threeQuarters}");
    }
}
