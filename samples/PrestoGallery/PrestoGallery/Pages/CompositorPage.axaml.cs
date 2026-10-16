using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class CompositorPage : ShowcasePage
{
    #region snippet ThreadBuilders
    private static readonly AnimationBuilder Fits = Presto.Build().X(150).Duration(1000).Ease(Eases.InOutSine).Loop().Alternate();
    private static readonly AnimationBuilder Unfit = Presto.Build().X(150).Blur(3).Duration(1000).Ease(Eases.InOutSine).Loop().Alternate();
    #endregion

    private readonly (Border Dot, TextBlock Readout, AnimationBuilder Builder, AnimationThread Thread)[] _lanes;
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

        _lanes = [.. new[] { Fits, Unfit }.SelectMany((builder, row) => Enum.GetValues<AnimationThread>().Select((thread, column) =>
        {
            var dot = new Border { Classes = { "dot" } };
            if (builder == Unfit) dot.Classes.Add("unfit");
            var readout = new TextBlock { Classes = { "caption" } };
            var lane = new StackPanel { Classes = { "lane" }, Children = { new Border { Classes = { "orbit", "lane" }, Child = dot }, readout } };
            Grid.SetRow(lane, row + 1);
            Grid.SetColumn(lane, column + 1);
            ThreadGrid.Children.Add(lane);
            return (dot, readout, builder, thread);
        }))];
        #region snippet ThreadQuery
        PrestoAvalonia.CanAnimateOnCompositor(Unfit, out var reason);
        UnfitLabel.Text = $"X + Blur · {reason} doesn't fit";
        #endregion
        ThreadCode.Code = $"{Snippets.ThreadBuilders}\n\n{Snippets.ThreadQuery}\n\n{Snippets.ThreadModes}";
    }

    /// <summary>The looping compositor animations on the real controls, which the transport drives.</summary>
    public IReadOnlyList<CompositorAnimation> Motions => _motions;

    /// <summary>What each thread-mode lane reports: the fitting builder first, each in UIThread, Compositor, PreferCompositor order.</summary>
    public IReadOnlyList<string?> ThreadReadouts => [.. _lanes.Select(lane => lane.Readout.Text)];

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

        foreach (var (dot, readout, builder, thread) in _lanes)
        {
            #region snippet ThreadModes
            try
            {
                var run = dot.Animate(builder, thread);
                readout.Text = run.OnCompositor ? "render thread" : "UI thread";
            }
            catch (NotSupportedException e)
            {
                readout.Text = e.Message;
            }
            #endregion
        }
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
