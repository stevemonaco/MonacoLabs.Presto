using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.DevTools;
using PrestoSample.ViewModels;

namespace PrestoSample.Views;

public partial class MainView : UserControl
{
    private const double RiseCascade = 300;
    private const double PageCascade = 150;

    public static readonly AnimationBuilder RiseIn = Presto.Build()
        .Opacity(1, from: 0).Y(0, from: 8).Duration(280).Delay((FunctionValue)RowOrder).Ease(Eases.OutQuart)
        .WhenReduced(reduced => reduced.Opacity(1).Duration(0).Delay(0));

    public static readonly AnimationBuilder Fold = Presto.Build().Duration(260).Ease(Eases.OutQuart).WhenReduced(reduced => reduced.Duration(0));

    public static readonly AnimationBuilder ChevronFolded = Presto.Build()
        .Rotate(-90).Ease(Springs.Snappy)
        .WhenReduced(reduced => reduced.Rotate(-90).Duration(0).Ease(Eases.None));

    public static readonly AnimationBuilder PageFolded = Presto.Build()
        .Opacity(0).Duration(120).Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(0).Duration(0));

    public static readonly AnimationBuilder PageUnfolded = Presto.Build()
        .Duration(200).Delay((FunctionValue)PageOrder).Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Duration(0).Delay(0));

    public static readonly AnimationBuilder HoverFade = Presto.Build()
        .Opacity(1).Duration(120).Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(1).Duration(0));

    public static readonly AnimationBuilder HoverNudge = Presto.Build().X(3).Duration(120).Ease(Eases.OutQuad).WhenReduced(_ => { });

    private readonly SidebarPane _pane;
    private TopLevel? _top;

    public MainView()
    {
        InitializeComponent();
        new NavSelectionPill(Nav);
        Nav.AddHandler(PointerPressedEvent, NavPressed, RoutingStrategies.Tunnel);
        _pane = new SidebarPane(Shell.ColumnDefinitions[0], Sidebar);
    }

    // On the top level, so Ctrl+F and Ctrl+B work with nothing focused and before a page can handle them.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        _top?.AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _top?.RemoveHandler(KeyDownEvent, HandleKeyDown);
        _top = null;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (command && e.Key == Key.F)
        {
            if (_pane.IsCollapsed) TogglePane(null, e);
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
        else if (command && e.Key == Key.B) TogglePane(null, e);
        else if (!SearchBox.IsFocused || DataContext is not MainViewModel viewModel) return;
        else if (e.Key == Key.Escape) viewModel.SearchText = "";
        else if (e.Key == Key.Enter) viewModel.OpenFirstMatch();
        else return;
        e.Handled = true;
    }

    private void TogglePane(object? sender, RoutedEventArgs e)
    {
        _pane.Toggle();
        ExpandPane.IsVisible = _pane.IsCollapsed;
        Splitter.IsVisible = !_pane.IsCollapsed;
    }

    private void ToggleDevTools(object? sender, RoutedEventArgs e) => PrestoDevTools.Toggle(TopLevel.GetTopLevel(this)!);

    private void NavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        // A section root only groups pages: putting it back leaves the current page selected and on screen.
        if (Nav.SelectedItem is GalleryPage page) viewModel.SelectedPage = page;
        else Nav.SelectedItem = viewModel.NavSelectedPage;
    }

    // Tunnelling and handled, so the TreeView never selects the section and scrolls the open page back into view.
    private void NavPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Nav).Properties.IsLeftButtonPressed
            || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true) is not { DataContext: NavSection section }) return;
        section.IsExpanded = !section.IsExpanded;
        e.Handled = true;
    }

    // The whole nav cascades in over RiseCascade ms however many rows it shows.
    private static TweenValue RowOrder(object target, int index, int total)
    {
        if (target is not Control { DataContext: { } item } header || header.FindAncestorOfType<TreeView>()?.DataContext is not MainViewModel gallery) return 0;
        var rows = gallery.VisibleSections.SelectMany(section => section.Pages.Prepend<object>(section)).ToList();
        return RiseCascade * rows.IndexOf(item) / rows.Count;
    }

    private static TweenValue PageOrder(object target, int index, int total) =>
        target is TreeViewItem { Parent: TreeViewItem section } page ? PageCascade * section.IndexFromContainer(page) / section.ItemCount : 0;
}
