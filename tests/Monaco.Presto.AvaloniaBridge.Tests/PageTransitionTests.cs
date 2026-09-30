using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>PAGE-TRANSITIONS-PLAN §4: <see cref="PrestoPageTransition.Start"/> on real hosts and on bare visuals.</summary>
public sealed class PageTransitionTests : CompositorTestBase
{
    private static AnimationBuilder FadeIn => Presto.Build().Opacity(from: 0).Duration(100).Ease("linear");

    private static AnimationBuilder FadeOut => Presto.Build().Opacity(0).Duration(100).Ease("linear");

    [AvaloniaFact]
    public void ASwapPlaysEnterOnTheNewPresenterAndExitOnTheOld()
    {
        var (host, first) = ShowHost(new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false });
        var second = new Border { Background = Brushes.Green };
        Swap(host, second);

        Frames.Advance(50);

        Assert.InRange(Presenter(host, second).Opacity, 0.2, 0.8);
        Assert.InRange(Presenter(host, first).Opacity, 0.2, 0.8);
    }

    [AvaloniaFact]
    public void CompletionHidesTheOldPresenterBeforeRevertingAndLeavesNothingAnimated()
    {
        var (host, first) = ShowHost(new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false });
        var second = new Border { Background = Brushes.Green };
        Swap(host, second);
        var (incoming, outgoing) = (Presenter(host, second), Presenter(host, first));
        var events = new List<string>();
        outgoing.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty || e.Property == Visual.OpacityProperty) events.Add(e.Property.Name);
        };

        Frames.Advance(120);
        events.Clear();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["IsVisible", "Opacity"], events);
        foreach (var presenter in new[] { incoming, outgoing })
        {
            Assert.False(presenter.IsAnimating(Visual.OpacityProperty));
            Assert.False(presenter.IsAnimating(Visual.RenderTransformProperty));
            Assert.False(presenter.IsAnimating(Visual.ZIndexProperty));
            Assert.Equal(1, presenter.Opacity);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheIncomingHostsFirstFrameIsAtTheEntrancesStartValues(bool compositor)
    {
        var transition = new PrestoPageTransition { Enter = Presto.Build().Opacity(from: 0).Duration(300).Ease("linear"), UseCompositor = compositor };
        var host = new TransitioningContentControl { PageTransition = transition, Content = new Border { Background = Brushes.White } };
        var window = Show(host);
        window.Background = Brushes.White;
        Tick();

        host.Content = new Border { Background = Brushes.Red };
        window.UpdateLayout();
        Tick();

        Assert.Null(Extent(host, Colors.Red));
        Frames.Advance(400);
        Dispatcher.UIThread.RunJobs();
        Assert.True(await Pump(() => Extent(host, Colors.Red) != null), "the incoming page never showed");
    }

    [AvaloniaFact]
    public void GoingBackPlaysTheForwardPairReversed()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition
        {
            Enter = Recipes.SlideIn(Edge.Right, distance: 100, duration: 100, ease: "linear"),
            Exit = Recipes.FadeOut(duration: 100, ease: "linear"),
            UseCompositor = false,
        };

        _ = transition.Start(from, to, forward: false, CancellationToken.None);

        Assert.Equal(0, to.Opacity);
        Assert.Equal(0, X(from));
        Frames.Advance(50);
        Assert.InRange(X(from), 20, 80);
        Assert.InRange(to.Opacity, 0.2, 0.8);
    }

    [AvaloniaFact]
    public void GoingBackOnTheCompositorPlaysTheForwardPairReversed()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition
        {
            Enter = Recipes.SlideIn(Edge.Right, distance: 100, duration: 100, ease: "linear"),
            Exit = Recipes.FadeOut(duration: 100, ease: "linear"),
        };

        _ = transition.Start(from, to, forward: false, CancellationToken.None);

        var entering = (CompositorAnimation)CompositorRegistry.Holder(to, CompositorTrackKind.Opacity)!;
        var leaving = (CompositorAnimation)CompositorRegistry.Holder(from, CompositorTrackKind.Translation)!;
        Assert.Equal(0, entering.ScratchOf(to)!.Opacity);
        Assert.Equal(0, leaving.ScratchOf(from)!.X);
        Frames.Advance(50);
        Assert.InRange(leaving.ScratchOf(from)!.X, 20, 80);
    }

    [AvaloniaFact]
    public void AnExplicitBackEnterWins()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition
        {
            Enter = Recipes.SlideIn(Edge.Right, distance: 100, duration: 100, ease: "linear"),
            Exit = Recipes.FadeOut(duration: 100, ease: "linear"),
            BackEnter = Recipes.SlideIn(Edge.Left, distance: 100, duration: 100, ease: "linear"),
            UseCompositor = false,
        };

        _ = transition.Start(from, to, forward: false, CancellationToken.None);

        Assert.Equal(-100, X(to));
    }

    [AvaloniaTheory]
    [InlineData(true, 1, 0)]
    [InlineData(false, 0, 1)]
    public void IncomingAboveForwardStacksByDirection(bool forward, int incoming, int outgoing)
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false };

        _ = transition.Start(from, to, forward, CancellationToken.None);

        Assert.Equal(incoming, to.ZIndex);
        Assert.Equal(outgoing, from.ZIndex);
        Assert.True(to.IsAnimating(Visual.ZIndexProperty));
    }

    [AvaloniaFact]
    public void HostStackingWritesNothing()
    {
        var (from, to) = ShowPair();
        from.ZIndex = 5;
        var transition = new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, Stacking = PageStacking.Host, UseCompositor = false };

        _ = transition.Start(from, to, true, CancellationToken.None);

        Assert.False(to.IsAnimating(Visual.ZIndexProperty));
        Assert.False(from.IsAnimating(Visual.ZIndexProperty));
        Assert.Equal(5, from.ZIndex);
    }

    [AvaloniaFact]
    public async Task ACancelledHostIsTakenOverFromWhereItStands()
    {
        var (a, b) = ShowPair();
        var transition = new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false };
        using var first = new CancellationTokenSource();
        var firstTask = transition.Start(a, b, true, first.Token);
        Frames.Advance(50);
        var entered = b.Opacity;

        first.Cancel();
        var secondTask = transition.Start(b, a, true, CancellationToken.None);

        Assert.InRange(entered, 0.2, 0.8);
        Assert.Equal(entered, b.Opacity);
        Frames.Advance(16);
        Assert.True(b.Opacity < entered, "the mid-entrance host did not exit from where it stood");
        await firstTask;
        Assert.Equal(TaskStatus.RanToCompletion, firstTask.Status);

        Settle(() => secondTask.IsCompleted);
        await secondTask;
        foreach (var host in new[] { a, b })
        {
            Assert.False(host.IsAnimating(Visual.OpacityProperty));
            Assert.False(host.IsAnimating(Visual.ZIndexProperty));
        }
    }

    [AvaloniaFact]
    public void ASecondSwapMidTransitionTakesTheEnteringPresenterOverFromWhereItStands()
    {
        var (host, _) = ShowHost(new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false });
        var second = new Border { Background = Brushes.Green };
        Swap(host, second);
        Frames.Advance(50);
        var entering = Presenter(host, second);
        var entered = entering.Opacity;

        Swap(host, new Border { Background = Brushes.Red });

        Assert.InRange(entered, 0.2, 0.8);
        Assert.Equal(entered, entering.Opacity);
        Frames.Advance(16);
        Assert.True(entering.Opacity < entered, "the mid-entrance presenter did not exit from where it stood");
        Settle(() => !entering.IsVisible);
        foreach (var presenter in host.GetVisualDescendants().OfType<ContentPresenter>())
        {
            Assert.False(presenter.IsAnimating(Visual.OpacityProperty));
            Assert.False(presenter.IsAnimating(Visual.ZIndexProperty));
        }
    }

    [AvaloniaFact]
    public void AnUnclaimedHostRevertsAfterOneFrame()
    {
        var (a, b) = ShowPair();
        var transition = new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false };
        using var cancel = new CancellationTokenSource();
        _ = transition.Start(a, b, true, cancel.Token);
        Frames.Advance(50);

        cancel.Cancel();
        Assert.True(b.IsAnimating(Visual.OpacityProperty), "the cancelled host reverted before a frame passed");
        Tick();
        Dispatcher.UIThread.RunJobs();

        foreach (var host in new[] { a, b })
        {
            Assert.False(host.IsAnimating(Visual.OpacityProperty));
            Assert.False(host.IsAnimating(Visual.ZIndexProperty));
            Assert.Equal(1, host.Opacity);
        }
    }

    [AvaloniaFact]
    public void EitherSideMayBeMissing()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false };

        var shown = transition.Start(null, to, true, CancellationToken.None);
        var dismissed = transition.Start(from, null, true, CancellationToken.None);
        Frames.Advance(50);

        Assert.InRange(to.Opacity, 0.2, 0.8);
        Assert.InRange(from.Opacity, 0.2, 0.8);
        Settle(() => shown.IsCompleted && dismissed.IsCompleted);
        Assert.True(shown.IsCompletedSuccessfully && dismissed.IsCompletedSuccessfully);
        Assert.False(from.IsVisible);
    }

    [AvaloniaFact]
    public void AutoRunsTransformAndOpacityOnTheCompositor()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition { Enter = Recipes.SlideIn(Edge.Right), Exit = Recipes.FadeOut() };

        _ = transition.Start(from, to, true, CancellationToken.None);

        Assert.IsType<CompositorAnimation>(CompositorRegistry.Holder(to, CompositorTrackKind.Translation));
        Assert.IsType<CompositorAnimation>(CompositorRegistry.Holder(from, CompositorTrackKind.Opacity));
        Assert.Empty(Presto.GetAnimations(to, "X"));
    }

    [AvaloniaFact]
    public void AutoFallsBackToTheUIThreadForBlur()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition { Enter = Recipes.BlurIn(), Exit = Recipes.FadeOut() };

        _ = transition.Start(from, to, true, CancellationToken.None);

        Assert.NotEmpty(Presto.GetAnimations(to, "Blur"));
        Assert.NotEmpty(Presto.GetAnimations(from, "Opacity"));
        Assert.Null(CompositorRegistry.Holder(from, CompositorTrackKind.Opacity));
    }

    [AvaloniaFact]
    public void ForcingTheCompositorWithBlurThrows()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition { Enter = Recipes.BlurIn(), UseCompositor = true };

        Assert.Throws<NotSupportedException>(() => { _ = transition.Start(from, to, true, CancellationToken.None); });
    }

    [AvaloniaFact]
    public void ACompositorTakeOverStartsWhereTheCancelledSlideStood()
    {
        var (a, b) = ShowPair();
        var transition = new PrestoPageTransition
        {
            Enter = Presto.Build().X(from: 100).Duration(100).Ease("linear"),
            Exit = Presto.Build().X(-100).Duration(100).Ease("linear"),
        };
        using var first = new CancellationTokenSource();
        _ = transition.Start(a, b, true, first.Token);
        Frames.Advance(50);

        first.Cancel();
        _ = transition.Start(b, a, true, CancellationToken.None);

        var exit = (CompositorAnimation)CompositorRegistry.Holder(b, CompositorTrackKind.Translation)!;
        Assert.InRange(exit.ScratchOf(b)!.X, 20, 80);
    }

    [AvaloniaFact]
    public void UpdateSeeksBothSidesOnTheUIThread()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition { Enter = SlideIn, Exit = FadeOut };

        transition.Update(0.5, from, to, true, 0, []);

        Assert.Equal(50, X(to), 3);
        Assert.Equal(0.5, from.Opacity, 3);
        Assert.Null(CompositorRegistry.Holder(to, CompositorTrackKind.Translation));
        Assert.Equal(1, to.ZIndex);
        transition.Update(0.25, from, to, true, 0, []);
        Assert.Equal(75, X(to), 3);
    }

    [AvaloniaFact]
    public void ResetRevertsWhatUpdateApplied()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition { Enter = SlideIn, Exit = FadeOut };
        transition.Update(0.5, from, to, true, 0, []);

        transition.Reset(from);
        transition.Reset(to);

        Assert.Equal(0, X(to));
        Assert.Equal(1, from.Opacity);
        Assert.False(to.IsAnimating(Visual.ZIndexProperty));
        Assert.False(from.IsAnimating(Visual.OpacityProperty));
    }

    [AvaloniaFact]
    public void UpdateWithVisibleItemsChangesNothing()
    {
        var (from, to) = ShowPair();
        var transition = new PrestoPageTransition { Enter = SlideIn, Exit = FadeOut };

        transition.Update(0.5, from, to, true, 0, [new PageTransitionItem(0, from, 0), new PageTransitionItem(1, to, 1)]);

        Assert.Equal(1, from.Opacity);
        Assert.Equal(0, X(to));
        Assert.False(to.IsAnimating(Visual.ZIndexProperty));
    }

    [AvaloniaFact]
    public void ACarouselSelectionChangeRunsTheTransitionEndToEnd()
    {
        var carousel = new Carousel
        {
            PageTransition = new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false },
            ItemsSource = new[] { "one", "two" },
        };
        var window = Show(carousel);
        var first = carousel.ContainerFromIndex(0)!;

        carousel.SelectedIndex = 1;
        window.UpdateLayout();
        Frames.Advance(50);
        var second = carousel.ContainerFromIndex(1)!;
        Assert.InRange(second.Opacity, 0.2, 0.8);
        Assert.InRange(first.Opacity, 0.2, 0.8);
        Settle(() => !first.IsVisible);

        Assert.False(first.IsVisible);
        foreach (var container in new[] { first, second })
        {
            Assert.Equal(1, container.Opacity);
            Assert.False(container.IsAnimating(Visual.OpacityProperty));
            Assert.False(container.IsAnimating(Visual.ZIndexProperty));
        }
    }

    [AvaloniaFact]
    public async Task ANavigationPagePushAndPopRunTheTransitionAndKeepItsOwnValues()
    {
        var navigation = new NavigationPage { PageTransition = new PrestoPageTransition { Enter = FadeIn, Exit = FadeOut, UseCompositor = false } };
        Show(navigation);
        await navigation.PushAsync(new ContentPage { Content = new Border { Background = Brushes.Blue } });
        var root = PagePresenter(navigation);

        var push = navigation.PushAsync(new ContentPage { Content = new Border { Background = Brushes.Green } });
        Settle(() => push.IsCompleted);
        await push;
        var pushed = PagePresenter(navigation);
        Assert.NotSame(root, pushed);
        AssertNavigationValues(pushed, zIndex: 1, root);

        var pop = navigation.PopAsync();
        Settle(() => pop.IsCompleted);
        await pop;
        Assert.Same(root, PagePresenter(navigation));
        AssertNavigationValues(root, zIndex: 0, pushed);
    }

    private static ContentPresenter PagePresenter(NavigationPage navigation) =>
        navigation.GetVisualDescendants().OfType<ContentPresenter>()
            .Single(p => p.Name is "PART_PagePresenter" or "PART_PageBackPresenter" && p.IsVisible);

    private static void AssertNavigationValues(ContentPresenter current, int zIndex, ContentPresenter previous)
    {
        Assert.False(previous.IsVisible);
        Assert.Equal(zIndex, current.ZIndex);
        foreach (var presenter in new[] { current, previous })
        {
            Assert.Equal(1, presenter.Opacity);
            Assert.Null(presenter.RenderTransform);
            Assert.False(presenter.IsAnimating(Visual.OpacityProperty));
            Assert.False(presenter.IsAnimating(Visual.ZIndexProperty));
        }
    }

    private (TransitioningContentControl Host, object First) ShowHost(PrestoPageTransition transition)
    {
        var first = new Border { Background = Brushes.Blue };
        var host = new TransitioningContentControl { PageTransition = transition, Content = first };
        Show(host);
        return (host, first);
    }

    private static ContentPresenter Presenter(TransitioningContentControl host, object? content) =>
        host.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Content == content);

    private static (Border From, Border To) ShowPair()
    {
        var from = new Border { Background = Brushes.Blue };
        var to = new Border { Background = Brushes.Green };
        Show(new Grid { Children = { from, to } });
        Tick();
        return (from, to);
    }
}
