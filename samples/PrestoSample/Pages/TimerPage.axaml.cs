using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Pages;

public partial class TimerPage : UserControl
{
    private Timer? _timer;
    private int _loops;

    public TimerPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = Presto.CreateTimer(new()
        {
            Duration = 1000,
            Loop = true,
            FrameRate = 30,
            OnUpdate = timer => CurrentTimeText.Text = timer.CurrentTime.ToString("0", CultureInfo.InvariantCulture),
            OnLoop = _ => LoopsText.Text = (++_loops).ToString(CultureInfo.InvariantCulture),
        });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Cancel();
        _timer = null;
    }

    private void Play(object? sender, RoutedEventArgs e) => _timer?.Play();

    private void Pause(object? sender, RoutedEventArgs e) => _timer?.Pause();

    private void Restart(object? sender, RoutedEventArgs e)
    {
        _loops = 0;
        LoopsText.Text = "0";
        _timer?.Restart();
    }
}
