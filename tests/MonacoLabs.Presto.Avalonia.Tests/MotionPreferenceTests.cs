using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Tests;

public sealed class MotionPreferenceTests : BridgeTestBase, IDisposable
{
    public void Dispose()
    {
        PrestoAvalonia.MotionPreference = null;
        PrestoAvalonia.SystemReducedMotionProbe = null;
        Presto.Defaults.ReducedMotion = false;
    }

    [AvaloniaFact]
    public void FullAndReducedWriteTheFlagAndStopFollowing()
    {
        bool? system = false;
        PrestoAvalonia.SystemReducedMotionProbe = () => system;
        PrestoAvalonia.MotionPreference = MotionPreference.System;

        PrestoAvalonia.MotionPreference = MotionPreference.Reduced;
        Assert.True(Presto.Defaults.ReducedMotion);
        PrestoAvalonia.RefreshSystemReducedMotion();
        Assert.True(Presto.Defaults.ReducedMotion);

        PrestoAvalonia.MotionPreference = MotionPreference.Full;
        system = true;
        PrestoAvalonia.RefreshSystemReducedMotion();
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void SystemFollowsTheProbeUntilTheChoiceChanges()
    {
        bool? system = true;
        PrestoAvalonia.SystemReducedMotionProbe = () => system;

        PrestoAvalonia.MotionPreference = MotionPreference.System;
        Assert.True(Presto.Defaults.ReducedMotion);
        system = false;
        PrestoAvalonia.RefreshSystemReducedMotion();
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void NullStopsFollowingAndLeavesTheFlag()
    {
        bool? system = true;
        PrestoAvalonia.SystemReducedMotionProbe = () => system;
        PrestoAvalonia.MotionPreference = MotionPreference.System;

        PrestoAvalonia.MotionPreference = null;
        system = false;
        PrestoAvalonia.RefreshSystemReducedMotion();

        Assert.True(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void SettingTheSameChoiceAgainKeepsTheFollower()
    {
        bool? system = true;
        var reads = 0;
        PrestoAvalonia.SystemReducedMotionProbe = () =>
        {
            reads++;
            return system;
        };
        PrestoAvalonia.MotionPreference = MotionPreference.System;

        PrestoAvalonia.MotionPreference = MotionPreference.System;
        Assert.Equal(1, reads);
        system = false;
        PrestoAvalonia.RefreshSystemReducedMotion();

        Assert.Equal(2, reads);
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void ThePreferenceNeverWritesReduceAll()
    {
        Presto.Defaults.ReduceAll = true;
        PrestoAvalonia.SystemReducedMotionProbe = () => false;

        PrestoAvalonia.MotionPreference = MotionPreference.Full;
        PrestoAvalonia.MotionPreference = MotionPreference.System;
        PrestoAvalonia.MotionPreference = MotionPreference.Reduced;
        PrestoAvalonia.MotionPreference = null;

        Assert.True(Presto.Defaults.ReduceAll);
    }

    [AvaloniaFact]
    public void ChangingTheProbeUnderSystemReadsAgain()
    {
        var oldReads = 0;
        PrestoAvalonia.SystemReducedMotionProbe = () =>
        {
            oldReads++;
            return true;
        };
        PrestoAvalonia.MotionPreference = MotionPreference.System;

        PrestoAvalonia.SystemReducedMotionProbe = () => false;
        Assert.False(Presto.Defaults.ReducedMotion);
        PrestoAvalonia.RefreshSystemReducedMotion();

        Assert.Equal(1, oldReads);
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void ABindingToSystemFromXamlStartsFollowing()
    {
        PrestoAvalonia.SystemReducedMotionProbe = () => true;
        var box = Box();
        box.Bind(Motion.PreferenceProperty, new Binding(nameof(SettingsModel.Motion)));

        box.DataContext = new SettingsModel();
        Show(box);

        Assert.Equal(MotionPreference.System, PrestoAvalonia.MotionPreference);
        Assert.True(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void ClearingOrDetachingTheAttachedPropertyLeavesThePreference()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetPreference(box, MotionPreference.Reduced);

        box.ClearValue(Motion.PreferenceProperty);
        Assert.Equal(MotionPreference.Reduced, PrestoAvalonia.MotionPreference);
        Motion.SetPreference(box, MotionPreference.Reduced);
        window.Content = null;

        Assert.Equal(MotionPreference.Reduced, PrestoAvalonia.MotionPreference);
        Assert.True(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void DesignModeIgnoresTheAttachedProperty()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        isDesignMode.SetValue(null, true);
        try
        {
            Motion.SetPreference(Box(), MotionPreference.Reduced);
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        Assert.Null(PrestoAvalonia.MotionPreference);
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void ThePreferenceGovernsTheMainEngineInsideAScope()
    {
        var scope = Presto.CreateScope(new() { Defaults = _ => { } });
        var other = new Engine(new ManualFrameSource());

        scope.Execute(_ =>
        {
            using (other.MakeCurrent()) PrestoAvalonia.MotionPreference = MotionPreference.Reduced;
        });

        Assert.True(Presto.Defaults.ReducedMotion);
        Assert.False(scope.Defaults.ReducedMotion);
        Assert.False(other.Defaults.ReducedMotion);
    }

    private sealed class SettingsModel
    {
        public MotionPreference Motion { get; } = MotionPreference.System;
    }
}
