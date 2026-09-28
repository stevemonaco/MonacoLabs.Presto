using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using PrestoSample.Pages;

namespace PrestoSample.Tests;

/// <summary>The reference pages whose targets are real controls: they animate, stay clickable and revert on detach.</summary>
public sealed class ReferencePagesTests
{
    [AvaloniaFact]
    public void AnimationBasicsRunsAndReverts() => Smoke(new AnimationBasicsPage(), "LikeButton");

    [AvaloniaFact]
    public void ValueTypesRunsAndReverts() => Smoke(new ValueTypesPage(), "NumberButton");

    [AvaloniaFact]
    public void KeyframesRunsAndReverts() => Smoke(new KeyframesPage(), "ArrayToggle");

    [AvaloniaFact]
    public void PlaybackSettingsRunsAndReverts() => Smoke(new PlaybackSettingsPage(), "ClickButton");

    [AvaloniaFact]
    public void ControlsRunsAndReverts() => Smoke(new ControlsPage(), "ClickButton", play: true);

    [AvaloniaFact]
    public void CallbacksRunsAndReverts() => Smoke(new CallbacksPage(), "ClickButton", play: true);

    [AvaloniaFact]
    public void TimelineRunsAndReverts() => Smoke(new TimelinePage(), "AbsoluteButton", play: true);

    [AvaloniaFact]
    public void LayoutShufflesAndCloses()
    {
        var page = new LayoutPage();
        var window = SceneHarness.Show(page, 900, 900);
        SceneHarness.Click(window, Button(page, "Shuffle"));
        TestFrames.Tick(60);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PresenceRemovesSwapsAndCloses()
    {
        var page = new PresencePage();
        var window = SceneHarness.Show(page, 900, 900);
        SceneHarness.Click(window, Button(page, "Remove random"));
        SceneHarness.Click(window, Button(page, "Swap"));
        TestFrames.Tick(30);

        Assert.Equal(3, page.Items.Count);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ADraggableCardsCheckBoxTogglesWithoutDraggingTheCard()
    {
        var page = new DraggablePage();
        var window = SceneHarness.Show(page, 900, 900);
        var card = page.GetControl<Border>("SpringCard");
        var check = page.GetControl<CheckBox>("SpringCheck");

        SceneHarness.Click(window, check);

        Assert.True(check.IsChecked);
        Assert.True(card.RenderTransform?.Value.IsIdentity ?? true, "pressing the CheckBox dragged the card");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheLikeButtonCountsAClickWhileItMoves()
    {
        var page = new AnimationBasicsPage();
        var window = SceneHarness.Show(page, 900, 600, frames: 45);
        var button = page.GetControl<Button>("LikeButton");
        Assert.False(button.RenderTransform!.Value.IsIdentity, "the button is not mid-animation");

        SceneHarness.Click(window, button);

        Assert.Equal("1", page.GetControl<TextBlock>("LikeCount").Text);
        SceneHarness.Close(window);
    }

    private static void Smoke(UserControl page, string targetName, bool play = false)
    {
        var window = SceneHarness.Show(page, 900, 900);
        var target = page.GetControl<Control>(targetName);
        if (play) SceneHarness.Click(window, Button(page, "Play"));
        TestFrames.Tick(60);
        Assert.False(target.RenderTransform!.Value.IsIdentity, $"{targetName} never moved");

        SceneHarness.Close(window);

        Assert.True(target.RenderTransform?.Value.IsIdentity ?? true, $"{targetName} kept its animated transform");
    }

    private static Button Button(Control page, string content) =>
        page.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Content, content));
}
