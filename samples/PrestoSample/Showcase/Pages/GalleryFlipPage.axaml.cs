using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace PrestoSample.Showcase.Pages;

public partial class GalleryFlipPage : ShowcasePage
{
    private const string Everything = "All";
    private const double ReflowDuration = 700;
    private const double EntryDuration = 420;
    private const double ExitDuration = 240;
    private const double RevealDuration = 260;

    private readonly ObservableCollection<Control> _shown = [];
    private readonly Dictionary<Datasets.Card, Border> _cards = [];
    private readonly HashSet<Control> _leaving = [];

    private Border? _open;
    private int _slot;

    public GalleryFlipPage()
    {
        InitializeComponent();
        Filters.ItemsSource = (string[])[Everything, .. Datasets.Categories];
        Filters.SelectedIndex = 0;
        // The cards are the items: an ItemsControl over a data template throws away and rebuilds the container of an
        // item the collection moves, which would leave a re-sorted card with no transition to ease it.
        Field.ItemsSource = _shown;
    }

    protected override void OnAttached()
    {
        foreach (var record in Datasets.Cards) _cards[record] = BuildCard(record);
        Apply();
    }

    private Border BuildCard(Datasets.Card record)
    {
        var card = CardView.Build(record, eyebrow: true);
        Arm(card);
        card.PointerPressed += Expand;
        return card;
    }

    private void Arm(Border card) => card.AnimateLayout(
        ReflowDuration,
        Eases.OutQuint,
        Sized.IsChecked == true ? LayoutAnimateMode.PositionAndSize : LayoutAnimateMode.Position);

    private void Rebuild(object? sender, RoutedEventArgs e)
    {
        if (_cards.Count > 0) Apply();
    }

    private void Rearm(object? sender, RoutedEventArgs e)
    {
        // Arming a control replaces the transition already on it, so the cards change mode without being rebuilt.
        if (_cards.Count > 0) Scope.Execute(_ => { foreach (var card in _cards.Values) Arm(card); });
    }

    private void Churn(object? sender, RoutedEventArgs e)
    {
        ByValue.IsChecked = ByValue.IsChecked != true;
        Scope.Execute(_ => Presto.CreateTimer(new TimerParams
        {
            Duration = ReflowDuration / 3,
            OnComplete = _ =>
            {
                if (_shown.Count < 3) return;
                var card = _shown[_shown.Count / 2];
                _leaving.Remove(card);
                _shown.Remove(card);
            },
        }));
    }

    private void Expand(object? sender, RoutedEventArgs e)
    {
        if (_open != null)
        {
            Collapse();
            return;
        }

        var card = (Border)sender!;
        _open = card;
        _slot = _shown.IndexOf(card);
        card.Classes.Remove("row");
        card.Classes.Add("expanded");
        // The card is the shared element and the transition it reflows with plays the move, so a second click inverts from
        // where the card is drawn; AnimateSharedElement is one-shot and would re-measure from the slot (SAMPLES-PLAN §8).
        _shown.Remove(card);
        Host.Child = card;
        Detail.IsVisible = true;
        Reveal(1);
    }

    private void Dismiss(object? sender, RoutedEventArgs e) => Collapse();

    private void Collapse()
    {
        if (_open is not { } card) return;
        _open = null;
        Host.Child = null;
        card.Classes.Remove("expanded");
        card.Classes.Set("row", AsRows.IsChecked == true);
        _shown.Insert(Math.Clamp(_slot, 0, _shown.Count), card);
        Reveal(0);
    }

    private void Reveal(double opacity) => Scope.Execute(_ =>
        Presto.Animate((Control[])[Veil, Chrome], new AvaloniaAnimationParams
        {
            ["Opacity"] = opacity,
            Duration = RevealDuration,
            Ease = Eases.OutQuad,
            OnComplete = _ => Detail.IsVisible = _open != null,
        }));

    private void Apply()
    {
        Collapse();
        foreach (var card in _cards.Values) card.Classes.Set("row", AsRows.IsChecked == true);

        var wanted = Wanted();
        List<Control> going = [];
        List<Control> coming = [];
        foreach (var card in _shown)
        {
            if (!wanted.Contains(CardView.Record(card)) && _leaving.Add(card)) going.Add(card);
        }
        foreach (var record in wanted)
        {
            var card = _cards[record];
            // A card the filter brings back while its exit is still playing enters again rather than fading out.
            if (_leaving.Remove(card) || !_shown.Contains(card)) coming.Add(card);
        }

        if (going.Count > 0) Leave(going);
        Reorder(wanted);
        if (coming.Count > 0) Enter(coming);
    }

    private IReadOnlyList<Datasets.Card> Wanted()
    {
        var category = (string?)Filters.SelectedItem ?? Everything;
        var wanted = Datasets.Cards.Where(record => category == Everything || record.Category == category);
        return
        [
            .. ByValue.IsChecked == true
                ? wanted.OrderByDescending(record => record.Value)
                : wanted.OrderBy(record => record.Title, StringComparer.Ordinal),
        ];
    }

    private void Reorder(IReadOnlyList<Datasets.Card> wanted)
    {
        for (var i = 0; i < wanted.Count; i++)
        {
            var card = _cards[wanted[i]];
            var at = _shown.IndexOf(card);
            if (at < 0) _shown.Insert(i, card);
            else if (at != i) _shown.Move(at, i);
        }
    }

    private void Enter(IReadOnlyList<Control> coming) => Scope.Execute(_ =>
        PrestoAvalonia.AnimateOnCompositor(coming, new CompositorAnimationParams
        {
            // Composition properties, so they meet the layout transition's translation rather than multiplying with it.
            ["Scale"] = TweenValue.FromTo(0.8, 1),
            ["Opacity"] = TweenValue.FromTo(0, 1),
            Duration = EntryDuration,
            Delay = Presto.Stagger(16),
            Ease = Eases.OutBack(2),
        }));

    private void Leave(IReadOnlyList<Control> going) => Scope.Execute(_ =>
        PrestoAvalonia.AnimateOnCompositor(going, new CompositorAnimationParams
        {
            ["Scale"] = 0.8,
            ["Opacity"] = 0,
            Duration = ExitDuration,
            Delay = Presto.Stagger(16),
            Ease = Eases.InQuad,
            OnComplete = _ =>
            {
                foreach (var card in going)
                {
                    if (_leaving.Remove(card)) _shown.Remove(card);
                }
            },
        }));
}
