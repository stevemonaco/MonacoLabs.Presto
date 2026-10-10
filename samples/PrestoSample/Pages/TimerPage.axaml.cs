using System.Globalization;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoSample.Showcase;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoSample.Pages;

public partial class TimerPage : ShowcasePage
{
    private int _loops;

    public TimerPage() => InitializeComponent();

    private Timer? _timer;

    protected override void OnAttached()
    {
        _timer = Presto.CreateTimer(new()
        {
            Duration = 1000,
            Loop = true,
            FrameRate = 30,
            OnUpdate = timer => CurrentTimeText.Text = timer.CurrentTime.ToString("0", CultureInfo.InvariantCulture),
            OnLoop = _ => LoopsText.Text = (++_loops).ToString(CultureInfo.InvariantCulture),
        });
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