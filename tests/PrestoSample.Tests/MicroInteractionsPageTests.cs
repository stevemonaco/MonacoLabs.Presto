using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class MicroInteractionsPageTests
{
    [AvaloniaFact]
    public void ThePageRunsAndRevertsItsScopeOnDetach()
    {
        var page = new MicroInteractionsPage();
        var reverted = false;
        page.Scope.Add(_ => () => reverted = true);
        var window = SceneHarness.Show(page, 1280, 900);
        SceneHarness.Click(window, page.GetControl<Button>("Like"));
        TestFrames.Tick(60);

        SceneHarness.Close(window);

        Assert.True(reverted);
    }

    [AvaloniaFact]
    public void SubmitNarrowsTheButtonToACircleAndSpringsBackToItsLabelWidth()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var submit = page.GetControl<Button>("Submit");
        var width = submit.Width;

        SceneHarness.Click(window, submit);
        TestFrames.Tick(40);
        Assert.Equal(submit.Height, submit.Width, 1);

        TestFrames.Tick(200);
        Assert.Equal(width, submit.Width, 1);
        Assert.Equal(1, page.GetControl<TextBlock>("SubmitLabel").Opacity, 2);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void NotifyAddsAToastThatDismissesItselfWhenItsTimerRunsOut()
    {
        var page = new MicroInteractionsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var toasts = page.GetControl<StackPanel>("Toasts");

        SceneHarness.Click(window, page.GetControl<Button>("Notify"));
        Assert.Single(toasts.Children);

        TestFrames.Tick(200);
        Assert.Single(toasts.Children);
        var bar = toasts.GetLogicalDescendants().OfType<ProgressBar>().Single();
        Assert.InRange(bar.Value, 0.05, 0.4);

        TestFrames.Tick(90);
        Assert.Empty(toasts.Children);
        SceneHarness.Close(window);
    }
}
