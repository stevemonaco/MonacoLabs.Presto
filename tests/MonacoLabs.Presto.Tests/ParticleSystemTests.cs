using MonacoLabs.Presto.Values;

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
    public void ConstructorRejectsNegativeMaxParticlesAndNonPositiveOrInvertedLifetime()
    {
        Assert.Throws<ArgumentOutOfRangeException>("parameters", () => new ParticleSystem(Straight with { MaxParticles = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>("parameters", () => new ParticleSystem(Straight with { Lifetime = (0, 100) }));
        Assert.Throws<ArgumentOutOfRangeException>("parameters", () => new ParticleSystem(Straight with { Lifetime = (200, 100) }));

        var empty = new ParticleSystem(Straight with { MaxParticles = 0 });
        empty.Start();
        Assert.Equal(0, empty.Live);
    }

    [Fact]
    public void StepRejectsNegativeDtAndAcceptsZero()
    {
        var system = new ParticleSystem(Straight);
        system.Start();

        Assert.Throws<ArgumentOutOfRangeException>("dt", () => system.Step(-1));
        system.Step(0);
        Assert.Equal(0, system.Particles[0].Age);
    }

    [Fact]
    public void ANewSystemIsStoppedAndFinished()
    {
        var system = new ParticleSystem(new() { Rate = 1000, Burst = 5, Seed = 1 });

        Assert.Equal(0, system.Live);
        Assert.True(system.IsFinished);

        system.Step(100);

        Assert.Equal(0, system.Live);
        Assert.True(system.IsFinished);
    }

    [Fact]
    public void ParticlesSpawnUniformlyWithinTheAreaCenteredOnTheOrigin()
    {
        var system = new ParticleSystem(Straight with { Burst = 500, MaxParticles = 500, Area = (100, 40) }, 10, 20);
        system.Start();

        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var p in system.Particles)
        {
            xs.Add(p.X);
            ys.Add(p.Y);
        }

        Assert.All(xs, x => Assert.InRange(x, -40, 60));
        Assert.All(ys, y => Assert.InRange(y, 0, 40));
        Assert.True(xs.Min() < -35 && xs.Max() > 55);
        Assert.True(ys.Min() < 2 && ys.Max() > 38);
        Assert.InRange(xs.Average(), 5, 15);
        Assert.InRange(ys.Average(), 18, 22);
    }

    [Fact]
    public void SpinningParticlesStartAtARandomRotationAndEveryParticleAtARandomFlutterPhase()
    {
        var spinning = new ParticleSystem(Straight with { Burst = 50, Spin = (1, 1) });
        var still = new ParticleSystem(Straight with { Burst = 50 });
        spinning.Start();
        still.Start();

        var rotations = new HashSet<double>();
        var phases = new HashSet<double>();
        foreach (var p in spinning.Particles)
        {
            Assert.InRange(p.Rotation, 0, 360);
            rotations.Add(p.Rotation);
        }
        foreach (var p in still.Particles)
        {
            Assert.Equal(0, p.Rotation);
            Assert.InRange(p.FlutterPhase, 0, 2 * Math.PI);
            phases.Add(p.FlutterPhase);
        }

        Assert.Equal(50, rotations.Count);
        Assert.Equal(50, phases.Count);
    }

    [Fact]
    public void RotationAdvancesBySpinAndFlutterPhaseByFlutterTurnsPerSecond()
    {
        var system = new ParticleSystem(Straight with { Spin = (.5, .5), Flutter = 2 });
        system.Start();
        var start = system.Particles[0];

        system.Step(100);
        system.Step(150);

        var p = system.Particles[0];
        Assert.Equal(.5, p.Spin);
        Assert.Equal(start.Rotation + 125, p.Rotation, 1e-9);
        Assert.Equal(start.FlutterPhase + Math.PI, p.FlutterPhase, 1e-9);
    }

    [Fact]
    public void ANullSeedTakesTheNextSeedFromTheCounter()
    {
        var parameters = Straight with { Burst = 10, Spread = 45, Speed = (.1, .9), Seed = null };
        var unseeded = new ParticleSystem(parameters);
        var probe = Utils.CreateSeededRandom();
        double[] draws = [probe(0, 1, 9), probe(0, 1, 9), probe(0, 1, 9)];
        var probeSeed = Enumerable.Range(0, 1_000_000).First(s =>
        {
            var candidate = Utils.CreateSeededRandom(s);
            return candidate(0, 1, 9) == draws[0] && candidate(0, 1, 9) == draws[1] && candidate(0, 1, 9) == draws[2];
        });
        var seeded = new ParticleSystem(parameters with { Seed = probeSeed - 1 });

        unseeded.Start();
        seeded.Start();
        unseeded.Step(16);
        seeded.Step(16);

        Assert.Equal(seeded.Particles.ToArray(), unseeded.Particles.ToArray());
    }

    [Fact]
    public void WithoutABurstStartSpawnsNothingAtOnce()
    {
        var system = new ParticleSystem(new() { Rate = 1000, Seed = 1 });

        system.Start();

        Assert.Equal(0, system.Live);
        system.Step(10);
        Assert.Equal(10, system.Live);
    }

    [Fact]
    public void DefaultLifetimeSpeedAndSpreadDrawFromTheirRanges()
    {
        var system = new ParticleSystem(new() { Burst = 500, Angle = 0, Seed = 3 });
        system.Start();

        var lifetimes = new List<double>();
        var speeds = new List<double>();
        var angles = new List<double>();
        foreach (var p in system.Particles)
        {
            lifetimes.Add(p.Lifetime);
            speeds.Add(Math.Sqrt(p.VelocityX * p.VelocityX + p.VelocityY * p.VelocityY));
            angles.Add(Utils.RadToDeg(Math.Atan2(p.VelocityY, p.VelocityX)));
        }

        Assert.All(lifetimes, l => Assert.InRange(l, 800, 1400));
        Assert.True(lifetimes.Min() < 820 && lifetimes.Max() > 1380);
        Assert.All(speeds, s => Assert.InRange(s, .2 - 1e-12, .6 + 1e-12));
        Assert.True(speeds.Min() < .21 && speeds.Max() > .59);
        Assert.All(angles, a => Assert.InRange(a, -60 - 1e-9, 60 + 1e-9));
        Assert.True(angles.Min() < -58 && angles.Max() > 58);
    }

    [Fact]
    public void DefaultAngleLaunchesUp()
    {
        var system = new ParticleSystem(new() { Burst = 20, Spread = 0, Seed = 3 });
        system.Start();

        foreach (var p in system.Particles)
        {
            Assert.Equal(0, p.VelocityX, 1e-12);
            Assert.True(p.VelocityY < 0);
        }
    }

    [Fact]
    public void WithoutSpinOrFlutterParticlesNeitherRotateNorFlutter()
    {
        var system = new ParticleSystem(new() { Burst = 20, Seed = 3 });
        system.Start();
        var phases = system.Particles.ToArray().Select(p => p.FlutterPhase).ToArray();

        system.Step(100);

        for (var i = 0; i < system.Live; i++)
        {
            Assert.Equal(0, system.Particles[i].Spin);
            Assert.Equal(0, system.Particles[i].Rotation);
            Assert.Equal(phases[i], system.Particles[i].FlutterPhase);
        }
    }

    [Fact]
    public void DefaultSizeOpacityAndColorAreSixUnitsLinearFadeAndOpaqueWhite()
    {
        var system = new ParticleSystem(Straight with { Lifetime = (1000, 1000), Burst = 5 });
        system.Start();

        AssertSampled(1);
        system.Step(250);
        AssertSampled(.75);
        system.Step(250);
        AssertSampled(.5);

        void AssertSampled(double opacity)
        {
            foreach (var p in system.Particles)
            {
                Assert.Equal(6, p.Size);
                Assert.Equal(opacity, p.Opacity, 1e-12);
                Assert.Equal(new PrestoColor(255, 255, 255, 1), p.BaseColor);
                Assert.Equal(p.BaseColor, p.Color);
            }
        }
    }

    [Fact]
    public void DefaultMaxParticlesIsFiveHundred()
    {
        var system = new ParticleSystem(new() { Burst = 600, Seed = 1 });

        system.Start();

        Assert.Equal(500, system.Live);
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
