using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// A long, live activity feed in a virtualized <see cref="ListBox"/> that uses every <see cref="Presence"/> trigger:
/// Initial on open, Reveal on scroll, Enter for live events and the new-events pill, Exit on dismiss, plus
/// <c>Animate.OnAttached</c> for the paging skeleton and <c>Animate.OnChanged</c> for the unread badge. Each builder counts
/// its plays from <c>OnBegin</c>, so the overlay shows what Presence classified.
/// </summary>
public partial class ActivityFeedPage : ShowcasePage
{
    public const double PageDelay = 600;
    private const double LiveInterval = 4000;
    private const double NearBottom = 600;

    private readonly AnimationBuilder _reveal;
    private readonly AnimationBuilder _attached;

    public ActivityFeedPage()
    {
        _reveal = Recipes.FadeUp(distance: 12).Delay(Presto.Stagger(30)).OnBegin(_ => ViewModel.RevealPlayed++);
        _attached = Recipes.FadeUp(distance: 12).OnBegin(_ => ViewModel.AttachedPlayed++);
        Resources["RowInitial"] = Recipes.FadeUp().Delay(Presto.Stagger(40)).OnBegin(_ => ViewModel.InitialPlayed++);
        Resources["RowEnter"] = Recipes.SlideIn(Edge.Top).OnBegin(_ => ViewModel.EnterPlayed++);
        Resources["RowExit"] = Presto.Build().ScaleY(0).Opacity(0).Duration(260).Ease(Eases.InQuad)
            .WhenReduced(reduced => reduced.Opacity(0))
            .OnBegin(_ => ViewModel.ExitPlayed++);
        Resources["SkeletonShimmer"] = Recipes.Shimmer().OnBegin(_ => ViewModel.AttachedPlayed++);
        Resources["BadgePop"] = Recipes.PopIn().OnBegin(_ => ViewModel.ChangedPlayed++);
        InitializeComponent();
        RevealBox.SelectedIndex = 1;
        Feed.AddHandler(ScrollViewer.ScrollChangedEvent, FeedScrolled);
    }

    public ActivityFeedViewModel ViewModel => (ActivityFeedViewModel)DataContext!;

    protected override void OnAttached() =>
        Presto.CreateTimer(new TimerParams
        {
            Duration = LiveInterval,
            Loop = true,
            OnLoop = _ => ViewModel.AddLive(above: Feed.Scroll!.Offset.Y > 0),
        });

    private void FeedScrolled(object? sender, ScrollChangedEventArgs e)
    {
        var scroll = Feed.Scroll!;
        if (scroll.Offset.Y <= 0)
        {
            ViewModel.Unseen = null;
            return;
        }
        if (ViewModel.Loading || scroll.Offset.Y + scroll.Viewport.Height < scroll.Extent.Height - NearBottom) return;
        ViewModel.BeginLoad();
        Scope.Execute(_ => Presto.CreateTimer(new TimerParams { Duration = PageDelay, OnComplete = _ => ViewModel.EndLoad() }));
    }

    private void RevealChanged(object? sender, RoutedEventArgs e)
    {
        var attached = UseOnAttached.IsChecked == true;
        Resources["RowReveal"] = RevealBox.SelectedIndex == 0 || attached ? null : _reveal;
        Resources["RevealOnce"] = RevealBox.SelectedIndex == 1;
        Resources["RowAttached"] = attached ? _attached : null;
    }

    private void DismissClicked(object? sender, RoutedEventArgs e) => ViewModel.Dismiss((ActivityEvent)((Control)sender!).DataContext!);

    private void PillClicked(object? sender, RoutedEventArgs e) => Feed.Scroll!.Offset = default;
}
