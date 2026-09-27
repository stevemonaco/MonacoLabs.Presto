using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Pages;

public partial class CallbacksPage : UserControl
{
    private const double Dim = 0.15;

    private Indicator[] _indicators = [];
    private Animation? _animation;
    private Timer? _decay;
    private int _clicks;

    public CallbacksPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var begin = new Indicator(BeginDot, BeginCount);
        var loop = new Indicator(LoopDot, LoopCount);
        var beforeUpdate = new Indicator(BeforeUpdateDot, BeforeUpdateCount);
        var render = new Indicator(RenderDot, RenderCount);
        var update = new Indicator(UpdateDot, UpdateCount);
        var complete = new Indicator(CompleteDot, CompleteCount);
        var pause = new Indicator(PauseDot, PauseCount);
        _indicators = [begin, loop, beforeUpdate, render, update, complete, pause];

        _animation = ClickButton.Animate(new()
        {
            ["X"] = 320,
            ["Rotate"] = 180,
            Duration = 900,
            Delay = 300,
            Loop = 2,
            Alternate = true,
            Autoplay = false,
            Ease = Eases.InOutQuad,
            OnBegin = _ => begin.Fire(),
            OnLoop = _ => loop.Fire(),
            OnBeforeUpdate = _ => beforeUpdate.Fire(),
            OnRender = _ => render.Fire(),
            OnUpdate = _ => update.Fire(),
            OnComplete = _ => complete.Fire(),
            OnPause = _ => pause.Fire(),
        });

        _decay = Presto.CreateTimer(new()
        {
            FrameRate = 30,
            OnUpdate = _ =>
            {
                foreach (var indicator in _indicators) indicator.Decay();
            },
        });

        ResetIndicators();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animation?.Revert();
        _animation = null;
        _decay?.Cancel();
        _decay = null;
    }

    private void Play(object? sender, RoutedEventArgs e) => _animation?.Play();

    private void Pause(object? sender, RoutedEventArgs e) => _animation?.Pause();

    private void Restart(object? sender, RoutedEventArgs e)
    {
        ResetIndicators();
        _animation?.Restart();
    }

    private async void AwaitCompletion(object? sender, RoutedEventArgs e)
    {
        if (_animation is not { } animation) return;
        ResetIndicators();
        AwaitText.Text = "awaiting…";
        await animation.Restart().Then(_ => AwaitText.Text = "Then(callback) fired");
        await animation;
        AwaitText.Text += " · await animation returned";
    }

    private void Clicked(object? sender, RoutedEventArgs e) =>
        ClickText.Text = string.Create(CultureInfo.InvariantCulture, $"Click me · {++_clicks}");

    private void ResetIndicators()
    {
        AwaitText.Text = "not awaited yet";
        foreach (var indicator in _indicators) indicator.Reset();
    }

    private sealed class Indicator(Border dot, TextBlock count)
    {
        private int _fired;

        public void Fire()
        {
            dot.Opacity = 1;
            count.Text = (++_fired).ToString(CultureInfo.InvariantCulture);
        }

        public void Decay() => dot.Opacity = Math.Max(Dim, dot.Opacity - 0.08);

        public void Reset()
        {
            _fired = 0;
            count.Text = "0";
            dot.Opacity = Dim;
        }
    }
}
