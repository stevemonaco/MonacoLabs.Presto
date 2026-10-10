namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Color RGB channels blend with premultiplied alpha as CSS Color 4 does, a deliberate deviation from anime.js 4.5.0's
/// straight alpha. Equal alphas keep upstream's straight pseudo-linear blend bit for bit.
/// </summary>
public sealed class PremultipliedColorTests : EngineTestBase
{
    private const string TransparentTeal = "rgba(45, 212, 191, 0)";
    private const string Gray = "rgba(58, 63, 75, 1)";

    private static (Animation Animation, Tween Tween) Fade(string from, string to, EaseValue? ease = null, Composition composition = Composition.Replace)
    {
        var target = new Dictionary<string, object?> { ["Color"] = from };
        var animation = Presto.Animate(target, new()
        {
            ["Color"] = TweenValue.FromTo(from, to),
            Duration = 100,
            Ease = ease ?? "linear",
            Composition = composition,
            Autoplay = false,
        });
        return (animation, TestHelpers.GetTweenAtIndex(animation, 0)!);
    }

    private static double Straight(double from, double to, double progress) => Math.Sqrt(from * from * (1 - progress) + to * to * progress);

    private static double Progress(Tween tween) => tween._ease(tween._currentTime / tween._updateDuration);

    [Theory]
    [InlineData(TransparentTeal, Gray, 58, 63, 75)]
    [InlineData("rgba(0, 0, 0, 0)", "rgb(255, 0, 0)", 255, 0, 0)]
    public void TransparentToOpaqueTakesTheOpaqueHueAtEveryMidFrame(string from, string to, double r, double g, double b)
    {
        var (animation, tween) = Fade(from, to);

        for (var time = 5; time < 100; time += 5)
        {
            animation.Seek(time);
            Assert.Equal(r, tween._numbers[0], 9);
            Assert.Equal(g, tween._numbers[1], 9);
            Assert.Equal(b, tween._numbers[2], 9);
            Assert.Equal(time / 100d, tween._numbers[3], 9);
        }
    }

    [Fact]
    public void OpaqueToTransparentKeepsTheStartRgbUntilTheEnd()
    {
        var (animation, tween) = Fade(Gray, TransparentTeal);

        for (var time = 5; time < 100; time += 5)
        {
            animation.Seek(time);
            Assert.Equal(58, tween._numbers[0], 9);
            Assert.Equal(63, tween._numbers[1], 9);
            Assert.Equal(75, tween._numbers[2], 9);
        }
        animation.Seek(100);
        Assert.Equal<double[]>([45, 212, 191, 0], tween._numbers);
    }

    [Theory]
    [InlineData("#f99", "#0ff")]
    [InlineData("rgba(255, 153, 153, .6)", "rgba(0, 255, 255, .6)")]
    public void EqualAlphasMatchTheStraightBlendBitForBit(string from, string to)
    {
        var (animation, tween) = Fade(from, to, "inOutQuad");

        for (var time = 0; time <= 100; time += 5)
        {
            animation.Seek(time);
            var progress = Progress(tween);
            for (var i = 0; i < 3; i++) Assert.Equal(Straight(tween._fromNumbers[i], tween._toNumbers[i], progress), tween._numbers[i]);
        }
    }

    [Fact]
    public void EndsAreExactToTheBit()
    {
        var (animation, tween) = Fade("rgba(45, 212, 191, .3)", "rgba(58, 63, 75, .9)", "outQuad");

        animation.Seek(0);
        Assert.Equal<double[]>([45, 212, 191, .3], tween._numbers);
        animation.Seek(100);
        Assert.Equal<double[]>([58, 63, 75, .9], tween._numbers);
    }

    [Theory]
    [InlineData("rgba(255, 0, 0, 0)", "rgba(0, 0, 255, 0)", "outBack(3)")]
    [InlineData("rgb(255, 255, 255)", "rgba(0, 0, 0, 0)", "outBack(3)")]
    [InlineData("rgba(0, 0, 0, 0)", "rgb(255, 255, 255)", "inBack(3)")]
    [InlineData("rgb(255, 255, 255)", "rgb(0, 0, 0)", "outBack(3)")]
    public void TransparentPairsAndOvershootingEasesNeverProduceNaN(string from, string to, string ease)
    {
        var (animation, tween) = Fade(from, to, ease);

        for (var time = 0; time <= 100; time++)
        {
            animation.Seek(time);
            Assert.All(tween._numbers, n => Assert.True(double.IsFinite(n), $"{n} at {time}ms"));
        }
    }

    [Fact]
    public void BlendedColorTweensKeepTheStraightBlend()
    {
        var (animation, tween) = Fade("rgba(45, 212, 191, .2)", Gray, composition: Composition.Blend);
        Assert.NotEqual(tween._fromNumbers[3], tween._toNumbers[3]);

        animation.Seek(40);
        var progress = Progress(tween);
        for (var i = 0; i < 3; i++) Assert.Equal(Straight(tween._fromNumbers[i], tween._toNumbers[i], progress), tween._numbers[i]);
    }

    [Fact]
    public void RetargetingAChannelFadeBetweenAlphasCarriesOnWithoutAJump()
    {
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new()
        {
            ["BackgroundColor"] = new(),
            Ease = new Spring(stiffness: 120, damping: 12),
            PreserveVelocity = true,
        });

        animatable.Set("BackgroundColor", [45, 212, 191, .2]);
        Frames.Advance(96, 16);
        var tween = animatable.Animations["BackgroundColor"]._headTween!;
        var (valuesBefore, velocitiesBefore) = Displayed(tween);
        animatable.Set("BackgroundColor", [58, 63, 75, 1]);
        var (valuesAfter, velocitiesAfter) = Displayed(tween);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(valuesBefore[i], valuesAfter[i], 1e-6);
            Assert.True(Math.Abs(velocitiesBefore[i]) > 1e-3, $"channel {i} moving at {velocitiesBefore[i]} per ms before the retarget");
            Assert.Equal(velocitiesBefore[i], velocitiesAfter[i], Math.Abs(velocitiesBefore[i]) * .02);
        }
    }

    /// <summary>Each channel's displayed value and its rate per ms at the tween's current time, blended as the renderer blends it.</summary>
    private static (double[] Values, double[] Velocities) Displayed(Tween tween)
    {
        const double step = 1e-4;
        var progress = tween._currentTime / tween._updateDuration;
        var before = Math.Max(0, progress - step);
        var after = Math.Min(1, progress + step);
        var values = new double[4];
        var velocities = new double[4];
        for (var i = 0; i < 4; i++)
        {
            values[i] = Channel(tween, i, progress);
            velocities[i] = (Channel(tween, i, after) - Channel(tween, i, before)) / ((after - before) * tween._updateDuration);
        }
        return (values, velocities);
    }

    private static double Channel(Tween tween, int i, double time)
    {
        var ease = tween._channelEases?[i] ?? tween._ease;
        if (ease.Target is Spring spring && ease == spring.Ease) ease = spring.PureEase;
        var progress = ease(time);
        var (from, to, fromAlpha, toAlpha) = (tween._fromNumbers[i], tween._toNumbers[i], tween._fromNumbers[3], tween._toNumbers[3]);
        if (i == 3) return from + (to - from) * progress;
        if (fromAlpha == toAlpha) return Straight(from, to, progress);
        var alpha = fromAlpha + (toAlpha - fromAlpha) * progress;
        return Math.Sqrt((fromAlpha * from * from * (1 - progress) + toAlpha * to * to * progress) / alpha);
    }
}
