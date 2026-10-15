using Avalonia.Headless.XUnit;
using MonacoLabs.Presto.Platform;

namespace MonacoLabs.Presto.Tests;

public sealed class SystemReducedMotionTests : BridgeTestBase, IDisposable
{
    public void Dispose() => Presto.Defaults.ReducedMotion = false;

    [AvaloniaFact]
    public void FollowingSetsTheFlagFromTheProbe()
    {
        using var following = PrestoAvalonia.FollowSystemReducedMotion(() => true);

        Assert.True(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void ARefreshPicksUpAFlippedProbeAndReportsIt()
    {
        bool? system = true;
        var reported = new List<bool>();
        using var following = PrestoAvalonia.FollowSystemReducedMotion(() => system, reported.Add);

        system = false;
        PrestoAvalonia.RefreshSystemReducedMotion();
        PrestoAvalonia.RefreshSystemReducedMotion();

        Assert.False(Presto.Defaults.ReducedMotion);
        Assert.Equal([true, false], reported);
    }

    [AvaloniaFact]
    public void DisposingStopsFollowingAndLeavesTheFlag()
    {
        bool? system = true;
        var following = PrestoAvalonia.FollowSystemReducedMotion(() => system);

        following.Dispose();
        system = false;
        PrestoAvalonia.RefreshSystemReducedMotion();

        Assert.True(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void AnUnknownReadingLeavesTheFlagAlone()
    {
        Presto.Defaults.ReducedMotion = true;
        using var following = PrestoAvalonia.FollowSystemReducedMotion(() => null);

        PrestoAvalonia.RefreshSystemReducedMotion();

        Assert.True(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void WatchingReportsTheSettingAndEachChangeWithoutTouchingTheFlag()
    {
        bool? system = true;
        var reported = new List<bool?>();
        var watching = PrestoAvalonia.WatchSystemReducedMotion(reported.Add, () => system);

        PrestoAvalonia.RefreshSystemReducedMotion();
        system = null;
        PrestoAvalonia.RefreshSystemReducedMotion();
        watching.Dispose();
        system = false;
        PrestoAvalonia.RefreshSystemReducedMotion();

        Assert.Equal([true, null], reported);
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void AnAppActivationAfterALateLifetimeLookupReadsTheSettingAgain()
    {
        using var lifetime = FakeActivatableLifetime.Install();
        bool? system = true;
        var reads = 0;
        using var following = PrestoAvalonia.FollowSystemReducedMotion(() =>
        {
            reads++;
            return system;
        });
        lifetime.Found = true;
        using var watching = PrestoAvalonia.WatchSystemReducedMotion(_ => { });

        system = false;
        reads = 0;
        lifetime.Background(activated: true);

        Assert.Equal(1, reads);
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void ThePlatformProbeReadsWithoutThrowing()
    {
        var reading = ReducedMotionProbe.Read();

        if (OperatingSystem.IsWindows()) Assert.NotNull(reading);
    }
}
