namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js directions.test.js; transform expectations become IterationProgress on a timer.</summary>
public sealed class DirectionsTests : EngineTestBase
{
    [Fact]
    public void DirectionNormalShouldUpdateFrom0To1()
    {
        double? completedProgress = null;
        var timer = Presto.CreateTimer(new()
        {
            Duration = 10,
            OnComplete = self => completedProgress = self.IterationProgress,
        });
        timer.Seek(0);
        Assert.Equal(0, timer.IterationProgress);

        RunUntil(() => completedProgress != null);

        Assert.Equal(1, completedProgress);
    }

    [Fact]
    public void DirectionReverseShouldUpdateFrom1To0()
    {
        double? completedProgress = null;
        bool? backwardsWhilePlaying = null;
        var timer = Presto.CreateTimer(new()
        {
            Reversed = true,
            Duration = 10,
            OnUpdate = self => backwardsWhilePlaying = self.Backwards,
            OnComplete = self => completedProgress = self.IterationProgress,
        });
        timer.Seek(0);
        Assert.Equal(1, timer.IterationProgress);

        RunUntil(() => completedProgress != null);

        Assert.Equal(0, completedProgress);
        Assert.True(backwardsWhilePlaying);
    }

    [Fact]
    public void ManuallyReversedInfiniteTimerShouldUpdate()
    {
        var loopCounts = 0;
        double? currentTimeAtSecondLoop = null;
        bool? pausedAtSecondLoop = null;
        var timer = Presto.CreateTimer(new()
        {
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
        timer.Reverse();

        RunUntil(() => loopCounts >= 2);

        Assert.Equal(2, loopCounts);
        Assert.True(currentTimeAtSecondLoop >= 40);
        Assert.True(pausedAtSecondLoop);
    }

    [Fact]
    public void DirectionAlternateShouldUpdateFrom0To1WhenNoLoopParameterIsSpecified()
    {
        double? completedProgress = null;
        var timer = Presto.CreateTimer(new()
        {
            Alternate = true,
            Duration = 10,
            OnComplete = self => completedProgress = self.IterationProgress,
        });
        timer.Seek(0);
        Assert.Equal(0, timer.IterationProgress);

        RunUntil(() => completedProgress != null);

        Assert.Equal(1, completedProgress);
    }

    [Fact]
    public void DirectionAlternateShouldUpdateFrom0To1Then1To0WhenALoopParameterIsSpecified()
    {
        double? completedProgress = null;
        var timer = Presto.CreateTimer(new()
        {
            Alternate = true,
            Duration = 10,
            Loop = 3,
            OnComplete = self => completedProgress = self.IterationProgress,
        });
        timer.Seek(0);
        Assert.Equal(0, timer.IterationProgress);

        RunUntil(() => completedProgress != null);

        Assert.Equal(0, completedProgress);
    }

    [Fact]
    public void InfiniteLoopWithDirectionAlternateShouldUpdateFrom0To1Then1To0()
    {
        var loopCounts = 0;
        double? progressAtFourthLoop = null;
        var timer = Presto.CreateTimer(new()
        {
            Alternate = true,
            Duration = 50,
            Loop = LoopCount.Infinite,
            OnLoop = self =>
            {
                loopCounts++;
                if (loopCounts == 4)
                {
                    progressAtFourthLoop = self.IterationProgress;
                    self.Pause();
                }
            },
        });
        timer.Seek(0);
        Assert.Equal(0, timer.IterationProgress);

        RunUntil(() => progressAtFourthLoop != null);

        Assert.True(progressAtFourthLoop < .5);
    }

    [Fact]
    public void DirectionAlternateReverseShouldUpdateFrom1To0WhenNoLoopParameterIsSpecified()
    {
        double? completedProgress = null;
        var timer = Presto.CreateTimer(new()
        {
            Alternate = true,
            Reversed = true,
            Duration = 10,
            OnComplete = self => completedProgress = self.IterationProgress,
        });
        timer.Seek(0);
        Assert.Equal(1, timer.IterationProgress);

        RunUntil(() => completedProgress != null);

        Assert.Equal(0, completedProgress);
    }

    [Fact]
    public void DirectionAlternateReverseShouldUpdateFrom1To0Then0To1WhenALoopParameterIsSpecified()
    {
        double? completedProgress = null;
        var timer = Presto.CreateTimer(new()
        {
            Alternate = true,
            Reversed = true,
            Duration = 10,
            Loop = 3,
            OnComplete = self => completedProgress = self.IterationProgress,
        });
        timer.Seek(0);
        Assert.Equal(1, timer.IterationProgress);

        RunUntil(() => completedProgress != null);

        Assert.Equal(1, completedProgress);
    }

    [Fact]
    public void InfiniteLoopWithDirectionAlternateReverseShouldUpdateFrom1To0Then0To1()
    {
        var loopCounts = 0;
        double? progressAtFourthLoop = null;
        var timer = Presto.CreateTimer(new()
        {
            Alternate = true,
            Reversed = true,
            Duration = 50,
            Loop = LoopCount.Infinite,
            OnLoop = self =>
            {
                loopCounts++;
                if (loopCounts == 4)
                {
                    progressAtFourthLoop = self.IterationProgress;
                    self.Pause();
                }
            },
        });
        timer.Seek(0);
        Assert.Equal(1, timer.IterationProgress);

        RunUntil(() => progressAtFourthLoop != null);

        Assert.True(progressAtFourthLoop > .5);
    }
}
