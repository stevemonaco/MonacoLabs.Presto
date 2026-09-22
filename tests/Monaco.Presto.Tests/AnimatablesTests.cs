namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js tests/suites/animatables.test.js.</summary>
public sealed class AnimatablesTests : EngineTestBase
{
    [Fact]
    public void GetAndAnimateAnimatableValues()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["TranslateX"] = 20,
            ["TranslateY"] = 30,
            ["Rotate"] = 40,
        });

        Assert.Equal(0, animatable.Get("TranslateX"));
        Assert.Equal(0, animatable.Get("TranslateY"));
        Assert.Equal(0, animatable.Get("Rotate"));

        animatable.Set("TranslateX", 100);
        animatable.Set("TranslateY", 150);
        animatable.Set("Rotate", 45);

        Assert.Equal(20, animatable.Animations["TranslateX"].Duration);
        Assert.Equal(30, animatable.Animations["TranslateY"].Duration);
        Assert.Equal(40, animatable.Animations["Rotate"].Duration);

        Frames.Advance(50, 16);

        Assert.Equal(100, animatable.Get("TranslateX"));
        Assert.Equal(150, animatable.Get("TranslateY"));
        Assert.Equal(45, animatable.Get("Rotate"));
    }

    [Fact]
    public void GetAndAnimateAnimatableComplexValues()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new() { ["BackgroundColor"] = 20 });

        Assert.Equal<double[]>([255, 255, 255, 1], animatable.GetChannels("BackgroundColor"));

        animatable.Set("BackgroundColor", [100, 200, 0, 1]);

        Assert.Equal(20, animatable.Animations["BackgroundColor"].Duration);

        Frames.Advance(50, 16);

        Assert.Equal<double[]>([100, 200, 0, 1], animatable.GetChannels("BackgroundColor"));
    }

    [Fact]
    public void GetAndSetAnimatableValues()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["TranslateX"] = 0,
            ["TranslateY"] = 0,
            ["Rotate"] = 0,
        });

        Assert.Equal(0, animatable.Get("TranslateX"));
        Assert.Equal(0, animatable.Get("TranslateY"));
        Assert.Equal(0, animatable.Get("Rotate"));

        animatable.Set("TranslateX", 100).Set("TranslateY", 150).Set("Rotate", 45);

        Assert.Equal(100, animatable.Get("TranslateX"));
        Assert.Equal(150, animatable.Get("TranslateY"));
        Assert.Equal(45, animatable.Get("Rotate"));
    }

    [Fact]
    public void DefinesCustomUnitsForAnimatableValues()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["TranslateX"] = new() { Unit = "em" },
            ["TranslateY"] = new() { Unit = "rem" },
            ["Rotate"] = new() { Unit = "rad", Duration = 50 },
            Duration = 40,
        });

        Assert.Equal(0, animatable.Get("TranslateX"));
        Assert.Equal(0, animatable.Get("TranslateY"));
        Assert.Equal(0, animatable.Get("Rotate"));

        animatable.Set("TranslateX", 10).Set("TranslateY", 15).Set("Rotate", 1);

        Frames.Advance(60, 16);

        Assert.Equal(10, animatable.Get("TranslateX"));
        Assert.Equal(15, animatable.Get("TranslateY"));
        Assert.Equal(1, animatable.Get("Rotate"));
        Assert.Equal("10em", Presto.Get(target, "TranslateX"));
        Assert.Equal("15rem", Presto.Get(target, "TranslateY"));
        Assert.Equal("1rad", Presto.Get(target, "Rotate"));
    }

    [Fact]
    public void AnimatableOnBeginCallbacks()
    {
        var began = 0;
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new()
        {
            ["TranslateX"] = 10,
            ["TranslateY"] = 10,
            ["Rotate"] = 5,
            OnBegin = _ => began++,
        });

        animatable.Set("TranslateX", 100).Set("TranslateY", 100).Set("Rotate", 100);
        Frames.Advance(20, 16);
        animatable.Set("TranslateX", -100).Set("TranslateY", -100).Set("Rotate", -100);
        Frames.Advance(20, 16);

        Assert.Equal(2, began);
    }

    [Fact]
    public void AnimatableOnCompleteCallbacks()
    {
        var completes = 0;
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new()
        {
            ["TranslateX"] = 40,
            ["TranslateY"] = 40,
            ["Rotate"] = 5,
            OnComplete = _ => completes++,
        });

        animatable.Set("TranslateX", 100).Set("TranslateY", 100).Set("Rotate", 100);
        Frames.Advance(20, 16);
        animatable.Set("TranslateX", -100).Set("TranslateY", -100).Set("Rotate", -100);
        Frames.Advance(60, 16);
        animatable.Set("TranslateX", -50).Set("TranslateY", -50).Set("Rotate", -50);
        Frames.Advance(60, 16);

        Assert.Equal(2, completes);
    }

    [Fact]
    public void AnimatableOnUpdateCallbacks()
    {
        var updates = 0;
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new()
        {
            ["TranslateX"] = 20,
            ["TranslateY"] = 20,
            ["Rotate"] = 5,
            OnUpdate = _ => updates++,
        });

        animatable.Set("TranslateX", 100).Set("TranslateY", 100).Set("Rotate", 100);
        Frames.Advance(20, 16);
        animatable.Set("TranslateX", -100).Set("TranslateY", -100).Set("Rotate", -100);
        Frames.Advance(20, 16);

        Assert.InRange(updates, 2, 5);
    }

    [Fact]
    public void StaggersAnimatableDurationsPerTarget()
    {
        IndexedTarget[] targets = [new(), new(), new()];
        var animatable = Presto.CreateAnimatable(targets, new() { ["TranslateX"] = new() { Duration = Presto.Stagger(10) } });

        var animation = animatable.Animations["TranslateX"];
        Assert.Equal(20, animation.Duration);
        Assert.Equal(10, TestHelpers.GetTweenAtIndex(animation, 1)!._changeDuration);
        Assert.Equal(20, TestHelpers.GetTweenAtIndex(animation, 2)!._changeDuration);
    }

    [Fact]
    public void OverrideAnAnimatableDurationAndEaseOnSet()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new() { ["TranslateX"] = 20 });

        animatable.Set("TranslateX", 100, 60, "linear");

        Assert.Equal(60, animatable.Animations["TranslateX"].Duration);

        Frames.Advance(18); // Resume backdates the start time by 12 ms, so this frame lands on the tween's midpoint.

        Assert.Equal(50, animatable.Get("TranslateX"));
    }

    [Fact]
    public void RevertAnAnimatable()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["TranslateX"] = new() { Unit = "em" },
            ["TranslateY"] = new() { Unit = "rem" },
            ["Rotate"] = new() { Unit = "rad", Duration = 50 },
            Duration = 40,
        });

        Assert.Single(animatable.Targets);

        animatable.Set("TranslateX", 10);
        animatable.Revert();

        Assert.Empty(animatable.Targets);
        Assert.Empty(animatable.Animations);
        Assert.Equal("0px", target.TranslateX);
    }
}
