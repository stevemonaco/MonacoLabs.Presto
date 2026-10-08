using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// The Phase 61 spike (GAPS-2-PLAN §7.3), kept as pinning tests: how Avalonia 12.1.2 opens and closes flyouts, context
/// menus, popups and tooltips headlessly, what canceling <c>Closing</c> and holding <c>IsOpen</c> at
/// <see cref="BindingPriority.Animation"/> do, and what the Fluent templates animate.
/// </summary>
public sealed class PopupSpikeTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData("flyout", true)]
    [InlineData("menu", true)]
    [InlineData("context", true)]
    [InlineData("popup", false)]
    [InlineData("tooltip", false)]
    public void PopupContentArrivesAsInitialAndOnReopenAsEnterUnlessItsTemplateWrapsItWithSiblings(string owner, bool wrapped)
    {
        var (window, anchor) = PopupStage();
        var content = Lime();
        var moments = new List<PresenceMoment>();
        content.AttachedToVisualTree += (_, _) => moments.Add(Presence.ClassifyArrival(content));
        var (open, close) = Owner(owner, window, anchor, content);
        Assert.DoesNotContain(window.GetVisualDescendants(), visual => visual.GetType().Name == "PopupOverlayLayer");

        open();
        Tick();
        close();
        Tick();
        open();
        Tick();

        // The first open creates the window's PopupOverlayLayer; a panel or ScrollViewer in the template stops the walk first.
        Assert.Equal([PresenceMoment.Initial, wrapped ? PresenceMoment.Initial : PresenceMoment.Enter], moments);
    }

    [AvaloniaFact]
    public void AFlyoutReusesItsPresenterInANewOverlayHostOnEachOpen()
    {
        var (window, anchor) = PopupStage();
        var flyout = new Flyout { Content = Lime() };
        flyout.ShowAt(anchor);
        Tick();
        var presenter = flyout.Popup.Child!;
        var host = presenter.FindAncestorOfType<OverlayPopupHost>();

        Assert.False(flyout.Popup.ShouldUseOverlayLayer);
        Assert.True(flyout.Popup.IsUsingOverlayLayer);
        Assert.Same(window, TopLevel.GetTopLevel(presenter));
        Assert.NotNull(host);

        flyout.Hide();
        Tick();
        flyout.ShowAt(anchor);
        Tick();

        Assert.Same(presenter, flyout.Popup.Child);
        Assert.NotSame(host, presenter.FindAncestorOfType<OverlayPopupHost>());
    }

    [AvaloniaTheory]
    [InlineData("flyout", "light")]
    [InlineData("flyout", "escape")]
    [InlineData("flyout", "hide")]
    [InlineData("menu", "light")]
    [InlineData("menu", "escape")]
    [InlineData("menu", "hide")]
    public void CancelingTheFirstClosingKeepsAFlyoutDrawnAndASecondHideClosesIt(string owner, string path)
    {
        var (window, anchor) = PopupStage();
        PopupFlyoutBase flyout = owner == "flyout" ? new Flyout { Content = Lime() } : new MenuFlyout { Items = { new MenuItem { Header = Lime() } } };
        var closings = CancelFirst(flyout);
        flyout.ShowAt(anchor);
        Tick();
        var drawn = Extent(window, Colors.Lime);

        Dismiss(window, flyout.Popup.Child!, path, flyout.Hide);
        Tick();

        Assert.Equal(1, closings());
        Assert.True(flyout.IsOpen);
        Assert.Equal(drawn, Extent(window, Colors.Lime));
        // Escape writes Popup.IsOpen = false before the canceled Closing, and nothing puts it back.
        Assert.Equal(path != "escape", flyout.Popup.IsOpen);

        flyout.Hide();
        Tick();

        Assert.Equal(2, closings());
        Assert.False(flyout.IsOpen);
        Assert.Null(Extent(window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("escape")]
    [InlineData("close")]
    public void CancelingTheFirstClosingKeepsAContextMenuDrawnButOnlyPopupCloseClosesItAgain(string path)
    {
        var (window, anchor) = PopupStage();
        var menu = new ContextMenu { Items = { new MenuItem { Header = Lime() } } };
        anchor.ContextMenu = menu;
        var closings = 0;
        menu.Closing += (_, e) => e.Cancel = ++closings == 1;
        menu.Open(anchor);
        Tick();
        var popup = menu.FindLogicalAncestorOfType<Popup>()!;
        var drawn = Extent(window, Colors.Lime);

        Dismiss(window, menu, path, menu.Close);
        Tick();

        Assert.Equal(1, closings);
        Assert.True(menu.IsOpen);
        Assert.False(popup.IsOpen);
        Assert.Equal(drawn, Extent(window, Colors.Lime));

        menu.Close();
        Tick();

        Assert.Equal(1, closings);
        Assert.Equal(drawn, Extent(window, Colors.Lime));

        popup.Close();
        Tick();

        Assert.Equal(2, closings);
        Assert.False(menu.IsOpen);
        Assert.Null(Extent(window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData("flyout", 1)]
    [InlineData("menu", 1)]
    [InlineData("context", 3)]
    public void AnOwnerDetachingClosesWhateverClosingSays(string owner, int closings)
    {
        var (window, anchor) = PopupStage();
        Func<int> raised;
        Func<bool> isOpen;
        if (owner == "context")
        {
            var menu = new ContextMenu { Items = { new MenuItem { Header = Lime() } } };
            anchor.ContextMenu = menu;
            var count = 0;
            menu.Closing += (_, e) => e.Cancel = ++count == 1;
            menu.Open(anchor);
            (raised, isOpen) = (() => count, () => menu.IsOpen);
        }
        else
        {
            PopupFlyoutBase flyout = owner == "flyout" ? new Flyout { Content = Lime() } : new MenuFlyout { Items = { new MenuItem { Header = Lime() } } };
            raised = CancelFirst(flyout);
            flyout.ShowAt(anchor);
            isOpen = () => flyout.IsOpen;
        }
        Tick();

        ((Panel)window.Content!).Children.Remove(anchor);
        Tick();

        Assert.Equal(closings, raised());
        Assert.False(isOpen());
        Assert.Null(Extent(window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void HoldingPopupIsOpenInsideItsChangeNotificationReopensItAndReleasingLeavesItOpen(bool comboBox)
    {
        var (popup, child, close) = OpenPopup(comboBox);
        var events = Events(popup, child);
        var host = child.FindAncestorOfType<OverlayPopupHost>();
        var hold = HoldOnFirstChange(popup, Popup.IsOpenProperty, e => events.Add($"IsOpen {e.NewValue}"));

        close();
        Tick();

        Assert.Equal(["detach", "closed", "IsOpen False", "attach", "opened"], events);
        Assert.NotSame(host, child.FindAncestorOfType<OverlayPopupHost>());
        Assert.False(popup.GetBaseValue(Popup.IsOpenProperty).GetValueOrDefault());

        hold()!.Dispose();
        Tick();

        Assert.True(popup.IsOpen);
        Assert.True(child.IsAttachedToVisualTree());
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AStandingIsOpenHoldKeepsAPopupInItsHostThroughAWriteAndClosesItOnRelease(bool comboBox)
    {
        var (popup, child, close) = OpenPopup(comboBox);
        var events = Events(popup, child);
        var host = child.FindAncestorOfType<OverlayPopupHost>();
        var hold = new AnimatedValue<bool>(popup, Popup.IsOpenProperty);
        hold.Push(true);
        var changes = 0;
        popup.PropertyChanged += (_, e) => changes += e.Property == Popup.IsOpenProperty ? 1 : 0;

        close();
        Tick();

        Assert.Empty(events);
        Assert.Equal(0, changes);
        Assert.False(popup.GetBaseValue(Popup.IsOpenProperty).GetValueOrDefault());
        Assert.Same(host, child.FindAncestorOfType<OverlayPopupHost>());

        hold.Dispose();
        Tick();

        Assert.Equal(["detach", "closed"], events);
        Assert.False(popup.IsOpen);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void LightDismissClosesAPopupAtOnceUnderEitherHold(bool standing)
    {
        var (window, anchor) = PopupStage();
        var child = Lime();
        var popup = new Popup { Child = child, PlacementTarget = anchor, IsLightDismissEnabled = true };
        ((Panel)window.Content!).Children.Add(popup);
        popup.IsOpen = true;
        Tick();
        if (standing)
            new AnimatedValue<bool>(popup, Popup.IsOpenProperty).Push(true);
        else
            HoldOnFirstChange(popup, Popup.IsOpenProperty);

        Press(window, new Point(150, 150));
        Tick();

        Assert.False(child.IsAttachedToVisualTree());
        Assert.Null(Extent(window, Colors.Lime));
        // Close sets IsOpen while it ignores its own change, so a nested hold leaves it reading true over a closed popup.
        Assert.Equal(!standing, popup.IsOpen);
    }

    [AvaloniaFact]
    public void AClearValueUnderAStandingHoldIsLostSoReleasingLeavesThePopupOpen()
    {
        var (popup, child, _) = OpenPopup(comboBox: false);
        var hold = new AnimatedValue<bool>(popup, Popup.IsOpenProperty);
        hold.Push(true);

        popup.ClearValue(Popup.IsOpenProperty);
        hold.Dispose();
        Tick();

        Assert.True(popup.GetBaseValue(Popup.IsOpenProperty).GetValueOrDefault());
        Assert.True(popup.IsOpen);
        Assert.True(child.IsAttachedToVisualTree());
    }

    [AvaloniaTheory]
    [InlineData("local")]
    [InlineData("leave")]
    [InlineData("press")]
    public void HoldingToolTipIsOpenInsideItsChangeNotificationReopensTheTipAndReleaseClosesIt(string path)
    {
        var (window, anchor, tip) = HoverTip();
        var events = Events(null, tip);
        var host = tip.FindAncestorOfType<OverlayPopupHost>();
        var hold = HoldOnFirstChange(anchor, ToolTip.IsOpenProperty, e => events.Add($"IsOpen False {e.Priority}"));

        CloseTip(window, anchor, path);
        Tick();

        var priority = path == "press" ? BindingPriority.Unset : BindingPriority.LocalValue;
        Assert.Equal(["detach", $"IsOpen False {priority}", "attach"], events);
        Assert.NotSame(host, tip.FindAncestorOfType<OverlayPopupHost>());
        Assert.True(ToolTip.GetIsOpen(anchor));

        hold()!.Dispose();
        Tick();

        Assert.False(ToolTip.GetIsOpen(anchor));
        Assert.False(tip.IsAttachedToVisualTree());
    }

    [AvaloniaTheory]
    [InlineData("local", false)]
    [InlineData("leave", false)]
    [InlineData("press", true)]
    public void AStandingToolTipHoldKeepsTheTipButLosesTheServicesClearValue(string path, bool stuck)
    {
        var (window, anchor, tip) = HoverTip();
        var host = tip.FindAncestorOfType<OverlayPopupHost>();
        var hold = new AnimatedValue<bool>(anchor, ToolTip.IsOpenProperty);
        hold.Push(true);

        CloseTip(window, anchor, path);
        Tick();

        Assert.Same(host, tip.FindAncestorOfType<OverlayPopupHost>());

        hold.Dispose();
        Tick();

        Assert.Equal(stuck, ToolTip.GetIsOpen(anchor));
        Assert.Equal(stuck, tip.IsAttachedToVisualTree());
    }

    [AvaloniaFact]
    public void AClosedToolTipIsDetachedAtOpacityZeroWithItsBoundsKeptAndRaisesNoClosingEvent()
    {
        var (_, anchor) = PopupStage();
        var tip = Lime();
        ToolTip.SetTip(anchor, tip);
        var closings = 0;
        ToolTip.AddToolTipClosingHandler(anchor, (_, _) => closings++);
        ToolTip.SetIsOpen(anchor, true);
        Tick();
        var toolTip = tip.FindAncestorOfType<ToolTip>()!;
        var size = toolTip.Bounds.Size;
        var opened = closings;
        (bool Attached, Size Size, double Opacity)? seen = null;
        anchor.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToolTip.IsOpenProperty) seen = (toolTip.IsAttachedToVisualTree(), toolTip.Bounds.Size, toolTip.Opacity);
        };

        ToolTip.SetIsOpen(anchor, false);

        // Open closes the previous popup first, which is the only path that raises ToolTipClosing.
        Assert.Equal(1, opened);
        Assert.Equal(opened, closings);
        Assert.Equal((false, size, 0d), seen);
        Assert.True(size.Width > 0);
    }

    [AvaloniaFact]
    public void EachOwnerCarriesItsOwnPlacementAndAFlipShowsOnlyInTheRects()
    {
        var anchor = new Button { Width = 60, Height = 30, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
        new Window { Content = new Panel { Children = { anchor } }, Width = 200, Height = 200 }.Show();
        Tick();
        var flyout = new Flyout { Content = new Border { Width = 40, Height = 80 } };

        Assert.Equal(PlacementMode.Bottom, flyout.Placement);
        Assert.Equal(PlacementMode.Bottom, new Popup().Placement);
        Assert.Equal(PlacementMode.Pointer, new ContextMenu().Placement);
        Assert.Equal(PlacementMode.Pointer, ToolTip.GetPlacement(anchor));

        flyout.ShowAt(anchor);
        Tick();
        Tick();

        Assert.Equal(PlacementMode.Bottom, flyout.Popup.Placement);
        Assert.Equal(SlotOf(anchor).Top, SlotOf(flyout.Popup.Child!).Bottom);
    }

    [AvaloniaFact]
    public void FlyoutBaseIsNotStyleableSoStylesReachFlyoutsThroughTheirPresenters()
    {
        var (window, anchor) = PopupStage();
        window.Styles.Add(new Style(x => x.OfType<FlyoutPresenter>()) { Setters = { new Setter(Control.TagProperty, "flyout") } });
        window.Styles.Add(new Style(x => x.OfType<MenuFlyoutPresenter>().Class("fancy")) { Setters = { new Setter(Control.TagProperty, "menu") } });
        var flyout = new Flyout { Content = Lime() };
        var menu = new MenuFlyout { Items = { new MenuItem { Header = "m" } } };
        menu.FlyoutPresenterClasses.Add("fancy");

        flyout.ShowAt(anchor);
        menu.ShowAt(anchor);
        Tick();

        Assert.False(typeof(StyledElement).IsAssignableFrom(typeof(FlyoutBase)));
        Assert.Equal("flyout", flyout.Popup.Child!.Tag);
        Assert.Equal("menu", menu.Popup.Child!.Tag);
    }

    [AvaloniaFact]
    public void OnlyTheFluentToolTipAnimatesItsOwnOpenWithAnOpacityTransition()
    {
        var (window, anchor) = PopupStage();
        var flyout = new Flyout { Content = Lime() };
        var menu = new MenuFlyout { Items = { new MenuItem { Header = "m" } } };
        var context = new ContextMenu { Items = { new MenuItem { Header = "c" } } };
        anchor.ContextMenu = context;
        var combo = new ComboBox { ItemsSource = new[] { "a" }, VerticalAlignment = VerticalAlignment.Bottom };
        ((Panel)window.Content!).Children.Add(combo);
        ToolTip.SetTip(anchor, "t");
        Tick();
        flyout.ShowAt(anchor);
        menu.ShowAt(anchor);
        context.Open(anchor);
        combo.IsDropDownOpen = true;
        ToolTip.SetIsOpen(anchor, true);
        Tick();
        var hosts = window.GetVisualDescendants().OfType<OverlayPopupHost>().ToList();
        var toolTip = window.GetVisualDescendants().OfType<ToolTip>().Single();
        var dropDown = combo.GetVisualDescendants().OfType<Popup>().Single().Child!;

        Assert.Equal(5, hosts.Count);
        Assert.All(new[] { flyout.Popup.Child!, menu.Popup.Child!, context, dropDown }.Concat(hosts), control => Assert.Null(control.Transitions));
        var fade = Assert.IsType<DoubleTransition>(Assert.Single(toolTip.Transitions!));
        Assert.Equal(Visual.OpacityProperty, fade.Property);
        Assert.Equal(TimeSpan.FromMilliseconds(150), fade.Duration);
        Assert.Contains(":open", toolTip.Classes);
        Assert.True(toolTip.Opacity < 1);
    }

    private static (Action Open, Action Close) Owner(string owner, Window window, Control anchor, Control content)
    {
        switch (owner)
        {
            case "flyout":
                var flyout = new Flyout { Content = content };
                return (() => flyout.ShowAt(anchor), flyout.Hide);
            case "menu":
                var menu = new MenuFlyout { Items = { new MenuItem { Header = content } } };
                return (() => menu.ShowAt(anchor), menu.Hide);
            case "context":
                var context = new ContextMenu { Items = { new MenuItem { Header = content } } };
                anchor.ContextMenu = context;
                return (() => context.Open(anchor), context.Close);
            case "popup":
                var popup = new Popup { Child = content, PlacementTarget = anchor };
                ((Panel)window.Content!).Children.Add(popup);
                return (() => popup.IsOpen = true, () => popup.IsOpen = false);
            default:
                ToolTip.SetTip(anchor, content);
                return (() => ToolTip.SetIsOpen(anchor, true), () => ToolTip.SetIsOpen(anchor, false));
        }
    }

    private static Func<int> CancelFirst(PopupFlyoutBase flyout)
    {
        var closings = 0;
        flyout.Closing += (_, e) => e.Cancel = ++closings == 1;
        return () => closings;
    }

    private static (Popup Popup, Control Child, Action Close) OpenPopup(bool comboBox)
    {
        var (window, anchor) = PopupStage();
        var panel = (Panel)window.Content!;
        if (comboBox)
        {
            var combo = new ComboBox { ItemsSource = new[] { "a", "b" }, Width = 100, VerticalAlignment = VerticalAlignment.Bottom };
            panel.Children.Add(combo);
            Tick();
            combo.IsDropDownOpen = true;
            Tick();
            var dropDown = combo.GetVisualDescendants().OfType<Popup>().Single(p => p.Name == "PART_Popup");
            return (dropDown, dropDown.Child!, () => combo.IsDropDownOpen = false);
        }
        var popup = new Popup { Child = Lime(), PlacementTarget = anchor };
        panel.Children.Add(popup);
        popup.IsOpen = true;
        Tick();
        return (popup, popup.Child!, () => popup.IsOpen = false);
    }

    private static Func<AnimatedValue<bool>?> HoldOnFirstChange(AvaloniaObject owner, AvaloniaProperty<bool> property, Action<AvaloniaPropertyChangedEventArgs>? seen = null)
    {
        AnimatedValue<bool>? hold = null;
        owner.PropertyChanged += (_, e) =>
        {
            if (e.Property != property || e.Priority == BindingPriority.Animation || hold is not null) return;
            seen?.Invoke(e);
            (hold = new AnimatedValue<bool>(owner, property)).Push(true);
        };
        return () => hold;
    }

    private static List<string> Events(Popup? popup, Control child)
    {
        var events = new List<string>();
        if (popup is not null)
        {
            popup.Opened += (_, _) => events.Add("opened");
            popup.Closed += (_, _) => events.Add("closed");
        }
        child.AttachedToVisualTree += (_, _) => events.Add("attach");
        child.DetachedFromVisualTree += (_, _) => events.Add("detach");
        return events;
    }

    private static (Window Window, Control Anchor, Control Tip) HoverTip()
    {
        var (window, anchor) = PopupStage();
        return (window, anchor, Hover(window, anchor));
    }
}
