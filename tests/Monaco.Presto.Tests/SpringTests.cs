namespace Monaco.Presto.Tests;

/// <summary>Ports the spring section of tests/suites/eases.test.js, both by constructing springs directly and through the duration they give an animation.</summary>
public sealed class SpringTests : EngineTestBase
{
    private static AnimationParams EasingParams(Spring spring) =>
        new() { ["Opacity"] = TweenValue.FromTo(0, 1), Ease = spring, Duration = 100, Autoplay = false };

    [Fact]
    public void SpringEaseOverridesTheAnimationDurationParameter() =>
        Assert.True(Presto.Animate(new StyleTarget(), EasingParams(Eases.Spring())).Duration > 1000);

    [Fact]
    public void SpringEaseOverridesTheTweenDurationParameter()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["Opacity"] = TweenValue.FromTo(0, 1),
            ["TranslateX"] = new TweenParams { To = 100, Ease = Eases.Spring(), Duration = 500 },
            Duration = 400,
            Autoplay = false,
        });

        Assert.True(animation.Duration > 1000);
    }

    [Fact]
    public void SpringEaseParametersAffectTheAnimationDuration()
    {
        var target = new StyleTarget();
        Assert.Equal(1760, Presto.Animate(target, EasingParams(Eases.Spring())).Duration);
        Assert.Equal(13700, Presto.Animate(target, EasingParams(Eases.Spring(mass: 10))).Duration);
        Assert.Equal(1760, Presto.Animate(target, EasingParams(Eases.Spring(stiffness: 50))).Duration);
        Assert.Equal(3880, Presto.Animate(target, EasingParams(Eases.Spring(damping: 50))).Duration);
        Assert.Equal(1700, Presto.Animate(target, EasingParams(Eases.Spring(velocity: 10))).Duration);
    }

    [Fact]
    public void SpringEaseParametersAffectSettlingDuration()
    {
        Assert.Equal(1760, Eases.Spring().SettlingDuration);
        Assert.Equal(13700, Eases.Spring(mass: 10).SettlingDuration);
        Assert.Equal(1760, Eases.Spring(stiffness: 50).SettlingDuration);
        Assert.Equal(3880, Eases.Spring(damping: 50).SettlingDuration);
        Assert.Equal(1700, Eases.Spring(velocity: 10).SettlingDuration);
    }

    [Fact]
    public void SettingASpringParameterAfterCreationShouldUpdateItsDuration()
    {
        var spring = Eases.Spring();
        Assert.Equal(1760, spring.SettlingDuration);
        spring.Mass = 10;
        Assert.Equal(13700, spring.SettlingDuration);
        Assert.Equal(10, spring.Mass);
        spring.Mass = 1;
        spring.Stiffness = 50;
        Assert.Equal(1, spring.Mass);
        Assert.Equal(50, spring.Stiffness);
        Assert.Equal(1760, spring.SettlingDuration);
        spring.Stiffness = 100;
        spring.Damping = 50;
        Assert.Equal(100, spring.Stiffness);
        Assert.Equal(50, spring.Damping);
        Assert.Equal(3880, spring.SettlingDuration);
        spring.Damping = 10;
        spring.Velocity = 10;
        Assert.Equal(10, spring.Damping);
        Assert.Equal(10, spring.Velocity);
        Assert.Equal(1700, spring.SettlingDuration);
    }

    [Fact]
    public void SpringParametersMustBeClampedAt10000()
    {
        var spring = Eases.Spring(mass: 15000, stiffness: 15000, damping: 15000, velocity: 15000);
        Assert.Equal(10000, spring.Mass);
        Assert.Equal(10000, spring.Stiffness);
        Assert.Equal(10000, spring.Damping);
        Assert.Equal(10000, spring.Velocity);
        Assert.True(spring.SettlingDuration > 0);

        spring.Mass = 20000;
        spring.Stiffness = 20000;
        spring.Damping = 20000;
        spring.Velocity = 20000;

        Assert.Equal(10000, spring.Mass);
        Assert.Equal(10000, spring.Stiffness);
        Assert.Equal(10000, spring.Damping);
        Assert.Equal(10000, spring.Velocity);
        Assert.True(spring.SettlingDuration > 0);
    }

    [Fact]
    public void SpringVelocityCanBeNegative()
    {
        var spring = Eases.Spring(velocity: -15000);
        Assert.Equal(-10000, spring.Velocity);
        Assert.True(spring.SettlingDuration > 0);
        spring.Velocity = -20000;
        Assert.Equal(-10000, spring.Velocity);
        Assert.True(spring.SettlingDuration > 0);
    }

    [Fact]
    public void EaseIsOneDelegateSharedWithTheImplicitConversion()
    {
        var spring = Eases.Spring();
        Ease converted = spring;
        Assert.Same(spring.Ease, converted);
    }

    [Fact]
    public void OnCompleteFiresOnceWhenSampledPastThePerceivedDuration()
    {
        var calls = new List<Timer?>();
        var spring = Eases.Spring(onComplete: calls.Add);
        var threshold = spring.Duration / spring.SettlingDuration;

        for (var t = 0.0; t <= 1; t += .05) spring.Ease(t);

        Assert.Single(calls);
        Assert.Null(calls[0]);
        Assert.True(spring.Completed);

        spring.Ease(threshold / 2);
        Assert.False(spring.Completed);
        Assert.Single(calls);
    }

    [Fact]
    public void SettingBounceAfterConstructionRederivesStiffnessAndDamping()
    {
        var spring = Eases.Spring();
        var stiffness = spring.Stiffness;
        var damping = spring.Damping;
        var settlingDuration = spring.SettlingDuration;

        spring.Bounce = .9;

        Assert.NotEqual(stiffness, spring.Stiffness);
        Assert.NotEqual(damping, spring.Damping);
        Assert.NotEqual(settlingDuration, spring.SettlingDuration);
    }

    [Fact]
    public void SecondsModeScalesSettlingDuration()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        Assert.Equal(1.76, Eases.Spring().SettlingDuration, 1e-9);
    }
}
