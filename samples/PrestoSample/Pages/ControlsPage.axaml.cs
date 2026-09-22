using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class ControlsPage : UserControl
{
    private Animation? _animation;
    private bool _syncingSlider;

    public ControlsPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _animation = Square.Animate(new()
        {
            ["X"] = 300,
            ["Rotate"] = 180,
            ["Scale"] = 1.5,
            ["Opacity"] = 0.35,
            ["Background"] = "#f5c542",
            Duration = 900,
            Loop = 2,
            Alternate = true,
            Autoplay = false,
            Ease = Eases.InOutQuad,
            OnUpdate = _ => UpdateReadouts(),
            // OnUpdate runs before the flags flip, so the last frame's readout would otherwise show Completed false.
            OnComplete = _ => UpdateReadouts(),
        });
        UpdateReadouts();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animation?.Revert();
        _animation = null;
    }

    private void Play(object? sender, RoutedEventArgs e) => Run(static a => a.Play());

    private void Pause(object? sender, RoutedEventArgs e) => Run(static a => a.Pause());

    private void Resume(object? sender, RoutedEventArgs e) => Run(static a => a.Resume());

    private void Restart(object? sender, RoutedEventArgs e) => Run(static a => a.Restart());

    private void Reverse(object? sender, RoutedEventArgs e) => Run(static a => a.Reverse());

    private void Alternate(object? sender, RoutedEventArgs e) => Run(static a => a.Alternate());

    private void Complete(object? sender, RoutedEventArgs e) => Run(static a => a.Complete());

    private void Cancel(object? sender, RoutedEventArgs e) => Run(static a => a.Cancel());

    private void Revert(object? sender, RoutedEventArgs e) => Run(static a => a.Revert());

    private void Reset(object? sender, RoutedEventArgs e) => Run(static a => a.Reset());

    private void Scrub(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingSlider) return;
        Run(a => a.Progress = e.NewValue);
    }

    private void StretchChanged(object? sender, RangeBaseValueChangedEventArgs e) => Run(a => a.Stretch(e.NewValue));

    private void Run(Action<Animation> control)
    {
        if (_animation == null) return;
        control(_animation);
        UpdateReadouts();
    }

    private void UpdateReadouts()
    {
        if (_animation is not { } animation) return;
        ReadoutText.Text = string.Create(CultureInfo.InvariantCulture,
            $"""
             CurrentTime       {animation.CurrentTime,8:0} / {animation.Duration:0} ms
             Progress          {animation.Progress,8:0.000}
             CurrentIteration  {animation.CurrentIteration,8}
             Began             {animation.Began,8}
             Completed         {animation.Completed,8}
             Paused            {animation.Paused,8}
             """);

        _syncingSlider = true;
        ScrubSlider.Value = animation.Progress;
        _syncingSlider = false;
    }
}
