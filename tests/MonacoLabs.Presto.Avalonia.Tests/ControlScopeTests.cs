using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers the control-rooted scope: the lifetime half of tests/suites/scope.test.js, where upstream uses the DOM.</summary>
public sealed class ControlScopeTests : BridgeTestBase
{
    private static (Panel Panel, Border Border) ShowBorder()
    {
        var border = new Border { Opacity = .25 };
        var panel = new StackPanel { Children = { border } };
        new Window { Content = panel, Width = 100, Height = 100 }.Show();
        Dispatcher.UIThread.RunJobs(); // Loaded is posted at DispatcherPriority.Loaded, so the root is only settled after it runs.
        return (panel, border);
    }

    [AvaloniaFact]
    public void RevertsWhatWasCreatedInsideWhenTheRootIsDetached()
    {
        var (panel, border) = ShowBorder();
        var scope = PrestoAvalonia.CreateScope(border);
        Animation? animation = null;
        Animatable? animatable = null;

        scope.Add(_ =>
        {
            animation = border.Animate(new() { ["X"] = 100, Duration = 100, Ease = "linear" });
            animatable = border.CreateAnimatable(new() { ["Opacity"] = new() { Duration = 0 } });
        });
        animatable!.Set("Opacity", 1);

        Frames.Advance(200, 16);
        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
        Assert.Equal(1, border.Opacity);

        panel.Children.Remove(border);

        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
        Assert.Equal(.25, border.Opacity);
        Assert.True(animation!.Canceled);
        Assert.Equal(0, animatable.Get("Opacity"));
    }

    [AvaloniaFact]
    public void RevertingByHandMakesTheDetachANoOp()
    {
        var (panel, border) = ShowBorder();
        var scope = PrestoAvalonia.CreateScope(border);
        var cleanups = 0;

        scope.Add(_ => () => cleanups++);

        scope.Revert();
        panel.Children.Remove(border);

        Assert.Equal(1, cleanups);
    }

    [AvaloniaFact]
    public void RevertsARootDetachedBeforeItEverLoads()
    {
        var (panel, _) = ShowBorder();
        var border = new Border();
        panel.Children.Add(border);
        var scope = PrestoAvalonia.CreateScope(border);
        var cleanups = 0;

        scope.Add(_ => () => cleanups++);

        panel.Children.Remove(border); // No RunJobs in between, so the root never loads and never unloads.

        Assert.Equal(1, cleanups);
    }

    [AvaloniaFact]
    public void RevertingAnEnclosingScopeMakesTheDetachANoOp()
    {
        var (panel, border) = ShowBorder();
        var outer = Presto.CreateScope();
        var cleanups = 0;

        outer.Add(_ => { PrestoAvalonia.CreateScope(border).Add(__ => () => cleanups++); });

        outer.Revert();
        panel.Children.Remove(border);

        Assert.Equal(1, cleanups);
    }

    [AvaloniaFact]
    public void AppliesScopedDefaultsToAnimationsCreatedInside()
    {
        var (_, border) = ShowBorder();
        var scope = PrestoAvalonia.CreateScope(border, new() { Defaults = defaults => defaults.Duration = 500 });

        var animation = scope.Execute(_ => border.Animate(new() { ["Opacity"] = 1 }));

        Assert.Equal(500, animation.Duration);
    }
}
