using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class ControlsPage : ShowcasePage
{
    private readonly SliderSync _scrub;
    private Animation? _animation;
    private int _clicks;

    public ControlsPage()
    {
        InitializeComponent();
        _scrub = new SliderSync(ScrubSlider, () => _animation, UpdateReadouts);
    }

    protected override void OnAttached()
    {
        _animation = ClickButton.Animate(new()
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

    private void StretchChanged(object? sender, RangeBaseValueChangedEventArgs e) => Run(a => a.Stretch(e.NewValue));

    private void Clicked(object? sender, RoutedEventArgs e) =>
        ClickText.Text = string.Create(CultureInfo.InvariantCulture, $"Click me · {++_clicks}");

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
        _scrub.Show(animation);
    }
}
