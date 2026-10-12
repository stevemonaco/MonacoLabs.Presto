using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.ViewModels;

namespace PrestoGallery.Views;

public partial class MainView : UserControl
{
    private const double CompactBreakpoint = 720;

    private readonly IPageTransition _fadeThrough = Recipes.PageFadeThrough();
    private readonly IPageTransition _push = Recipes.PagePush(Edge.Right);
    private TopLevel? _top;
    private MainViewModel? _gallery;
    private bool? _compact;
    private CancellationTokenSource? _pushing;
    private Control? _page;
    private Thickness _wideMargin;

    public MainView()
    {
        InitializeComponent();
    }

    // On the top level, so Ctrl+F and Ctrl+B work with nothing focused and before a page can handle them.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        _top?.AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        _top?.AddHandler(PointerPressedEvent, HandleBackButton);
        _top?.AddHandler(TopLevel.BackRequestedEvent, HandleBackRequest);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _top?.RemoveHandler(KeyDownEvent, HandleKeyDown);
        _top?.RemoveHandler(PointerPressedEvent, HandleBackButton);
        _top?.RemoveHandler(TopLevel.BackRequestedEvent, HandleBackRequest);
        _top = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_gallery != null) _gallery.PropertyChanged -= GalleryChanged;
        _gallery = DataContext as MainViewModel;
        if (_gallery != null) _gallery.PropertyChanged += GalleryChanged;
        WatchPage(_gallery?.CurrentPage);
        _compact = null;
        InvalidateMeasure();
    }

    // Measured before the page host is templated, so the first page arrives as its initial content.
    protected override Size MeasureOverride(Size availableSize)
    {
        ApplyLayout(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    private void ApplyLayout(double width)
    {
        if (_gallery == null || double.IsInfinity(width)) return;
        var compact = width < CompactBreakpoint;
        if (compact == _compact) return;
        // A collapsed pane clips the nav through a negative margin, which Home must not keep.
        if (compact)
        {
            _wideMargin = Navigation.Margin;
            Navigation.Margin = default;
        }
        else if (_compact == true) Navigation.Margin = _wideMargin;
        // Reported before _compact changes, so a first AtHome plays no push.
        _gallery.IsCompact = compact;
        _compact = compact;
        Move(Navigation, compact ? Home : SidebarSlot);
        Move(PageColumn, compact ? PageLevel : PageSlot);
        Navigation.Classes.Set("compact", compact);
        PageColumn.Classes.Set("compact", compact);
        Shell.IsVisible = !compact;
        Levels.IsVisible = compact;
        Home.IsVisible = _gallery.AtHome;
        PageLevel.IsVisible = !_gallery.AtHome;
        ExpandPane.IsVisible = !compact && Pane.IsCollapsed(SidebarColumn);
        UpdatePageTransition();
    }

    private static void Move(Control child, Decorator to)
    {
        if (child.Parent == to) return;
        ((Decorator)child.Parent!).Child = null;
        to.Child = child;
    }

    // At Home the page host swaps without a transition, so a page opened from Home only rides in on its level.
    private void UpdatePageTransition() =>
        PageHost.PageTransition = _compact != true ? _fadeThrough : _gallery!.AtHome ? null : _push;

    private void GalleryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage)) WatchPage(_gallery!.CurrentPage);
        // Going wide from Home opens a page while _compact is still true; the switch itself plays no push.
        if (e.PropertyName != nameof(MainViewModel.AtHome) || _compact != true || !_gallery!.IsCompact) return;
        UpdatePageTransition();
        _pushing?.Cancel();
        _pushing = new CancellationTokenSource();
        var home = _gallery.AtHome;
        _ = _push.Start(home ? PageLevel : Home, home ? Home : PageLevel, !home, _pushing.Token);
    }

    private void WatchPage(Control? page)
    {
        if (_page != null) _page.Loaded -= ListenForBackLast;
        _page = page;
        if (_page != null) _page.Loaded += ListenForBackLast;
    }

    // Overlays subscribe to the back request as the page attaches, after the shell; Loaded follows their attach.
    private void ListenForBackLast(object? sender, RoutedEventArgs e)
    {
        _top?.RemoveHandler(TopLevel.BackRequestedEvent, HandleBackRequest);
        _top?.AddHandler(TopLevel.BackRequestedEvent, HandleBackRequest);
    }

    // Left unhandled at Home, so the platform can close or background the app.
    private void HandleBackRequest(object? sender, RoutedEventArgs e)
    {
        if (_compact == true) e.Handled = GoBack();
    }

    // Inside the page, Alt+Left goes back only when the page left it unhandled, so the Board's reorder keeps it.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && IsBackKey(e)) e.Handled = GoBack();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        // Outside the page the nav's and directional focus's arrow handling would take Alt+Left first.
        if (IsBackKey(e) && !(e.Source is Visual source && PageHost.IsVisualAncestorOf(source)))
        {
            e.Handled = GoBack();
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var compact = _compact == true;
        if (command && e.Key == Key.F && (!compact || _gallery is { AtHome: true }))
        {
            if (!compact && Pane.IsCollapsed(SidebarColumn)) TogglePane(null, e);
            Navigation.FocusSearch();
        }
        else if (command && e.Key == Key.B && !compact) TogglePane(null, e);
        else if (!Navigation.IsSearchFocused || _gallery is not { } viewModel) return;
        else if (e.Key == Key.Escape) viewModel.SearchText = "";
        else if (e.Key == Key.Enter) viewModel.OpenFirstMatch();
        else return;
        e.Handled = true;
    }

    private void HandleBackButton(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.XButton1Pressed) e.Handled = GoBack();
    }

    private static bool IsBackKey(KeyEventArgs e) => e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt;

    private bool GoBack()
    {
        if (_gallery is not { CanGoBack: true } viewModel) return false;
        viewModel.GoBack();
        return true;
    }

    private void TogglePane(object? sender, RoutedEventArgs e)
    {
        Pane.Toggle(SidebarColumn, Navigation);
        var collapsed = Pane.IsCollapsed(SidebarColumn);
        ExpandPane.IsVisible = collapsed;
        Splitter.IsVisible = !collapsed;
    }

    private ColumnDefinition SidebarColumn => Shell.ColumnDefinitions[0];
}
