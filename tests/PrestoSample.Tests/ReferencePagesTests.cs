using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Pages;
using PrestoSample.Showcase;

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
    public void LifecycleLogsWhatPresenceClassified()
    {
        var page = new LifecyclePage();
        var window = SceneHarness.Show(page, 900, 900);
        TestFrames.Tick(40);
        SceneHarness.Click(window, Button(page, "Add row"));
        TestFrames.Tick(40);
        SceneHarness.Click(window, Button(page, "Remove row"));
        TestFrames.Tick(40);
        SceneHarness.Click(window, Button(page, "Navigate"));
        SceneHarness.Click(window, Button(page, "Swap"));
        TestFrames.Tick(40);

        var moments = page.GetControl<ItemsControl>("Moments").Items.Cast<(string Moment, Avalonia.Media.IBrush Tint, string Detail)>().Select(entry => entry.Moment).ToList();
        Assert.Contains("Initial", moments);
        Assert.Contains("Enter", moments);
        Assert.Contains("Exit", moments);
        Assert.Contains("Unload", moments);
        Assert.Contains("Presence.SetEnter(row, Recipes.SlideIn(Edge.Left", page.GetControl<TextBlock>("RowCode").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ScrollingTheLifecycleArchiveLogsNothingButAnAddedRowEnters()
    {
        var page = new LifecyclePage();
        var window = SceneHarness.Show(page, 900, 900);
        SceneHarness.Click(window, Button(page, "Navigate"));
        SceneHarness.Click(window, Button(page, "Navigate"));
        TestFrames.Tick(60);
        var archive = page.GetLogicalDescendants().OfType<ListBox>().Single();
        var moments = page.GetControl<ItemsControl>("Moments");
        var before = Log(moments);

        archive.Scroll!.Offset = new Avalonia.Vector(0, 1200);
        TestFrames.Tick(60);

        Assert.Equal(before, Log(moments));
        SceneHarness.Click(window, Button(page, "Add row"));
        TestFrames.Tick(60);
        Assert.StartsWith("Enter", Log(moments)[0]);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AHoveredStatesCardLiftsAndReturns()
    {
        var page = new StatesPage();
        var window = SceneHarness.Show(page, 1100, 900);
        var card = page.GetControl<Border>("InboxCard");

        window.MouseMove(SceneHarness.Centre(card, window));
        TestFrames.Tick(30);
        Assert.Equal(-4, (double)PrestoAvalonia.Get(card, "Y")!, 6);
        Assert.Equal("hot", Motion.GetState(page.GetControl<Avalonia.Controls.Shapes.Path>("InboxArrow")));
        Assert.Contains("InboxCard  + :pointerover", page.GetControl<ItemsControl>("Log").Items.Cast<string>());

        window.MouseMove(new Avalonia.Point(1, 1));
        TestFrames.Tick(30);
        Assert.Empty(Presto.GetAnimations(card));
        Assert.True(card.RenderTransform?.Value.IsIdentity ?? true, "the card kept its lift");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheUploadCardFollowsItsViewModelEnumAndClassBinding()
    {
        var page = new StatesPage();
        SceneHarness.Show(page, 1100, 900);
        var card = page.GetControl<Border>("UploadCard");
        var model = (UploadViewModel)card.DataContext!;

        model.IsStarred = true;
        TestFrames.Tick(30);
        Assert.Contains("starred", card.Classes);
        Assert.Equal(-4, (double)PrestoAvalonia.Get(card, "Y")!, 6);
        Assert.Equal(1, (double)PrestoAvalonia.Get(card, "Scale")!, 6);

        model.Phase = UploadPhase.Uploaded;
        TestFrames.Tick(40);
        Assert.Equal("Uploaded", Motion.GetState(card)?.ToString());
        Assert.Equal(1.06, (double)PrestoAvalonia.Get(card, "Scale")!, 6);
        Assert.Equal(Palette.Background.Color, ((ISolidColorBrush)card.GetLogicalDescendants().OfType<TextBlock>().Single().Foreground!).Color);

        model.IsStarred = false;
        TestFrames.Tick(40);
        Assert.Equal(0, (double)PrestoAvalonia.Get(card, "Y")!, 6);
        Assert.Equal(1, (double)PrestoAvalonia.Get(card, "Scale")!, 6);
    }

    [AvaloniaFact]
    public void ClosingTheStatesPageLeavesNothingAnimating()
    {
        var page = new StatesPage();
        var window = SceneHarness.Show(page, 1100, 900);
        page.Status = "Syncing";
        window.MouseMove(SceneHarness.Centre(page.GetControl<Border>("DraftsCard"), window));
        TestFrames.Tick(10);
        Assert.NotEmpty(Presto.GetAnimations(page.GetControl<Border>("StatusChip")));

        SceneHarness.Close(window);

        Assert.Empty(page.GetLogicalDescendants().SelectMany(target => Presto.GetAnimations(target)));
    }

    [AvaloniaFact]
    public void StatesSnippetsMatchTheMarkup() =>
        BuilderPagesTests.AssertSnippetsMatchMarkup(typeof(StatesPage), "samples/PrestoSample/Pages/StatesPage.axaml", 7);

    [AvaloniaFact]
    public void EveryEffectsStageRunsAndClosingLeavesNoBridgeOwnedEffectClipOrMask()
    {
        var page = new EffectsPage();
        var window = SceneHarness.Show(page, 1100, 2600);
        foreach (var content in new[] { "Blur out", "Raise", "Flip", "Inset", "Circle", "Sweep" }) SceneHarness.Click(window, Button(page, content));
        page.GetControl<Slider>("ShadowXSlider").Value = -20;
        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Content, "✕")));
        window.MouseMove(SceneHarness.Centre(page.GetControl<Border>("TiltCard"), window) + new Point(40, 30));
        TestFrames.Tick(20);

        Assert.True(page.GetControl<Image>("BlurPicture").IsAnimating(Visual.EffectProperty), "the picture never blurred");
        Assert.True(page.GetControl<Border>("EffectCard").IsAnimating(Visual.EffectProperty), "the effect card never raised its shadow");
        Assert.True(page.GetControl<Image>("RevealPicture").IsAnimating(Visual.OpacityMaskProperty), "the sweep never masked the picture");
        Assert.False(page.GetControl<Border>("TiltCard").RenderTransform!.Value.IsIdentity, "the card never tilted");
        Assert.Equal(3, page.Items.Count);

        SceneHarness.Close(window);

        var visuals = page.GetVisualDescendants().Prepend(page).ToList();
        Assert.Empty(visuals.SelectMany(visual => Presto.GetAnimations(visual)));
        Assert.All(visuals, visual => Assert.False(
            visual.IsAnimating(Visual.EffectProperty) || visual.IsAnimating(Visual.ClipProperty) || visual.IsAnimating(Visual.OpacityMaskProperty),
            $"{visual.GetType().Name} {visual.Name} kept a bridge-owned effect, clip or mask"));
        Assert.IsType<Avalonia.Media.DropShadowEffect>(page.GetControl<Border>("EffectCard").Effect);
    }

    [Fact]
    public void EffectsSnippetsMatchTheMarkup() =>
        BuilderPagesTests.AssertSnippetsMatchMarkup(typeof(EffectsPage), "samples/PrestoSample/Pages/EffectsPage.axaml", 1);

    private static List<string> Log(ItemsControl moments) =>
        moments.Items.Cast<(string Moment, Avalonia.Media.IBrush Tint, string Detail)>().Select(entry => $"{entry.Moment} {entry.Detail}").ToList();

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
