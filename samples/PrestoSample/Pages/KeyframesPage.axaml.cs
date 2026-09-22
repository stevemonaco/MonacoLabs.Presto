using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class KeyframesPage : UserControl
{
    private static readonly Keyframe[] March =
    [
        new() { ["X"] = 60 },
        new() { ["X"] = 120 },
        new() { ["X"] = 180 },
        new() { ["X"] = 240 },
    ];

    private readonly List<Animation> _animations = [];

    public KeyframesPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _animations.Add(PerPropertySquare.Animate(new()
        {
            ["X"] = TweenValue.Keyframes(
                new TweenParams { To = 200, Duration = 500, Ease = Eases.OutQuad },
                new TweenParams { To = 80, Duration = 300, Delay = 200 },
                new TweenParams { To = 0, Duration = 600, Ease = Eases.InOutQuad }),
            Loop = true,
        }));

        _animations.Add(ArraySquare.Animate(new()
        {
            Keyframes = new Keyframe[]
            {
                new() { ["Y"] = -40, ["Rotate"] = -20 },
                new() { ["X"] = 220, Ease = Eases.OutQuad },
                new() { ["Y"] = 0, ["Rotate"] = 20 },
                new() { ["X"] = 0, ["Rotate"] = 0, Duration = 600 },
            },
            Duration = 2000,
            Loop = true,
        }));

        _animations.Add(PercentSquare.Animate(new()
        {
            Keyframes = new Dictionary<string, Keyframe>
            {
                ["0%"] = new() { ["X"] = 0, ["Scale"] = 1, ["Background"] = "#4c8dff" },
                ["30%"] = new() { ["X"] = 220, ["Background"] = "#f5c542" },
                ["60%"] = new() { ["Scale"] = 1.6 },
                ["100%"] = new() { ["X"] = 0, ["Scale"] = 1, ["Background"] = "#4c8dff" },
            },
            Duration = 2500,
            Ease = "linear",
            Loop = true,
        }));

        _animations.Add(LinearSquare.Animate(new()
        {
            Keyframes = March,
            Duration = 2000,
            Ease = "linear",
            Loop = true,
            Alternate = true,
        }));
        _animations.Add(EasedSquare.Animate(new()
        {
            Keyframes = March,
            Duration = 2000,
            PlaybackEase = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        }));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var animation in _animations) animation.Revert();
        _animations.Clear();
    }

    private void Replay(object? sender, RoutedEventArgs e)
    {
        foreach (var animation in _animations) animation.Restart();
    }
}
