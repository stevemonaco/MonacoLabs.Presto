using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Magnetic"/>: the pull towards the pointer through the followed <c>"X"</c>/<c>"Y"</c> shorthands.</summary>
public sealed class MagneticTests : BridgeTestBase
{
    // The box spans 0 to 50 on both axes; at the defaults the pull is offset × 0.35 × (1 − edge distance ÷ 48).
    [AvaloniaFact]
    public void ThePullGrowsAsThePointerNearsAndIsCappedByTheStrength()
    {
        var box = Box();
        Magnetic.SetEnabled(box, true);
        var window = Show(box);

        window.MouseMove(new Point(150, 25));
        Frames.Advance(1000, 16);
        Assert.Equal(0, X(box));

        window.MouseMove(new Point(90, 25));
        Frames.Advance(1000, 16);
        Assert.Equal(65 * .35 * 8 / 48, X(box), 6);

        window.MouseMove(new Point(60, 40));
        Frames.Advance(1000, 16);
        Assert.Equal(35 * .35 * 38 / 48, X(box), 6);
        Assert.Equal(15 * .35 * 38 / 48, Y(box), 6);

        window.MouseMove(new Point(50, 25));
        Frames.Advance(1000, 16);
        Assert.Equal(25 * .35, X(box), 6);
        Assert.Equal(0, Y(box), 6);
    }

    [AvaloniaFact]
    public void LeavingSpringsBackToRest()
    {
        var box = Box();
        Magnetic.SetEnabled(box, true);
        var window = Show(box);
        window.MouseMove(new Point(60, 40));
        Frames.Advance(1000, 16);

        window.MouseMove(new Point(150, 150));
        Frames.Advance(48, 16);
        Assert.InRange(X(box), .1, 9);
        Frames.Advance(1000, 16);

        Assert.Equal(0, X(box), 6);
        Assert.Equal(0, Y(box), 6);
    }

    [AvaloniaFact]
    public void DisablingOrDetachingRestoresTheElementsOwnRenderTransform()
    {
        var own = new RotateTransform(5);
        var box = Box();
        box.RenderTransform = own;
        Magnetic.SetEnabled(box, true);
        var window = Show(box);
        window.MouseMove(new Point(60, 40));
        Frames.Advance(1000, 16);
        Assert.NotSame(own, box.RenderTransform);

        window.Content = null;
        Assert.Same(own, box.RenderTransform);

        window.Content = box;
        window.MouseMove(new Point(60, 30));
        Frames.Advance(1000, 16);
        Assert.NotSame(own, box.RenderTransform);

        Magnetic.SetEnabled(box, false);
        Assert.Same(own, box.RenderTransform);
        Assert.Null(Follow.GetX(box));
    }

    [AvaloniaFact]
    public void AnXAnimationLayersOverThePullAndRevertingItShowsThePull()
    {
        var box = Box();
        Magnetic.SetEnabled(box, true);
        var window = Show(box);
        window.MouseMove(new Point(50, 25));
        Frames.Advance(1000, 16);

        var animation = box.Animate(new() { ["X"] = 100, Duration = 100, Ease = "linear" });
        Frames.Advance(200, 16);
        Assert.Equal(100, X(box));

        animation.Revert();
        Assert.Equal(25 * .35, X(box), 6);
    }

    [AvaloniaFact]
    public void ReducedMotionStaysAtRest()
    {
        var box = Box();
        Magnetic.SetEnabled(box, true);
        var window = Show(box);
        Presto.Defaults.ReducedMotion = true;

        window.MouseMove(new Point(60, 40));
        Frames.Advance(1000, 16);

        Assert.Equal(0, X(box));
        Assert.Equal(0, Y(box));
    }

    [AvaloniaFact]
    public void AStrengthChangeAppliesFromTheNextMoveAndARadiusChangeAtOnce()
    {
        var box = Box();
        Magnetic.SetEnabled(box, true);
        var window = Show(box);
        window.MouseMove(new Point(50, 25));
        Frames.Advance(1000, 16);

        Magnetic.SetStrength(box, .7);
        Frames.Advance(1000, 16);
        Assert.Equal(25 * .35, X(box), 6);
        window.MouseMove(new Point(49, 25));
        Frames.Advance(1000, 16);
        Assert.Equal(24 * .7, X(box), 6);

        window.MouseMove(new Point(60, 25));
        Frames.Advance(1000, 16);
        Assert.Equal(35 * .7 * 38 / 48, X(box), 6);
        Magnetic.SetRadius(box, 20);
        Frames.Advance(16, 16);
        Assert.Equal(35 * .7 * .5, X(box), 6);
    }

    [AvaloniaFact]
    public void ASpringSetsHowThePullMovesAndANameThatIsNoSpringThrows()
    {
        var box = Box();
        Magnetic.SetEnabled(box, true);
        Magnetic.SetSpring(box, "0,2000");
        var window = Show(box);

        window.MouseMove(new Point(50, 25));
        Frames.Advance(160, 16);
        Assert.InRange(X(box), .01, 25 * .35 / 2);
        Frames.Advance(5000, 16);
        Assert.Equal(25 * .35, X(box), 6);

        Magnetic.SetSpring(box, "Stiff");
        window.MouseMove(new Point(49, 25));
        Frames.Advance(1000, 16);
        Assert.Equal(24 * .35, X(box), 6);
        Assert.Throws<FormatException>(() => Magnetic.SetSpring(box, "wobbly"));
        Assert.Equal("Stiff", Magnetic.GetSpring(box));
    }

    [AvaloniaFact]
    public void ANeverEnabledElementIsNotPulled()
    {
        var box = Box();
        var window = Show(box);

        window.MouseMove(new Point(50, 25));
        Frames.Advance(1000, 16);

        Assert.False(Magnetic.GetEnabled(box));
        Assert.Null(Follow.GetX(box));
        Assert.Null(box.RenderTransform);
    }

    [AvaloniaFact]
    public void DoesNothingInDesignMode()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        var box = Box();
        var window = Show(box);
        isDesignMode.SetValue(null, true);
        try
        {
            Magnetic.SetEnabled(box, true);
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        window.MouseMove(new Point(50, 25));
        Frames.Advance(1000, 16);

        Assert.Null(Follow.GetX(box));
        Assert.Null(box.RenderTransform);
    }

    private static double X(Visual visual) => Convert.ToDouble(Presto.Get(visual, "X", withUnit: false));

    private static double Y(Visual visual) => Convert.ToDouble(Presto.Get(visual, "Y", withUnit: false));
}
