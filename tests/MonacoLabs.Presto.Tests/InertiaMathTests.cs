using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Tests;

/// <summary>The inertia solver's math from INERTIA-PLAN §5, with no upstream equivalent.</summary>
public sealed class InertiaMathTests : EngineTestBase
{
    private const double Tau = 350;
    private const double RestDelta = .5;
    private const double H = 1e-3;

    private static InertiaSolver.Plan Resolve(
        double from, double velocity, double power = 1, double tau = Tau, Func<double, double>? snap = null, double? min = null, double? max = null) =>
        InertiaSolver.Resolve(from, velocity, new Inertia(velocity)
        {
            Power = power, TimeConstant = tau, RestDelta = RestDelta, ModifyTarget = snap, Min = min, Max = max, BounceStiffness = 500, BounceDamping = 10,
        });

    private static double X(InertiaSolver.Plan plan, double from, double t) => from + (plan.To - from) * plan.Ease(t / plan.Duration);

    // The ease divides by 1 - e^(-D/τ) = 1 - RestDelta/|A'| so it lands exactly; this undoes that for the raw decay's slope.
    private static double StartVelocity(InertiaSolver.Plan plan, double from) =>
        (X(plan, from, H) - X(plan, from, -H)) / (2 * H) * (1 - RestDelta / Math.Abs(plan.To - from));

    [Fact]
    public void StartsAtPowerTimesVelocity()
    {
        var plan = Resolve(0, 2, power: 1.5);
        Assert.Equal(3, StartVelocity(plan, 0), 1e-6);
    }

    [Fact]
    public void DecaysWithinRestDeltaByTheDurationAndLandsExactly()
    {
        var plan = Resolve(10, 1.5);
        var travel = plan.To - 10;
        Assert.Equal(10 + 1.5 * Tau, plan.To, 1e-9);
        Assert.True(Math.Abs(travel) * Math.Exp(-plan.Duration / Tau) <= RestDelta + 1e-9);
        Assert.Equal(plan.To, X(plan, 10, plan.Duration));
    }

    [Fact]
    public void DoublingTheTimeConstantDoublesTheTravel() =>
        Assert.Equal(2 * Resolve(0, 1, tau: 200).To, Resolve(0, 1, tau: 400).To, 1e-9);

    [Fact]
    public void NegativeVelocityTravelsBackward()
    {
        var plan = Resolve(100, -1);
        Assert.True(plan.To < 100);
        for (var t = 1.0; t <= plan.Duration; t++) Assert.True(X(plan, 100, t) < X(plan, 100, t - 1));
    }

    [Fact]
    public void VelocityBelowRestSetsTheValue()
    {
        var plan = Resolve(42, RestDelta / Tau * .9);
        Assert.Equal(Constants.MinValue, plan.Duration);
        Assert.Equal(42, plan.To);
    }

    [Fact]
    public void SnapIncrementLandsOnTheNearestMultiple()
    {
        var plan = Resolve(0, 1.2, snap: Utils.Snap(100));
        Assert.Equal(400, plan.To);
        Assert.Equal(400 / Tau, StartVelocity(plan, 0), 1e-6);
    }

    [Fact]
    public void SnapPointsLandOnTheNearestPoint() =>
        Assert.Equal(500, Resolve(0, 1.2, snap: Utils.Snap(new double[] { 0, 120, 500 })).To);

    [Fact]
    public void SnappedBeyondABoundStillHitsTheBound()
    {
        var plan = Resolve(0, 1.2, snap: Utils.Snap(100), max: 350);
        Assert.Equal(350, plan.To);
        Assert.Equal(350, X(plan, 0, plan.Duration));
    }

    [Fact]
    public void BoundCrossingIsContinuousThenPeaksPastAndSettlesOnTheBound()
    {
        var plan = Resolve(0, 1, max: 200);
        var crossing = -Tau * Math.Log(1 - 200 / Tau);
        var crossingVelocity = (Tau - 200) / Tau;

        Assert.Equal(200, X(plan, 0, crossing - H), 1e-3);
        Assert.Equal(200, X(plan, 0, crossing + H), 1e-3);
        Assert.Equal(crossingVelocity, (X(plan, 0, crossing - H) - X(plan, 0, crossing - 3 * H)) / (2 * H), 1e-3);
        Assert.Equal(crossingVelocity, (X(plan, 0, crossing + 3 * H) - X(plan, 0, crossing + H)) / (2 * H), 1e-3);

        var peak = double.MinValue;
        for (var t = 0.0; t < plan.Duration; t++) peak = Math.Max(peak, X(plan, 0, t));
        Assert.True(peak > 201);
        Assert.Equal(200, X(plan, 0, plan.Duration - 1), .1);
        Assert.Equal(200, X(plan, 0, plan.Duration));
    }

    [Fact]
    public void StartOutsideTheBoundsSpringsBack()
    {
        var plan = Resolve(300, .5, max: 200);
        Assert.Equal(200, plan.To);
        Assert.Null(plan.Then);
        Assert.True(X(plan, 300, 10) > 300);
        Assert.Equal(200, X(plan, 300, plan.Duration));
    }

    [Fact]
    public void ReleaseOnABoundMovingOutwardGoesOutAndBack()
    {
        var plan = Resolve(200, 1, max: 200);
        Assert.True(plan.To > 200);
        Assert.Equal(1, (X(plan, 200, H) - 200) / H, 1e-2);
        for (var t = 1.0; t <= plan.Duration; t++) Assert.True(X(plan, 200, t) > X(plan, 200, t - 1));

        var back = Assert.IsType<InertiaSolver.Plan>(plan.Then);
        Assert.Equal(200, back.To);
        Assert.Equal(200, X(back, plan.To, back.Duration));
    }

    [Fact]
    public void TrajectoryMatchesNumericalIntegration()
    {
        var plan = Resolve(0, 1, max: 200);
        var crossing = -Tau * Math.Log(1 - 200 / Tau);

        var decay = (X: 0.0, V: 0.0);
        for (var t = 1; t < crossing; t++)
        {
            decay = Rk4(decay, 1, s => ((Tau - s.X) / Tau, 0));
            Assert.Equal(decay.X, X(plan, 0, t), 1e-6);
        }

        // The spring runs in solver seconds, so a 1 ms step is 1e-3 and the crossing speed is per second.
        var spring = (X: 200.0, V: (Tau - 200) / Tau * Constants.K);
        for (var n = 1; n < 1000; n++)
        {
            spring = Rk4(spring, 1 / Constants.K, s => (s.V, -500 * (s.X - 200) - 10 * s.V));
            Assert.Equal(spring.X, X(plan, 0, crossing + n), 1e-4);
        }
    }

    private static (double X, double V) Rk4((double X, double V) s, double h, Func<(double X, double V), (double X, double V)> f)
    {
        var k1 = f(s);
        var k2 = f((s.X + k1.X * h / 2, s.V + k1.V * h / 2));
        var k3 = f((s.X + k2.X * h / 2, s.V + k2.V * h / 2));
        var k4 = f((s.X + k3.X * h, s.V + k3.V * h));
        return (s.X + (k1.X + 2 * k2.X + 2 * k3.X + k4.X) * h / 6, s.V + (k1.V + 2 * k2.V + 2 * k3.V + k4.V) * h / 6);
    }
}
