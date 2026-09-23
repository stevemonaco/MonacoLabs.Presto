using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace PrestoSample.Pages;

public partial class CompositorPage : UserControl
{
    private Animation? _uiRing;
    private CompositorAnimation? _spin;

    public CompositorPage()
    {
        InitializeComponent();

        var center = UiRing.Width / 2;
        var size = DotRingScene.DotRadius * 2;
        for (var i = 0; i < DotRingScene.DotCount; i++)
        {
            var angle = DotRingScene.AngleOf(i);
            var dot = new Border
            {
                Classes = { "dot" },
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(DotRingScene.DotRadius),
            };
            Canvas.SetLeft(dot, center + Math.Cos(angle) * DotRingScene.RingRadius - DotRingScene.DotRadius);
            Canvas.SetTop(dot, center + Math.Sin(angle) * DotRingScene.RingRadius - DotRingScene.DotRadius);
            UiRing.Children.Add(dot);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _uiRing = Presto.Animate(UiRing.Children, DotRingScene.RingAnimation);
        _spin = SpinIcon.AnimateOnCompositor(new() { ["Rotate"] = 360, Duration = 1200, Ease = Eases.None, Loop = true });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _uiRing?.Revert();
        _uiRing = null;
        _spin?.Revert();
        _spin = null;
    }

    // Posted below Render priority so the button paints its pressed state before the dispatcher stops answering.
    private void Freeze(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() => Thread.Sleep(2000), DispatcherPriority.Background);

    private void PauseSpin(object? sender, RoutedEventArgs e) => _spin?.Pause();

    private void ResumeSpin(object? sender, RoutedEventArgs e) => _spin?.Resume();

    private void ReverseSpin(object? sender, RoutedEventArgs e) => _spin?.Reverse();

    private void SpinSpeedChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_spin is { } spin) spin.Speed = e.NewValue;
    }
}
