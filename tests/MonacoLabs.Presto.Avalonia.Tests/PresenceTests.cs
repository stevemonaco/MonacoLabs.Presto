using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Presence"/> and <see cref="PrestoAvalonia.AnimateExit"/> (MOTION-GAPS-PLAN §3): a leaving element's exit plays
/// on a ghost in the window's overlay. Leaving elements are red, measured in pixels of the rendered frame.
/// </summary>
public sealed class PresenceTests : CompositorTestBase
{
    private static readonly AnimationBuilder Fade = Presto.Build().Opacity(to: 0).Duration(200).Ease("linear");

    [AvaloniaFact]
    public void ARemovedPanelChildLeavesAGhostInItsSlotUntilItsExitEnds()
    {
        var item = Leaving();
        var (window, panel) = Show(new Border { Height = 30 }, item);
        var slot = SlotOf(item);

        panel.Children.Remove(item);
        Tick();

        Assert.Equal(slot, Box(window));
        Assert.Single(Ghosts(window));

        Frames.Advance(250, 16);
        Tick();

        Assert.Null(Extent(window, Colors.Red));
        Assert.Empty(Ghosts(window));
    }

    [AvaloniaFact]
    public void AnItemRemovedFromItsCollectionLeavesAGhost()
    {
        var items = new ObservableCollection<string> { "stays", "leaves" };
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((item, _) =>
            {
                var row = new Border { Height = 20, Width = 40, HorizontalAlignment = HorizontalAlignment.Left, Background = item == "leaves" ? Brushes.Red : Brushes.Blue };
                Presence.SetExit(row, Fade);
                return row;
            }),
        };
        var (window, _) = Show(list);
        var slot = Extent(window, Colors.Red)!.Value;

        items.Remove("leaves");
        Tick();

        Assert.Equal(slot, Box(window));
    }

    [AvaloniaFact]
    public void AGhostKeepsTheStylesAndResourcesTheRemovedItemHad()
    {
        var items = new ObservableCollection<string> { "stays", "leaves" };
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((item, _) =>
            {
                var row = new Border { Height = 20, Width = 40, HorizontalAlignment = HorizontalAlignment.Left, Classes = { item } };
                Presence.SetExit(row, Fade);
                return row;
            }),
        };
        var window = new Window { Content = new StackPanel { Children = { list } }, Background = Brushes.White, Width = 100, Height = 100 };
        window.Resources["Leaving"] = Brushes.Red;
        window.Styles.Add(new Style(x => x.OfType<ItemsControl>().Descendant().OfType<Border>().Class("leaves"))
        {
            Setters = { new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Leaving")) },
        });
        window.Show();
        Tick();
        var slot = Box(window);

        items.Remove("leaves");
        Tick();

        Assert.Equal(slot, Box(window));
        Assert.Equal(1, list.ItemCount);
    }

    [AvaloniaFact]
    public void ReplacedContentLeavesAGhostBesideTheNewContent()
    {
        var host = new ContentControl { Content = Leaving() };
        var (window, _) = Show(host);
        var slot = Box(window);

        host.Content = new Border { Width = 40, Height = 20, Background = Brushes.Blue, HorizontalAlignment = HorizontalAlignment.Right };
        Tick();

        Assert.Equal(slot, Box(window));
        Assert.NotNull(Extent(window, Colors.Blue));
    }

    [AvaloniaFact]
    public void RemovingTheWholeViewAnElementLivesInLeavesNoGhost()
    {
        var view = new StackPanel { Children = { Leaving(), new Border { Height = 20 } } };
        var (window, panel) = Show(view);

        panel.Children.Remove(view);
        Tick();

        Assert.Null(Extent(window, Colors.Red));
        Assert.Empty(Ghosts(window));
    }

    [AvaloniaFact]
    public void ClosingTheWindowLeavesNoGhost()
    {
        var (window, _) = Show(Leaving());
        var overlay = OverlayLayer.GetOverlayLayer(window)!;

        window.Close();

        Assert.DoesNotContain(overlay.Children, child => child is Canvas);
    }

    [AvaloniaFact]
    public void AGhostLeavingAScrolledListIsClippedToTheViewport()
    {
        var item = Leaving();
        var content = new StackPanel { Children = { new Border { Height = 40 }, item, new Border { Height = 200 } } };
        var scroller = new ScrollViewer { Height = 50, VerticalAlignment = VerticalAlignment.Top, Content = content };
        var (window, _) = Show(scroller);
        var visible = Box(window);

        content.Children.Remove(item);
        Tick();

        Assert.Equal(new Rect(0, 40, 40, 10), visible);
        Assert.Equal(visible, Box(window));
    }

    [AvaloniaFact]
    public void AnimateExitGhostsAnAttachedControlThatIsThenRemoved()
    {
        var item = new Border { Width = 40, Height = 20, Background = Brushes.Red, HorizontalAlignment = HorizontalAlignment.Left };
        var (window, panel) = Show(new Border { Height = 30 }, item);
        var slot = SlotOf(item);

        var exit = PrestoAvalonia.AnimateExit(item, Fade);
        panel.Children.Remove(item);
        Tick();

        Assert.NotNull(exit);
        Assert.Equal(slot, Box(window));
        Assert.Null(PrestoAvalonia.AnimateExit(item, Fade));
    }

    [AvaloniaFact]
    public void AnimateExitGhostsNothingHiddenOrAtOpacityZeroItselfOrThroughAnAncestor()
    {
        Border Red() => new() { Width = 40, Height = 20, Background = Brushes.Red };
        Border hidden = Red(), transparent = Red(), underHidden = Red(), underTransparent = Red();
        hidden.IsVisible = false;
        transparent.Opacity = 0;
        var (window, _) = Show(hidden, transparent, new Border { IsVisible = false, Child = underHidden }, new Border { Opacity = 0, Child = underTransparent });

        Assert.All(new[] { hidden, transparent, underHidden, underTransparent }, control => Assert.Null(PrestoAvalonia.AnimateExit(control, Fade)));
        Assert.Empty(Ghosts(window));
    }

    [AvaloniaFact]
    public void CancelingTheExitRemovesTheGhost()
    {
        var item = new Border { Width = 40, Height = 20, Background = Brushes.Red };
        var (window, _) = Show(item);

        PrestoAvalonia.AnimateExit(item, Fade)!.Cancel();
        Tick();

        Assert.Empty(Ghosts(window));
    }

    [AvaloniaFact]
    public void TheExitPlaysOnTheGhost()
    {
        var item = Leaving();
        var (window, panel) = Show(item);

        panel.Children.Remove(item);
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(100, 16);
        Tick();

        var ghost = Assert.IsType<Image>(Assert.Single(((Canvas)Assert.Single(Ghosts(window)).Children[0]).Children));
        Assert.InRange(ghost.Opacity, .3, .7);
        Assert.Equal(1, item.Opacity);
    }

    [AvaloniaFact]
    public void ABlurExitBlursTheGhostAndLeavesTheElementUntouched()
    {
        var item = new Border { Width = 40, Height = 20, Background = Brushes.Red };
        var (window, _) = Show(item);

        PrestoAvalonia.AnimateExit(item, Presto.Build().Blur(to: 10).Duration(200).Ease(Eases.None));
        Frames.Advance(100, 16);
        Tick();

        var ghost = Assert.IsType<Image>(Assert.Single(((Canvas)Assert.Single(Ghosts(window)).Children[0]).Children));
        Assert.InRange(Assert.IsType<BlurEffect>(ghost.Effect).Radius, 3, 7);
        Assert.Null(item.Effect);
    }

    [AvaloniaFact]
    public void AGhostIsDrawnWhereTheElementsRenderTransformHadMovedIt()
    {
        var item = Leaving();
        var (window, panel) = Show(item);
        PrestoAvalonia.Set(item, new() { ["X"] = 20, ["Scale"] = .5 });
        Tick();
        var drawn = Box(window);

        panel.Children.Remove(item);
        Tick();

        Assert.Equal(drawn, Box(window));
    }

    [AvaloniaFact]
    public void AClippingAncestorShrinkingAsTheElementLeavesKeepsItsExitStartSizeForTheGhost()
    {
        var item = Leaving();
        var clip = new Border { ClipToBounds = true, VerticalAlignment = VerticalAlignment.Top, Child = new StackPanel { Children = { new Border { Height = 30 }, item } } };
        var (window, _) = Show(clip);
        var slot = SlotOf(item);

        ((Panel)item.Parent!).Children.Remove(item);
        Tick();

        Assert.Equal(30, clip.Bounds.Height);
        Assert.Equal(slot, Box(window));
    }

    [AvaloniaFact]
    public void AZeroSizedElementLeavesNoGhost()
    {
        var item = Leaving();
        item.Width = 0;
        var (window, panel) = Show(item, new Border { Height = 20 });

        Assert.Null(PrestoAvalonia.AnimateExit(item, Fade));
        panel.Children.Remove(item);
        Tick();

        Assert.Empty(Ghosts(window));
    }

    [AvaloniaFact]
    public void TheExitCarriesThePresenceOrigin()
    {
        List<Timer> created = [];
        Presto.Engine.Diagnostics.Enabled = true;
        Presto.Engine.Diagnostics.TimerCreated += created.Add;
        try
        {
            var item = Leaving();
            var (_, panel) = Show(item, new Border { Height = 20 });

            panel.Children.Remove(item);
            Tick();

            Assert.Equal("Presence", Assert.Single(created).Origin);
        }
        finally
        {
            Presto.Engine.Diagnostics.TimerCreated -= created.Add;
        }
    }

    [AvaloniaFact]
    public void AnimateExitGhostsAPageAPrestoTransitionHosts()
    {
        var page = new Border { Width = 40, Height = 20, Background = Brushes.Red, HorizontalAlignment = HorizontalAlignment.Left };
        var host = new TransitioningContentControl { PageTransition = new PrestoPageTransition(), Content = page };
        var (window, _) = Show(host);
        var slot = SlotOf(page);

        Assert.NotNull(PrestoAvalonia.AnimateExit(page, Fade));
        Tick();

        Assert.Single(Ghosts(window));
        Assert.Equal(slot, Box(window));
    }

    [AvaloniaFact]
    public void WithoutAnExitADepartureLeavesNoGhost()
    {
        var item = new Border { Width = 40, Height = 20, Background = Brushes.Red };
        Presence.SetEnter(item, Presto.Build().Opacity(from: 0).Duration(100));
        var (window, panel) = Show(new Border { Height = 20 }, item);
        Frames.Advance(200, 16);

        panel.Children.Remove(item);
        Tick();

        Assert.Empty(Ghosts(window));
    }

    private static Border Leaving()
    {
        var item = new Border { Width = 40, Height = 20, Background = Brushes.Red, HorizontalAlignment = HorizontalAlignment.Left };
        Presence.SetExit(item, Fade);
        return item;
    }

    private static (Window Window, StackPanel Panel) Show(params Control[] children)
    {
        var panel = new StackPanel();
        panel.Children.AddRange(children);
        var window = new Window { Content = panel, Background = Brushes.White, Width = 100, Height = 100 };
        window.Show();
        Tick();
        return (window, panel);
    }
}
