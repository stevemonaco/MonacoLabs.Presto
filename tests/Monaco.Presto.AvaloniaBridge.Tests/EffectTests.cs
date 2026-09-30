using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The effect shorthands: <c>Blur</c> and the <c>Shadow*</c> channels over one bridge-owned effect (MOTION-KIT-PLAN §3.4).</summary>
public sealed class EffectTests : CompositorTestBase
{
    [AvaloniaFact]
    public void BlurAnimatesTheRadius()
    {
        var border = new Border();

        Seeked(border, new() { ["Blur"] = 8 }, 0.5);

        Assert.Equal(4, Assert.IsType<BlurEffect>(border.Effect).Radius, 6);
    }

    [AvaloniaFact]
    public void BlurStartsFromTheUsersBlurRadius()
    {
        var own = new BlurEffect { Radius = 10 };
        var border = new Border { Effect = own };

        Seeked(border, new() { ["Blur"] = 20 }, 0.5);

        Assert.Equal(15, Assert.IsType<BlurEffect>(border.Effect).Radius, 6);
        Assert.Equal(10, own.Radius);
    }

    [AvaloniaTheory]
    [InlineData("Blur")]
    [InlineData("ShadowX")]
    public void RevertRestoresTheUsersOwnEffect(string shorthand)
    {
        IEffect own = shorthand == "Blur" ? new BlurEffect { Radius = 2 } : new DropShadowEffect { OffsetX = 2 };
        var border = new Border { Effect = own };

        var animation = Seeked(border, new() { [shorthand] = 12 });
        Assert.NotSame(own, border.Effect);

        animation.Revert();

        Assert.Same(own, border.Effect);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlurBackToZeroRestoresTheUsersEffectOrNone(bool withOwnEffect)
    {
        var own = withOwnEffect ? new DropShadowEffect() : null;
        var border = new Border { Effect = own };
        Seeked(border, new() { ["Blur"] = 8 });

        Seeked(border, new() { ["Blur"] = 0 });

        Assert.Same(own, border.Effect);
    }

    [AvaloniaFact]
    public void AShadowFadedOutIsRemoved()
    {
        var border = new Border();
        Seeked(border, new() { ["ShadowBlur"] = 8 });
        Assert.Equal(1, Assert.IsType<DropShadowEffect>(border.Effect).Opacity);

        var fading = Seeked(border, new() { ["ShadowOpacity"] = 0 }, 0.99);
        Assert.IsType<DropShadowEffect>(border.Effect);
        fading.Seek(fading.Duration);

        Assert.Null(border.Effect);
    }

    [AvaloniaFact]
    public void TheShadowShorthandsShareOneDropShadowEffect()
    {
        var border = new Border();
        var parameters = new AnimationParams
        {
            ["ShadowX"] = 4, ["ShadowY"] = 6, ["ShadowBlur"] = 10, ["ShadowColor"] = "#ff0000", ["ShadowOpacity"] = 0.5,
        };

        var animation = Seeked(border, parameters, 0.5);
        var shadow = Assert.IsType<DropShadowEffect>(border.Effect);
        animation.Seek(animation.Duration);

        Assert.Same(shadow, border.Effect);
        Assert.Equal(4, shadow.OffsetX, 6);
        Assert.Equal(6, shadow.OffsetY, 6);
        Assert.Equal(10, shadow.BlurRadius, 6);
        Assert.Equal(Colors.Red, shadow.Color);
        Assert.Equal(0.5, shadow.Opacity, 6);
    }

    [AvaloniaFact]
    public void ShadowColorTakesAColor()
    {
        var border = new Border();

        Seeked(border, new() { ["ShadowColor"] = TweenValue.Of(Colors.Blue) });

        Assert.Equal(Colors.Blue, Assert.IsType<DropShadowEffect>(border.Effect).Color);
    }

    [AvaloniaFact]
    public void ShadowOpacityIsClamped()
    {
        var border = new Border();

        Seeked(border, new() { ["ShadowOpacity"] = 3 }, 0.5);

        Assert.Equal(1, Assert.IsType<DropShadowEffect>(border.Effect).Opacity);
    }

    [AvaloniaFact]
    public void BlurAndShadowInOneAnimationThrow()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new Border().Animate(new() { ["Blur"] = 4, ["ShadowX"] = 2 }));

        Assert.Contains("Blur", error.Message);
        Assert.Contains("ShadowX", error.Message);
    }

    [AvaloniaFact]
    public void AShadowWhileABlurRunsThrows()
    {
        var border = new Border();
        border.Animate(new() { ["Blur"] = 4, Duration = 100 });
        Frames.Advance(16);

        var error = Assert.Throws<InvalidOperationException>(() => border.Animate(new() { ["ShadowBlur"] = 2, Duration = 100 }));

        Assert.Contains("Blur", error.Message);
        Assert.Contains("ShadowBlur", error.Message);
    }

    [AvaloniaFact]
    public void BlurThenShadowSwitchesKind()
    {
        var border = new Border();
        border.Animate(new() { ["Blur"] = 4, Duration = 100 });
        Frames.Advance(200, 16);
        Assert.IsType<BlurEffect>(border.Effect);

        border.Animate(new() { ["ShadowX"] = 5, Duration = 100 });
        Frames.Advance(200, 16);

        Assert.Equal(5, Assert.IsType<DropShadowEffect>(border.Effect).OffsetX);
    }

    [AvaloniaFact]
    public void AHoverBlurReturnsToRestAndRemovesTheEffect()
    {
        var box = Box();
        var window = Show(box);
        box.WhileHover(b => b.Property("Blur", 6).Duration(100).Ease(Eases.None));

        window.MouseMove(new Point(25, 25));
        Frames.Advance(150, 10);
        Assert.Equal(6, Assert.IsType<BlurEffect>(box.Effect).Radius, 6);

        window.MouseMove(new Point(150, 150));
        Frames.Advance(150, 10);

        Assert.Null(box.Effect);
        Assert.Empty(Presto.GetAnimations(box));
    }

    [AvaloniaFact]
    public void ABlurRendersAndBackAtZeroMatchesTheOriginal()
    {
        var bar = ShowBar();
        var original = Capture(bar);

        Seeked(bar, new() { ["Blur"] = 8 });
        Tick();
        var blurred = Capture(bar);
        Seeked(bar, new() { ["Blur"] = 0 });
        Tick();

        Assert.True(Difference(original, blurred) > 0);
        Assert.Equal(0, Difference(original, Capture(bar)));
    }
}
