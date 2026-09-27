using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>Per-property settings, both kinds of keyframes and typed values, each spelled with the builder and printed beneath its stage.</summary>
public partial class PropertiesPage : ShowcasePage
{
    private Animation? _settings;
    private Animation? _tweenKeyframes;
    private Animation? _keyframes;
    private Animation? _card;

    public PropertiesPage() => InitializeComponent();

    public IEnumerable<Animation?> Animations => [_settings, _tweenKeyframes, _keyframes, _card];

    protected override void OnAttached()
    {
        (_settings, SettingsCode.Text) = Snippet.Of(SettingsBox.Animate(Presto.Build()
            .X(from: -120, to: 120, ease: Eases.OutElastic())
            .Rotate(360, duration: 1600, delay: 400)
            .Duration(1200)
            .Loop()
            .Alternate()));

        (_tweenKeyframes, TweenKeyframesCode.Text) = Snippet.Of(Ball.Animate(Presto.Build()
            .Y(k => k.To(-40, duration: 300, ease: Eases.OutQuad).To(0, duration: 600, ease: Eases.OutBounce))
            .Loop()
            .LoopDelay(400)));

        (_keyframes, KeyframesCode.Text) = Snippet.Of(KeyframesBox.Animate(Presto.Build()
            .Keyframes(k => k
                .At(0, f => f.X(-120).Scale(1))
                .At(30, f => f.X(0).Scale(1.4).Rotate(90))
                .At(100, f => f.X(120).Scale(1).Rotate(180)))
            .Duration(2000)
            .Ease(Eases.InOutSine)
            .Loop()
            .Alternate()));

        (_card, CardCode.Text) = Snippet.Of(Card.Animate(Presto.Build()
            .Background(Palette.Violet)
            .CornerRadius(new CornerRadius(4, 40, 4, 40))
            .Property(Border.BorderThicknessProperty, TweenValue.Of(new Thickness(6)))
            .Duration(1400)
            .Ease(Eases.InOutQuad)
            .Loop()
            .Alternate()));
    }

    private void ReplaySettings(object? sender, RoutedEventArgs e) => _settings?.Restart();

    private void ReplayKeyframes(object? sender, RoutedEventArgs e)
    {
        _tweenKeyframes?.Restart();
        _keyframes?.Restart();
    }

    private void ReplayCard(object? sender, RoutedEventArgs e) => _card?.Restart();
}
