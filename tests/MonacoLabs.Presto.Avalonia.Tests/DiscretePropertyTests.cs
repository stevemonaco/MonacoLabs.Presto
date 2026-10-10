using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary>Avalonia properties with no codec tween discretely, switching halfway except <see cref="Visual.IsVisible"/>, which stays true throughout.</summary>
public sealed class DiscretePropertyTests : BridgeTestBase
{
    public static TheoryData<AvaloniaProperty, object, object> Properties => new()
    {
        { InputElement.IsHitTestVisibleProperty, true, false },
        { Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Left, HorizontalAlignment.Right },
        { TextBlock.TextAlignmentProperty, TextAlignment.Left, TextAlignment.Right },
        { DockPanel.DockProperty, Dock.Left, Dock.Bottom },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Properties))]
    public void APropertyWithNoCodecSwitchesHalfway(AvaloniaProperty property, object from, object to)
    {
        var text = new TextBlock();
        text.SetValue(property, from);
        var animation = Seeked(text, new AvaloniaAnimationParams { [property] = TweenValue.Of(to) }, .49);

        Assert.Equal(from, text.GetValue(property));
        animation.Seek(50);
        Assert.Equal(to, text.GetValue(property));
    }

    [AvaloniaTheory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IsVisibleStaysTrueThroughoutAFadeEitherWay(bool from, bool to)
    {
        var border = new Border { IsVisible = from, Opacity = from ? 1 : 0 };
        var animation = Seeked(border, new AvaloniaAnimationParams { ["Opacity"] = to ? 1 : 0, [Visual.IsVisibleProperty] = TweenValue.Of(to) }, .01);

        Assert.True(border.IsVisible);
        animation.Seek(99);
        Assert.True(border.IsVisible);
        animation.Seek(100);
        Assert.Equal(to, border.IsVisible);
        animation.Seek(0);
        Assert.Equal(from, border.IsVisible);
    }

    [AvaloniaFact]
    public void RevertRestoresTheLocalValue()
    {
        var border = new Border { IsHitTestVisible = true, HorizontalAlignment = HorizontalAlignment.Center };
        var animation = Seeked(border, new AvaloniaAnimationParams
        {
            ["IsHitTestVisible"] = TweenValue.Of(false),
            [Layoutable.HorizontalAlignmentProperty] = TweenValue.Of(HorizontalAlignment.Stretch),
        });
        Assert.False(border.IsHitTestVisible);

        animation.Revert();

        Assert.True(border.IsHitTestVisible);
        Assert.Equal(HorizontalAlignment.Center, border.HorizontalAlignment);
    }

    [AvaloniaFact]
    public void SetWritesABoolAndAnEnumAtOnce()
    {
        var border = new Border();

        PrestoAvalonia.Set(border, new AvaloniaAnimationParams
        {
            [InputElement.IsEnabledProperty] = TweenValue.Of(false),
            [Layoutable.VerticalAlignmentProperty] = TweenValue.Of(VerticalAlignment.Bottom),
        });

        Assert.False(border.IsEnabled);
        Assert.Equal(VerticalAlignment.Bottom, border.VerticalAlignment);
    }

    [AvaloniaFact]
    public void ATimelineSetHidesAtItsPosition()
    {
        var border = new Border();
        Presto.CreateTimeline().Set(border, new AvaloniaAnimationParams { [Visual.IsVisibleProperty] = TweenValue.Of(false) }, 100);

        Frames.Advance(50);
        Assert.True(border.IsVisible);
        RunUntil(() => !border.IsVisible, max: 500);
    }

    [AvaloniaFact]
    public void ABoolHasNoCompositorCounterpart() =>
        Assert.Throws<NotSupportedException>(() => new Border().AnimateOnCompositor(b => b.Property(Visual.IsVisibleProperty, TweenValue.Of(false))));
}
