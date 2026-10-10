using System.Globalization;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

public partial class AnimationBasicsPage : ShowcasePage
{
    private Animation? _animation;
    private int _likes;

    public AnimationBasicsPage() => InitializeComponent();

    protected override void OnAttached()
    {
        (_animation, Code.Code) = Snippet.Of(LikeButton.Animate(new()
        {
            ["X"] = 320,
            ["Y"] = -40,
            ["Rotate"] = new TweenParams { From = -180 },
            ["Scale"] = 1.4,
            ["Opacity"] = 0.4,
            ["Background"] = "#ff6b6b",
            Duration = 1250,
            Delay = 250,
            Ease = Eases.InOutQuint,
            Loop = true,
            Alternate = true,
        }));
    }

    private void Replay(object? sender, RoutedEventArgs e) => _animation?.Restart();

    private void Like(object? sender, RoutedEventArgs e) => LikeCount.Text = (++_likes).ToString(CultureInfo.InvariantCulture);
}