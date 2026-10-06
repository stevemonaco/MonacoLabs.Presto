using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

public partial class CompositorPage : ShowcasePage
{
    private readonly Ellipse[] _dots;
    private readonly Canvas[] _tail;
    private CompositorAnimation[] _motions = [];

    public CompositorPage()
    {
        InitializeComponent();

        _dots = [.. OrbitScene.Dots.Select(dot =>
        {
            var ellipse = new Ellipse
            {
                Width = OrbitScene.DotRadius * 2,
                Height = OrbitScene.DotRadius * 2,
                Fill = OrbitScene.RingBrushes[dot.Ring],
            };
            Canvas.SetLeft(ellipse, dot.Center.X - OrbitScene.DotRadius);
            Canvas.SetTop(ellipse, dot.Center.Y - OrbitScene.DotRadius);
            return ellipse;
        })];

        // Each segment is a full-size layer, so its Rotate turns the ellipse on it around the middle of the panel.
        _tail = [.. Enumerable.Range(0, OrbitScene.TailLength).Select(i =>
        {
            var radius = OrbitScene.TailRadius(i);
            var head = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = Palette.Aqua, Opacity = OrbitScene.TailOpacity(i) };
            Canvas.SetLeft(head, (OrbitScene.Extent / 2) - radius);
            Canvas.SetTop(head, (OrbitScene.Extent / 2) - OrbitScene.CometOrbit - radius);
            return new Canvas { Width = OrbitScene.Extent, Height = OrbitScene.Extent, Children = { head } };
        })];

        UiOrbit.Children.AddRange(_dots);
        UiOrbit.Children.AddRange(Enumerable.Reverse(_tail));
    }

    /// <summary>The looping compositor animations on the real controls, which the transport drives.</summary>
    public IReadOnlyList<CompositorAnimation> Motions => _motions;

    protected override void OnAttached()
    {
        Orbit.Scene = new OrbitScene();
        Presto.Animate(_dots, OrbitScene.RingAnimation);
        Presto.Animate(_tail, OrbitScene.CometAnimation);
        Presto.CreateTimer(new() { OnUpdate = _ => Orbit.Post(new OrbitScene.Heartbeat(Stopwatch.GetTimestamp())) });

        _motions =
        [
            PulseButton.AnimateOnCompositor(new() { ["Scale"] = 1.12, Duration = 700, Ease = Eases.InOutSine, Loop = true, Alternate = true }),
            BobCard.AnimateOnCompositor(new() { ["Y"] = -10, Duration = 900, Ease = Eases.InOutSine, Loop = true, Alternate = true }),
            Gear.AnimateOnCompositor(new() { ["Rotate"] = 360, Duration = 2400, Ease = Eases.None, Loop = true }),
            BreatheToggle.AnimateOnCompositor(new() { ["Opacity"] = .3, Duration = 1100, Ease = Eases.InOutSine, Loop = true, Alternate = true }),
        ];
    }

    private void Freeze(object? sender, RoutedEventArgs e) => UiThread.Freeze(TimeSpan.FromSeconds(2));

    private void PauseMotions(object? sender, RoutedEventArgs e)
    {
        foreach (var motion in _motions) motion.Pause();
    }

    private void ResumeMotions(object? sender, RoutedEventArgs e)
    {
        foreach (var motion in _motions) motion.Resume();
    }

    private void ReverseMotions(object? sender, RoutedEventArgs e)
    {
        foreach (var motion in _motions) motion.Reverse();
    }

    private void SpeedChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        foreach (var motion in _motions) motion.Speed = e.NewValue;
    }
}
