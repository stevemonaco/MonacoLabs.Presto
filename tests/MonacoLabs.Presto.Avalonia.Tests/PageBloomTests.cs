using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Recipes.PageBloom"/> (Phase 65d) on a 200x200 <see cref="TransitioningContentControl"/>, and the circular reveals under it.</summary>
public sealed class PageBloomTests : CompositorTestBase
{
    private static double Bloom => Recipes.Tokens.Long;

    [AvaloniaFact]
    public void TheClipGrowsFromThePressedPoint()
    {
        var (window, host) = ShowBloomHost();

        Press(window, new Point(30, 40));
        var next = Page(Brushes.Green);
        Swap(host, next);
        Frames.Advance(Bloom / 3, 16);
        var circle = Assert.IsType<EllipseGeometry>(Presenter(host, next).Clip);
        var early = circle.Rect.Width;
        Frames.Advance(Bloom / 3, 16);

        Assert.Equal(new Point(30, 40), circle.Rect.Center);
        Assert.InRange(early, 1, 400);
        Assert.True(circle.Rect.Width > early, $"the circle stayed {early} wide");
    }

    [AvaloniaFact]
    public void TheKeyboardOriginIsTheCenter()
    {
        var (window, host) = ShowBloomHost();

        Press(window, new Point(30, 40));
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        var next = Page(Brushes.Green);
        Swap(host, next);
        Frames.Advance(Bloom / 2, 16);

        Assert.Equal(new Point(100, 100), Assert.IsType<EllipseGeometry>(Presenter(host, next).Clip).Rect.Center);
    }

    [AvaloniaFact]
    public void ThePressIsSpentByTheBloomItStarted()
    {
        var (window, host) = ShowBloomHost();
        Press(window, new Point(30, 40));
        Swap(host, Page(Brushes.Green));
        Frames.Advance(Bloom * 2, 16);
        Settle(() => Presenters(host).Count(p => p.IsVisible) == 1);

        var next = Page(Brushes.Red);
        Swap(host, next);
        Frames.Advance(Bloom / 2, 16);

        Assert.Equal(new Point(100, 100), Assert.IsType<EllipseGeometry>(Presenter(host, next).Clip).Rect.Center);
    }

    [AvaloniaFact]
    public void TheNewPagesChildrenRiseInFromTheCenterAt65Percent()
    {
        var (_, host) = ShowBloomHost();
        var next = Page(Brushes.Green, tiles: 3);
        var tiles = ((Panel)next.Child!).Children;

        Swap(host, next);
        Frames.Advance(Bloom * .6, 16);
        Assert.All(tiles, tile => Assert.Equal(0, tile.Opacity));

        Frames.Advance(Bloom * .05 + Recipes.Tokens.StaggerStep / 2, 16);
        Assert.InRange(tiles[1].Opacity, .01, .99);
        Assert.Equal(0, tiles[0].Opacity);
        Assert.Equal(0, tiles[2].Opacity);

        Frames.Advance(2000, 16);
        Assert.All(tiles, tile => Assert.Equal(1, tile.Opacity));
    }

    [AvaloniaFact]
    public void RapidNavigationLeavesNoStackedLayers()
    {
        var (window, host) = ShowBloomHost();
        Border last = null!;

        foreach (var brush in new[] { Brushes.Green, Brushes.Red, Brushes.Yellow })
        {
            Press(window, new Point(30, 40));
            Swap(host, last = Page(brush));
            Frames.Advance(Bloom / 4, 16);
        }
        Settle(() => Presenters(host).Count(p => p.IsVisible) == 1);
        Frames.Advance(Bloom, 16);
        Settle(() => true);

        var shown = Assert.Single(Presenters(host), p => p.IsVisible);
        Assert.Same(last, shown.Content);
        Assert.All(Presenters(host), presenter =>
        {
            Assert.Null(presenter.Clip);
            Assert.False(presenter.IsAnimating(Visual.ClipProperty));
            Assert.Empty(Presto.GetAnimations(presenter));
        });
    }

    [AvaloniaFact]
    public void ReducedFadesTheNewPageInWithNoBloom()
    {
        var (window, host) = ShowBloomHost();
        Presto.Defaults.ReducedMotion = true;
        var next = Page(Brushes.Green, tiles: 3);

        Press(window, new Point(30, 40));
        Swap(host, next);
        Frames.Advance(Recipes.Tokens.Short / 2, 16);

        var presenter = Presenter(host, next);
        Assert.Null(presenter.Clip);
        Assert.InRange(presenter.Opacity, .1, .9);
        Assert.All(((Panel)next.Child!).Children, tile => Assert.Equal(1, tile.Opacity));
    }

    [AvaloniaFact]
    public void ReducedFinishesAnInterruptedBloomAtOnce()
    {
        var (_, host) = ShowBloomHost();
        var interrupted = Page(Brushes.Green);
        Swap(host, interrupted);
        Frames.Advance(Bloom / 4, 16);
        Assert.IsType<EllipseGeometry>(Presenter(host, interrupted).Clip);

        Presto.Defaults.ReducedMotion = true;
        Swap(host, Page(Brushes.Red));
        Frames.Advance(16, 16);

        Assert.Null(Presenter(host, interrupted).Clip);
        Frames.Advance(Recipes.Tokens.Short * 2, 16);
        Settle(() => Presenters(host).Count(p => p.IsVisible) == 1);
    }

    [AvaloniaFact]
    public void ACircularRevealFromAnElementGrowsFromItsCenter()
    {
        var opener = new Border { Width = 40, Height = 40, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var panel = new Border { Background = Brushes.Green };
        Show(new Grid { Children = { panel, opener } });
        Tick();

        Recipes.CircularReveal(opener, duration: 100, ease: "linear").Animate(panel);
        Frames.Advance(50, 10);

        var circle = Assert.IsType<EllipseGeometry>(panel.Clip);
        Assert.Equal(new Point(20, 20), circle.Rect.Center);
        Frames.Advance(100, 10);
        Assert.Null(panel.Clip);
    }

    [AvaloniaFact]
    public void AThemeRevealShowsTheNewLookInACircleOverTheOldThenRemovesItsPictures()
    {
        var content = new Border { Background = Brushes.Red };
        var toggle = new Border { Width = 20, Height = 20, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = Show(new Grid { Children = { content, toggle } });
        Tick();
        var layer = OverlayLayer.GetOverlayLayer(window)!;

        Recipes.ThemeReveal(window, () => content.Background = Brushes.Blue, toggle, duration: 400, ease: "linear");
        Frames.Advance(200, 20);
        Tick();

        Assert.Single(layer.Children);
        AssertNear(Colors.Blue, Row(window, 10)[10], 8);
        AssertNear(Colors.Red, Row(window, 190)[190], 8);

        Frames.Advance(400, 20);
        Settle(() => layer.Children.Count == 0);
    }

    [AvaloniaFact]
    public void ASecondThemeRevealReplacesTheFirst()
    {
        var content = new Border { Background = Brushes.Red };
        var window = Show(content);
        Tick();
        var layer = OverlayLayer.GetOverlayLayer(window)!;

        Recipes.ThemeReveal(window, () => content.Background = Brushes.Blue, duration: 400);
        Frames.Advance(100, 20);
        Recipes.ThemeReveal(window, () => content.Background = Brushes.Red, duration: 400);

        Assert.Single(layer.Children);
        Frames.Advance(1000, 20);
        Settle(() => layer.Children.Count == 0);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CancelingOrRevertingAThemeRevealRemovesItsPictures(bool revert, bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var content = new Border { Background = Brushes.Red };
        var window = Show(content);
        Tick();
        var layer = OverlayLayer.GetOverlayLayer(window)!;

        var reveal = Recipes.ThemeReveal(window, () => content.Background = Brushes.Blue, duration: 400);
        Frames.Advance(50, 10);
        Assert.Single(layer.Children);
        if (revert) reveal.Revert();
        else reveal.Cancel();

        Settle(() => layer.Children.Count == 0);
        Assert.Empty(layer.Children);
    }

    private (Window Window, TransitioningContentControl Host) ShowBloomHost()
    {
        var host = new TransitioningContentControl { PageTransition = Recipes.PageBloom(), Content = Page(Brushes.Blue) };
        var window = Show(host);
        Tick();
        Frames.Advance(Bloom * 2, 16);
        Dispatcher.UIThread.RunJobs();
        return (window, host);
    }

    private static Border Page(IBrush background, int tiles = 0)
    {
        var grid = new UniformGrid { Columns = tiles };
        for (var i = 0; i < tiles; i++) grid.Children.Add(new Border { Height = 20, Background = Brushes.White });
        return new Border { Background = background, Child = grid };
    }

    private static IEnumerable<ContentPresenter> Presenters(TransitioningContentControl host) =>
        host.GetVisualDescendants().OfType<ContentPresenter>();

    private static ContentPresenter Presenter(TransitioningContentControl host, object content) =>
        Presenters(host).Single(p => p.Content == content);
}
