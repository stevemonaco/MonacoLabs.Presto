using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

public sealed class ClockTests
{
    private sealed class TestClock(double initTime = 0) : Clock(initTime);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FpsZeroOrNegativeIsUncapped(double fps)
    {
        var clock = new TestClock { Fps = fps };

        Assert.Equal(Constants.MaxFps, clock.Fps);
        Assert.Equal(Constants.K / Constants.MaxFps, clock._frameDuration);
    }

    [Fact]
    public void FpsHigherThanDefaultFrameRateRaisesDefault()
    {
        try
        {
            var clock = new TestClock { Fps = 60 };
            Assert.Equal(Constants.MaxFps, Presto.Engine.Defaults.FrameRate);

            clock.Fps = 300;
            Assert.Equal(300, Presto.Engine.Defaults.FrameRate);
        }
        finally
        {
            Presto.Engine.Defaults = new PrestoDefaults();
        }
    }

    [Fact]
    public void SpeedClampsToMinValue()
    {
        var clock = new TestClock { Speed = -1 };

        Assert.Equal(Constants.MinValue, clock.Speed);

        clock.Speed = 2;
        Assert.Equal(2, clock.Speed);
    }

    [Fact]
    public void RequestTickReturnsNoneWhenFrameArrivesTooEarly()
    {
        var clock = new TestClock { Fps = 60 };

        Assert.Equal(TickMode.None, clock.RequestTick(12));
        Assert.Equal(0, clock._lastTickTime);
    }

    [Fact]
    public void RequestTickReturnsAutoWithinTolerance()
    {
        var clock = new TestClock { Fps = 60 };

        Assert.Equal(TickMode.Auto, clock.RequestTick(15));
        Assert.Equal(15, clock._lastTickTime);
    }

    [Fact]
    public void RequestTickAlignsLastTickTimeToFrameGrid()
    {
        var clock = new TestClock { Fps = 60 };
        var frameDuration = Constants.K / 60;

        Assert.Equal(TickMode.Auto, clock.RequestTick(40));
        Assert.Equal(2 * frameDuration, clock._lastTickTime, 1e-9);
    }

    [Fact]
    public void ComputeDeltaTimeSetsDeltaTimeAndAdvancesLastTime()
    {
        var clock = new TestClock(10);

        Assert.Equal(6, clock.ComputeDeltaTime(16));
        Assert.Equal(6, clock.DeltaTime);
        Assert.Equal(16, clock._lastTime);

        Assert.Equal(24, clock.ComputeDeltaTime(40));
        Assert.Equal(24, clock.DeltaTime);
        Assert.Equal(40, clock._lastTime);
    }
}
