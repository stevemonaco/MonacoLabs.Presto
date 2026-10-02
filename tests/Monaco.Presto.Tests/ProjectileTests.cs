using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>A <see cref="Projectile"/> to-value resolved through real animations, with no upstream equivalent.</summary>
public sealed class ProjectileTests : EngineTestBase
{
    private static Animation Throw(StyleTarget target, Projectile projectile, double duration = 1000) =>
        Presto.Animate(target, new() { ["X"] = projectile, Duration = duration, Autoplay = false });

    [Fact]
    public void ConstantVelocityWithoutGravityMovesLinearly()
    {
        var target = new StyleTarget { X = 10 };
        var animation = Throw(target, new Projectile(0.5));

        foreach (var t in new double[] { 100, 400, 1000 })
        {
            animation.Seek(t);
            Assert.Equal(10 + 0.5 * t, target.X, 1e-6);
        }
    }

    [Fact]
    public void GravityFollowsTheBallisticFormula()
    {
        var target = new StyleTarget { X = 20 };
        var animation = Throw(target, new Projectile(0.3) { Acceleration = 0.002 });

        foreach (var t in new double[] { 16, 250, 600, 1000 })
        {
            animation.Seek(t);
            Assert.Equal(20 + 0.3 * t + 0.002 * t * t / 2, target.X, 1e-6);
        }
    }

    [Fact]
    public void FrictionReducesTheTravel()
    {
        var free = new StyleTarget();
        var damped = new StyleTarget();
        Throw(free, new Projectile(1)).Complete();
        Throw(damped, new Projectile(1) { Friction = 0.002 }).Complete();

        Assert.Equal(1000, free.X, 1e-9);
        Assert.InRange(damped.X, 1, 999);
    }

    [Fact]
    public void ATossLandingOnItsStartArcsAndEndsExactlyThere()
    {
        var target = new StyleTarget { X = 40 };
        var animation = Presto.Animate(target, new() { ["X"] = new Projectile(-1) { Acceleration = 1.0 / 512 }, Duration = 1024 });
        var peak = 40.0;

        RunUntil(() =>
        {
            peak = Math.Min(peak, target.X);
            return animation.Completed;
        });

        Assert.Equal(2, TestHelpers.GetTweenCount(animation));
        Assert.Equal(40 - 256, peak, 1);
        Assert.Equal(40, target.X);
    }

    [Fact]
    public void ArcSeeksMatchTheBallisticFormulaOnBothSidesOfTheApex()
    {
        var target = new StyleTarget();
        var animation = Throw(target, new Projectile(-0.8) { Acceleration = 0.002 });

        foreach (var t in new double[] { 100, 400, 600, 1000 })
        {
            animation.Seek(t);
            Assert.Equal(-0.8 * t + 0.002 * t * t / 2, target.X, 1e-6);
        }
    }

    [Fact]
    public void LaunchStraightDownHasNoHorizontalVelocity()
    {
        var (x, y) = Projectile.Launch(2, 90, 0.001, 0.01);

        Assert.Equal(0, x.Velocity);
        Assert.Equal(0, x.Acceleration);
        Assert.Equal(2, y.Velocity);
        Assert.Equal(0.001, y.Acceleration);
        Assert.Equal(0.01, y.Friction);
    }

    [Fact]
    public void ProjectileDurationOverridesTheTweenDuration()
    {
        var animation = Throw(new StyleTarget(), new Projectile(1) { Duration = 300 });

        Assert.Equal(300, animation.Duration, 1e-9);
    }

    [Fact]
    public void AKeyframeOrFromToPairThrows()
    {
        Assert.Throws<ArgumentException>(() => Presto.Animate(new StyleTarget(), new() { ["X"] = TweenValue.Keyframes(0, new Projectile(1), 50) }));
        Assert.Throws<ArgumentException>(() => Presto.Animate(new StyleTarget(), new() { ["X"] = TweenValue.FromTo(0, new Projectile(1)) }));
        Assert.Throws<ArgumentException>(() => Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = new TweenParams[] { new() { To = 50 }, new() { To = new Projectile(1) } },
        }));
    }

    [Fact]
    public void AColourPropertyThrows() =>
        Assert.Throws<ArgumentException>(() => Presto.Animate(new StyleTarget(), new() { ["BackgroundColor"] = new Projectile(1) }));

    [Fact]
    public void SecondsModeGivesTheSameTrajectoryScaled()
    {
        double[] times = [100, 300, 700, 1000];
        var millis = new StyleTarget();
        var inMillis = Throw(millis, new Projectile(-0.8) { Acceleration = 0.002, Friction = 0.001 });
        var expected = times.Select(t => { inMillis.Seek(t); return millis.X; }).ToArray();

        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var seconds = new StyleTarget();
        var inSeconds = Throw(seconds, new Projectile(-800) { Acceleration = 2000, Friction = 0.001 }, duration: 1);
        var actual = times.Select(t => { inSeconds.Seek(t / 1000); return seconds.X; }).ToArray();

        Assert.Equal(inMillis.Duration / 1000, inSeconds.Duration, 1e-9);
        for (var i = 0; i < times.Length; i++) Assert.Equal(expected[i], actual[i], 1e-6);
    }

    [Fact]
    public void ReduceAllLandsOnTheEndAtOnce()
    {
        Presto.Defaults.ReduceAll = true;
        var target = new StyleTarget();

        var animation = Presto.Animate(target, new() { ["X"] = new Projectile(-0.8) { Acceleration = 0.002 }, Duration = 1000 });
        Frames.Advance(16);

        Assert.Equal(200, target.X, 1e-6);
        Assert.True(animation.Completed);
    }
}
