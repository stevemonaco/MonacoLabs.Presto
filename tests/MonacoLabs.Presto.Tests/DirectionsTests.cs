namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js directions.test.js; the suite's transform string is a plain double member here.</summary>
public sealed class DirectionsTests : EngineTestBase
{
    [Fact]
    public void DirectionNormalShouldUpdateFrom0To1()
    {
        var target = new StyleTarget();
        double? completedValue = null;
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Duration = 10,
            OnComplete = _ => completedValue = target.X,
        });
        animation.Seek(0);
        Assert.Equal(0, target.X);

        RunUntil(() => completedValue != null);

        Assert.Equal(100, completedValue);
    }

    [Fact]
    public void DirectionReverseShouldUpdateFrom1To0()
    {
        var target = new StyleTarget();
        double? completedValue = null;
        bool? backwardsWhilePlaying = null;
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Reversed = true,
            Duration = 10,
            OnUpdate = self => backwardsWhilePlaying = self.Backwards,
            OnComplete = _ => completedValue = target.X,
        });
        animation.Seek(0);
        Assert.Equal(100, target.X);

        RunUntil(() => completedValue != null);

        Assert.Equal(0, completedValue);
        Assert.True(backwardsWhilePlaying);
    }

    [Fact]
    public void ManuallyReversedInfiniteAnimationShouldUpdate()
    {
        var loopCounts = 0;
        double? currentTimeAtSecondLoop = null;
        bool? pausedAtSecondLoop = null;
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = 100,
            Reversed = false,
            Loop = true,
            Duration = 20,
            OnLoop = self =>
            {
                loopCounts++;
                if (loopCounts == 2)
                {
                    self.Pause();
                    currentTimeAtSecondLoop = self.CurrentTime;
                    pausedAtSecondLoop = self.Paused;
                }
            },
        });
        animation.Reverse();

        RunUntil(() => loopCounts >= 2);

        Assert.Equal(2, loopCounts);
        Assert.True(currentTimeAtSecondLoop >= 40);
        Assert.True(pausedAtSecondLoop);
    }

    [Fact]
    public void DirectionAlternateShouldUpdateFrom0To1WhenNoLoopParameterIsSpecified()
    {
        var target = new StyleTarget();
        double? completedValue = null;
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Alternate = true,
            Duration = 10,
            OnComplete = _ => completedValue = target.X,
        });
        animation.Seek(0);
        Assert.Equal(0, target.X);

        RunUntil(() => completedValue != null);

        Assert.Equal(100, completedValue);
    }

    [Fact]
    public void DirectionAlternateShouldUpdateFrom0To1Then1To0WhenALoopParameterIsSpecified()
    {
        var target = new StyleTarget();
        double? completedValue = null;
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Alternate = true,
            Duration = 10,
            Loop = 3,
            OnComplete = _ => completedValue = target.X,
        });
        animation.Seek(0);
        Assert.Equal(0, target.X);

        RunUntil(() => completedValue != null);

        Assert.Equal(0, completedValue);
    }

    [Fact]
    public void InfiniteLoopWithDirectionAlternateShouldUpdateFrom0To1Then1To0()
    {
        var target = new StyleTarget();
        var loopCounts = 0;
        double? valueAtFourthLoop = null;
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Alternate = true,
            Duration = 50,
            Ease = "linear",
            Loop = LoopCount.Infinite,
            OnLoop = self =>
            {
                loopCounts++;
                if (loopCounts == 4)
                {
                    valueAtFourthLoop = target.X;
                    self.Pause();
                }
            },
        });
        animation.Seek(0);
        Assert.Equal(0, target.X);

        RunUntil(() => valueAtFourthLoop != null);

        Assert.True(valueAtFourthLoop < 50);
    }

    [Fact]
    public void DirectionAlternateReverseShouldUpdateFrom1To0WhenNoLoopParameterIsSpecified()
    {
        var target = new StyleTarget();
        double? completedValue = null;
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Alternate = true,
            Reversed = true,
            Duration = 10,
            OnComplete = _ => completedValue = target.X,
        });
        animation.Seek(0);
        Assert.Equal(100, target.X);

        RunUntil(() => completedValue != null);

        Assert.Equal(0, completedValue);
    }

    [Fact]
    public void DirectionAlternateReverseShouldUpdateFrom1To0Then0To1WhenALoopParameterIsSpecified()
    {
        var target = new StyleTarget();
        double? completedValue = null;
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Alternate = true,
            Reversed = true,
            Duration = 10,
            Loop = 3,
            OnComplete = _ => completedValue = target.X,
        });
        animation.Seek(0);
        Assert.Equal(100, target.X);

        RunUntil(() => completedValue != null);

        Assert.Equal(100, completedValue);
    }

    [Fact]
    public void InfiniteLoopWithDirectionAlternateReverseShouldUpdateFrom1To0Then0To1()
    {
        var target = new StyleTarget();
        var loopCounts = 0;
        double? valueAtFourthLoop = null;
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Alternate = true,
            Reversed = true,
            Duration = 50,
            Loop = LoopCount.Infinite,
            OnLoop = self =>
            {
                loopCounts++;
                if (loopCounts == 4)
                {
                    valueAtFourthLoop = target.X;
                    self.Pause();
                }
            },
        });
        animation.Seek(0);
        Assert.Equal(100, target.X);

        RunUntil(() => valueAtFourthLoop != null);

        Assert.True(valueAtFourthLoop > 50);
    }

    [Fact]
    public void BackwardsCombinesReversedAlternationAndTheParentsDirection()
    {
        var reversed = Presto.CreateTimer(new() { Duration = 100, Reversed = true });
        var alternating = Presto.CreateTimer(new() { Duration = 100, Loop = 1, Alternate = true });
        var forwards = Presto.CreateTimer(new() { Duration = 100 });
        var tl = Presto.CreateTimeline(new() { Reversed = true })
            .Add(new TimerParams { Duration = 100 });
        var child = TestHelpers.GetChildAtIndex(tl, 0)!;

        Frames.Advance(48, 16);
        Assert.True(reversed.Backwards);
        Assert.False(alternating.Backwards);
        Assert.False(forwards.Backwards);
        Assert.True(child.Backwards);

        Frames.Advance(100, 16);
        Assert.True(alternating.Backwards);
    }
}
