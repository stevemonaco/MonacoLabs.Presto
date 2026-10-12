using Avalonia.Threading;

namespace Tempo.ViewModels;

/// <summary>Ticks four times a second on the UI thread.</summary>
public sealed class DispatcherTickSource : ITickSource
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public DispatcherTickSource() => _timer.Start();

    public event EventHandler? Tick
    {
        add => _timer.Tick += value;
        remove => _timer.Tick -= value;
    }
}
