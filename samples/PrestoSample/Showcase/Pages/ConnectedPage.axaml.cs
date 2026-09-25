using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace PrestoSample.Showcase.Pages;

public partial class ConnectedPage : ShowcasePage
{
    private const double ArtDuration = 560;
    private const double TitleDuration = 460;
    private const double ScatterDuration = 420;
    private const double Push = 40;

    private readonly Tile[] _tiles;
    private CompositorAnimation? _scatter;
    private CompositorAnimation? _shell;
    private Animation[] _content = [];
    private int _scattered = -1;
    private int? _open;

    public ConnectedPage()
    {
        InitializeComponent();
        _tiles = [.. Datasets.Albums.Select(BuildTile)];
        foreach (var tile in _tiles) Albums.Children.Add(tile.Card);
        PrestoAvalonia.Set(Play, new AvaloniaAnimationParams { ["Scale"] = 0 });
    }

    protected override void OnAttached()
    {
        foreach (var tile in _tiles)
        {
            tile.Art.AnimateLayout(ArtDuration, Eases.OutQuint, LayoutAnimateMode.PositionAndSize);
            tile.Title.AnimateLayout(TitleDuration, Eases.OutExpo, LayoutAnimateMode.PositionAndSize);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || _open == null) return;
        Close();
        e.Handled = true;
    }

    private Tile BuildTile(Datasets.Album album, int index)
    {
        var art = new Border
        {
            Classes = { "art" },
            Background = Palette.HueGradient(album.Hue),
            Child = new TextBlock { Text = album.Title[..1] },
        };
        var title = new TextBlock { Classes = { "heading" }, Text = album.Title };
        var artist = new TextBlock { Classes = { "caption" }, Text = album.Artist, Margin = new Thickness(10, 1, 10, 9), [Grid.RowProperty] = 2 };
        var shell = new Border { Classes = { "shell" }, [Grid.RowSpanProperty] = 3 };
        var artSlot = new Border { Child = art, Margin = new Thickness(8, 8, 8, 0) };
        var titleSlot = new Border { Child = title, Margin = new Thickness(10, 7, 10, 0), [Grid.RowProperty] = 1 };
        var card = new Grid
        {
            Classes = { "album" },
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            Children = { shell, artSlot, titleSlot, artist },
        };
        card.PointerPressed += (_, _) => Open(index);
        return new Tile(card, shell, artist, artSlot, titleSlot, art, title);
    }

    private void Open(int index)
    {
        if (_open != null) return;
        _open = index;
        var tile = _tiles[index];
        var album = Datasets.Albums[index];
        Lift(tile.ArtSlot, tile.Art, Hero);
        Lift(tile.TitleSlot, tile.Title, Heading);
        Meta.Text = $"{album.Artist} · {album.Year} · {album.Tracks.Count} tracks";
        Detail.IsHitTestVisible = true;
        Focus();

        Scope.Execute(_ =>
        {
            if (_scattered == index)
            {
                _scatter!.Play();
                _shell!.Play();
            }
            else
            {
                Retire();
                Scatter(index);
            }

            Stage.Animate(new()
            {
                ["Background"] = TweenValue.Of(new ImmutableSolidColorBrush(HsvColor.ToRgb(album.Hue, .55, .2))),
                Duration = ArtDuration,
                Ease = Eases.OutQuint,
            });
            Cancel();
            List(index);
            _content =
            [
                Presto.Animate(Tracks.Children, new AvaloniaAnimationParams
                {
                    ["Y"] = new TweenParams { To = 0, Ease = Eases.OutQuart },
                    ["Opacity"] = new TweenParams { To = 1, Ease = Eases.OutQuad },
                    Duration = 500,
                    Delay = Presto.Stagger(35, new StaggerParams { Start = 140 }),
                }),
                Play.Animate(new() { ["Scale"] = 1, Duration = 520, Delay = 220, Ease = Eases.OutBack(1.7) }),
                Presto.Animate((Control[])[Meta, Back], new AvaloniaAnimationParams { ["Opacity"] = 1, Duration = 320, Delay = 180, Ease = Eases.OutQuad }),
            ];
        });
    }

    private void Close()
    {
        if (_open is not { } index) return;
        _open = null;
        var tile = _tiles[index];
        Drop(tile.ArtSlot, tile.Art, Hero);
        Drop(tile.TitleSlot, tile.Title, Heading);
        Detail.IsHitTestVisible = false;

        Scope.Execute(_ =>
        {
            var scatter = _scatter!.Reverse();
            _shell!.Reverse();
            // Reopening before the cards are home plays the scatter forward again, which resolves this too.
            scatter.Then(_ =>
            {
                if (_open == null && scatter == _scatter) Retire();
            });
            Stage.Animate(new() { ["Background"] = TweenValue.Of(Palette.Surface), Duration = ArtDuration, Ease = Eases.OutQuint });
            Cancel();
            _content =
            [
                Presto.Animate(Tracks.Children, new AvaloniaAnimationParams
                {
                    ["Opacity"] = 0,
                    Duration = 220,
                    Delay = Presto.Stagger(25, new StaggerParams { From = StaggerFrom.Last }),
                    Ease = Eases.OutQuad,
                }),
                Play.Animate(new() { ["Scale"] = 0, Duration = 240, Ease = Eases.InQuad }),
                Presto.Animate((Control[])[Meta, Back], new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 200, Ease = Eases.OutQuad }),
            ];
        });
    }

    private void BackClicked(object? sender, RoutedEventArgs e) => Close();

    // Replace composition only overrides tweens already under way, so a staggered row still in its delay would play after the reversal.
    private void Cancel()
    {
        foreach (var animation in _content) animation.Cancel();
    }

    private void Retire()
    {
        _scatter?.Revert();
        _shell?.Revert();
        _scattered = -1;
    }

    private void Scatter(int index)
    {
        _scattered = index;
        var tile = _tiles[index];
        var centre = tile.Card.Bounds.Center;
        _scatter = PrestoAvalonia.AnimateOnCompositor(_tiles.Where(other => other != tile).Select(other => other.Card).ToArray(), new CompositorAnimationParams
        {
            ["Opacity"] = 0,
            ["Scale"] = .85,
            ["X"] = (FunctionValue)((target, _, _) => Away((Control)target, centre).X),
            ["Y"] = (FunctionValue)((target, _, _) => Away((Control)target, centre).Y),
            Duration = ScatterDuration,
            Delay = (FunctionValue)((_, i, _) => 22 * Math.Abs((i >= index ? i + 1 : i) - index)),
            Ease = Eases.OutQuint,
        });
        _shell = PrestoAvalonia.AnimateOnCompositor((Control[])[tile.Shell, tile.Artist], new CompositorAnimationParams
        {
            ["Opacity"] = 0,
            Duration = ScatterDuration,
            Ease = Eases.OutQuint,
        });
    }

    private void List(int index)
    {
        Tracks.Children.Clear();
        var tracks = Datasets.Albums[index].Tracks;
        for (var i = 0; i < tracks.Count; i++)
        {
            Tracks.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("28,*,Auto"),
                Children =
                {
                    new TextBlock { Classes = { "caption" }, Text = $"{i + 1}" },
                    new TextBlock { Text = tracks[i].Title, [Grid.ColumnProperty] = 1 },
                    new TextBlock { Classes = { "caption" }, Text = tracks[i].Length.ToString(@"m\:ss"), [Grid.ColumnProperty] = 2 },
                },
            });
        }
        PrestoAvalonia.Set(Tracks.Children, new AvaloniaAnimationParams { ["Y"] = 16, ["Opacity"] = 0 });
    }

    private static Vector Away(Control card, Point centre)
    {
        Vector away = card.Bounds.Center - centre;
        return away * (Push / Math.Max(away.Length, 1));
    }

    private static void Lift(Border slot, Control element, Border host)
    {
        slot.Child = new Border { Width = element.Bounds.Width, Height = element.Bounds.Height };
        element.Classes.Add("hero");
        host.Child = element;
    }

    private static void Drop(Border slot, Control element, Border host)
    {
        host.Child = null;
        element.Classes.Remove("hero");
        slot.Child = element;
    }

    private sealed record Tile(Grid Card, Border Shell, TextBlock Artist, Border ArtSlot, Border TitleSlot, Border Art, TextBlock Title);
}
