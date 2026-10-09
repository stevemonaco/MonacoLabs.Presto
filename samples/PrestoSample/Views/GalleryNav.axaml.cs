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

/// <summary>The gallery's nav tree, search box and settings, hosted by <see cref="MainView"/>.</summary>
public partial class GalleryNav : UserControl
{
    private const double RiseCascade = 300;
    private const double PageCascade = 150;

    public static readonly AnimationBuilder RiseIn = Presto.Build()
        .Opacity(1, from: 0).Y(0, from: 8).Duration(280).Delay((FunctionValue)RowOrder).Ease(Eases.OutQuart)
        .WhenReduced(reduced => reduced.Opacity(1).Duration(0).Delay(0));

    public static readonly AnimationBuilder PageUnfolded = Presto.Build()
        .Opacity(1, from: 0).Duration(200).Delay((FunctionValue)PageOrder).Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(1).Duration(0).Delay(0));

    public static readonly AnimationBuilder HoverFade = Presto.Build()
        .Opacity(1).Duration(120).Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(1).Duration(0));

    public static readonly AnimationBuilder Glide = Presto.Build().Duration(450).Ease(Eases.Spring(bounce: .2, duration: 450).Ease);

    public static readonly AnimationBuilder HoverNudge = Presto.Build().X(3).Duration(120).Ease(Eases.OutQuad).WhenReduced(_ => { });

    public GalleryNav()
    {
        InitializeComponent();
        Nav.AddHandler(PointerPressedEvent, NavPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>Raised by the hide-sidebar button.</summary>
    public event EventHandler<RoutedEventArgs>? CollapseRequested;

    public bool IsSearchFocused => SearchBox.IsFocused;

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void RequestCollapse(object? sender, RoutedEventArgs e) => CollapseRequested?.Invoke(this, e);

    private void ToggleDevTools(object? sender, RoutedEventArgs e) => PrestoDevTools.Toggle(TopLevel.GetTopLevel(this)!);

    private void NavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        // A section root only groups pages: putting it back leaves the current page selected and on screen.
        if (Nav.SelectedItem is GalleryPage page) viewModel.SelectedPage = page;
        else Nav.SelectedItem = viewModel.NavSelectedPage;
    }

    private void NavTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel
            && (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true) is { DataContext: GalleryPage page })
            viewModel.OpenFromNav(page);
    }

    private void NavKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel viewModel && Nav.SelectedItem is GalleryPage page) viewModel.OpenFromNav(page);
    }

    // Tunneling and handled, so the TreeView never selects the section and scrolls the open page back into view.
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
