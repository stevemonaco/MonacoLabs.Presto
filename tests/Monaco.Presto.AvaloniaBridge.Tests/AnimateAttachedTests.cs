using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The lifetime of <see cref="Animate.OnAttachedProperty"/>: play on attach, revert on detach, value change or null.</summary>
public sealed class AnimateAttachedTests : BridgeTestBase
{
    private static StackPanel ShowPanel()
    {
        var panel = new StackPanel();
        new Window { Content = panel, Width = 100, Height = 100 }.Show();
        Dispatcher.UIThread.RunJobs();
        return panel;
    }

    private static Border AddBorder(Panel panel)
    {
        var border = new Border();
        panel.Children.Add(border);
        return border;
    }

    private static AnimationBuilder SlideX(double to) => Presto.Build().X(to).Duration(100).Ease(Eases.None);

    [AvaloniaFact]
    public void SettingOnAnAttachedControlPlaysOneAnimationToItsEnd()
    {
        var border = AddBorder(ShowPanel());

        Animate.SetOnAttached(border, SlideX(100));

        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void SettingBeforeAttachmentWaitsForTheAttach()
    {
        var panel = ShowPanel();
        var border = new Border();

        Animate.SetOnAttached(border, SlideX(100));
        Assert.Empty(Presto.GetAnimations(border));

        panel.Children.Add(border);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void DetachingRevertsAndReattachingPlaysFromTheStart()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);
        var midway = (double)PrestoAvalonia.Get(border, "X")!;
        Assert.InRange(midway, 1, 99);

        panel.Children.Remove(border);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
        Assert.Empty(Presto.GetAnimations(border));

        panel.Children.Add(border);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(20, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, 1, midway - 1);
    }

    [AvaloniaFact]
    public void ChangingTheValueRevertsTheFirstAnimationAndPlaysTheSecond()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);
        var first = Assert.Single(Presto.GetAnimations(border));

        Animate.SetOnAttached(border, Presto.Build().Opacity(0).Duration(100).Ease(Eases.None));

        Assert.True(first.Cancelled);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
        Assert.Single(Presto.GetAnimations(border, "Opacity"));
        Frames.Advance(200, 16);
        Assert.Equal(0, border.Opacity);
    }

    [AvaloniaFact]
    public void SettingNullRevertsAndStopsListening()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);

        Animate.SetOnAttached(border, null);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));

        panel.Children.Remove(border);
        panel.Children.Add(border);
        Assert.Empty(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void OneBuilderOnTwoControlsGivesIndependentAnimations()
    {
        var panel = ShowPanel();
        var builder = SlideX(100);
        var a = AddBorder(panel);
        var b = AddBorder(panel);

        Animate.SetOnAttached(a, builder);
        Animate.SetOnAttached(b, builder);
        Assert.Single(Presto.GetAnimations(a)).Cancel();
        Frames.Advance(200, 16);

        Assert.Empty(Presto.GetAnimations(a));
        Assert.Equal(100d, PrestoAvalonia.Get(b, "X"));
    }

    [AvaloniaFact]
    public void EachXamlSpellingAnimates()
    {
        var view = new AttachedXamlView { DataContext = new AttachedXamlViewModel() };
        new Window { Content = view }.Show(); // A UserControl's content joins the tree when its template applies, on the first layout.

        Frames.Advance(200, 16);

        Assert.Equal(100d, PrestoAvalonia.Get(view.StaticBorder, "X"));
        Assert.Equal(40d, PrestoAvalonia.Get(view.ExtensionBorder, "X"));
        Assert.Equal(60d, PrestoAvalonia.Get(view.BindingBorder, "X"));
    }

    [AvaloniaFact]
    public void DoesNothingInDesignMode()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        var border = AddBorder(ShowPanel());
        isDesignMode.SetValue(null, true);
        try
        {
            Animate.SetOnAttached(border, SlideX(100));
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        Assert.Empty(Presto.GetAnimations(border));
    }
}
