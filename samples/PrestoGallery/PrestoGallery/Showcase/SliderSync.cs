using Avalonia.Controls;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoGallery.Showcase;

/// <summary>
/// Binds a scrub <see cref="Slider"/> to a timer's Progress: moving the slider seeks the timer, and
/// <see cref="Show"/> moves the slider without that echo seeking it again.
/// </summary>
public sealed class SliderSync
{
    private readonly Slider _slider;
    private bool _showing;

    /// <param name="timer">The timer to seek, or null while the page has none.</param>
    public SliderSync(Slider slider, Func<Timer?> timer, Action seeked)
    {
        _slider = slider;
        slider.ValueChanged += (_, e) =>
        {
            if (_showing || timer() is not { } target) return;
            target.Progress = e.NewValue;
            seeked();
        };
    }

    public void Show(Timer timer)
    {
        _showing = true;
        _slider.Value = timer.Progress;
        _showing = false;
    }
}
