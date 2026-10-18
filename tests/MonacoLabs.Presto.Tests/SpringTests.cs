namespace MonacoLabs.Presto.Tests;

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

        Assert.Equal(stiffness, spring.Stiffness, 1e-3);
        Assert.NotEqual(damping, spring.Damping);
        Assert.Equal(.1, spring.DampingRatio, 1e-3);
        Assert.NotEqual(settlingDuration, spring.SettlingDuration);
    }

    [Fact]
    public void SettingMassRederivesBounceAndDuration()
    {
        var spring = new Spring(bounce: .3, duration: 800);
        var (stiffness, damping) = (spring.Stiffness, spring.Damping);

        spring.Mass = 4;

        Assert.Equal((stiffness, damping), (spring.Stiffness, spring.Damping));
        Assert.Equal(1 - spring.DampingRatio, spring.Bounce, 1e-3);
        Assert.Equal(1600, spring.Duration, 1);

        spring.Stiffness = stiffness * 4;
        Assert.Equal(800, spring.Duration, 1);
        spring.Damping = damping * 4;
        Assert.Equal(.3, spring.Bounce, 1e-3);
    }

    [Fact]
    public void SettingBounceOrDurationKeepsMassAndVelocity()
    {
        var spring = new Spring(bounce: .3, duration: 800) { Mass = 3, Velocity = 2 };

        spring.Bounce = .3;
        Assert.Equal((3, 2), (spring.Mass, spring.Velocity));
        spring.Duration = 800;

        Assert.Equal((3, 2), (spring.Mass, spring.Velocity));
        Assert.Equal((.3, 800), (spring.Bounce, spring.Duration));
        Assert.Equal(3 * Math.Pow(2 * Math.PI / .8, 2), spring.Stiffness, 1e-3);
        Assert.Equal(.7, spring.DampingRatio, 1e-3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void PhysicalAndDesignerFormsRoundTrip(double mass)
    {
        var physical = new Spring(mass, stiffness: 200, damping: 15);
        Assert.Equal(2 * Math.PI * Math.Sqrt(mass / 200) * 1000, physical.Duration, 1e-3);
        Assert.Equal(Math.Round(1 - physical.DampingRatio, 3), physical.Bounce);

        var designer = new Spring(mass, bounce: physical.Bounce, duration: physical.Duration);
        Assert.Equal(200, designer.Stiffness, .01);
        Assert.Equal(15, designer.Damping, .05);

        var back = new Spring(mass, designer.Stiffness, designer.Damping);
        Assert.Equal(physical.Bounce, back.Bounce, 1e-3);
        Assert.Equal(physical.Duration, back.Duration, .01);
    }

    [Theory]
    [InlineData(1, 100, 10, .5)]
    [InlineData(4, 100, 40, 1)]
    [InlineData(1, 100, 40, 2)]
    public void DampingRatioIsDampingOverCriticalDamping(double mass, double stiffness, double damping, double ratio)
    {
        var spring = new Spring(1, 100, 10) { Mass = mass, Stiffness = stiffness, Damping = damping };
        Assert.Equal(ratio, spring.DampingRatio, 1e-12);
    }

    // CalculateSDFromBD rounds stiffness and damping to 3 decimals, which leaves zero bounce a hair off exactly 1.
    [Fact]
    public void BounceSignPicksTheDampingRegime()
    {
        Assert.Equal(1, new Spring(bounce: 0, duration: 500).DampingRatio, 1e-3);
        Assert.True(new Spring(bounce: .4).DampingRatio < 1);
        Assert.True(new Spring(bounce: -.4).DampingRatio > 1);
    }

    [Fact]
    public void SecondsModeScalesSettlingDuration()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        Assert.Equal(1.76, Eases.Spring().SettlingDuration, 1e-9);
    }

    [Fact]
    public void TheDefaultDurationFollowsTheTimeUnit()
    {
        var bounceOnly = new Spring(bounce: .4);
        Assert.Equal((.5, 628.319), (new Spring().Bounce, new Spring().Duration));
        Presto.Engine.TimeUnit = TimeUnit.Seconds;

        Assert.Equal(.628, new Spring().Duration, 12);
        var inSeconds = new Spring(bounce: .4);
        Assert.Equal(bounceOnly.Stiffness, inSeconds.Stiffness, 3);
        Assert.Equal(bounceOnly.SettlingDuration / 1000, inSeconds.SettlingDuration, 3);
    }

    [Fact]
    public void PresetsHaveTheirBounceAndDurationAndAreNewOnEachRead()
    {
        Assert.Equal((0, 500), (Springs.Gentle.Bounce, Springs.Gentle.Duration));
        Assert.Equal((.15, 300), (Springs.Snappy.Bounce, Springs.Snappy.Duration));
        Assert.Equal((.35, 500), (Springs.Bouncy.Bounce, Springs.Bouncy.Duration));
        Assert.Equal((0, 250), (Springs.Stiff.Bounce, Springs.Stiff.Duration));
        Assert.NotSame(Springs.Gentle, Springs.Gentle);
    }

    [Fact]
    public void PresetsKeepTheirPerceivedDurationInSecondsMode()
    {
        var milliseconds = Springs.Gentle.SettlingDuration;
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var gentle = Springs.Gentle;
        Assert.Equal(.5, gentle.Duration, 1e-12);
        Assert.Equal(milliseconds / 1000, gentle.SettlingDuration, 3);
    }

    [Fact]
    public void SamplingThePureEaseLeavesCompletionAlone()
    {
        var calls = 0;
        var spring = Eases.Spring(onComplete: _ => calls++);

        for (var t = 0.0; t <= 1; t += .05) spring.PureEase(t);
        Assert.False(spring.Completed);

        spring.Ease(1);
        spring.PureEase(.01);
        Assert.True(spring.Completed);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OnlyAnAnimationsOwnSpringIsParentedToIt()
    {
        var own = Eases.Spring();
        var perTween = Eases.Spring();
        var perTarget = Eases.Spring();

        var animation = Presto.Animate(new StyleTarget(), EasingParams(own));
        Presto.Animate(new StyleTarget(), new()
        {
            ["Opacity"] = new TweenParams { To = 1, Ease = perTween },
            ["TranslateX"] = new TweenParams { To = 100, Ease = new FunctionValue((_, _, _) => perTarget) },
            Autoplay = false,
        });

        Assert.Same(animation, own.Parent);
        Assert.Null(perTween.Parent);
        Assert.Null(perTarget.Parent);
    }

    [Fact]
    public void SwitchingTheTimeUnitDoesNotRescaleAnExistingSpring()
    {
        var spring = new Spring();
        Presto.Engine.TimeUnit = TimeUnit.Seconds;

        Assert.Equal(1760, spring.SettlingDuration);
        Assert.Equal(628.319, spring.Duration);

        spring.Velocity = 0;
        Assert.Equal(1.76, spring.SettlingDuration, 1e-9);
        Assert.Equal(.628, spring.Duration, 12);
    }

    [Fact]
    public void ASpringWithoutOnCompleteCompletesSilently()
    {
        var spring = new Spring();

        spring.Ease(1);

        Assert.Null(spring.OnComplete);
        Assert.True(spring.Completed);
    }
}
