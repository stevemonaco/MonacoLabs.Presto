using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

public sealed class LayoutSizeSnapTests : BridgeTestBase
{
    private static void AssertOnFourDevicePixelSteps(double size, double scaling)
    {
        var steps = (size - 168) * scaling / 4;
        Assert.Equal(Math.Round(steps), steps, 1e-9);
    }

    [AvaloniaTheory]
    [InlineData("linear", 200, 76)]
    [InlineData("linear", 201, 77)]
    [InlineData("bouncy", 200, 77)]
    [InlineData("bouncy", 201, 76)]
    public void ACenteredControlsEdgesMoveTogetherAndItsCenteredContentHoldsStill(string ease, double parentWidth, double contentWidth)
    {
        var content = new Border { Width = contentWidth, HorizontalAlignment = HorizontalAlignment.Center };
        var box = new Border { Width = 168, Height = 40, HorizontalAlignment = HorizontalAlignment.Center, Child = content };
        var parent = new Panel { Width = parentWidth, Children = { box } };
        Show(parent);
        parent.UpdateLayout();
        var contentX = box.Bounds.X + content.Bounds.X;
        var gap = box.Bounds.X - (parentWidth - box.Bounds.Right);

        var animation = box.Animate(new()
        {
            ["Width"] = 44,
            Duration = 100,
            Ease = ease == "linear" ? "linear" : Springs.Bouncy,
            Autoplay = false,
        });
        var overshot = false;
        for (var time = 0d; time <= animation.Duration; time += 1)
        {
            animation.Seek(time);
            parent.UpdateLayout();
            Assert.Equal(gap, box.Bounds.X - (parentWidth - box.Bounds.Right), 6);
            if (box.Bounds.Width >= contentWidth) Assert.Equal(contentX, box.Bounds.X + content.Bounds.X, 6);
            overshot |= box.Bounds.Width < 44;
        }

        Assert.Equal(44, box.Width);
        Assert.Equal(ease == "bouncy", overshot);
    }

    [AvaloniaFact]
    public void AnElementWithoutLayoutRoundingGetsTheRawSize()
    {
        var box = new Border { Width = 168, UseLayoutRounding = false };
        var animation = box.Animate(new() { ["Width"] = 44, Duration = 100, Ease = "linear", Autoplay = false });

        animation.Seek(33);

        Assert.Equal(168 + (44 - 168) * .33, box.Width, 9);
    }

    [AvaloniaTheory]
    [InlineData("Width", 1)]
    [InlineData("Width", 1.25)]
    [InlineData("Width", 1.5)]
    [InlineData("Height", 1)]
    public void SnappedSizesStepByFourDevicePixelsAtTheElementsScaling(string property, double scaling)
    {
        var box = property == "Width"
            ? new Border { Width = 168, Height = 40, HorizontalAlignment = HorizontalAlignment.Left }
            : new Border { Width = 40, Height = 168, VerticalAlignment = VerticalAlignment.Top };
        var sizeProperty = property == "Width" ? Layoutable.WidthProperty : Layoutable.HeightProperty;
        var window = Show(box);
        window.SetRenderScaling(scaling);
        Dispatcher.UIThread.RunJobs();
        var animation = box.Animate(new() { [property] = 44, Duration = 100, Ease = "linear", Autoplay = false });

        for (var time = 0d; time < animation.Duration; time += 1)
        {
            animation.Seek(time);
            AssertOnFourDevicePixelSteps(box.GetValue(sizeProperty), scaling);
        }
        animation.Seek(animation.Duration);

        Assert.Equal(44, box.GetValue(sizeProperty));
    }

    [AvaloniaFact]
    public void ANewScalingAppliesFromTheNextFrame()
    {
        var box = new Border { Width = 168, Height = 40, HorizontalAlignment = HorizontalAlignment.Left };
        var window = Show(box);
        var animation = box.Animate(new() { ["Width"] = 44, Duration = 100, Ease = "linear", Autoplay = false });
        animation.Seek(30);
        AssertOnFourDevicePixelSteps(box.Width, 1);

        window.SetRenderScaling(1.5);
        Dispatcher.UIThread.RunJobs();

        for (var time = 31d; time < animation.Duration; time += 1)
        {
            animation.Seek(time);
            AssertOnFourDevicePixelSteps(box.Width, 1.5);
        }
    }
}
