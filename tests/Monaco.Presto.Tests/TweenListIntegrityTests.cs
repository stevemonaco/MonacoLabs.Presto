using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>Repeat cancels and removals of blended animations keep every tween list intact (PLAN Phase 51, a deviation from upstream).</summary>
public sealed class TweenListIntegrityTests : EngineTestBase
{
    private static Animation Blend(StyleTarget target, double to) =>
        Presto.Animate(target, new() { ["X"] = to, Duration = 100, Composition = Composition.Blend });

    private static void AssertAllIntact(StyleTarget target, params Timer[] animations)
    {
        var additive = Presto.Engine.Additive;
        if (additive.Lookups.TryGetValue(target, out var properties))
        {
            foreach (var siblings in properties.Values) TweenList.CheckIntact(siblings);
        }
        if (additive.Animation is { } additiveAnimation) TweenList.CheckIntact(additiveAnimation);
        foreach (var animation in animations) TweenList.CheckIntact(animation);
    }

    [Fact]
    public void CancelingABlendedAnimationTwiceKeepsTheListsIntactForTheNextBlend()
    {
        var target = new StyleTarget();
        var first = Blend(target, 100);
        var second = Blend(target, 50);

        first.Cancel();
        first.Cancel();
        var third = Blend(target, 25);

        AssertAllIntact(target, first, second, third);
        Assert.Same(second._headTween, third._headTween!._prevAdd);
    }

    [Fact]
    public void RemovingATargetAfterRepeatedCancelsEmptiesTheLists()
    {
        var target = new StyleTarget();
        var first = Blend(target, 100);
        var second = Blend(target, 50);

        first.Cancel();
        first.Cancel();
        second.Cancel();
        var third = Blend(target, 25);
        Presto.Remove(target);

        AssertAllIntact(target, first, second, third);
        Assert.False(Presto.Engine.Additive.Lookups.ContainsKey(target));
        Assert.Null(Presto.Engine.Additive.Animation!._headTween);
        Assert.Null(third._headTween);
    }

    [Fact]
    public void CancelingACompletedBlendedAnimationKeepsTheListsIntact()
    {
        var target = new StyleTarget();
        var first = Blend(target, 100);
        var second = Blend(target, 50);

        first.Complete();
        first.Cancel();
        var third = Blend(target, 25);

        AssertAllIntact(target, first, second, third);
        Assert.Same(second._headTween, third._headTween!._prevAdd);
    }

    [Fact]
    public void BlendingAndCancelingTwentyThousandTargetsStaysFast()
    {
        var targets = new StyleTarget[20_000];
        for (var i = 0; i < targets.Length; i++) targets[i] = new StyleTarget();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        Presto.Animate(targets, new() { ["X"] = 100, Duration = 100, Composition = Composition.Blend }).Cancel();

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"took {clock.Elapsed.TotalMilliseconds:0} ms");
    }
}
