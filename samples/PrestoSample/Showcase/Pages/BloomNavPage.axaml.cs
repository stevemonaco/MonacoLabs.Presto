using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MonacoLabs.Presto;

namespace PrestoSample.Showcase.Pages;

public partial class BloomNavPage : ShowcasePage
{
    public static readonly AnimationBuilder TabGlide = Presto.Build().Duration(420).Ease(Eases.OutExpo);

    private static readonly string[] Names = ["Inbox", "Photos", "Music", "Settings"];
    private static readonly ImmutableSolidColorBrush[] Hues = [Palette.Accent, Palette.Aqua, Palette.Violet, Palette.Amber];

    private readonly TextBlock[] _labels;
    private int _selected;

    public BloomNavPage()
    {
        InitializeComponent();
        _labels = [.. Names.Select((name, i) => new TextBlock { Text = name, Foreground = i == 0 ? Palette.Ink : Palette.InkDim })];
        foreach (var label in _labels) Tabs.Items.Add(new TabStripItem { Content = label });
        Tabs.SelectedIndex = 0;
        Tabs.SelectionChanged += (_, _) => Select(Tabs.SelectedIndex);
        Header.Background = Tint(Hues[0].Color);
        Stage.Content = Build(0);
    }

    private void Select(int tab)
    {
        var old = _selected;
        _selected = tab;
        Stage.Content = Build(tab);

        Scope.Execute(_ =>
        {
            _labels[old].Animate(new() { ["Foreground"] = TweenValue.Of(Palette.InkDim), Duration = 300 });
            _labels[tab].Animate(new() { ["Foreground"] = TweenValue.Of(Palette.Ink), Duration = 300 });
            Header.Animate(new() { ["Background"] = TweenValue.Of(Tint(Hues[tab].Color)), Duration = Recipes.Tokens.Long, Ease = Eases.OutQuad });
        });
    }

    private static Border Build(int tab)
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
        return new Border { Background = Hues[tab], Padding = new(12), Child = grid };
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
