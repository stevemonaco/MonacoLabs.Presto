using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// A connected animation across two views, as the Community Toolkit's <c>ConnectedAnimationService</c> does between pages:
/// the gallery is swapped out for a detail view built from scratch, and the photo and city name fly between the two.
/// </summary>
public partial class ConnectedNavPage : ShowcasePage
{
    private const int Columns = 4;
    private const double HeroDuration = 620;
    private const double TitleDuration = 520;
    private static readonly string[] Months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    private const string PhotoKey = "destination-photo";
    private const string CityKey = "destination-city";

    private readonly Card[] _cards;
    private readonly ScrollViewer _gallery;
    private Detail? _detail;
    private CompositorAnimation? _cascade;
    private int? _open;

    public ConnectedNavPage()
    {
        InitializeComponent();
        _cards = [.. Datasets.Places.Select(BuildCard)];
        var grid = new UniformGrid { Columns = Columns };
        foreach (var card in _cards) grid.Children.Add(card.Root);
        // The header scrolls with the cards so the list's viewport covers the whole stage: a photo flying home is clipped to it.
        _gallery = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new StackPanel
                    {
                        Margin = new Thickness(5, 0),
                        Children =
                        {
                            new TextBlock { Classes = { "title" }, Text = "Where to next?" },
                            new TextBlock { Classes = { "caption" }, Text = $"{_cards.Length} destinations" },
                        },
                    },
                    grid,
                },
            },
        };
        Host.Child = _gallery;
        HowCode.Code = $"// PhotoFlight.cs\n{Snippets.ConnectedXamlArc}\n\n{Snippets.ConnectedNavOpen}";
    }

    private double Pace => SlowMotion.IsChecked == true ? 4 : 1;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || _open == null) return;
        Close();
        e.Handled = true;
    }

    private void Open(int index)
    {
        if (_open != null) return;
        _open = index;
        var card = _cards[index];
        var detail = _detail = BuildDetail(index);
        var pace = Pace;

        #region snippet ConnectedNavOpen
        // The gallery only records where its pieces are, keyed, and the detail view claims them: neither needs the other's controls.
        PrestoAvalonia.PrepareSharedElement(PhotoKey, card.Photo);
        PrestoAvalonia.PrepareSharedElement(CityKey, card.City);
        Host.Child = detail.Root;

        Scope.Execute(_ =>
        {
            PrestoAvalonia.AnimateSharedElement(PhotoKey, detail.Hero, PhotoFlight.Arc.Duration(HeroDuration * pace));
            PrestoAvalonia.AnimateSharedElement(CityKey, detail.City, TitleDuration * pace, Eases.OutExpo);

            _cascade?.Revert();
            _cascade = PrestoAvalonia.AnimateOnCompositor(detail.Coordinated,
                Recipes.FadeUp(distance: 28, duration: 480 * pace, ease: Eases.OutQuart).Delay(Presto.Stagger(45 * pace, new StaggerParams { Start = 200 * pace })));
        });
        #endregion
        Jam();
        Focus();
    }

    private void Close()
    {
        if (_open is not { } index || _detail is not { } detail) return;
        _open = null;
        var card = _cards[index];
        var pace = Pace;

        PrestoAvalonia.PrepareSharedElement(PhotoKey, detail.Hero);
        PrestoAvalonia.PrepareSharedElement(CityKey, detail.City);
        Host.Child = _gallery;

        Scope.Execute(_ =>
        {
            PrestoAvalonia.AnimateSharedElement(PhotoKey, card.Photo, PhotoFlight.Arc.Duration(HeroDuration * pace));
            PrestoAvalonia.AnimateSharedElement(CityKey, card.City, TitleDuration * pace, Eases.OutExpo);

            _cascade?.Revert();
            Control[] returning = [.. _cards.Where(other => other != card).Select(other => other.Root), card.Country];
            _cascade = PrestoAvalonia.AnimateOnCompositor(returning, Recipes.ScaleIn(scale: .9, duration: 420 * pace, ease: Eases.OutQuart)
                .Delay((FunctionValue)((target, _, _) => (120 + 70 * Distance((int)((Control)target).Tag!, index)) * pace)));
        });
        Jam();
    }

    // Posted below Render priority, so the flight is committed to the compositor before the dispatcher stops answering.
    private void Jam()
    {
        if (JamUi.IsChecked == true) UiThread.Freeze(TimeSpan.FromMilliseconds(1500));
    }

    private static double Distance(int from, int to) =>
        Math.Sqrt(Math.Pow(from % Columns - to % Columns, 2) + Math.Pow(from / Columns - to / Columns, 2));

    private Card BuildCard(Datasets.Place place, int index)
    {
        var photo = new Border { Classes = { "photo" }, Child = new Scenery { Hue = place.Hue, Index = index } };
        var city = new TextBlock { Classes = { "city" }, Text = place.City, Margin = new Thickness(2, 8, 0, 0) };
        var country = new TextBlock { Classes = { "caption" }, Text = place.Country, Margin = new Thickness(2, 1, 0, 0), Tag = index };
        var root = new Border { Classes = { "place", "showcase" }, Tag = index, Child = new StackPanel { Children = { photo, city, country } } };
        root.PointerPressed += (_, _) => Open(index);
        return new Card(root, photo, city, country);
    }

    private Detail BuildDetail(int index)
    {
        var place = Datasets.Places[index];
        var hero = new Border { Classes = { "photo", "hero" }, Child = new Scenery { Hue = place.Hue, Index = index } };
        var city = new TextBlock { Classes = { "city", "hero" }, Text = place.City };
        var back = new Button { Content = "Back" };
        back.Click += (_, _) => Close();
        var book = new Button { Content = "Book a trip", Classes = { "accent" } };
        book.Click += (_, _) => book.Animate(Presto.Build().Scale(k => k.To(1.08, duration: 110).To(1, duration: 320, ease: Eases.OutBack())));

        Control[] coordinated =
        [
            new TextBlock { Classes = { "caption" }, Text = place.Country, FontSize = 14 },
            new TextBlock { Classes = { "blurb" }, Text = place.Blurb, Margin = new Thickness(0, 12, 0, 16) },
            new WrapPanel { Children = { Fact($"Best in {Months[index * 5 % 12]}"), Fact($"{3 + index % 5}h {index * 13 % 60:00}m flight"), Fact($"★ {4.1 + index % 9 / 10.0:0.0}") } },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 12, 0, 0), Children = { book, back } },
        ];

        var text = new StackPanel { Children = { city } };
        foreach (var control in coordinated) text.Children.Add(control);
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("330,*"),
            ColumnSpacing = 24,
            Margin = new Thickness(5),
            Children = { new StackPanel { Children = { hero } }, text },
        };
        Grid.SetColumn(text, 1);
        return new Detail(root, hero, city, coordinated);
    }

    private static Border Fact(string text) => new() { Classes = { "fact" }, Child = new TextBlock { Text = text } };

    private sealed record Card(Border Root, Border Photo, TextBlock City, TextBlock Country);

    private sealed record Detail(Grid Root, Border Hero, TextBlock City, Control[] Coordinated);
}
