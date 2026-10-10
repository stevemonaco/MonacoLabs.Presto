using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class ActivityFeedPageTests
{
    [AvaloniaFact]
    public void ThePageOpensWithTheFirstRowsRising()
    {
        var page = new ActivityFeedPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(5);

        var rows = Rows(page).ToList();
        Assert.NotEmpty(rows);
        Assert.Contains(rows, row => (double)PrestoAvalonia.Get(row, "Y")! > 0 && row.Opacity < 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ScrollingALittleRevealsTheNewlyRealizedRows()
    {
        var (window, page) = Loaded();
        var loaded = Rows(page).Select(row => row.DataContext).ToHashSet();

        Feed(page).Scroll!.Offset = new Vector(0, 150);
        TestFrames.Tick();

        var revealed = Rows(page).Where(row => !loaded.Contains(row.DataContext)).ToList();
        Assert.NotEmpty(revealed);
        Assert.All(revealed, row => Assert.True(row.Opacity < 1, "a row scrolled into view was not revealed"));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AJumpFarDownTheFeedLandsAtRest()
    {
        var (window, page) = Loaded();
        var played = page.ViewModel.RevealPlayed;

        Feed(page).Scroll!.Offset = new Vector(0, 5000);
        TestFrames.Tick();

        Assert.All(Rows(page), row => Assert.Equal(1, row.Opacity));
        Assert.Equal(played, page.ViewModel.RevealPlayed);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void DismissingARowRemovesItsEventAndCollapsesAGhost()
    {
        var (window, page) = Loaded();
        var row = Rows(page).First();
        var item = row.DataContext;

        SceneHarness.Click(window, row.GetVisualDescendants().OfType<Button>().Single());

        Assert.DoesNotContain(item, page.ViewModel.Items);
        Assert.Single(OverlayLayer.GetOverlayLayer(page)!.Children.OfType<Canvas>());
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void WithReducedMotionOnADismissedRowFadesOutWithoutCollapsing()
    {
        var (window, page) = Loaded();
        using var reduced = ReducedMotion.On();

        SceneHarness.Click(window, Rows(page).First().GetVisualDescendants().OfType<Button>().Single());
        TestFrames.Tick(4);

        var ghost = OverlayLayer.GetOverlayLayer(page)!.GetVisualDescendants().OfType<Image>().Single();
        Assert.InRange(ghost.Opacity, 0, 0.99);
        Assert.Equal(1, (double)(PrestoAvalonia.Get(ghost, "ScaleY") ?? 1d), 6);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheLiveTimerInsertsAnEventAtTheTopAndPulsesTheUnreadBadge()
    {
        var (window, page) = Loaded();
        var count = page.ViewModel.Items.Count;
        var unread = page.ViewModel.UnreadCount;

        TickUntil(() => page.ViewModel.Items.Count > count);

        var live = Assert.IsType<ActivityEvent>(page.ViewModel.Items[1]);
        Assert.True(live.Unread);
        Assert.Equal(unread + 1, page.ViewModel.UnreadCount);
        Assert.NotEmpty(Presto.GetAnimations(Rows(page).Single(row => ReferenceEquals(row.DataContext, live))));
        Assert.NotEmpty(Presto.GetAnimations(page.GetControl<Border>("Badge")));
        Assert.Null(page.ViewModel.Unseen);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void DismissingTheTopRowAfterLiveInsertsLeavesTheFeedAtTheTop(bool itemLayout)
    {
        var (window, page) = Loaded();
        var feed = Feed(page);
        if (!itemLayout) Animate.SetItemLayout(feed, null);
        SelectTopRow(window, feed);
        for (var i = 0; i < 2; i++)
        {
            var count = page.ViewModel.Items.Count;
            TickUntil(() => page.ViewModel.Items.Count > count);
        }
        TestFrames.Tick(30);
        Assert.Equal(3, feed.SelectedIndex);
        Assert.Equal(default, feed.Scroll!.Offset);

        var dismiss = SelectTopRow(window, feed);
        SceneHarness.Click(window, dismiss);
        TestFrames.Tick(60);

        Assert.Equal(default, feed.Scroll!.Offset);
        Assert.Null(page.ViewModel.Unseen);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ScrolledDownLiveEventsInsertAtOnceWithoutMovingTheRowsAndAPillCountsThem()
    {
        var (window, page) = Loaded();
        var feed = Feed(page);
        feed.Scroll!.Offset = new Vector(0, 3000);
        TestFrames.Tick(30);
        var reading = Rows(page).Where(row => Top(row, feed) >= 0).MinBy(row => Top(row, feed))!.DataContext;
        var top = Top(Row(page, reading), feed);
        var events = page.ViewModel.Items.OfType<ActivityEvent>().Count();

        for (var i = 1; i <= 2; i++)
        {
            TickUntil(() => page.ViewModel.Unseen == i);
            TestFrames.Tick(2);

            Assert.Equal(events + i, page.ViewModel.Items.OfType<ActivityEvent>().Count());
            Assert.Equal(top, Top(Row(page, reading), feed), 1);
        }
        var pill = PillHost(page).GetVisualDescendants().OfType<Button>().Single();
        Assert.Equal("2 new above", pill.Content);

        TestFrames.Tick(30);
        SceneHarness.Click(window, pill);
        TestFrames.Tick(JumpFrames);
        Assert.Equal(0, feed.Scroll.Offset.Y);
        Assert.Null(page.ViewModel.Unseen);
        var live = page.ViewModel.Items.Skip(1).Take(2).ToList();
        Assert.All(live, item => Assert.True(Assert.IsType<ActivityEvent>(item).Unread));
        Assert.All(live, item => Row(page, item));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ThePillJumpsFromFarDownToTheNewestEventWhenOneLandsMidFlight()
    {
        var (window, page) = Loaded();
        var feed = Feed(page);
        feed.Scroll!.Offset = new Vector(0, 40_000);
        TestFrames.Tick(30);
        TickUntil(() => page.ViewModel.Unseen == 1);
        TestFrames.Tick(30);

        SceneHarness.Click(window, PillHost(page).GetVisualDescendants().OfType<Button>().Single());
        TestFrames.Tick(3);
        Assert.InRange(feed.Scroll.Offset.Y, 1, 40_000);
        page.ViewModel.AddLive(above: true);
        TestFrames.Tick(JumpFrames + EnterFrames);

        Assert.Equal(default, feed.Scroll.Offset);
        Assert.Null(page.ViewModel.Unseen);
        var newest = page.ViewModel.Items[1];
        Assert.True(Top(Row(page, newest), feed) >= 0);
        Assert.Empty(PillHost(page).GetVisualDescendants().OfType<Button>());
        SceneHarness.Close(window);
    }

    private static int JumpFrames => (int)Math.Ceiling(Recipes.Tokens.Long / 16) + 2;

    private static ContentControl PillHost(ActivityFeedPage page) => page.GetControl<ContentControl>("PillHost");

    [AvaloniaFact]
    public void NearTheBottomSkeletonRowsShimmerUntilTheNextPageIsAppended()
    {
        var (window, page) = Loaded();
        var scroll = Feed(page).Scroll!;
        var events = page.ViewModel.Items.OfType<ActivityEvent>().Count();

        TickUntil(() =>
        {
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            return page.ViewModel.Loading;
        });
        TestFrames.Tick();

        var skeletons = page.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("skeleton")).ToList();
        Assert.NotEmpty(skeletons);
        Assert.All(skeletons, skeleton => Assert.NotEmpty(Presto.GetAnimations(skeleton)));

        TestFrames.Tick((int)(ActivityFeedPage.PageDelay / 16) + 2);
        Assert.False(page.ViewModel.Loading);
        Assert.Equal(events + ActivityFeedViewModel.PageSize, page.ViewModel.Items.OfType<ActivityEvent>().Count());
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void ScrollingBackOverSeenRowsRevealsThemAgainOnlyEveryTime(int mode, bool replays)
    {
        var (window, page) = Loaded();
        page.GetControl<ComboBox>("RevealBox").SelectedIndex = mode;

        var played = ScrollDownAndBack(page, () => page.ViewModel.RevealPlayed);

        Assert.Equal(replays, played > 0);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UseOnAttachedReplaysOnScrollingBackOverSeenRows()
    {
        var (window, page) = Loaded();
        page.GetControl<ToggleSwitch>("UseOnAttached").IsChecked = true;
        var revealed = page.ViewModel.RevealPlayed;

        Assert.True(ScrollDownAndBack(page, () => page.ViewModel.AttachedPlayed) > 0);
        Assert.Equal(revealed, page.ViewModel.RevealPlayed);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RevealOffPlaysNoReveal()
    {
        var (window, page) = Loaded();
        page.GetControl<ComboBox>("RevealBox").SelectedIndex = 0;
        var played = page.ViewModel.RevealPlayed;

        Feed(page).Scroll!.Offset = new Vector(0, 400);
        TestFrames.Tick(30);

        Assert.Equal(played, page.ViewModel.RevealPlayed);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(300, false, 15)]
    [InlineData(300, false, 60)]
    [InlineData(3000, false, 15)]
    [InlineData(300, true, 15)]
    [InlineData(3000, true, 15)]
    public void EventsInsertedAboveRevealOnlyInThePassThatBringsThemIntoView(double offset, bool drag, int gap)
    {
        var (window, page) = Loaded();
        var feed = Feed(page);
        var inserted = ScrollDownAndInsert(window, page, offset, 3);
        var grab = drag ? PressThumb(window, feed) : default;
        var revealed = new Dictionary<object, string>();
        var log = new List<string>();

        for (var step = 1; feed.Scroll!.Offset.Y > 0 && step < 200; step++)
        {
            var before = feed.Scroll.Offset.Y;
            var played = page.ViewModel.RevealPlayed;
            Scroll(window, feed, ref grab, drag, 1);
            for (var frame = 0; frame <= gap; frame++)
            {
                TestFrames.Tick();
                foreach (var item in inserted)
                {
                    if (revealed.ContainsKey(item) || feed.ContainerFromItem(item) is null || Row(page, item) is not { Opacity: < 1 } row) continue;
                    var top = Top(row, feed);
                    revealed[item] = top + row.Bounds.Height > 0 && top < feed.Scroll.Viewport.Height ? "in view" : $"out of view at {top:0}";
                }
            }
            log.Add($"{before:0}->{feed.Scroll.Offset.Y:0} +{page.ViewModel.RevealPlayed - played} [{string.Join(",", inserted.Select(item => revealed.GetValueOrDefault(item, "-")))}]");
        }
        if (drag) window.MouseUp(grab, MouseButton.Left);

        Assert.True(inserted.All(item => revealed.GetValueOrDefault(item) == "in view"), string.Join(Environment.NewLine, log));
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(false, "near the top")]
    [InlineData(true, "near the top")]
    [InlineData(false, "before the anchor clears")]
    [InlineData(true, "before the anchor clears")]
    public void ScrollingBackToTheTopLandsThereWithTheNewestEventShown(bool drag, string insert)
    {
        var (window, page) = Loaded();
        var feed = Feed(page);
        ScrollDownAndInsert(window, page, 300, 3);
        var unqueued = insert == "before the anchor clears";
        var dragging = drag && !unqueued;
        var grab = dragging ? PressThumb(window, feed) : default;
        var log = new List<string>();
        void Step(Action input)
        {
            var before = feed.Scroll!.Offset.Y;
            input();
            log.Add($"{before:0}->{feed.Scroll.Offset.Y:0}");
        }

        // On the tall feed a quarter-pixel thumb step can jump from above 120 straight to the top.
        while (feed.Scroll!.Offset.Y > 120) Step(() =>
        {
            Scroll(window, feed, ref grab, dragging, dragging ? .1 : 1);
            TestFrames.Tick(20);
        });
        if (unqueued)
        {
            if (drag) grab = PressThumb(window, feed);
            page.ViewModel.AddLive(above: true);
            Step(window.UpdateLayout);
            Step(() => Unqueued(window, drag ? "MouseMove" : "MouseWheel", drag ? [grab -= new Vector(0, 100), RawInputModifiers.LeftMouseButton] : [SceneHarness.Center(feed, window), new Vector(0, 10), RawInputModifiers.None]));
            Step(window.UpdateLayout);
        }
        else
        {
            var unseen = page.ViewModel.Unseen;
            if (insert == "near the top") Step(() => TickUntil(() => page.ViewModel.Unseen != unseen));
            while (feed.Scroll.Offset.Y > 0 && log.Count < 100) Step(() =>
            {
                Scroll(window, feed, ref grab, drag, drag ? 100 : 10);
                TestFrames.Tick();
            });
        }
        var settling = page.ViewModel.Items[1];
        TestFrames.Tick(30);
        if (drag) window.MouseUp(grab, MouseButton.Left);
        TestFrames.Tick(30);
        // A live event landing while the feed settles slides in from above the top; let its Enter finish.
        if (page.ViewModel.Items[1] != settling) TestFrames.Tick(EnterFrames);

        var trace = string.Join(" ", log);
        Assert.True(feed.Scroll.Offset == default, $"ended at {feed.Scroll.Offset.Y}: {trace}");
        var newest = page.ViewModel.Items[1];
        Assert.True(feed.ContainerFromItem(newest) is not null && Top(Row(page, newest), feed) >= 0, $"the newest event is not shown: {trace}");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ALiveEventLandingAtTheTopSlidesInFromAboveAndIsShownWithinItsEnter()
    {
        var (window, page) = Loaded();
        var feed = Feed(page);
        ScrollDownAndInsert(window, page, 300, 3);
        while (feed.Scroll!.Offset.Y > 0)
        {
            window.MouseWheel(SceneHarness.Center(feed, window), new Vector(0, 10));
            TestFrames.Tick();
        }
        var count = page.ViewModel.Items.Count;

        TickUntil(() => page.ViewModel.Items.Count > count);
        var live = page.ViewModel.Items[1];
        Assert.True(Top(Row(page, live), feed) < 0);

        TestFrames.Tick(EnterFrames);
        Assert.Equal(default, feed.Scroll.Offset);
        Assert.True(Top(Row(page, live), feed) >= 0);
        Assert.Empty(Presto.GetAnimations(Row(page, live)));
        SceneHarness.Close(window);
    }

    private static int EnterFrames => (int)Math.Ceiling(Recipes.Tokens.Medium / 16);

    private static void Scroll(Window window, ListBox feed, ref Point grab, bool drag, double by)
    {
        if (drag) window.MouseMove(grab -= new Vector(0, by));
        else window.MouseWheel(SceneHarness.Center(feed, window), new Vector(0, by));
    }

    // Headless input extensions drain Background jobs first; a desktop lets input outrank them.
    private static void Unqueued(Window window, string input, object[] args) =>
        window.PlatformImpl!.GetType().GetInterface("IHeadlessWindow")!.GetMethod(input)!.Invoke(window.PlatformImpl, args);

    private static List<object> ScrollDownAndInsert(Window window, ActivityFeedPage page, double offset, int events)
    {
        var feed = Feed(page);
        if (offset < 1000) window.MouseWheel(SceneHarness.Center(feed, window), new Vector(0, -offset / 50));
        else feed.Scroll!.Offset = new Vector(0, offset);
        TestFrames.Tick(30);
        var inserted = new List<object>();
        for (var i = 1; i <= events; i++)
        {
            TickUntil(() => page.ViewModel.Unseen == i);
            inserted.Insert(0, page.ViewModel.Items[1]);
        }
        TestFrames.Tick(30);
        return inserted;
    }

    private static Point PressThumb(Window window, ListBox feed)
    {
        var bar = ((ScrollViewer)feed.Scroll!).GetVisualDescendants().OfType<ScrollBar>().Single(bar => bar.Orientation == Orientation.Vertical);
        var grab = SceneHarness.Center(bar.GetVisualDescendants().OfType<Thumb>().Single(), window);
        window.MouseDown(grab, MouseButton.Left);
        return grab;
    }

    private static int ScrollDownAndBack(ActivityFeedPage page, Func<int> counter)
    {
        var scroll = Feed(page).Scroll!;
        scroll.Offset = new Vector(0, 400);
        TestFrames.Tick(30);
        var before = counter();
        scroll.Offset = default;
        TestFrames.Tick(30);
        return counter() - before;
    }

    private static Button SelectTopRow(Window window, ListBox feed)
    {
        var row = feed.ContainerFromIndex(1)!;
        SceneHarness.Click(window, row.GetVisualDescendants().OfType<TextBlock>().First(text => text.Classes.Contains("title")));
        Assert.Equal(1, feed.SelectedIndex);
        return row.GetVisualDescendants().OfType<Button>().Single();
    }

    private static (Window Window, ActivityFeedPage Page) Loaded()
    {
        // The seed's time of day moves the first day header, and with it every offset these tests scroll through.
        var page = new ActivityFeedPage { DataContext = new ActivityFeedViewModel(DateTime.Today.AddHours(18)) };
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(90);
        return (window, page);
    }

    private static void TickUntil(Func<bool> done, int frames = 400)
    {
        for (var i = 0; i < frames && !done(); i++) TestFrames.Tick();
        Assert.True(done());
    }

    private static ListBox Feed(ActivityFeedPage page) => page.GetControl<ListBox>("Feed");

    private static IEnumerable<Border> Rows(Visual page) =>
        page.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("event") && border.IsEffectivelyVisible);

    private static Border Row(Visual page, object? item) => Rows(page).Single(row => ReferenceEquals(row.DataContext, item));

    private static double Top(Visual row, Visual feed) => row.TranslatePoint(default, feed)!.Value.Y;
}
