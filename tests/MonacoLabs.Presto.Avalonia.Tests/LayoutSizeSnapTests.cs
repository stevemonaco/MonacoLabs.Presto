using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;

namespace MonacoLabs.Presto.Tests;

public sealed class LayoutSizeSnapTests : BridgeTestBase
{
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
    public void HeightSnapsToFourDevicePixelSteps()
    {
        var box = new Border { Width = 40, Height = 168 };
        var animation = box.Animate(new() { ["Height"] = 44, Duration = 100, Ease = "linear", Autoplay = false });

        for (var time = 0d; time < animation.Duration; time += 1)
        {
            animation.Seek(time);
            Assert.Equal(0, (box.Height - 168) / 4 % 1);
        }
        animation.Seek(animation.Duration);

        Assert.Equal(44, box.Height);
    }

    [AvaloniaFact]
    public void AnElementWithoutLayoutRoundingGetsTheRawSize()
    {
        var box = new Border { Width = 168, UseLayoutRounding = false };
        var animation = box.Animate(new() { ["Width"] = 44, Duration = 100, Ease = "linear", Autoplay = false });

        animation.Seek(33);

        Assert.Equal(168 + (44 - 168) * .33, box.Width, 9);
    }
}
