using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Tilt"/>: the pointer-follow tilt over <c>RotateX</c>/<c>RotateY</c>.</summary>
public sealed class TiltTests : BridgeTestBase
{
    [AvaloniaFact]
    public void PointerMovesTurnTheElementTowardsThePointerWithinTheStrength()
    {
        var box = Box();
        Tilt.SetEnabled(box, true);
        var window = Show(box);

        window.MouseMove(new Point(45, 25));
        Frames.Advance(2000, 16);
        Assert.Equal(8, Value(box, "RotateY"), 6);
        Assert.Equal(0, Value(box, "RotateX"), 6);

        window.MouseMove(new Point(15, 40));
        Frames.Advance(2000, 16);
        Assert.Equal(-4, Value(box, "RotateY"), 6);
        Assert.Equal(-6, Value(box, "RotateX"), 6);

        Tilt.SetStrength(box, 20);
        window.MouseMove(new Point(25, 10));
        Frames.Advance(2000, 16);
        Assert.Equal(0, Value(box, "RotateY"), 6);
        Assert.Equal(12, Value(box, "RotateX"), 6);
    }

    [AvaloniaFact]
    public void LeavingSpringsBothAxesBackToZero()
    {
        var box = Box();
        Tilt.SetEnabled(box, true);
        var window = Show(box);
        window.MouseMove(new Point(40, 10));
        Frames.Advance(2000, 16);

        window.MouseMove(new Point(150, 150));
        Frames.Advance(100, 16);
        Assert.NotEqual(0, Value(box, "RotateY"));
        Frames.Advance(2000, 16);

        Assert.Equal(0, Value(box, "RotateX"), 6);
        Assert.Equal(0, Value(box, "RotateY"), 6);
    }

    [AvaloniaFact]
    public void DetachingOrDisablingRevertsToTheElementsOwnRenderTransform()
    {
        var own = new RotateTransform(5);
        var box = Box();
        box.RenderTransform = own;
        Tilt.SetEnabled(box, true);
        var window = Show(box);
        window.MouseMove(new Point(40, 10));
        Frames.Advance(2000, 16);
        Assert.NotSame(own, box.RenderTransform);

        window.Content = null;
        Assert.Same(own, box.RenderTransform);

        window.Content = box;
        window.MouseMove(new Point(150, 150));
        window.MouseMove(new Point(40, 10));
        Frames.Advance(2000, 16);
        Assert.NotSame(own, box.RenderTransform);

        Tilt.SetEnabled(box, false);
        Assert.Same(own, box.RenderTransform);
    }

    [AvaloniaFact]
    public void ReducedMotionLeavesTheElementFlat()
    {
        var box = Box();
        Tilt.SetEnabled(box, true);
        var window = Show(box);
        Presto.Defaults.ReducedMotion = true;

        window.MouseMove(new Point(45, 10));
        Frames.Advance(2000, 16);

        Assert.Equal(0, Value(box, "RotateX"));
        Assert.Equal(0, Value(box, "RotateY"));
    }

    [AvaloniaFact]
    public void ANeverEnabledElementDoesNotTilt()
    {
        var box = Box();
        var window = Show(box);

        window.MouseMove(new Point(45, 10));
        Frames.Advance(2000, 16);

        Assert.False(Tilt.GetEnabled(box));
        Assert.Null(box.RenderTransform);
    }

    [AvaloniaFact]
    public void DoesNothingInDesignMode()
    {
        var isDesignMode = typeof(Avalonia.Controls.Design).GetProperty(nameof(Avalonia.Controls.Design.IsDesignMode))!; // The setter is internal.
        var box = Box();
        var window = Show(box);
        isDesignMode.SetValue(null, true);
        try
        {
            Tilt.SetEnabled(box, true);
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        window.MouseMove(new Point(45, 10));
        Frames.Advance(2000, 16);

        Assert.Null(box.RenderTransform);
    }

    private static double Value(Visual visual, string property) => Convert.ToDouble(Presto.Get(visual, property, withUnit: false));
}
