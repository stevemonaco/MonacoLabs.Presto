using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using PrestoSample.Pages;

namespace PrestoSample.Tests;

/// <summary>The Visibility page: cards bound to a switch through IsVisible, and an Expander whose rows re-enter on show.</summary>
public sealed class VisibilityPageTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheBoundToggleHidesTheCardOnlyOnceItsExitHasPlayed(bool reduced)
    {
        using var motion = reduced ? ReducedMotion.On() : null;
        var page = new VisibilityPage();
        var window = SceneHarness.Show(page, 1280, 900, frames: 30);
        var card = page.GetControl<Border>("HoldCard");

        page.GetControl<ToggleSwitch>("HoldShown").IsChecked = false;
        TestFrames.Tick(2);
        Assert.True(card.IsVisible);
        Assert.InRange(card.Opacity, 0, .99);

        TestFrames.Tick(60);
        Assert.False(card.IsVisible);
        Assert.Equal(1, card.Opacity);
        Assert.Contains(page.GetControl<ItemsControl>("Moments").Items, entry => entry is ValueTuple<string, IBrush, string> { Item1: "Hide" });
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheGhostCardLeavesTheLayoutAtOnce()
    {
        var page = new VisibilityPage();
        var window = SceneHarness.Show(page, 1280, 900, frames: 30);

        page.GetControl<ToggleSwitch>("GhostShown").IsChecked = false;
        TestFrames.Tick();

        Assert.False(page.GetControl<Border>("GhostCard").IsVisible);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void OpeningTheSettingsTheFirstTimeStaggersItsRowsIn()
    {
        var page = new VisibilityPage();
        var window = SceneHarness.Show(page, 1280, 900, frames: 30);
        var settings = page.GetControl<Expander>("Settings");
        var rows = settings.GetLogicalDescendants().OfType<Border>().Where(border => border.Classes.Contains("setting")).ToList();

        settings.IsExpanded = true;
        TestFrames.Tick(4);
        Assert.True(rows[0].Opacity > rows[^1].Opacity, $"the first row was at {rows[0].Opacity:0.00} and the last at {rows[^1].Opacity:0.00}");

        TestFrames.Tick(60);
        Assert.All(rows, row => Assert.Equal(1, row.Opacity));
        SceneHarness.Close(window);
    }
}
