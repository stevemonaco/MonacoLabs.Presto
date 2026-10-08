using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>PAGE-TRANSITIONS-PLAN decision 10: <see cref="Presence"/> sees a page transition as navigation.</summary>
public sealed class PresencePageTransitionTests : CompositorTestBase
{
    private static AnimationBuilder Fade => Presto.Build().Opacity(to: 0).Duration(200).Ease("linear");

    [AvaloniaFact]
    public void APageRootsEnterDoesNotPlayUnderAPrestoTransition()
    {
        var host = PrestoHost();
        var page = new Border();
        Presence.SetEnter(page, SlideIn);

        Swap(host, page);
        Frames.Advance(50);

        Assert.Equal(0, X(page));
    }

    [AvaloniaFact]
    public void APageRootsInitialPlaysUnderAPrestoTransition()
    {
        var host = PrestoHost();
        var page = new Border();
        Presence.SetInitial(page, SlideIn);

        Swap(host, page);
        Frames.Advance(50);

        Assert.InRange(X(page), 20, 80);
    }

    [AvaloniaFact]
    public void APageRootsExitLeavesNoGhostUnderAPrestoTransition()
    {
        var page = Page();
        var host = PrestoHost(page);

        Swap(host, new Border());
        RunUntil(() =>
        {
            Dispatcher.UIThread.RunJobs();
            return !page.IsAttachedToVisualTree();
        });

        Assert.False(page.IsAttachedToVisualTree());
        Assert.Empty(Ghosts(host));
    }

    [AvaloniaFact]
    public async Task APageRootsExitLeavesNoGhostAfterACrossFade()
    {
        var page = Page();
        var host = new TransitioningContentControl { PageTransition = new CrossFade(TimeSpan.FromMilliseconds(50)), Content = page };
        Show(host);
        Tick();

        Swap(host, new Border());

        for (var i = 0; i < 120 && page.IsAttachedToVisualTree(); i++)
        {
            Tick(Frame);
            // CrossFade runs on Avalonia's own animation clock, which moves only in real time with the dispatcher free.
            await Task.Delay(8);
        }
        Assert.False(page.IsAttachedToVisualTree(), "the old page never left");
        Assert.Empty(Ghosts(host));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnElementLeavingAParentThatIsNotOnScreenLeavesNoGhost(bool hidden)
    {
        var item = Page();
        var panel = new StackPanel { Children = { item } };
        Show(new Border { Child = panel });
        Tick();
        if (hidden) panel.IsVisible = false;
        else ((Border)panel.GetVisualParent()!).Opacity = 0;

        panel.Children.Remove(item);
        Tick();

        Assert.Empty(Ghosts(panel));
    }

    [AvaloniaFact]
    public void AnElementLeavingAVisibleLoadedParentStillLeavesAGhost()
    {
        var item = Page();
        var panel = new StackPanel { Children = { item } };
        Show(new Border { Child = panel, Opacity = 0.5 });
        Tick();

        panel.Children.Remove(item);
        Tick();

        Assert.Single(Ghosts(panel));
    }

    [AvaloniaFact]
    public async Task ANavigationPageRootNeitherEntersNorLeavesAGhost()
    {
        var navigation = new NavigationPage { PageTransition = Transition() };
        Show(navigation);
        await navigation.PushAsync(new ContentPage());
        var page = new ContentPage { Content = new Border() };
        Presence.SetEnter(page, SlideIn);
        Presence.SetExit(page, Fade);

        var push = navigation.PushAsync(page);
        Frames.Advance(50);
        Assert.Equal(0, X(page));
        Settle(() => push.IsCompleted);
        await push;
        var pop = navigation.PopAsync();
        Settle(() => pop.IsCompleted && !page.IsAttachedToVisualTree());
        await pop;

        Assert.False(page.IsAttachedToVisualTree());
        Assert.Empty(Ghosts(navigation));
    }

    [AvaloniaFact]
    public void ATabPageNeitherEntersNorLeavesAGhost()
    {
        var (first, second) = (NavigatedPage(), NavigatedPage());
        var tabs = new TabControl
        {
            PageTransition = Transition(),
            Items = { new TabItem { Header = "one", Content = first }, new TabItem { Header = "two", Content = second } },
        };
        var window = Show(tabs);
        Tick();

        tabs.SelectedIndex = 1;
        window.UpdateLayout();
        Frames.Advance(50);
        Assert.Equal(0, X(second));
        Settle(() => !first.IsAttachedToVisualTree());

        Assert.False(first.IsAttachedToVisualTree());
        Assert.Empty(Ghosts(tabs));
    }

    [AvaloniaFact]
    public void ACarouselPageNeitherEntersNorLeavesAGhost()
    {
        var pages = new Dictionary<string, Border>();
        var items = new ObservableCollection<string> { "one" };
        var carousel = new Carousel
        {
            PageTransition = Transition(),
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((item, _) => pages[item!] = NavigatedPage()),
        };
        var window = Show(carousel);
        Tick();
        var first = carousel.ContainerFromIndex(0)!;

        items.Add("two");
        carousel.SelectedIndex = 1;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(50);
        Assert.Equal(0, X(pages["two"]));
        Settle(() => !first.IsVisible);

        Assert.False(first.IsVisible);
        Assert.Empty(Ghosts(carousel));
    }

    private static Border Page()
    {
        var page = new Border { Width = 40, Height = 20, Background = Brushes.Red };
        Presence.SetExit(page, Fade);
        return page;
    }

    private static Border NavigatedPage()
    {
        var page = Page();
        Presence.SetEnter(page, SlideIn);
        return page;
    }

    private static PrestoPageTransition Transition() => new()
    {
        Enter = Presto.Build().Opacity(from: 0).Duration(100).Ease("linear"),
        Exit = Presto.Build().Opacity(0).Duration(100).Ease("linear"),
        Thread = AnimationThread.UIThread,
    };

    private static TransitioningContentControl PrestoHost(Control? content = null)
    {
        var host = new TransitioningContentControl { PageTransition = Transition(), Content = content ?? new Border() };
        Show(host);
        Tick();
        return host;
    }
}
