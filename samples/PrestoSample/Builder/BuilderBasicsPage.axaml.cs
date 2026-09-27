using System.Globalization;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>The Animation basics like-button, spelled with the builder and printed beneath the stage.</summary>
public partial class BuilderBasicsPage : ShowcasePage
{
    private int _likes;

    public BuilderBasicsPage() => InitializeComponent();

    public Animation? Animation { get; private set; }

    protected override void OnAttached()
    {
        (Animation, CodeText.Text) = Snippet.Of(LikeButton.Animate(Presto.Build()
            .X(320)
            .Y(-40)
            .Rotate(from: -180)
            .Scale(1.4)
            .Opacity(0.4)
            .Background(Palette.Coral)
            .Duration(1250)
            .Delay(250)
            .Ease(Eases.InOutQuint)
            .Loop()
            .Alternate()));
    }

    private void Replay(object? sender, RoutedEventArgs e) => Animation?.Restart();

    private void Like(object? sender, RoutedEventArgs e) => LikeCount.Text = (++_likes).ToString(CultureInfo.InvariantCulture);
}
