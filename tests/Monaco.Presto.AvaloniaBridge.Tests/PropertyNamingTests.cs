using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// One Avalonia property can be written as a bare name, a qualified name or a typed handle. Tweens are keyed by the
/// canonical name so the spellings are interchangeable wherever a property is named.
/// </summary>
public class PropertyNamingTests : BridgeTestBase
{
    [AvaloniaFact]
    public void ATypedPropertyOverridesATweenDeclaredByName()
    {
        var border = new Border { Opacity = 1 };
        var byName = border.Animate(new() { ["Opacity"] = 0.0, Duration = 100, Ease = Eases.None });
        var byHandle = border.Animate(new AvaloniaAnimationParams { [Visual.OpacityProperty] = 1.0, Duration = 100, Ease = Eases.None });

        Frames.Advance(50);

        Assert.True(byName.Cancelled);
        Assert.False(byHandle.Cancelled);
    }

    [AvaloniaFact]
    public void ANamedPropertyOverridesATweenDeclaredByHandle()
    {
        var border = new Border { Opacity = 1 };
        var byHandle = border.Animate(new AvaloniaAnimationParams { [Visual.OpacityProperty] = 0.0, Duration = 100, Ease = Eases.None });
        var byName = border.Animate(new() { ["Opacity"] = 1.0, Duration = 100, Ease = Eases.None });

        Frames.Advance(50);

        Assert.True(byHandle.Cancelled);
        Assert.False(byName.Cancelled);
    }

    [AvaloniaFact]
    public void TheQualifiedSpellingSharesTheSameKey()
    {
        var border = new Border { Opacity = 1 };
        var bare = border.Animate(new() { ["Opacity"] = 0.0, Duration = 100, Ease = Eases.None });
        var qualified = border.Animate(new() { ["Visual.Opacity"] = 1.0, Duration = 100, Ease = Eases.None });

        Frames.Advance(50);

        Assert.True(bare.Cancelled);
        Assert.False(qualified.Cancelled);
    }

    [AvaloniaFact]
    public void RemoveByNameCancelsATweenDeclaredByHandle()
    {
        var border = new Border { Opacity = 1 };
        var animation = border.Animate(new AvaloniaAnimationParams { [Visual.OpacityProperty] = 0.0, Duration = 100, Ease = Eases.None });

        PrestoAvalonia.Remove(border, null, "Opacity");
        Frames.Advance(50);

        Assert.Equal(1, border.Opacity);
    }

    [AvaloniaFact]
    public void RemoveByHandleCancelsATweenDeclaredByName()
    {
        var border = new Border { Opacity = 1 };
        var animation = border.Animate(new() { ["Opacity"] = 0.0, Duration = 100, Ease = Eases.None });

        PrestoAvalonia.Remove(border, Visual.OpacityProperty);
        Frames.Advance(50);

        Assert.Equal(1, border.Opacity);
    }

    [AvaloniaFact]
    public void AnAnimatableDeclaredByNameIsReadAndSetByHandle()
    {
        var border = new Border { Opacity = 1 };
        var animatable = border.CreateAnimatable(new() { ["Opacity"] = 100 });

        animatable.Set(Visual.OpacityProperty, 0.5);
        Frames.Advance(200);

        Assert.Equal(0.5, animatable.Get(Visual.OpacityProperty), 3);
        Assert.Equal(0.5, border.Opacity, 3);
    }

    [AvaloniaFact]
    public void TransformShorthandsKeepTheirOwnName()
    {
        var border = new Border();
        var adapter = new AvaloniaObjectTargetAdapter();

        Assert.Equal("X", adapter.Canonicalise(border, "X"));
        Assert.Equal("Rotate", adapter.Canonicalise(border, "Rotate"));
        Assert.Null(adapter.Canonicalise(border, "NotAProperty"));
        Assert.Null(adapter.Canonicalise(new object(), "Opacity"));
    }
}
