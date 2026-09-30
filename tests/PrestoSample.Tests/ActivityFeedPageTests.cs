using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
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

    [AvaloniaFact]
    public void ScrolledDownALiveEventWaitsBehindAPillThatScrollsToTheTopAndLetsItIn()
    {
        var (window, page) = Loaded();
        var feed = Feed(page);
        feed.Scroll!.Offset = new Vector(0, 3000);
        TestFrames.Tick(30);
        var reading = Rows(page).OrderBy(row => Top(row, feed)).First(row => Top(row, feed) >= 0);
        var top = Top(reading, feed);
        var count = page.ViewModel.Items.Count;
        var events = page.ViewModel.Items.OfType<ActivityEvent>().Count();

        TickUntil(() => page.ViewModel.Unseen == 1);
        TestFrames.Tick();

        Assert.Equal(count, page.ViewModel.Items.Count);
        Assert.Equal(top, Top(reading, feed), 1);
        var pill = page.GetControl<ContentControl>("PillHost").GetVisualDescendants().OfType<Button>().Single();
        Assert.Equal("1 new", pill.Content);

        TestFrames.Tick(30);
        SceneHarness.Click(window, pill);
        Assert.Equal(0, feed.Scroll.Offset.Y);
        Assert.Null(page.ViewModel.Unseen);
        Assert.Equal(events + 1, page.ViewModel.Items.OfType<ActivityEvent>().Count());
        SceneHarness.Close(window);
    }

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

    private static (Window Window, ActivityFeedPage Page) Loaded()
    {
        var page = new ActivityFeedPage();
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

    private static double Top(Visual row, Visual feed) => row.TranslatePoint(default, feed)!.Value.Y;
}
