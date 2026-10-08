using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Gestures;

namespace PrestoSample.Showcase.Pages;

public partial class GesturesPage : ShowcasePage
{
    private const double RefreshDelay = 1200;
    private const double RowHeight = 40;
    private const double HandleHeight = 28;

    private readonly ObservableCollection<Datasets.Card> _inbox = new(Datasets.Cards.Take(8));
    private static readonly AnimationBuilder s_fresh = Presto.Build()
        .Height(from: 0, to: RowHeight).Opacity(from: 0, to: 1).Duration(420).Ease(Eases.OutQuart).Delay(Presto.Stagger(70));

    private readonly Stack<(Datasets.Card Card, int Index)> _dismissed = [];
    private int _fresh = 8;

    public GesturesPage()
    {
        InitializeComponent();
        HowCode.Code = $"{Snippets.GesturesPull}\n\n{Snippets.GesturesRefresh}\n\n{Snippets.GesturesCurtainSheet}\n\n{Snippets.GesturesCurtain}\n\n{Snippets.GesturesInboxSwipe}";
        foreach (var album in Datasets.Albums.Take(_fresh)) Feed.Children.Add(FeedRow(album));
        Inbox.ItemsSource = _inbox;
        Inbox.ItemTemplate = new FuncDataTemplate<Datasets.Card>((card, _) => InboxRow(card));
        Animate.SetItemLayout(Inbox, Presto.Build().Duration(360).Ease(Eases.OutQuart));
        foreach (var album in Datasets.Albums.Skip(8).Take(4)) Home.Children.Add(FeedRow(album));
        foreach (var album in Datasets.Albums.Skip(4).Take(3)) Notifications.Children.Add(FeedRow(album));
        foreach (var album in Datasets.Albums.Skip(12).Take(5)) Queue.Children.Add(FeedRow(album));
        Handle.Tapped += (_, _) => Curtain.DetentIndex = (Curtain.DetentIndex + 1) % Curtain.Detents.Count;
        Curtain.Position.VelocityOf().Changed += v => CurtainVelocity.Text = Utils.RoundPad(v.Value, 2);
        QueueSheet.Position.VelocityOf().Changed += v => SheetVelocity.Text = Utils.RoundPad(v.Value, 2);
        Puller.Pull.VelocityOf().Changed += v => PullVelocity.Text = Utils.RoundPad(v.Value, 2);
        Puller.Refresh = Refresh;
        #region snippet GesturesCurtain
        var pulled = Curtain.Position.Map(p => Travel > 0 ? (p - HandleHeight) / Travel : 0);
        Follow.SetY(Home, pulled.Map(p => .3 * p * Travel));
        Follow.SetScale(Home, pulled.Map([0, 1], [1, .94]));
        #endregion
    }

    private double Travel => Math.Max(0, CurtainStage.Bounds.Height - HandleHeight);

    #region snippet GesturesRefresh
    private async Task Refresh()
    {
        await Presto.CreateTimer(new TimerParams { Duration = RefreshDelay });
        var rows = Enumerable.Range(0, 3)
            .Select(i => FeedRow(Datasets.Albums[(_fresh + i) % Datasets.Albums.Count]))
            .ToArray();
        _fresh += rows.Length;
        foreach (var row in rows) Presence.SetEnter(row, s_fresh);
        Feed.Children.InsertRange(0, rows);
    }
    #endregion

    #region snippet GesturesInboxSwipe
    private Border InboxRow(Datasets.Card card)
    {
        var dismiss = new RelayCommand(() => Dismiss(card));
        var row = new Border
        {
            Classes = { "row" },
            Margin = new Thickness(0, 0, 0, 4),
            Focusable = true,
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = card.Owner, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Classes = { "caption" }, Text = card.Title },
                },
            },
        };
        Swipe.SetLeading(row, SwipeAction("ArchiveIcon", "archive", dismiss));
        Swipe.SetTrailing(row, SwipeAction("DeleteIcon", "delete", dismiss));
        Swipe.SetCommand(row, dismiss);
        Swipe.SetDismiss(row, true);
        Presence.SetExit(row, Recipes.FadeOut());
        return row;
    }
    #endregion

    private Button SwipeAction(string icon, string kind, ICommand command) => new()
    {
        Classes = { "swipe", kind },
        Command = command,
        Content = new PathIcon { Data = (Geometry)this.FindResource(icon)! },
    };

    private void Dismiss(Datasets.Card card)
    {
        _dismissed.Push((card, _inbox.IndexOf(card)));
        _inbox.Remove(card);
        UndoButton.IsEnabled = true;
    }

    private void UndoClicked(object? sender, RoutedEventArgs e)
    {
        if (!_dismissed.TryPop(out var dismissed)) return;
        UndoButton.IsEnabled = _dismissed.Count > 0;
        _inbox.Insert(Math.Min(dismissed.Index, _inbox.Count), dismissed.Card);
    }

    private static Border FeedRow(Datasets.Album album) => new()
    {
        Classes = { "row" },
        Child = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = album.Title },
                new TextBlock { Classes = { "caption" }, Text = $"{album.Artist} · {album.Year}" },
            },
        },
    };
}
