namespace MonacoLabs.Presto.Tests;

public sealed class ParticleSystemTests
{
    private static readonly EmitterParams Straight = new()
    {
        Burst = 1,
        Speed = (0.4, 0.4),
        Angle = 30,
        Spread = 0,
        Lifetime = (10000, 10000),
        Seed = 1,
    };

    [Fact]
    public void BurstSpawnsItsCount()
    {
        var system = new ParticleSystem(new() { Burst = 40, Seed = 1 });

        system.Start();

        Assert.Equal(40, system.Live);
        Assert.Equal(40, system.Particles.Length);
    }

    [Fact]
    public void NeverExceedsMaxParticles()
    {
        var system = new ParticleSystem(new() { Burst = 80, Rate = 1000, MaxParticles = 50, Seed = 1 });

        system.Start();
        Assert.Equal(50, system.Live);
        for (var i = 0; i < 100; i++)
        {
            system.Step(16);
            Assert.True(system.Live <= 50);
        }
    }

    [Fact]
    public void SpawnBudgetDropsSpawns()
    {
        var system = new ParticleSystem(new() { Burst = 20, Seed = 1 }) { SpawnBudget = 5 };

        system.Start();

        Assert.Equal(5, system.Live);
    }

    [Fact]
    public void SameSeedAndStepsGiveIdenticalStates()
    {
        var parameters = new EmitterParams { Burst = 30, Rate = 200, Gravity = .001, Drag = 2, Spin = (-1, 1), Flutter = 3, Seed = 7 };
        var a = Run(parameters);
        var b = Run(parameters);
        var c = Run(parameters with { Seed = 8 });

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);

        static ParticleState[] Run(EmitterParams parameters)
        {
            var system = new ParticleSystem(parameters, 100, 200);
            system.Start();
            double[] steps = [16, 7, 33, 16, 16, 4];
            foreach (var dt in steps) system.Step(dt);
            return system.Particles.ToArray();
        }
    }

    [Fact]
    public void WithoutForcesParticleTravelsSpeedTimesTimeAlongItsAngle()
    {
        var system = new ParticleSystem(Straight, 10, 20);
        system.Start();

        for (var i = 0; i < 25; i++) system.Step(20);

        var p = system.Particles[0];
        var angle = Utils.DegToRad(30);
        Assert.Equal(10 + Math.Cos(angle) * .4 * 500, p.X, 1e-9);
        Assert.Equal(20 + Math.Sin(angle) * .4 * 500, p.Y, 1e-9);
    }

    [Fact]
    public void GravityFollowsTheClosedFormParabola()
    {
        const double g = .002;
        var system = new ParticleSystem(Straight with { Angle = -60, Gravity = g });
        system.Start();
        var angle = Utils.DegToRad(-60);

        for (var t = 1; t <= 1000; t++)
        {
            system.Step(1);
            var p = system.Particles[0];
            Assert.Equal(Math.Cos(angle) * .4 * t, p.X, 1e-6);
            Assert.Equal(Math.Sin(angle) * .4 * t + g * t * t / 2, p.Y, 1e-6);
        }
    }

    [Fact]
    public void DragHalvesSpeedAfterLn2OverDragSecondsAtAnyStepSize()
    {
        var drag = Math.Log(2);

        var at60 = SpeedAfterOneSecond(60);
        var at144 = SpeedAfterOneSecond(144);

        Assert.Equal(.2, at60, 1e-9);
        Assert.Equal(at60, at144, at60 * .01);

        double SpeedAfterOneSecond(int steps)
        {
            var system = new ParticleSystem(Straight with { Drag = drag });
            system.Start();
            for (var i = 0; i < steps; i++) system.Step(1000.0 / steps);
            var p = system.Particles[0];
            return Math.Sqrt(p.VelocityX * p.VelocityX + p.VelocityY * p.VelocityY);
        }
    }

    [Fact]
    public void CurvesSampleFromEasedMidpointAndTo()
    {
        var system = new ParticleSystem(Straight with
        {
            Lifetime = (1000, 1000),
            Size = new(4, 12, Eases.OutQuad),
            Opacity = new(1, 0, "inQuad"),
        });
        system.Start();

        Assert.Equal(4, system.Particles[0].Size);
        Assert.Equal(1, system.Particles[0].Opacity);

        system.Step(500);
        Assert.Equal(4 + 8 * Eases.OutQuad(.5), system.Particles[0].Size, 1e-9);
        Assert.Equal(1 - Eases.InQuad(.5), system.Particles[0].Opacity, 1e-9);

        system.Step(499.999999);
        Assert.Equal(12, system.Particles[0].Size, 1e-6);
        Assert.Equal(0, system.Particles[0].Opacity, 1e-6);
    }

    [Fact]
    public void ColorMixesTowardEndColor()
    {
        var system = new ParticleSystem(Straight with
        {
            Lifetime = (1000, 1000),
            Colors = [new(200, 0, 100)],
            ColorMix = new(0, 1),
            EndColor = new(0, 200, 100, 0),
        });
        system.Start();

        system.Step(500);

        Assert.Equal(new(100, 100, 100, .5), system.Particles[0].Color);
        Assert.Equal(new(200, 0, 100), system.Particles[0].BaseColor);
    }

    [Fact]
    public void ParticleDiesAtItsLifetimeAndItsSlotIsReused()
    {
        var system = new ParticleSystem(Straight with { Burst = 2, Lifetime = (100, 100), MaxParticles = 2 });
        system.Start();

        system.Step(99);
        Assert.Equal(2, system.Live);
        system.Step(1);
        Assert.Equal(0, system.Live);

        system.Start();
        Assert.Equal(2, system.Live);
        Assert.Equal(0, system.Particles[0].Age);
    }

    [Fact]
    public void RateEmitsForDurationAccumulatingFractions()
    {
        var system = new ParticleSystem(Straight with { Rate = 100, Duration = 1000 });
        system.Start();
        Assert.Equal(1, system.Live);

        for (var i = 0; i < 50; i++) system.Step(25);

        Assert.Equal(101, system.Live);
    }

    [Fact]
    public void IsFinishedOnceEmissionStoppedAndNoneIsLive()
    {
        var system = new ParticleSystem(Straight with { Burst = 3, Rate = 50, Duration = 200, Lifetime = (300, 300) });
        system.Start();

        system.Step(200);
        Assert.False(system.IsFinished);
        system.Step(200);
        Assert.False(system.IsFinished);
        system.Step(200);
        Assert.True(system.IsFinished);
    }

    [Fact]
    public void StopEndsEmissionButLetsLiveParticlesFinish()
    {
        var system = new ParticleSystem(Straight with { Rate = 100, Lifetime = (300, 300) });
        system.Start();
        system.Step(100);
        var live = system.Live;

        system.Stop();
        system.Step(100);

        Assert.Equal(live, system.Live);
        Assert.False(system.IsFinished);
        system.Step(300);
        Assert.True(system.IsFinished);
    }

    [Fact]
    public void ClearRemovesLiveParticlesAtOnce()
    {
        var system = new ParticleSystem(Straight with { Burst = 10 });
        system.Start();

        system.Clear();

        Assert.Equal(0, system.Live);
        Assert.True(system.IsFinished);
    }
}
