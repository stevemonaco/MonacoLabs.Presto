using System.Runtime.CompilerServices;
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
        if (composition != Composition.Overlap) Composer.ComposeTween(tween, Composer.GetTweenSiblings(target, property, Presto.Engine));
        return tween;
    }

    [Fact]
    public void ReplacedTweensAreSortedByAbsoluteStartTime()
    {
        var target = CreateTarget();
        var first = AddTween(CreateAnimation(), target, "x", 0, 10);
        var last = AddTween(CreateAnimation(), target, "x", 40, 10);
        var middle = AddTween(CreateAnimation(), target, "x", 20, 10);

        var siblings = Composer.GetTweenSiblings(target, "x", Presto.Engine);
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

        var siblings = Composer.GetTweenSiblings(target, "x", Presto.Engine);
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
    public void AdditiveTweensShareOneLookupTweenPerProperty()
    {
        var target = CreateTarget();
        var first = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Additive, toNumber: 100);

        var siblings = Composer.GetTweenSiblings(target, "x", Presto.Engine.Additive);
        var lookupTween = siblings._head;
        Assert.NotNull(lookupTween);
        Assert.NotSame(first, lookupTween);
        Assert.Equal(100, lookupTween._fromNumber);
        Assert.Equal(-100, first._fromNumber);
        Assert.Equal(0, first._toNumber);
        Assert.Same(first, siblings._tail);

        var second = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Additive, toNumber: 50);

        Assert.Same(lookupTween, siblings._head);
        Assert.Equal(50, lookupTween._fromNumber);
        Assert.Equal(50, second._fromNumber);
        Assert.Equal(0, second._toNumber);
        Assert.Same(second, siblings._tail);
    }

    [Fact]
    public void AdditiveUpdateSumsTheAdditiveTweensIntoTheLookupTween()
    {
        var target = CreateTarget();
        var first = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Additive, toNumber: 100);
        var second = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Additive, toNumber: 50);
        first._number = 10;
        second._number = 5;

        Presto.Engine.Additive.Update();

        var lookupTween = Composer.GetTweenSiblings(target, "x", Presto.Engine.Additive)._head!;
        Assert.Equal(lookupTween._fromNumber + 15, lookupTween._toNumber);
    }

    [Fact]
    public void SeekingAPausedAdditiveAnimationWritesTheSumWithoutAnEngineUpdate()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Duration = 100,
            Ease = "linear",
            Composition = Composition.Additive,
            Autoplay = false,
        });

        animation.Seek(50);
        Assert.Equal(50, target.X, 6);

        animation.Seek(20);
        Assert.Equal(20, target.X, 6);
    }

    [Fact]
    public void SeekingACanceledAdditiveAnimationRecomposesItFromItsOwnValues()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["X"] = new TweenParams[] { new() { To = 200 }, new() { To = 0 } },
            Duration = 200,
            Ease = "linear",
            Composition = Composition.Additive,
            Autoplay = false,
        });
        animation.Seek(50);

        animation.Cancel();
        animation.Seek(50);
        Assert.Equal(100, target.X, 6);

        animation.Seek(150);
        Assert.Equal(100, target.X, 6);
    }

    [Fact]
    public void SeekingACanceledAdditiveColorAnimationSumsTheKeyframeThatHasNotRenderedAtItsFrom()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["BackgroundColor"] = new TweenParams[] { new() { To = "rgb(155,255,255)" }, new() { To = "rgb(55,255,255)" } },
            Duration = 200,
            Ease = "linear",
            Composition = Composition.Additive,
            Autoplay = false,
        });
        animation.Seek(50);
        var firstPlay = target.BackgroundColor;

        animation.Cancel();
        animation.Seek(50);
        Assert.Equal(firstPlay, target.BackgroundColor);
    }

    [Fact]
    public void RemoveTweenSiblingsDropsTheLookupTweenWithTheLastAdditiveTween()
    {
        var target = CreateTarget();
        var first = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Additive, toNumber: 100);
        var second = AddTween(CreateAnimation(), target, "x", 0, 100, Composition.Additive, toNumber: 50);
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
        Assert.Same(later, Composer.GetTweenSiblings(target, "x", Presto.Engine)._head);

        animation.Seek(0);

        Assert.False(animation._canceled);
        Assert.Same(later, tween._nextRep);
        Assert.Same(tween, Composer.GetTweenSiblings(target, "x", Presto.Engine)._head);
    }

    [Fact]
    public void SetAndAnimationsOfAThousandTargetsDefaultToOverlap()
    {
        static StyleTarget[] Targets(int count) => Enumerable.Range(0, count).Select(_ => new StyleTarget()).ToArray();

        var set = Presto.Set(new StyleTarget(), new() { ["X"] = 1 });
        var thousand = Presto.Animate(Targets(1000), new() { ["X"] = 1, Autoplay = false });
        var fewer = Presto.Animate(Targets(999), new() { ["X"] = 1, Autoplay = false });
        var chosen = Presto.Animate(Targets(1000), new() { ["X"] = 1, Composition = Composition.Replace, Autoplay = false });

        Assert.Equal(Composition.Overlap, set._headTween!._composition);
        Assert.Equal(Composition.Overlap, thousand._headTween!._composition);
        Assert.Equal(Composition.Replace, fewer._headTween!._composition);
        Assert.Equal(Composition.Replace, chosen._headTween!._composition);
    }

    [Fact]
    public void TheAdditivePassOverwritesAReplaceTweenWritingTheSameFrame()
    {
        var target = new StyleTarget();
        var replace = Presto.Animate(target, new() { ["X"] = 1000, Duration = 200, Ease = "linear" });
        Presto.Animate(target, new() { ["X"] = 100, Duration = 100, Ease = "linear", Composition = Composition.Additive });

        Frames.Advance(50, 10);

        Assert.False(replace.Canceled);
        Assert.InRange(target.X, 30, 70);

        Frames.Advance(100, 10);

        Assert.InRange(target.X, 600, 900);
    }

    [Fact]
    public void AnAdditiveTweenOverridesTheReplaceSiblingsAtOrAfterItsStart()
    {
        var target = CreateTarget();
        var firstAnimation = CreateAnimation();
        var first = AddTween(firstAnimation, target, "x", 0, 1000);
        AddTween(firstAnimation, target, "y", 0, 1000);
        var later = AddTween(CreateAnimation(), target, "x", 500, 200);
        Assert.False(later._isOverridden);

        Presto.Animate(target, new() { ["x"] = 10, Duration = 100, Delay = 100, Composition = Composition.Additive, Autoplay = false });

        Assert.True(later._isOverridden);
        Assert.False(first._isOverridden);
    }

    private static Animation PausedAdditive(StyleTarget target, double to = 100)
    {
        var animation = Presto.Animate(target, new() { ["X"] = to, Duration = 200, Ease = "linear", Composition = Composition.Additive, Autoplay = false });
        animation.Seek(100);
        return animation;
    }

    private static bool InAdditiveAnimation(Tween lookupTween)
    {
        for (var tween = Presto.Engine.Additive.Animation!._headTween; tween != null; tween = tween._next)
        {
            if (tween == lookupTween) return true;
        }
        return false;
    }

    [Fact]
    public void ParkingAPausedAdditiveAnimationTakesItsTargetsOutOfTheRenderedLookups()
    {
        var target = new StyleTarget();
        var animation = PausedAdditive(target);
        var additive = Presto.Engine.Additive;
        var lookupTween = Composer.GetTweenSiblings(target, "X", additive)._head!;

        additive.Park(animation);

        Assert.False(additive.Lookups.ContainsKey(target));
        Assert.True(additive.Parked.TryGetValue(target, out _));
        Assert.False(InAdditiveAnimation(lookupTween));
        target.X = -1;
        additive.Update();
        Assert.Equal(-1, target.X);
    }

    [Fact]
    public void UnparkingRestoresTheEntryAndResumeContinuesFromTheSameProgress()
    {
        var target = new StyleTarget();
        var animation = PausedAdditive(target);
        var additive = Presto.Engine.Additive;
        additive.Park(animation);
        target.X = -1;

        additive.Unpark(animation);

        Assert.True(additive.Lookups.TryGetValue(target, out var properties));
        Assert.False(additive.Parked.TryGetValue(target, out _));
        Assert.True(InAdditiveAnimation(properties["X"]._head!));
        additive.Update();
        Assert.Equal(50, target.X, 6);

        animation.Resume();
        Frames.Advance(50, 10);
        Assert.InRange(target.X, 60, 90);
    }

    [Fact]
    public void ComposingAnAdditiveTweenOnAParkedTargetUnparksItAndSums()
    {
        var target = new StyleTarget();
        var first = PausedAdditive(target);
        var additive = Presto.Engine.Additive;
        additive.Park(first);

        var second = Presto.Animate(target, new() { ["X"] = 50, Duration = 100, Ease = "linear", Composition = Composition.Additive, Autoplay = false });

        Assert.True(additive.Lookups.TryGetValue(target, out var properties));
        Assert.False(additive.Parked.TryGetValue(target, out _));
        var siblings = properties["X"];
        Assert.True(InAdditiveAnimation(siblings._head!));
        Assert.Same(second._headTween, siblings._tail);
        Assert.Same(first._headTween, second._headTween!._prevAdd);
        second.Seek(100);
        Assert.Equal(siblings._head!._fromNumber + first._headTween!._number, target.X, 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelingOrRevertingAParkedAnimationRemovesItsTweensInPlace(bool revert)
    {
        var target = new StyleTarget();
        var first = PausedAdditive(target);
        var second = PausedAdditive(target, 50);
        var additive = Presto.Engine.Additive;
        additive.Park(first);
        Assert.True(additive.Parked.TryGetValue(target, out var properties));
        var siblings = properties["X"];

        if (revert) first.Revert(); else first.Cancel();

        Assert.True(additive.Parked.TryGetValue(target, out _));
        Assert.Same(second._headTween, siblings._tail);
        Assert.Same(siblings._head, second._headTween!._prevAdd);
        TweenList.CheckIntact(siblings);

        if (revert) second.Revert(); else second.Cancel();

        Assert.False(additive.Parked.TryGetValue(target, out _));
        Assert.False(additive.Lookups.ContainsKey(target));
        Assert.Null(siblings._head);
        TweenList.CheckIntact(siblings);
        TweenList.CheckIntact(additive.Animation!);
    }

    [Fact]
    public void ATimerCompletedWhileParkedWritesItsFinalSumAsItsTweensDetach()
    {
        var target = new StyleTarget();
        var animation = PausedAdditive(target);
        var additive = Presto.Engine.Additive;
        additive.Park(animation);

        animation.Complete();

        Assert.Equal(100, target.X, 6);
        Assert.False(additive.Parked.TryGetValue(target, out _));
        Assert.False(additive.Lookups.ContainsKey(target));
    }

    [Fact]
    public void AnEntryWithATweenOfAPlayingRootIsNotParked()
    {
        var target = new StyleTarget();
        var paused = PausedAdditive(target);
        Presto.Animate(target, new() { ["X"] = 50, Duration = 200, Composition = Composition.Additive });
        var additive = Presto.Engine.Additive;

        additive.Park(paused);

        Assert.True(additive.Lookups.ContainsKey(target));
        Assert.False(additive.Parked.TryGetValue(target, out _));
    }

    [Fact]
    public async Task AParkedTargetIsCollectableOnceNothingHoldsItsAnimation()
    {
        var target = ParkPausedAdditive();
        Frames.Advance(50, 10);

        for (var i = 0; i < 20 && target.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.False(target.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ParkPausedAdditive()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 200, Composition = Composition.Additive, Loop = true });
        animation.Pause();
        Presto.Engine.Additive.Park(animation);
        return new WeakReference(target);
    }
}
