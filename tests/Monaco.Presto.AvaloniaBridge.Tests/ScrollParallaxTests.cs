using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Events;
using Monaco.Presto.AvaloniaBridge.Transforms;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Covers <see cref="Scroll.SpeedProperty"/>, which upstream has no counterpart of.</summary>
/// <remarks>
/// A 200 by 200 scroller holds a 100 unit target 300 units in, then 600 more, so the target's top meets the viewport's
/// bottom at a scroll of 100.
/// </remarks>
public sealed class ScrollParallaxTests : BridgeTestBase
{
    private const double ViewportSize = ScrollStage.ViewportSize;
    private const double Lead = ScrollStage.Lead;
    private const double Entry = Lead - ViewportSize;

    private static (ScrollViewer Scroller, StackPanel Panel) Show(Border target, Orientation orientation = Orientation.Vertical)
    {
        var stage = ScrollStage.Show(orientation, 600, target);
        return (stage.Scroller, stage.Panel);
    }

    private static Border Target(double speed)
    {
        var target = new Border();
        Scroll.SetSpeed(target, speed);
        return target;
    }

    private static double Translate(Control target, TransformShorthand axis = TransformShorthand.Y) => RenderTransformComposer.Get(target, axis);

    [AvaloniaFact]
    public void AHalfSpeedElementLagsByHalfTheScrollSinceItEnteredWithoutAFrame()
    {
        var target = Target(.5);
        var (scroller, _) = Show(target);

        foreach (var offset in new[] { 150d, 230, 400, 512 })
        {
            scroller.Offset = new Vector(0, offset);
            Assert.Equal(.5 * (offset - Entry), Translate(target), 6);
        }
    }

    [AvaloniaFact]
    public void BeforeItEntersTheElementIsNotTranslated()
    {
        var target = Target(.5);
        var (scroller, _) = Show(target);

        scroller.Offset = new Vector(0, 60);

        Assert.Equal(0, Translate(target));
    }

    [AvaloniaFact]
    public void AnElementInViewAtTheTopStartsUntranslatedAndMovesWithTheWholeScroll()
    {
        var target = Target(.5);
        var (scroller, panel) = Show(target);
        ((Border)panel.Children[0]).Height = 50;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, Translate(target));

        scroller.Offset = new Vector(0, 120);
        Assert.Equal(.5 * 120, Translate(target), 6);
    }

    [AvaloniaFact]
    public void SpeedOneLeavesTheRenderTransformUntouched()
    {
        var target = Target(1);
        var (scroller, _) = Show(target);

        scroller.Offset = new Vector(0, 300);

        Assert.Null(target.RenderTransform);
        Assert.False(ScrollContainer.Containers.ContainsKey(scroller));
    }

    [AvaloniaFact]
    public void AFasterElementLeads()
    {
        var target = Target(1.5);
        var (scroller, _) = Show(target);

        scroller.Offset = new Vector(0, 300);

        Assert.Equal(-.5 * (300 - Entry), Translate(target), 6);
    }

    [AvaloniaFact]
    public void AViewportResizeMeasuresTheEntryAgain()
    {
        var target = Target(.5);
        var (scroller, _) = Show(target);

        scroller.Height = 100;
        Dispatcher.UIThread.RunJobs();
        scroller.Offset = new Vector(0, 250);

        Assert.Equal(.5 * (250 - (Lead - 100)), Translate(target), 6);
    }

    [AvaloniaFact]
    public void ALayoutChangeAboveTheElementMeasuresTheEntryAgain()
    {
        var target = Target(.5);
        var (scroller, panel) = Show(target);

        ((Border)panel.Children[0]).Height = Lead + 100;
        Dispatcher.UIThread.RunJobs();
        scroller.Offset = new Vector(0, 300);

        Assert.Equal(.5 * (300 - (Entry + 100)), Translate(target), 6);
    }

    [AvaloniaFact]
    public void DetachingRevertsTheUsersTransformAndReleasesTheContainer()
    {
        var translate = new TranslateTransform(0, 7);
        var target = Target(.5);
        target.RenderTransform = translate;
        var (scroller, panel) = Show(target);
        scroller.Offset = new Vector(0, 300);
        Assert.NotSame(translate, target.RenderTransform);

        panel.Children.Remove(target);

        Assert.Same(translate, target.RenderTransform);
        Assert.Equal(7, translate.Y);
        Assert.False(ScrollContainer.Containers.ContainsKey(scroller));
    }

    [AvaloniaFact]
    public void ChangingTheSpeedTakesEffectAtTheNextScroll()
    {
        var target = Target(.5);
        var (scroller, _) = Show(target);
        scroller.Offset = new Vector(0, 200);

        Scroll.SetSpeed(target, .75);
        scroller.Offset = new Vector(0, 300);

        Assert.Equal(.25 * (300 - Entry), Translate(target), 6);
    }

    [AvaloniaFact]
    public void SettingTheSpeedBackToOneReverts()
    {
        var target = Target(.5);
        var (scroller, _) = Show(target);
        scroller.Offset = new Vector(0, 300);

        target.ClearValue(Scroll.SpeedProperty);

        Assert.Null(target.RenderTransform);
        Assert.False(ScrollContainer.Containers.ContainsKey(scroller));
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheElementKeepsItsLayoutPositionAndResumesAtTheScrollAfterItClears()
    {
        var target = Target(.5);
        var (scroller, _) = Show(target);
        Presto.Defaults.ReducedMotion = true;

        foreach (var offset in new[] { 150d, 300 })
        {
            scroller.Offset = new Vector(0, offset);
            Assert.Equal(0, Translate(target));
        }

        Presto.Defaults.ReducedMotion = false;
        scroller.Offset = new Vector(0, 400);

        Assert.Equal(.5 * (400 - Entry), Translate(target), 6);
    }

    [AvaloniaFact]
    public void AHorizontalScrollerTranslatesAlongX()
    {
        var target = Target(.5);
        var (scroller, _) = Show(target, Orientation.Horizontal);

        scroller.Offset = new Vector(300, 0);

        Assert.Equal(.5 * (300 - Entry), Translate(target, TransformShorthand.X), 6);
        Assert.Equal(0, Translate(target));
    }

    [AvaloniaFact]
    public void TheSpeedCanBeSetInXaml()
    {
        var view = new ScrollParallaxXamlView();
        new Window { Content = view, Width = 300, Height = 300 }.Show();
        Dispatcher.UIThread.RunJobs();
        var scroller = view.FindControl<ScrollViewer>("Scroller")!;

        scroller.Offset = new Vector(0, 300);

        Assert.Equal(.5 * (300 - Entry), Translate(view.FindControl<Border>("Target")!), 6);
    }
}
