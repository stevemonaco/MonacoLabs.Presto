namespace MonacoLabs.Presto.Tests;

/// <summary>Port of the target cases of anime.js tests/suites/utils.test.js: <c>utils.get</c>, <c>utils.set</c> and <c>utils.remove</c>.</summary>
public sealed class UtilsTargetsTests : EngineTestBase
{
    [Fact]
    public void GetObjectProperties()
    {
        var target = new TestObject();

        Assert.Equal(10d, Presto.Get(target, "PlainValue"));
        Assert.Equal("10px", Presto.Get(target, "ValueWithUnit"));
        Assert.Equal("16 32 64 128", Presto.Get(target, "MultiplePlainValues"));
        Assert.Equal("16px 32em 64% 128ch", Presto.Get(target, "MultipleValuesWithUnits"));
    }

    [Fact]
    public void UndefinedTargetsHaveNothingToGetOrRemove()
    {
        Assert.Null(Presto.Get(null, "PlainValue"));
        Assert.Empty(Presto.Remove(null));
    }

    [Fact]
    public void SetObjectProperties()
    {
        var target = new TestObject();

        Presto.Set(target, new()
        {
            ["PlainValue"] = 42,
            ["ValueWithUnit"] = "42px",
            ["MultiplePlainValues"] = "40 41 42 43",
            ["MultipleValuesWithUnits"] = "40px 41em 42% 43ch",
        });

        Assert.Equal(42, target.PlainValue);
        Assert.Equal("42px", target.ValueWithUnit);
        Assert.Equal("40 41 42 43", target.MultiplePlainValues);
        Assert.Equal("40px 41em 42% 43ch", target.MultipleValuesWithUnits);
    }

    [Fact]
    public void SetWritesTheToValueOfAFunctionReturningAFromToPair()
    {
        var target = new TestObject();

        Presto.Set(target, new() { ["PlainValue"] = new FunctionValue((_, _, _) => TweenValue.FromTo(10, 42)) });

        Assert.Equal(42, target.PlainValue);
    }

    /// <summary>Upstream measures CSS units against the DOM; the core scales the angle units and adopts any other unit as is.</summary>
    [Fact]
    public void GetObjectPropertiesAndConvertUnit()
    {
        var target = new StyleTarget { Rotate = "45deg" };

        Assert.Equal("45deg", Presto.Get(target, "Rotate", "deg"));
        Assert.Equal("0.7854rad", Presto.Get(target, "Rotate", "rad"));
        Assert.Equal("0.125turn", Presto.Get(target, "Rotate", "turn"));
        Assert.Equal(45d, Presto.Get(target, "Rotate", false));
    }

    [Fact]
    public void SetObjectPropertiesToSpecificUnit()
    {
        var target = new Dictionary<string, object?>
        {
            ["PlainValue"] = 10d,
            ["ValueWithUnit"] = "10px",
            ["MultiplePlainValues"] = "16 32 64 128",
            ["MultipleValuesWithUnits"] = "16px 32em 64% 128ch",
        };

        Presto.Set(target, new()
        {
            ["PlainValue"] = "42px",
            ["ValueWithUnit"] = "42rem",
            ["MultiplePlainValues"] = "40% 41px 42rem 43vh",
            ["MultipleValuesWithUnits"] = "40% 41px 42rem 43vh",
        });

        Assert.Equal("42px", target["PlainValue"]);
        Assert.Equal("42rem", target["ValueWithUnit"]);
        Assert.Equal("40% 41px 42rem 43vh", target["MultiplePlainValues"]);
        Assert.Equal("40% 41px 42rem 43vh", target["MultipleValuesWithUnits"]);
    }

    [Fact]
    public void RemoveTarget()
    {
        var target = new StyleTarget();
        var first = Presto.Animate(target, new() { ["X"] = 100, ["Y"] = 100 });
        var second = Presto.Animate(target, new() { ["Rotate"] = 180 });
        var third = Presto.Animate(target, new() { ["Scale"] = 2 });

        Assert.False(first.Paused);
        Assert.False(second.Paused);
        Assert.False(third.Paused);

        Presto.Remove(target);

        Assert.Equal(0, TestHelpers.GetTweenCount(first));
        Assert.Equal(0, TestHelpers.GetTweenCount(second));
        Assert.Equal(0, TestHelpers.GetTweenCount(third));
        Assert.True(first.Paused);
        Assert.True(second.Paused);
        Assert.True(third.Paused);
    }

    [Fact]
    public void RemoveTargetsWithObjectsRef()
    {
        var first = new TestObject();
        var second = new TestObject();
        var animation = Presto.Animate(new[] { first, second }, new() { ["PlainValue"] = 200, Duration = 100 });

        Assert.Equal(2, TestHelpers.GetTweenCount(animation));

        Presto.Remove(first);
        Assert.Equal(1, TestHelpers.GetTweenCount(animation));

        Presto.Remove(second);
        Assert.Equal(0, TestHelpers.GetTweenCount(animation));
        Assert.False(animation._hasChildren);
    }

    [Fact]
    public void RemoveTargetsFromMultipleAnimationsAtOnce()
    {
        var first = new TestObject();
        var second = new TestObject();
        var animation1 = Presto.Animate(new[] { first, second }, new() { ["PlainValue"] = 200, Duration = 100 });
        var animation2 = Presto.Animate(second, new() { ["PlainValue"] = 300, Duration = 100 });

        Presto.Remove(first);
        Assert.Equal(1, TestHelpers.GetTweenCount(animation1));
        Assert.Equal(1, TestHelpers.GetTweenCount(animation2));

        Presto.Remove(second);
        Assert.Equal(0, TestHelpers.GetTweenCount(animation1));
        Assert.Equal(0, TestHelpers.GetTweenCount(animation2));
    }

    [Fact]
    public void RemoveTargetsFromTimeline()
    {
        var first = new TestObject();
        var second = new TestObject();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 100 } })
            .Add(new[] { first, second }, new() { ["PlainValue"] = 200 })
            .Add(second, new() { ["PlainValue"] = 300 });

        Assert.True(tl._hasChildren);
        Assert.Equal(2, TestHelpers.GetTweenCount(TestHelpers.GetChildAtIndex(tl, 0)!));
        Assert.Equal(1, TestHelpers.GetTweenCount(TestHelpers.GetChildAtIndex(tl, 1)!));

        Presto.Remove(first);
        Assert.Equal(1, TestHelpers.GetTweenCount(TestHelpers.GetChildAtIndex(tl, 0)!));
        Assert.Equal(1, TestHelpers.GetTweenCount(TestHelpers.GetChildAtIndex(tl, 1)!));
        Assert.True(tl._hasChildren);

        Presto.Remove(second);
        Assert.Null(tl._head);
        Assert.Null(tl._tail);
        Assert.False(tl._hasChildren);
    }

    [Fact]
    public void RemoveTargetsOnASpecificAnimation()
    {
        var first = new TestObject();
        var second = new TestObject();
        var animation1 = Presto.Animate(new[] { first, second }, new() { ["PlainValue"] = 200, Duration = 100 });
        var animation2 = Presto.Animate(new[] { second, first }, new() { ["PlainValue"] = 300, Duration = 100 });

        Presto.Remove(second, animation1);
        Assert.Equal(1, TestHelpers.GetTweenCount(animation1));
        Assert.Equal(2, TestHelpers.GetTweenCount(animation2));

        Presto.Remove(first, animation2);
        Assert.Equal(1, TestHelpers.GetTweenCount(animation1));
        Assert.Equal(1, TestHelpers.GetTweenCount(animation2));

        Presto.Remove(first, animation1);
        Assert.Equal(0, TestHelpers.GetTweenCount(animation1));
        Assert.Equal(1, TestHelpers.GetTweenCount(animation2));

        Presto.Remove(second, animation2);
        Assert.Equal(0, TestHelpers.GetTweenCount(animation1));
        Assert.Equal(0, TestHelpers.GetTweenCount(animation2));
    }

    [Fact]
    public void GetAnimationsListsTheRunningAnimationsOfATarget()
    {
        var target = new StyleTarget();
        var other = new StyleTarget();
        var moving = Presto.Animate(target, new() { ["X"] = 100, Duration = 1000 });
        var fading = Presto.Animate(new[] { other, target }, new() { ["Y"] = 100, Duration = 1000 });
        Presto.Animate(other, new() { ["X"] = 100, Duration = 1000 });

        Assert.Equal([moving, fading], Presto.GetAnimations(target));
        Assert.Equal([fading], Presto.GetAnimations(target, "Y"));
    }

    [Fact]
    public void GetAnimationsSkipsCompletedAndCanceledAnimations()
    {
        var target = new StyleTarget();
        var completed = Presto.Animate(target, new() { ["X"] = 100, Duration = 10 });
        var canceled = Presto.Animate(target, new() { ["Y"] = 100, Duration = 1000 });
        var running = Presto.Animate(target, new() { ["Opacity"] = 1, Duration = 1000 });

        Frames.Advance(50, 16);
        canceled.Cancel();

        Assert.True(completed.Completed);
        Assert.Equal([running], Presto.GetAnimations(target));
    }

    [Fact]
    public void GetAnimationsDropsAPausedAnimationOnceTheEngineReleasesIt()
    {
        var target = new StyleTarget();
        var paused = Presto.Animate(target, new() { ["X"] = 100, Duration = 1000 });
        Frames.Advance(50, 16);

        paused.Pause();
        Assert.Equal([paused], Presto.GetAnimations(target));

        Frames.Advance(16);
        Assert.Empty(Presto.GetAnimations(target));

        paused.Resume();
        Assert.Equal([paused], Presto.GetAnimations(target));
    }

    [Fact]
    public void GetAnimationsFindsTimelineChildren()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 100 } })
            .Add(target, new() { ["X"] = 100 })
            .Add(new StyleTarget(), new() { ["X"] = 100 });

        Assert.Same(TestHelpers.GetChildAtIndex(tl, 0), Assert.Single(Presto.GetAnimations(target)));
    }

    [Fact]
    public void CancelAnimationsWithNoTweensLeftAfterCallingRemove()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = 100 });

        Presto.Remove(target);

        Assert.Equal(0, TestHelpers.GetTweenCount(animation));
        Assert.True(animation.Canceled);
        Assert.True(animation.Paused);
    }

    [Fact]
    public void DoNotCancelAnimationsIfTweensLeftAfterCallingRemove()
    {
        var first = new StyleTarget();
        var second = new StyleTarget();
        var animation = Presto.Animate(new[] { first, second }, new() { ["X"] = 100 });

        Presto.Remove(first);

        Assert.Equal(1, TestHelpers.GetTweenCount(animation));
        Assert.False(animation.Canceled);
    }

    [Fact]
    public void RemoveSpecificTweenProperty()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = 100, ["Y"] = 100 });

        Presto.Remove(target, animation, "X");

        Assert.Equal(1, TestHelpers.GetTweenCount(animation));
        Assert.Equal("Y", TestHelpers.GetTweenAtIndex(animation, 0)!.Property);
        Assert.False(animation.Canceled);
    }

    [Fact]
    public void RemoveSpecificTweenPropertyAndCancelTheAnimationIfNoTweensAreLeft()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = 100, ["Y"] = 100 });

        Presto.Remove(target, animation, "X");
        Presto.Remove(target, animation, "Y");

        Assert.Equal(0, TestHelpers.GetTweenCount(animation));
        Assert.True(animation.Canceled);
        Assert.True(animation.Paused);
    }
}
