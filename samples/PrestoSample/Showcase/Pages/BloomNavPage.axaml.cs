using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Showcase.Pages;

public partial class BloomNavPage : ShowcasePage
{
    private const double BloomDuration = 620;
    private const double SwapAt = .65;
    private const double FadeDuration = 260;
    private const double Rise = 24;
    private const double TabInset = 12;

    private static readonly string[] Names = ["Inbox", "Photos", "Music", "Settings"];
    private static readonly ImmutableSolidColorBrush[] Hues = [Palette.Accent, Palette.Aqua, Palette.Violet, Palette.Amber];

    private readonly TextBlock[] _labels;
    private Point? _pressed;
    private int _selected;

    public BloomNavPage()
    {
        InitializeComponent();
        _labels = [.. Names.Select((name, i) => new TextBlock { Text = name, Foreground = i == 0 ? Palette.Ink : Palette.InkDim })];
        for (var i = 0; i < _labels.Length; i++)
        {
            var tab = i;
            var button = new Button { Classes = { "tab" }, Content = _labels[i] };
            button.Click += (_, _) => Select(tab, button);
            Tabs.Children.Add(button);
        }
        Tabs.AddHandler(PointerPressedEvent, (_, e) => _pressed = e.GetPosition(Stage), RoutingStrategies.Tunnel);
        Header.Background = Tint(Hues[0].Color);
        Stage.Background = Hues[0];
        Screen.Child = Build(0);
    }

    protected override void OnAttached() => AfterFirstArrange(Tabs, () =>
    {
        var slot = Slot(_selected);
        PrestoAvalonia.Set(Underline, new AvaloniaAnimationParams { ["X"] = slot.X, ["Width"] = slot.Width });
    });

    private Rect Slot(int tab) => TabIndicator.Under(Tabs.Children[tab], Tabs, TabInset);

    // A keyboard click has no pointer press behind it, so the bloom grows from the tab's centre.
    private void Select(int tab, Button button)
    {
        var origin = _pressed ?? button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), Stage)!.Value;
        _pressed = null;
        if (tab == _selected) return;
        var old = _selected;
        _selected = tab;

        var hue = Hues[tab];
        var size = Stage.Bounds.Size;
        var radius = new Vector(Math.Max(origin.X, size.Width - origin.X), Math.Max(origin.Y, size.Height - origin.Y)).Length;
        var bloom = new Ellipse
        {
            Width = 2 * radius,
            Height = 2 * radius,
            Fill = hue,
            [Canvas.LeftProperty] = origin.X - radius,
            [Canvas.TopProperty] = origin.Y - radius,
        };
        Overlay.Children.Add(bloom);
        var content = Build(tab);

        Scope.Execute(_ =>
        {
            PrestoAvalonia.Set(bloom, new AvaloniaAnimationParams { ["Scale"] = 0 });
            PrestoAvalonia.Set(content.Children, new AvaloniaAnimationParams { ["Y"] = Rise, ["Opacity"] = 0 });
            #region snippet BloomNavBloom
            Presto.CreateTimeline(new TimelineParams { OnComplete = _ => Overlay.Children.RemoveRange(0, Overlay.Children.IndexOf(bloom) + 1) })
                .Add(bloom, new() { ["Scale"] = 1, Duration = BloomDuration, Ease = Eases.InOutQuart })
                .Call(_ =>
                {
                    Stage.Background = hue;
                    Screen.Child = content;
                }, BloomDuration * SwapAt)
                .Add(bloom, new() { ["Opacity"] = 0, Duration = FadeDuration, Ease = Eases.OutQuad }, BloomDuration)
                .Add(content.Children, new()
                {
                    ["Y"] = 0,
                    ["Opacity"] = 1,
                    Duration = 480,
                    Ease = Eases.OutQuart,
                    Delay = Presto.Stagger(40, new StaggerParams { From = StaggerFrom.Center }),
                }, "<<");
            #endregion

            var slot = Slot(tab);
            Underline.Animate(new() { ["X"] = slot.X, ["Width"] = slot.Width, Duration = 420, Ease = Eases.OutExpo });
            _labels[old].Animate(new() { ["Foreground"] = TweenValue.Of(Palette.InkDim), Duration = 300 });
            _labels[tab].Animate(new() { ["Foreground"] = TweenValue.Of(Palette.Ink), Duration = 300 });
            Header.Animate(new() { ["Background"] = TweenValue.Of(Tint(hue.Color)), Duration = BloomDuration, Ease = Eases.OutQuad });
        });
    }

    private static UniformGrid Build(int tab)
    {
        IEnumerable<Border> tiles = tab switch
        {
            0 => Datasets.Cards.Take(5).Select(card => Tile(card.Owner, card.Title)),
            1 => Datasets.Albums.Take(8).Select(album => Tile(album.Title, $"{album.Year}", Palette.HueGradient(album.Hue))),
            2 => Datasets.Albums.Take(6).Select(album => Tile(album.Title, album.Artist)),
            _ => Datasets.Metrics.Select(metric => Tile(
                metric.Readings[0].ToString($"N{metric.Decimals}", CultureInfo.InvariantCulture),
                $"{metric.Label} · {metric.Unit}")),
        };
        var grid = new UniformGrid { Columns = tab switch { 0 => 1, 1 => 4, 2 => 3, _ => 2 } };
        grid.Children.AddRange(tiles);
        return grid;
    }

    private static Border Tile(string heading, string caption, IBrush? background = null) => new()
    {
        Classes = { "tile" },
        Background = background ?? Palette.Surface,
        Child = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Children =
            {
                new TextBlock { Classes = { "heading" }, Text = heading },
                new TextBlock { Classes = { "caption" }, Text = caption },
            },
        },
    };

    private static ImmutableSolidColorBrush Tint(Color hue)
    {
        var from = Palette.SurfaceHigh.Color;
        byte Mix(byte a, byte b) => (byte)(a + ((b - a) * .3));
        return new(Color.FromRgb(Mix(from.R, hue.R), Mix(from.G, hue.G), Mix(from.B, hue.B)));
    }
}
