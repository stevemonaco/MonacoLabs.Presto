using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using MonacoLabs.Presto.Lottie;
using PrestoGallery.Pages;
using PrestoGallery.Showcase;

namespace PrestoGallery.Tests;

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
    public void TheFaqCardEasesOpenToItsNaturalHeightWithoutRewrappingTheAnswer()
    {
        var page = new LayoutPage();
        var window = SceneHarness.Show(page, 900, 1200);
        var faq = page.GetControl<Border>("Faq");
        var answer = page.GetControl<TextBlock>("FaqAnswer");
        var closed = faq.Bounds.Height;

        SceneHarness.Click(window, page.GetControl<ToggleButton>("FaqToggle"));
        TestFrames.Tick(4);
        var midFlight = faq.Bounds.Height;
        var answerMidFlight = answer.Bounds.Size;

        TestFrames.Tick(40);
        var open = faq.Child!.Bounds.Height + faq.Padding.Top + faq.Padding.Bottom;
        Assert.Equal(open, faq.Bounds.Height, 6);
        Assert.InRange(midFlight, closed + 1, open - 1);
        Assert.Equal(answerMidFlight, answer.Bounds.Size);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void NextViewSwapsThePanelThroughAGhostAndEndsAtTheNewViewsSize()
    {
        var page = new LayoutPage();
        var window = SceneHarness.Show(page, 900, 1200);
        var host = page.GetControl<Border>("SwapHost");
        var before = host.Bounds.Size;

        SceneHarness.Click(window, Button(page, "Next view"));
        var midFlight = host.Bounds.Size;
        Assert.NotEmpty(OverlayLayer.GetOverlayLayer(host)!.Children.OfType<Canvas>());

        TestFrames.Tick(60);
        var view = host.Child!;
        Assert.Equal(view.Bounds.Width + host.Padding.Left + host.Padding.Right, host.Bounds.Width, 6);
        Assert.Equal(view.Bounds.Height + host.Padding.Top + host.Padding.Bottom, host.Bounds.Height, 6);
        Assert.InRange(midFlight.Width, before.Width + 1, host.Bounds.Width - 1);
        Assert.InRange(midFlight.Height, before.Height + 1, host.Bounds.Height - 1);
        Assert.Equal(1, view.Opacity);
        Assert.Empty(OverlayLayer.GetOverlayLayer(host)!.Children.OfType<Canvas>());
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionNextViewLandsAtTheNewViewsSizeAtOnce()
    {
        using var reduced = ReducedMotion.On();
        var page = new LayoutPage();
        var window = SceneHarness.Show(page, 900, 1200);
        var host = page.GetControl<Border>("SwapHost");

        SceneHarness.Click(window, Button(page, "Next view"));
        TestFrames.Tick();

        var view = host.Child!;
        Assert.Equal(view.Bounds.Height + host.Padding.Top + host.Padding.Bottom, host.Bounds.Height, 6);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheCorrectedAvatarKeepsItsSizeWhileItsCardGrows()
    {
        var page = new LayoutPage();
        var window = SceneHarness.Show(page, 900, 1200);
        page.GetControl<ToggleSwitch>("Slow").IsChecked = true;
        var card = page.GetControl<Border>("AvatarCard");
        var cardColor = ((ISolidColorBrush)card.Background!).Color;
        var avatarColor = ((ISolidColorBrush)page.GetControl<Avalonia.Controls.Shapes.Ellipse>("Avatar").Fill!).Color;
        TestFrames.Tick();
        var avatar = SceneHarness.Extent(page, avatarColor)!.Value;

        SceneHarness.Click(window, Button(page, "Grow panel"));

        Assert.True(await SceneHarness.Pump(() => SceneHarness.Extent(page, cardColor) is { Width: > 170 and < 220 }), "the card never drew mid-flight");
        var midFlight = SceneHarness.Extent(page, avatarColor)!.Value;
        Assert.InRange(midFlight.Width, avatar.Width - 1, avatar.Width + 1);
        Assert.InRange(midFlight.Height, avatar.Height - 1, avatar.Height + 1);
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
        Assert.Contains("Presence.SetEnter(row, Recipes.SlideIn(Edge.Left", page.GetControl<CodePanel>("RowCode").Code);
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
    public void TheWaitingWizardHoldsTheNextStepUntilTheOldStepsGhostEnds()
    {
        var page = new LifecyclePage();
        var window = SceneHarness.Show(page, 900, 2000);
        var wizard = page.GetControl<ContentControl>("Wizard");
        TestFrames.Tick(40);

        SceneHarness.Click(window, Button(page, "Next"));
        var incoming = (Control)wizard.Content!;
        TestFrames.Tick(4);
        Assert.Single(SceneHarness.Ghosts(wizard));
        Assert.Equal(0, incoming.Opacity);

        for (var frame = 0; frame < 120 && SceneHarness.Ghosts(wizard).Any(); frame++) TestFrames.Tick();
        Assert.Empty(SceneHarness.Ghosts(wizard));
        TestFrames.Tick(60);

        Assert.Equal(1, incoming.Opacity, 6);
        Assert.Equal(0, (double)PrestoAvalonia.Get(incoming, "X")!, 6);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void DoubleClickingNextInTheWaitingWizardEndsOnTheThirdStepVisibleAndReleasesTheSkippedStep()
    {
        var page = new LifecyclePage();
        var window = SceneHarness.Show(page, 900, 2000);
        var wizard = page.GetControl<ContentControl>("Wizard");
        TestFrames.Tick(40);

        SceneHarness.Click(window, Button(page, "Next"));
        var held = (Control)wizard.Content!;
        TestFrames.Tick();
        SceneHarness.Click(window, Button(page, "Next"));
        TestFrames.Tick(120);

        var third = (Control)wizard.Content!;
        Assert.Contains("3 · Done", third.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text));
        Assert.Empty(SceneHarness.Ghosts(wizard));
        Assert.Equal(1, third.Opacity, 6);
        Assert.False(held.IsAnimating(Visual.OpacityProperty));
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(PresenceMode.Wait, false)]
    [InlineData(PresenceMode.Wait, true)]
    [InlineData(PresenceMode.Sync, false)]
    [InlineData(PresenceMode.Sync, true)]
    public void ClearRowsHoldsTheEmptyLineUntilTheLastGhostEndsUnderWaitAndFadesItInWhileTheyLeaveUnderSync(PresenceMode mode, bool reduced)
    {
        using var motion = reduced ? ReducedMotion.On() : null;
        var page = new LifecyclePage();
        var window = SceneHarness.Show(page, 900, 900);
        TestFrames.Tick(60);
        page.GetControl<ComboBox>("HostModePicker").SelectedIndex = (int)mode;
        Assert.Contains($"PresenceMode.{mode}", page.GetControl<CodePanel>("RowCode").Code);
        var empty = page.GetLogicalDescendants().OfType<TextBlock>().First(text => text.Text == "No rows" && text.IsAttachedToVisualTree());

        SceneHarness.Click(window, Button(page, "Clear rows"));
        Assert.True(empty.IsVisible);
        var opacityWhileLeaving = 0.0;
        for (var frame = 0; frame < 120 && SceneHarness.Ghosts(page).Any(); frame++)
        {
            if (mode == PresenceMode.Wait) Assert.Equal(0, empty.Opacity);
            opacityWhileLeaving = Math.Max(opacityWhileLeaving, empty.Opacity);
            TestFrames.Tick();
        }

        Assert.Empty(SceneHarness.Ghosts(page));
        if (mode == PresenceMode.Sync) Assert.True(opacityWhileLeaving > 0, "the empty line waited for the ghosts under Sync");
        TestFrames.Tick(60);
        Assert.Equal(1, empty.Opacity, 6);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData("First", new[] { 0.0, 40, 80, 120 }, false)]
    [InlineData("Last", new[] { 120.0, 80, 40, 0 }, false)]
    [InlineData("Center", new[] { 60.0, 20, 20, 60 }, false)]
    [InlineData("First", new[] { 0.0, 40, 80, 120 }, true)]
    [InlineData("Last", new[] { 120.0, 80, 40, 0 }, true)]
    [InlineData("Center", new[] { 60.0, 20, 20, 60 }, true)]
    public void ClearRowsStaggersTheGhostsFromThePickedEndAndLogsEachRowInThatOrder(string order, double[] delays, bool reduced)
    {
        using var motion = reduced ? ReducedMotion.On() : null;
        var page = new LifecyclePage();
        var window = SceneHarness.Show(page, 900, 900);
        TestFrames.Tick(60);
        page.GetControl<ComboBox>("ExitOrderPicker").SelectedItem = order;
        Assert.Contains($"From = StaggerFrom.{order}", page.GetControl<CodePanel>("RowCode").Code);

        SceneHarness.Click(window, Button(page, "Clear rows"));
        Dispatcher.UIThread.RunJobs();
        var ghosts = SceneHarness.GhostDelays<string>(page);
        string[] rows = ["Reply to Grace", "Review the pull request", "Book the venue", "Pay the invoice"];
        Assert.Equal(delays, rows.Select(row => ghosts[row]));

        for (var frame = 0; frame < 120 && SceneHarness.Ghosts(page).Any(); frame++) TestFrames.Tick();
        var logged = LoggedExits(page);
        Assert.Equal(rows.Order(), logged.Order());
        Assert.Equal(logged.OrderBy(row => ghosts[row]), logged);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoveThreeExitsEveryOtherShownRowAsOneBatchLoggedInPositionOrder(bool reduced)
    {
        using var motion = reduced ? ReducedMotion.On() : null;
        var page = new LifecyclePage();
        var window = SceneHarness.Show(page, 900, 900);
        SceneHarness.Click(window, Button(page, "Navigate"));
        SceneHarness.Click(window, Button(page, "Navigate"));
        TestFrames.Tick(60);

        SceneHarness.Click(window, Button(page, "Remove three"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([0.0, 40, 80], new[] { 1, 3, 5 }.Select(row => SceneHarness.GhostDelays<string>(page)[$"Archived task #{row}"]));

        for (var frame = 0; frame < 120 && SceneHarness.Ghosts(page).Any(); frame++) TestFrames.Tick();
        Assert.Equal(["Archived task #1", "Archived task #3", "Archived task #5"], LoggedExits(page));
        SceneHarness.Close(window);
    }

    private static List<string> LoggedExits(Control page) =>
        [.. Log(page.GetControl<ItemsControl>("Moments")).Where(line => line.StartsWith("Exit ")).Select(line => line[6..^1]).Reverse()];

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALifecycleRowClickedIntoDoneFliesWithNoExitGhostOrEntrance(bool reduced)
    {
        using var motion = reduced ? ReducedMotion.On() : null;
        var page = new LifecyclePage();
        var window = SceneHarness.Show(page, 900, 3600);
        TestFrames.Tick(60);

        SceneHarness.Click(window, ChoreRow(page.GetControl<ItemsControl>("OpenList"), "Send the report"));
        TestFrames.Tick(2);

        Assert.Empty(SceneHarness.Ghosts(page));
        var arrived = ChoreRow(page.GetControl<ItemsControl>("DoneList"), "Send the report");
        Assert.Equal(1, arrived.Opacity);
        SceneHarness.Close(window);
    }

    private static Border ChoreRow(ItemsControl list, string title) =>
        list.GetVisualDescendants().OfType<Border>().Single(row => row.Classes.Contains("row") && row.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == title));

    [AvaloniaFact]
    public void TheLifecycleWizardPrintsItsMarkup() =>
        Assert.Equal($"{Snippets.LifecycleWizard}\n\n{Snippets.LifecycleStep}", new LifecyclePage().GetControl<CodePanel>("WizardCode").Code);

    [AvaloniaFact]
    public void AHoveredStatesCardLiftsAndReturns()
    {
        var page = new StatesPage();
        var window = SceneHarness.Show(page, 1100, 900);
        var card = page.GetControl<Border>("InboxCard");

        window.MouseMove(SceneHarness.Center(card, window));
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
        window.MouseMove(SceneHarness.Center(page.GetControl<Border>("DraftsCard"), window));
        TestFrames.Tick(10);
        Assert.NotEmpty(Presto.GetAnimations(page.GetControl<Border>("StatusChip")));

        SceneHarness.Close(window);

        Assert.Empty(page.GetLogicalDescendants().SelectMany(target => Presto.GetAnimations(target)));
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheSyncingChipPulsesOnceAndRests()
    {
        using var reduced = ReducedMotion.On();
        var page = new StatesPage();
        var window = SceneHarness.Show(page, 1100, 900);
        var chip = page.GetControl<Border>("StatusChip");

        page.Status = "Syncing";
        TestFrames.Tick(70);

        Assert.Empty(Presto.GetAnimations(chip));
        Assert.Equal(1, chip.Opacity, 6);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheStatesMenuStaggersItsItemsInAfterThePanelAndClosesToRest()
    {
        var page = new StatesPage();
        var window = SceneHarness.Show(page, 1100, 900);
        var panel = page.GetControl<Border>("MenuPanel");
        var items = panel.GetVisualDescendants().OfType<Border>().Where(item => item.Classes.Contains("item")).ToList();
        var begins = new int?[items.Count];
        var panelOpen = 0;

        page.GetControl<ToggleButton>("MenuToggle").IsChecked = true;
        for (var frame = 1; frame <= 60; frame++)
        {
            TestFrames.Tick();
            if (panelOpen == 0 && panel.Opacity == 1) panelOpen = frame;
            for (var i = 0; i < items.Count; i++) begins[i] ??= items[i].Opacity > 0 ? frame : null;
        }

        Assert.Equal(5, items.Count);
        Assert.All(begins, begin => Assert.True(begin > panelOpen, "an item began before the panel unfolded"));
        for (var i = 1; i < items.Count; i++) Assert.True(begins[i] > begins[i - 1], $"item {i} began at frame {begins[i]}, item {i - 1} at {begins[i - 1]}");
        Assert.All(items, item => Assert.Equal(1, item.Opacity, 6));
        Assert.All(items, item => Assert.Equal(0, (double)PrestoAvalonia.Get(item, "Y")!, 6));

        page.GetControl<ToggleButton>("MenuToggle").IsChecked = false;
        TestFrames.Tick(60);
        Assert.Null(Motion.GetState(panel));
        Assert.Equal(0, panel.Opacity, 6);
        Assert.All(items, item => Assert.Equal(0, item.Opacity, 6));
        Assert.Empty(items.Append(panel).SelectMany(target => Presto.GetAnimations(target)));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheStatesPagePrintsEverySnippet()
    {
        var page = new StatesPage();
        var code = page.GetControl<CodeTabs>("Code");
        string[] snippets = [Snippets.StatesButton, Snippets.StatesTextBox, Snippets.StatesCheckBox, Snippets.StatesArrow, Snippets.StatesCard,
            Snippets.StatesChip, Snippets.StatesUpload, Snippets.StatesMenu, Snippets.StatesMenuItem];
        Assert.All(snippets, snippet => Assert.Contains(snippet, code.Xaml));
        Assert.Contains(Snippets.StatesBuilders, code.CSharp);
        Assert.Equal(Snippets.StatesComposed, page.GetControl<CodePanel>("ComposedCode").Code);
        Assert.Equal(Snippets.StatesLayering, page.GetControl<CodePanel>("LayeringCode").Code);
    }

    [AvaloniaFact]
    public void TheComposedChipReadsOutTheTokensItHolds()
    {
        var page = new StatesPage();
        var window = SceneHarness.Show(page, 1100, 1400);
        var readout = page.GetControl<TextBlock>("TokenReadout");

        page.GetControl<ComboBox>("ModePicker").SelectedIndex = 1;
        page.GetControl<ToggleButton>("SelectedToggle").IsChecked = true;
        page.GetControl<ToggleButton>("BusyToggle").IsChecked = true;
        TestFrames.Tick(30);
        Assert.Equal("Review IsSelected IsBusy", readout.Text);
        Assert.Equal(Palette.Violet.Color, Background(page.GetControl<Border>("ComposedChip")));

        page.GetControl<ToggleButton>("SelectedToggle").IsChecked = false;
        page.GetControl<ToggleButton>("BusyToggle").IsChecked = false;
        TestFrames.Tick(70);
        Assert.Equal("Review", readout.Text);
        Assert.Empty(Presto.GetAnimations(page.GetControl<Border>("ComposedChip")));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ABusyLocalStateWinsTheBackgroundOverTheStylesModeColor()
    {
        var page = new StatesPage();
        var window = SceneHarness.Show(page, 1100, 1400);
        var local = page.GetControl<Border>("LocalLayerCard");

        page.Mode = "Review";
        page.IsBusy = true;
        TestFrames.Tick(30);

        Assert.Equal(Palette.Amber.Color, Background(local));
        Assert.Equal(Palette.Violet.Color, Background(page.GetControl<Border>("StyleLayerCard")));
        Assert.NotEmpty(Presto.GetAnimations(local));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheBusyPulseAndBreatheSettleWhileTheColorsChange()
    {
        using var reduced = ReducedMotion.On();
        var page = new StatesPage();
        var window = SceneHarness.Show(page, 1100, 1400);
        var chip = page.GetControl<Border>("ComposedChip");
        var local = page.GetControl<Border>("LocalLayerCard");

        page.Mode = "Live";
        page.IsBusy = true;
        TestFrames.Tick(70);

        Assert.Empty(Presto.GetAnimations(chip));
        Assert.Empty(Presto.GetAnimations(local));
        Assert.Equal(1, chip.Opacity, 6);
        Assert.Equal(1, (double)PrestoAvalonia.Get(local, "Scale")!, 6);
        Assert.Equal(Palette.Aqua.Color, Background(chip));
        Assert.Equal(Palette.Amber.Color, Background(local));
        Assert.Equal(Palette.Aqua.Color, Background(page.GetControl<Border>("StyleLayerCard")));
        SceneHarness.Close(window);
    }

    private static Color Background(Border border) => ((ISolidColorBrush)border.Background!).Color;

    [AvaloniaFact]
    public async Task EveryEffectsStageRunsAndClosingLeavesNoBridgeOwnedEffectClipOrMask()
    {
        var page = new EffectsPage();
        var window = SceneHarness.Show(page, 1100, 2600);
        foreach (var content in new[] { "Blur out", "Raise", "Flip", "Inset", "Circle", "Sweep" }) SceneHarness.Click(window, Button(page, content));
        page.GetControl<Slider>("ShadowXSlider").Value = -20;
        var remove = Button(page, "✕");
        // The stages above push the rows down, and a row takes clicks only once it is drawn in its new slot.
        Assert.True(await SceneHarness.Pump(() =>
            (window.InputHitTest(SceneHarness.Center(remove, window)) as Visual)?.FindAncestorOfType<Button>(includeSelf: true) == remove));
        SceneHarness.Click(window, remove);
        window.MouseMove(SceneHarness.Center(page.GetControl<Border>("TiltCard"), window) + new Point(40, 30));
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

    [AvaloniaFact]
    public void TheEffectsExitPrintsTheListAndRowMarkup() =>
        Assert.Equal($"{Snippets.EffectsList}\n    {Snippets.EffectsRow}", new EffectsPage().GetControl<CodeTabs>("ExitCode").Xaml);

    [AvaloniaFact]
    public void TheImageEffectsSlidersPrintTheirFiltersAndResetDrawsPlain()
    {
        var page = new ImageEffectsPage();
        var window = SceneHarness.Show(page, 1100, 2000);
        var photo = page.GetControl<FilteredImage>("Photo");
        var code = page.GetControl<CodeTabs>("FilterCode");

        page.GetControl<Slider>("GrayscaleSlider").Value = 0.5;
        Assert.Equal(0.5, photo.Grayscale);
        Assert.Contains("Grayscale=\"0.5\"", code.Xaml);
        Assert.Contains(".Grayscale(0.5)", code.CSharp);
        SceneHarness.Click(window, Button(page, "Reset"));
        Assert.Equal(0, photo.Grayscale);
        Assert.Equal("<presto:FilteredImage Source=\"/Assets/Photos/mongolia.jpg\" />", code.Xaml);
        Assert.Contains("draws plain", code.CSharp);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheImageEffectsStagesPrintTheirMarkup()
    {
        var page = new ImageEffectsPage();
        Assert.Equal(Snippets.ImageEffectsThumb, page.GetControl<CodeTabs>("ColorizeCode").Xaml);
        Assert.Equal(Snippets.ImageEffectsDevelop, page.GetControl<CodeTabs>("DevelopCode").Xaml);
        Assert.Equal(Snippets.ImageEffectsHue, page.GetControl<CodeTabs>("HueCode").Xaml);
        Assert.Equal(Snippets.ImageEffectsCard, page.GetControl<CodeTabs>("CardCode").Xaml);
    }

    [AvaloniaFact]
    public void TheSlideshowDissolvesToTheNextPhotoAndPrintsThePickedKind()
    {
        var page = new ImageEffectsPage();
        var window = SceneHarness.Show(page, 1100, 3000);
        var slideshow = page.GetControl<FilteredImage>("Slideshow");
        var code = page.GetControl<CodePanel>("SlideshowCode");
        var first = slideshow.Source;

        SceneHarness.Click(window, Button(page, "Next"));

        Assert.NotSame(first, slideshow.Source);
        Assert.Equal(ImageTransitionKind.Dissolve, slideshow.SourceTransition!.Kind);
        Assert.Contains("ImageTransitionKind.Dissolve", code.Code);
        page.GetControl<ComboBox>("KindBox").SelectedItem = ImageTransitionKind.Wipe;
        page.GetControl<Slider>("AngleSlider").Value = 45;
        Assert.Contains("ImageTransitionKind.Wipe", code.Code);
        Assert.Contains("Angle = 45", code.Code);
        Assert.Equal(45, slideshow.SourceTransition.Angle);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheDissolveCardPixelatesOutAndDissolvesBackIn()
    {
        var page = new ImageEffectsPage();
        var window = SceneHarness.Show(page, 1100, 3000);
        var card = page.GetControl<FilteredImage>("Card");
        var frame = page.GetControl<Border>("CardFrame");
        var button = page.GetControl<Button>("CardButton");

        SceneHarness.Click(window, button);
        Assert.True(await SceneHarness.Pump(() => card.Pixelate > 1), "the card never pixelated");
        Assert.True(await SceneHarness.Pump(() => frame.Child is null, rounds: 120), "the card was never removed");
        Assert.Equal(1, card.Pixelate);

        SceneHarness.Click(window, button);
        Assert.Same(card, frame.Child);
        Assert.True(await SceneHarness.Pump(() => card.Dissolve is > 0 and < 1), "the card never dissolved in");
        Assert.True(await SceneHarness.Pump(() => card.Dissolve == 0, rounds: 120), "the card never finished dissolving in");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheParticlesPageEmitsABurstCountsItAndPrintsTheChangedGravity()
    {
        var page = new ParticlesPage();
        var window = SceneHarness.Show(page, 1100, 1000);
        var live = page.GetControl<TextBlock>("LiveCount");

        SceneHarness.Click(window, Button(page, "Emit"));

        Assert.True(await SceneHarness.Pump(() => live.Text is not "0 live"), "the burst never showed in the live count");
        page.GetControl<Slider>("GravitySlider").Value = 0.002;
        Assert.Contains("Gravity = 0.002,", page.GetControl<CodePanel>("Code").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheValueDrivenBarEasesToANewLevelAndSnapsUnderReducedMotion()
    {
        var page = new ValueDrivenPage();
        var window = SceneHarness.Show(page, 1100, 1600);
        var bar = page.GetControl<Border>("Bar");
        var model = (ValueDrivenViewModel)page.DataContext!;
        Assert.Equal(120, bar.Width);

        model.Level = 90;
        TestFrames.Tick(4);
        Assert.InRange(bar.Width, 121, 269);
        TestFrames.Tick(120);
        Assert.Equal(270, bar.Width, 6);

        Presto.Defaults.ReducedMotion = true;
        try
        {
            model.Level = 10;
            Assert.Equal(30, bar.Width);
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
            SceneHarness.Close(window);
        }
    }

    [AvaloniaFact]
    public void TheValueDrivenStagesPrintTheirMarkup()
    {
        var page = new ValueDrivenPage();
        Assert.Equal(Snippets.ValueDrivenBar, page.GetControl<CodePanel>("BarCode").Code);
        Assert.Equal(Snippets.ValueDrivenGauge, page.GetControl<CodeTabs>("GaugeCode").Xaml);
        Assert.Equal(Snippets.ValueDrivenSwatch, page.GetControl<CodePanel>("SwatchCode").Code);
        Assert.Equal(Snippets.ValueDrivenCard, page.GetControl<CodePanel>("CardCode").Code);
        Assert.Equal(Snippets.ValueDrivenRow, page.GetControl<CodePanel>("RowCode").Code);
    }

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
    public void TheInertiaReleaseCoastsTheCardOntoTheGridInsideTheStage()
    {
        var page = new DraggablePage();
        var window = SceneHarness.Show(page, 900, 900);
        var card = page.GetControl<Border>("SpringCard");
        page.GetControl<ComboBox>("ReleaseBox").SelectedIndex = 1;
        var grab = SceneHarness.Center(card, window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(grab + new Vector(15 * i, 0));
            TestFrames.Tick();
        }
        window.MouseUp(grab + new Vector(60, 0), MouseButton.Left);
        TestFrames.Tick(240);

        var x = (double)PrestoAvalonia.Get(card, "X")!;
        var max = page.GetControl<Canvas>("Stage").Bounds.Width - card.Bounds.Width - Canvas.GetLeft(card);
        Assert.True(x > 60, $"the card did not coast past the pointer ({x})");
        Assert.True(x % 40 == 0 || Math.Abs(x - max) < 1e-6, $"the card rested off the grid and off the bound ({x}, max {max})");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void FlingCoastsTheFollowerAndKeepsItOnTheStage()
    {
        var page = new AnimatablePage();
        var window = SceneHarness.Show(page, 900, 1400);
        var follower = page.GetControl<Border>("Follower");
        SceneHarness.Click(window, Button(page, "Set(\"X\", -140, 1200, \"inOutQuad\")"));
        TestFrames.Tick(20);
        var released = (double)PrestoAvalonia.Get(follower, "X")!;

        SceneHarness.Click(window, Button(page, "Fling()"));
        TestFrames.Tick(240);

        var edge = page.GetControl<Border>("FollowerStage").Bounds.Width / 2 - follower.Width / 2;
        Assert.InRange((double)PrestoAvalonia.Get(follower, "X")!, -edge - 1e-6, released - 10);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TossArcsTheBallUpAndLandsItBackOnTheFloor()
    {
        var page = new AnimatablePage();
        var window = SceneHarness.Show(page, 900, 1400);
        var ball = page.GetControl<Border>("Ball");
        SceneHarness.Click(window, Button(page, "Toss"));
        TestFrames.Tick(20);
        Assert.True((double)PrestoAvalonia.Get(ball, "Y")! < -40, "the ball did not rise");

        TestFrames.Tick(60);
        Assert.Equal(0, (double)PrestoAvalonia.Get(ball, "Y")!, 6);
        Assert.InRange((double)PrestoAvalonia.Get(ball, "X")!, 200, 220);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void EveryCatalogEaseAndTypedEaseFormPlots()
    {
        var page = new EasesPage();
        var window = SceneHarness.Show(page, 900, 900);
        var box = page.GetControl<ComboBox>("EaseBox");
        var dot = page.GetControl<Border>("Dot");
        for (var i = 0; i < box.ItemCount; i++)
        {
            box.SelectedIndex = i;
            TestFrames.Tick(3);
            Assert.InRange(dot.Margin.Bottom, 0, 160);
        }

        var status = page.GetControl<TextBlock>("StatusText");
        foreach (var typed in new[] { "wiggle(6, anticipate)", "slowMo(0.5)", "fromPath(M0,0 C.1,.6 .3,1.3 .5,1 .7,.8 .85,1.05 1,1)" })
        {
            page.GetControl<TextBox>("EaseNameBox").Text = typed;
            TestFrames.Tick();
            Assert.StartsWith("EaseParser.Parse(", status.Text);
        }

        page.GetControl<TextBox>("EaseNameBox").Text = "wiggle(0)";
        TestFrames.Tick();
        Assert.DoesNotContain("EaseParser.Parse(", status.Text);
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

    [AvaloniaFact]
    public void AnimationBasicsPrintsTheCallItRan()
    {
        var page = new AnimationBasicsPage();
        var window = SceneHarness.Show(page, 900, 600);
        var code = page.GetControl<CodePanel>("Code").Code;
        Assert.StartsWith("LikeButton.Animate(new()", code);
        Assert.Contains("[\"X\"] = 320", code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void LottieRunsAndReverts()
    {
        var page = new LottiePage();
        var window = SceneHarness.Show(page, 1100, 2000, frames: 20);
        var player = page.GetControl<LottieView>("Player");
        var toggle = page.GetControl<LottieView>("Toggle");
        Assert.InRange(player.Frame, 1, 59);
        Assert.Equal("hover 0–20   on 20–40   off 40–60", page.GetControl<TextBlock>("MarkerList").Text);

        SceneHarness.Click(window, Button(page, "on"));
        TestFrames.Tick(30);
        Assert.Equal(40, toggle.Frame);

        var hover = page.GetControl<LottieView>("HoverIcon");
        window.MouseMove(SceneHarness.Center(hover, window));
        TestFrames.Tick(30);
        Assert.Equal(20, hover.Frame);
        window.MouseMove(default);
        TestFrames.Tick(30);
        Assert.Equal(0, hover.Frame);

        SceneHarness.Close(window);

        Assert.Empty(Presto.GetAnimations(player));
        Assert.Empty(Presto.GetAnimations(toggle));
    }

    [AvaloniaFact]
    public void TheLottieRenderThreadToggleHandsTheSpinnerToTheRenderThreadAndBack()
    {
        var page = new LottiePage();
        var window = SceneHarness.Show(page, 1100, 2000);
        var spinner = page.GetControl<LottieView>("RenderSpinner");
        var toggle = page.GetControl<ToggleSwitch>("RenderToggle");
        Assert.NotEmpty(Presto.GetAnimations(spinner));

        toggle.IsChecked = true;
        Assert.True(spinner.RenderThread);
        Assert.Empty(Presto.GetAnimations(spinner));

        toggle.IsChecked = false;
        Assert.False(spinner.RenderThread);
        Assert.NotEmpty(Presto.GetAnimations(spinner));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheTimerStopsWhenThePageDetachesAndAFreshOneRunsWhenItReturns()
    {
        var page = new TimerPage();
        var window = SceneHarness.Show(page, 900, 600, frames: 10);
        var host = window.Content;
        var currentTime = page.GetControl<TextBlock>("CurrentTimeText");
        window.Content = null;
        var stopped = currentTime.Text;
        TestFrames.Tick(10);
        Assert.Equal(stopped, currentTime.Text);

        window.Content = host;
        TestFrames.Tick(10);
        Assert.InRange(double.Parse(currentTime.Text!, CultureInfo.InvariantCulture), 1, 200);

        window.Content = null;
        var second = currentTime.Text;
        TestFrames.Tick(10);
        Assert.Equal(second, currentTime.Text);
        window.Close();
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
