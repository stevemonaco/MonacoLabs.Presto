using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class AnimationBasicsPage : UserControl
{
    private Animation? _animation;
    private int _likes;

    public AnimationBasicsPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _animation = LikeButton.Animate(new()
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
        });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animation?.Revert();
        _animation = null;
    }

    private void Replay(object? sender, RoutedEventArgs e) => _animation?.Restart();

    private void Like(object? sender, RoutedEventArgs e) => LikeCount.Text = (++_likes).ToString(CultureInfo.InvariantCulture);
}
