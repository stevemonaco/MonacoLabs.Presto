using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Tests;

/// <summary>Port of the composition cases of anime.js tweens.test.js, driven with hand-built tweens.</summary>
public sealed class CompositionTests : EngineTestBase
{
    private static Dictionary<string, object?> CreateTarget() => [];

    private static Timer CreateAnimation(double duration = 100, LoopCount? loop = null, bool autoplay = false) =>
        Presto.CreateTimer(new() { Duration = duration, Loop = loop, Autoplay = autoplay });

    /// <summary>Builds the tween an animation of one keyframe starting at <paramref name="start"/> would build, then composes it.</summary>
    private static Tween AddTween(
        Timer parent,
        object target,
        string property,
        double start,
        double duration,
        Composition composition = Composition.Replace,
        double toNumber = 0)
    {
        var tween = new Tween(parent, property, target, Presto.ResolveAccessor(target, property))
        {
            _composition = composition,
            _toNumber = toNumber,
            _updateDuration = duration,
            _changeDuration = duration,
            _absoluteStartTime = start,
            _absoluteUpdateStartTime = start,
            _absoluteEndTime = start + duration,
        };
        TweenList.AddChild(parent, tween);
        if (composition != Composition.None) Composer.ComposeTween(tween, Composer.GetTweenSiblings(target, property));
        return tween;
    }

    [Fact]
    public void ReplacedTweensAreSortedByAbsoluteStartTime()
    {
        var target = CreateTarget();
        var first = AddTween(CreateAnimation(), target, "x", 0, 10);
        var last = AddTween(CreateAnimation(), target, "x", 40, 10);
        var middle = AddTween(CreateAnimation(), target, "x", 20, 10);

        var siblings = Composer.GetTweenSiblings(target, "x");
        Assert.Same(first, siblings._head);
        Assert.Same(middle, first._nextRep);
        Assert.Same(last, middle._nextRep);
        Assert.Same(last, siblings._tail);
    }

    [Fact]
    public void OverriddenTweensAreSkippedWhenInsertingASibling()
    {
        var target = CreateTarget();
        var overridden = AddTween(CreateAnimation(), target, "x", 0, 10);
        Composer.OverrideTween(overridden);

        var added = AddTween(CreateAnimation(), target, "x", 40, 10);

        var siblings = Composer.GetTweenSiblings(target, "x");
        Assert.Same(added, siblings._head);
        Assert.Same(overridden, added._nextRep);
        Assert.Null(added._prevRep);
    }

    [Fact]
    public void OverlappedSiblingIsTruncatedToTheNewTweenStart()
    {
        var target = CreateTarget();
        var animation = CreateAnimation();
        var truncated = AddTween(animation, target, "x", 0, 100);
        AddTween(animation, target, "y", 0, 100);

        AddTween(CreateAnimation(), target, "x", 40, 100);

        Assert.Equal(40, truncated._changeDuration);
        Assert.Equal(40, truncated._currentTime);
        Assert.True(truncated._isOverlapped);
        Assert.False(truncated._isOverridden);
    }

    [Fact]
    public void OverlappedSiblingIsOverriddenWhenTruncatedToNothing()
    {
        var target = CreateTarget();
        var animation = CreateAnimation();
        var overridden = AddTween(animation, target, "x", 0, 100);
        AddTween(animation, target, "y", 0, 100);

        AddTween(CreateAnimation(), target, "x", 0, 100);

        Assert.True(overridden._isOverlapped);
        Assert.True(overridden._isOverridden);
        Assert.Equal(Constants.MinValue, overridden._changeDuration);
        Assert.Equal(Constants.MinValue, overridden._currentTime);
    }

    [Fact]
    public void PreviousAnimationIsCanceledOnlyOnceAllItsTweensAreOverlapped()
    {
        var target = CreateTarget();
        var animation = CreateAnimation(autoplay: true);
        AddTween(animation, target, "x", 0, 100);
        AddTween(animation, target, "y", 0, 100);

        var replacement = CreateAnimation();
        AddTween(replacement, target, "x", 40, 100);

        var overlapped = 0;
        TweenList.ForEachChildren(animation, tween =>
        {
            if (tween._isOverlapped) overlapped++;
        });
        Assert.Equal(1, overlapped);
        Assert.False(animation._canceled);
        Assert.False(animation.Paused);

        AddTween(replacement, target, "y", 40, 100);

        Assert.True(animation._canceled);
        Assert.True(animation.Paused);
    }

    [Fact]
    public void LoopedPreviousAnimationIsOverriddenWithItsKeyframeChain()
    {
        var target = CreateTarget();
        var looping = CreateAnimation(50, loop: 1);
        var firstKeyframe = AddTween(looping, target, "x", 0, 25);
        var secondKeyframe = AddTween(looping, target, "x", 25, 25);

        Assert.False(firstKeyframe._isOverlapped);

        AddTween(CreateAnimation(), target, "x", 60, 20);

        Assert.True(firstKeyframe._isOverridden);
        Assert.True(secondKeyframe._isOverridden);
        Assert.True(looping._canceled);
    }

    [Fact]
    public void BlendedTweensShareOneLookupTweenPerProperty()
    {
        var target = CreateTarget();
        var first = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Blend, toNumber: 100);

        var siblings = Composer.GetTweenSiblings(target, "x", Presto.Engine.Additive);
        var lookupTween = siblings._head;
        Assert.NotNull(lookupTween);
        Assert.NotSame(first, lookupTween);
        Assert.Equal(100, lookupTween._fromNumber);
        Assert.Equal(-100, first._fromNumber);
        Assert.Equal(0, first._toNumber);
        Assert.Same(first, siblings._tail);

        var second = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Blend, toNumber: 50);

        Assert.Same(lookupTween, siblings._head);
        Assert.Equal(50, lookupTween._fromNumber);
        Assert.Equal(50, second._fromNumber);
        Assert.Equal(0, second._toNumber);
        Assert.Same(second, siblings._tail);
    }

    [Fact]
    public void AdditiveUpdateSumsTheBlendedTweensIntoTheLookupTween()
    {
        var target = CreateTarget();
        var first = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Blend, toNumber: 100);
        var second = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Blend, toNumber: 50);
        first._number = 10;
        second._number = 5;

        Presto.Engine.Additive.Update();

        var lookupTween = Composer.GetTweenSiblings(target, "x", Presto.Engine.Additive)._head!;
        Assert.Equal(lookupTween._fromNumber + 15, lookupTween._toNumber);
    }

    [Fact]
    public void RemoveTweenSiblingsDropsTheLookupTweenWithTheLastBlendedTween()
    {
        var target = CreateTarget();
        var first = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Blend, toNumber: 100);
        var second = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Blend, toNumber: 50);
        var siblings = Composer.GetTweenSiblings(target, "x", Presto.Engine.Additive);
        var lookupTween = siblings._head!;

        Composer.RemoveTweenSiblings(first);

        Assert.Same(lookupTween, siblings._head);
        Assert.Same(second, siblings._tail);

        Composer.RemoveTweenSiblings(second);

        Assert.Null(siblings._head);
        Assert.Null(siblings._tail);
        Assert.NotSame(siblings, Composer.GetTweenSiblings(target, "x", Presto.Engine.Additive));

        var stillOwnedByTheAdditiveAnimation = false;
        TweenList.ForEachChildren(Presto.Engine.Additive.Animation!, tween => stillOwnedByTheAdditiveAnimation |= ReferenceEquals(tween, lookupTween));
        Assert.False(stillOwnedByTheAdditiveAnimation);
    }

    [Fact]
    public void CancelRemovesTweenSiblingsAndSeekReAddsThem()
    {
        var target = CreateTarget();
        var animation = CreateAnimation();
        AddTween(animation, target, "x", 0, 50);
        AddTween(animation, target, "y", 0, 50);
        var later = AddTween(CreateAnimation(), target, "x", 10, 50);

        Assert.Equal(2, TestHelpers.GetTweenCount(animation));
        var tween = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Same(later, tween._nextRep);

        animation.Cancel();

        Assert.True(animation._canceled);
        Assert.True(animation.Paused);
        Assert.Null(tween._nextRep);
        Assert.Null(later._prevRep);
        Assert.Same(later, Composer.GetTweenSiblings(target, "x")._head);

        animation.Seek(0);

        Assert.False(animation._canceled);
        Assert.Same(later, tween._nextRep);
        Assert.Same(tween, Composer.GetTweenSiblings(target, "x")._head);
    }
}
