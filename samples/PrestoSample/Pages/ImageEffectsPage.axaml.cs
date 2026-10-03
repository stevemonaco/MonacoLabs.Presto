using System.Globalization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// The colour filters of <see cref="FilteredImage"/>: a slider per filter printing the XAML and builder it amounts to,
/// thumbnails that colorize on hover, a develop entrance, a duotone that follows its theme variant and a looping hue
/// cycle, a slideshow with a transition picker and a card that dissolves in and pixelates out.
/// </summary>
public partial class ImageEffectsPage : ShowcasePage
{
    public static readonly AnimationBuilder Colorize;
    public static readonly AnimationBuilder Develop;
    public static readonly AnimationBuilder HueCycle;
    public static readonly AnimationBuilder DissolveIn;
    public static readonly AnimationBuilder PixelateOut;

    private static readonly string ColorizeSnippet;
    private static readonly string DevelopSnippet;
    private static readonly string HueCycleSnippet;
    private static readonly string CardSnippet;

    private readonly Bitmap[] _photos = new[] { "mongolia.jpg", "margarita-sunset.jpg", "port-blanc.jpg" }
        .Select(file => new Bitmap(AssetLoader.Open(new Uri($"avares://PrestoSample/Assets/Photos/{file}")))).ToArray();
    private int _photo;

    private static readonly (StyledProperty<double> Property, double Identity)[] Filters =
    [
        (PrestoImageBase.BrightnessProperty, 1),
        (PrestoImageBase.ContrastProperty, 1),
        (PrestoImageBase.SaturateProperty, 1),
        (PrestoImageBase.GrayscaleProperty, 0),
        (PrestoImageBase.SepiaProperty, 0),
        (PrestoImageBase.HueRotateProperty, 0),
        (PrestoImageBase.InvertProperty, 0),
        (PrestoImageBase.TintProperty, 0),
        (PrestoImageBase.DuotoneProperty, 0),
    ];

    static ImageEffectsPage()
    {
        (Colorize, var colorize) = Snippet.Of(Recipes.Colorize());
        ColorizeSnippet = Snippet.Declare("public static readonly AnimationBuilder Colorize", colorize);
        (Develop, var develop) = Snippet.Of(Recipes.Develop());
        DevelopSnippet = Snippet.Declare("public static readonly AnimationBuilder Develop", develop);
        (HueCycle, var hue) = Snippet.Of(Presto.Build().HueRotate(from: 0, to: 360).Duration(6000).Ease(Eases.None).Loop());
        HueCycleSnippet = Snippet.Declare("public static readonly AnimationBuilder HueCycle", hue);
        (DissolveIn, var dissolveIn) = Snippet.Of(Presto.Build().Dissolve(from: 1, to: 0).Duration(900).Ease(Eases.OutQuad));
        (PixelateOut, var pixelateOut) = Snippet.Of(Presto.Build()
            .Pixelate(to: 32, ease: Eases.InQuad)
            .Opacity(to: 0, delay: 300, duration: 400)
            .Duration(700));
        CardSnippet = $"""
            {Snippet.Declare("public static readonly AnimationBuilder DissolveIn", dissolveIn)}

            {Snippet.Declare("public static readonly AnimationBuilder PixelateOut", pixelateOut)}

            // Hide
            var exit = Card.Animate(PixelateOut);
            await exit;
            CardFrame.Child = null;
            exit.Revert();

            // Show: Presence.Enter plays DissolveIn
            CardFrame.Child = Card;
            """;
    }

    public ImageEffectsPage()
    {
        InitializeComponent();
        Photo.TintColor = TintColor(TintHueSlider.Value);
        Photo.PropertyChanged += PhotoChanged;
        DuotonePhoto.ActualThemeVariantChanged += (_, _) => FollowTheme();
        PrintFilters();
        ColorizeCode.CSharp = ColorizeSnippet;
        DevelopCode.CSharp = DevelopSnippet;
        HueCode.CSharp = HueCycleSnippet;
        CardCode.CSharp = CardSnippet;
        KindBox.ItemsSource = Enum.GetValues<ImageTransitionKind>();
        KindBox.SelectedItem = ImageTransitionKind.Dissolve;
        KindBox.SelectionChanged += (_, _) => TransitionChanged();
        foreach (var slider in new[] { DurationSlider, AngleSlider, PeakSlider }) slider.ValueChanged += (_, _) => TransitionChanged();
        PrintTransition();
    }

    private static Color TintColor(double hue) => HsvColor.ToRgb(hue, .8, 1);

    private void PhotoChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PrestoImageBase.TintColorProperty || Array.Exists(Filters, filter => filter.Property == e.Property)) PrintFilters();
    }

    private void PrintFilters()
    {
        List<(string Name, string Value)> changed = [];
        foreach (var (property, identity) in Filters)
        {
            var value = Photo.GetValue(property);
            if (Math.Abs(value - identity) > 1e-3) changed.Add((property.Name, value.ToString("0.##", CultureInfo.InvariantCulture)));
        }
        if (Photo.Tint > 1e-3) changed.Add(("TintColor", Photo.TintColor.ToString()));
        if (Photo.Duotone > 1e-3)
        {
            changed.Add(("DuotoneShadow", Photo.DuotoneShadow.ToString()));
            changed.Add(("DuotoneHighlight", Photo.DuotoneHighlight.ToString()));
        }

        var xaml = string.Concat(changed.Select(filter => $" {filter.Name}=\"{filter.Value}\""));
        var builder = string.Concat(changed.Select(filter => filter.Value.StartsWith('#')
            ? $".{filter.Name}(Color.Parse(\"{filter.Value}\"))"
            : $".{filter.Name}({filter.Value})"));
        FilterCode.Code = changed.Count == 0
            ? "<presto:FilteredImage Source=\"/Assets/Photos/mongolia.jpg\" />\n\n// Every filter at its identity: the photo draws plain."
            : $"<presto:FilteredImage Source=\"/Assets/Photos/mongolia.jpg\"{xaml} />\n\nPresto.Build(){builder}.Animate(Photo);";
    }

    private void TintHueChanged(object? sender, RangeBaseValueChangedEventArgs e) => Photo.TintColor = TintColor(e.NewValue);

    private void ResetClicked(object? sender, RoutedEventArgs e)
    {
        foreach (var (property, _) in Filters) Photo.ClearValue(property);
    }

    private void ReplayClicked(object? sender, RoutedEventArgs e) => Scope.Execute(_ => Developed.Animate(Develop));

    private void PreviousClicked(object? sender, RoutedEventArgs e) => Show(_photo + _photos.Length - 1);

    private void NextClicked(object? sender, RoutedEventArgs e) => Show(_photo + 1);

    private void Show(int photo)
    {
        _photo = photo % _photos.Length;
        Slideshow.Source = _photos[_photo];
    }

    private void TransitionChanged()
    {
        Slideshow.SourceTransition = new ImageTransition
        {
            Kind = (ImageTransitionKind)KindBox.SelectedItem!,
            Builder = Presto.Build().Duration(DurationSlider.Value).Ease(Eases.InOutQuad),
            Angle = AngleSlider.Value,
            PeakPixelate = PeakSlider.Value,
        };
        PrintTransition();
    }

    private void PrintTransition()
    {
        var kind = (ImageTransitionKind)KindBox.SelectedItem!;
        AngleSlider.IsVisible = kind == ImageTransitionKind.Wipe;
        PeakSlider.IsVisible = kind == ImageTransitionKind.Pixelate;
        var tuning = kind switch
        {
            ImageTransitionKind.Wipe when AngleSlider.Value != 0 => $"    Angle = {AngleSlider.Value:0},\n",
            ImageTransitionKind.Pixelate when PeakSlider.Value != 24 => $"    PeakPixelate = {PeakSlider.Value:0},\n",
            _ => "",
        };
        var xaml = tuning == "" ? $"\n\n// or in XAML: SourceTransition=\"{kind} {DurationSlider.Value:0}\"" : "";
        SlideshowCode.Code = $$"""
            Slideshow.SourceTransition = new ImageTransition
            {
                Kind = ImageTransitionKind.{{kind}},
                Builder = Presto.Build().Duration({{DurationSlider.Value:0}}).Ease(Eases.InOutQuad),
            {{tuning}}};
            Slideshow.Source = next;{{xaml}}
            """;
    }

    private async void CardClicked(object? sender, RoutedEventArgs e)
    {
        if (CardFrame.Child is null)
        {
            CardFrame.Child = Card;
            CardButton.Content = "Hide";
            return;
        }

        CardButton.IsEnabled = false;
        var exit = Scope.Execute(_ => Card.Animate(PixelateOut));
        await exit;
        CardFrame.Child = null;
        _ = exit.Revert();
        CardButton.Content = "Show";
        CardButton.IsEnabled = true;
    }

    private void LightChanged(object? sender, RoutedEventArgs e) =>
        DuotoneScope.RequestedThemeVariant = LightToggle.IsChecked == true ? ThemeVariant.Light : null;

    private void FollowTheme()
    {
        var (shadow, highlight) = DuotonePhoto.ActualThemeVariant == ThemeVariant.Light
            ? (Color.Parse("#7c2d12"), Color.Parse("#fef3c7"))
            : (Color.Parse("#0c1445"), Color.Parse("#5eead4"));
        var (_, code) = Scope.Execute(_ => Snippet.Of(Presto.Build()
            .DuotoneShadow(shadow)
            .DuotoneHighlight(highlight)
            .Duration(800)
            .Ease(Eases.InOutQuad)
            .Animate(DuotonePhoto)));
        DuotoneCode.Code = $"// ActualThemeVariantChanged: {DuotonePhoto.ActualThemeVariant}\n{code};";
    }
}
