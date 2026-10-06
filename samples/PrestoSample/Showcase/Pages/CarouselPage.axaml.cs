using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Controls;

namespace PrestoSample.Showcase.Pages;

public partial class CarouselPage : ShowcasePage
{
    private const double DotPitch = 16;
    private const double MarkDuration = 420;
    // Each layer's offset as a share of its slide's distance from centre: negative lags behind the strip, positive leads it.
    private const double BackgroundDepth = -.5;
    private const double ArtDepth = -.2;
    private const double TitleLead = .35;

    private static readonly (string Title, string Blurb, string Icon, ImmutableSolidColorBrush Tint)[] Slides =
    [
        ("Throw it", "The Pager's x-only drag coasts on inertia and lands on a slide: flick hard to skip a few, or past the end to loop.", "PlayIcon", Palette.Accent),
        ("Parallax", "Every layer follows its own Map of the slide's OffsetOf, so each moves at its own rate.", "StarIcon", Palette.Aqua),
        ("Indicators", "The PagerIndicator widens the active dot while the ring draws the autoplay's progress.", "HeartIcon", Palette.Coral),
        ("Autoplay", "The pager advances every four seconds and holds while hovered or focused from the keyboard.", "GearIcon", Palette.AccentBright),
        ("Keyboard", "Left, Right, Home and End, or the arrows, spring the pager to a slide.", "StarIcon", Palette.AccentDim),
    ];

    private readonly List<(Control Background, Control Art, Control Title)> _layers = [];

    public CarouselPage()
    {
        InitializeComponent();
        for (var i = 0; i < Slides.Length; i++) Pager.Slides.Add(BuildSlide(i));
        Pager.PropertyChanged += (_, e) =>
        {
            if (e.Property != Pager.SelectedIndexProperty) return;
            Scope.Execute(_ => Ring.Animate(new() { ["X"] = Pager.SelectedIndex * DotPitch, Duration = MarkDuration, Ease = Eases.OutQuint }));
        };
    }

    protected override void OnAttached()
    {
        Follow.SetDraw(Ring, Pager.AutoplayProgress);
        #region snippet CarouselParallax
        foreach (var (slide, (background, art, title)) in _layers.Index())
        {
            var offset = Pager.OffsetOf(slide).Map(v => v * Pager.Bounds.Width);
            Follow.SetX(background, offset.Map(v => v * BackgroundDepth));
            Follow.SetX(art, offset.Map(v => v * ArtDepth));
            Follow.SetX(title, offset.Map(v => v * TitleLead));
        }
        #endregion
    }

    private Panel BuildSlide(int index)
    {
        var (title, blurb, icon, tint) = Slides[index];
        var background = new Panel
        {
            Background = new LinearGradientBrush
            {
                StartPoint = RelativePoint.TopLeft,
                EndPoint = RelativePoint.BottomRight,
                GradientStops = { new GradientStop(Color.FromArgb(0x55, tint.Color.R, tint.Color.G, tint.Color.B), 0), new GradientStop(Palette.Surface.Color, .75) },
            },
            Children =
            {
                new Ellipse { Width = 420, Height = 420, Margin = new Thickness(0, -140, -120, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Fill = tint, Opacity = .12 },
                new Ellipse { Width = 160, Height = 160, Margin = new Thickness(-40, 0, 0, -60), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Fill = tint, Opacity = .18 },
            },
        };
        var text = new StackPanel
        {
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Classes = { "eyebrow", "showcase" }, Text = $"0{index + 1} / 0{Slides.Length}" },
                new TextBlock { Text = title, FontSize = 40, FontWeight = FontWeight.SemiBold, Foreground = Palette.Ink },
                new TextBlock { Text = blurb, FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = Palette.InkDim },
            },
        };
        var art = new Border
        {
            [Grid.ColumnProperty] = 1,
            Width = 220,
            Height = 160,
            CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new LinearGradientBrush
            {
                StartPoint = RelativePoint.TopLeft,
                EndPoint = RelativePoint.BottomRight,
                GradientStops = { new GradientStop(tint.Color, 0), new GradientStop(Palette.AccentDeep.Color, 1) },
            },
            Child = new PathIcon
            {
                Width = 56,
                Height = 56,
                Foreground = Palette.Ink,
                Data = (Geometry)Application.Current!.FindResource(icon)!,
            },
        };
        _layers.Add((background, art, text));
        return new Panel
        {
            ClipToBounds = true,
            Children =
            {
                background,
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new Thickness(96, 0), Children = { text, art } },
            },
        };
    }

    private void PreviousClicked(object? sender, RoutedEventArgs e) => Pager.GoTo(Pager.SelectedIndex - 1);

    private void NextClicked(object? sender, RoutedEventArgs e) => Pager.GoTo(Pager.SelectedIndex + 1);
}
