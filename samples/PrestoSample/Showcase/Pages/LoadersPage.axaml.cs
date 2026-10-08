using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

/// <summary>Nine loading indicators, each one builder printed beneath it and played on the UI thread or, where it can be, the compositor.</summary>
public partial class LoadersPage : ShowcasePage
{
    private static readonly Geometry Splat = Geometry.Parse("M30,10 C48,2 60,22 50,34 C42,46 40,58 26,54 C10,50 0,40 8,26 C14,14 18,14 30,10 Z");

    #region snippet LoaderEqualizer
    private static readonly AnimationBuilder Equalizer = Presto.Build()
        .ScaleY(from: .2, to: 1)
        .Delay(Presto.Stagger(90)).Duration(480)
        .Ease(Eases.InOutSine).Loop().Alternate();
    #endregion

    #region snippet LoaderOrbit
    private static readonly AnimationBuilder Orbiting = Presto.Build()
        .Rotate(360)
        .Delay(Presto.Stagger(70)).Duration(1400)
        .Ease(Eases.InOutCubic).Loop().LoopDelay(280);
    #endregion

    #region snippet LoaderGrid
    private static readonly AnimationBuilder GridWave = Presto.Build()
        .Scale(.2).Opacity(.3)
        .Delay(Presto.Stagger(80, new() { Grid = [5, 5], From = StaggerFrom.Center }))
        .Duration(560).Ease(Eases.InOutQuad).Loop().Alternate();
    #endregion

    #region snippet LoaderBall
    private static readonly AnimationBuilder Bounce = Presto.Build()
        .Y(k => k.To(-56, ease: Eases.OutQuad).To(0, ease: Eases.InQuad).To(0, 90).To(0, 160))
        .ScaleX(k => k.To(.9, ease: Eases.OutQuad).To(.9).To(1.4, 90).To(1, 160))
        .ScaleY(k => k.To(1.1, ease: Eases.OutQuad).To(1.1).To(.6, 90).To(1, 160))
        .Duration(1000).Loop();
    #endregion

    #region snippet LoaderTyping
    private static readonly AnimationBuilder TypingDots = Presto.Build()
        .Y(TweenValue.Keyframes(0, -7, 0))
        .Opacity(TweenValue.Keyframes(.4, 1, .4))
        .Delay(Presto.Stagger(150)).Duration(600)
        .Ease(Eases.InOutSine).Loop().LoopDelay(450);
    #endregion

    #region snippet LoaderRipples
    private static readonly AnimationBuilder Ripple = Presto.Build()
        .Scale(from: .15, to: 1).Opacity(from: 1, to: 0)
        .Delay(Presto.Stagger(600)).Duration(1800)
        .Ease(Eases.OutQuad).Loop();
    #endregion

    #region snippet LoaderBlob
    private static readonly AnimationBuilder Morph = Presto.Build()
        .Data(PrestoAvalonia.MorphTo(Splat))
        .Rotate(180)
        .Duration(1300).Ease(Eases.InOutSine).Loop().Alternate();
    #endregion

    #region snippet LoaderRing
    private static readonly AnimationBuilder DrawRing = Presto.Build()
        .Draw(TweenValue.Keyframes("0 0", "0 1", "1 1"))
        .Duration(1800).Ease(Eases.InOutQuad).Loop();
    #endregion

    #region snippet LoaderCounter
    private static readonly AnimationBuilder CountUp = Presto.Build()
        .Text(from: 0, to: 100, textModifier: v => $"{v:0}%")
        .Duration(2400).Ease(Eases.InOutQuad);

    private static readonly AnimationBuilder Fill = Presto.Build()
        .Property("Value", from: 0, to: 100)
        .Duration(2400).Ease(Eases.InOutQuad);

    private Timeline Counter() => Presto.CreateTimeline(new() { Loop = true, LoopDelay = 500 })
        .Add(Percent, CountUp)
        .Add(Meter, Fill, "<<");
    #endregion

    private readonly List<Timer> _loaders = [];
    private readonly List<CompositorAnimation> _onCompositor = [];

    public LoadersPage()
    {
        InitializeComponent();
        for (var i = 0; i < 25; i++) Squares.Children.Add(new Border());
    }

    /// <summary>The loaders running on the UI thread: all nine, or the three with no composition property while <c>RenderThread</c> is on.</summary>
    public IReadOnlyList<Timer> Loaders => _loaders;

    /// <summary>The six loaders <c>RenderThread</c> moves onto the compositor; empty while it is off.</summary>
    public IReadOnlyList<CompositorAnimation> CompositorLoaders => _onCompositor;

    protected override void OnAttached() => Scope.Add(_ => Build());

    private void Build()
    {
        _loaders.Clear();
        _onCompositor.Clear();
        Play(Bars.Children, Equalizer);
        Play(Orbit.Children, Orbiting);
        Play(Squares.Children, GridWave);
        Play(Ball, Bounce);
        Play(Typing.Children, TypingDots);
        Play(Ripples.Children, Ripple);
        _loaders.Add(Morph.Animate(Blob));
        _loaders.Add(DrawRing.Animate(PrestoAvalonia.CreateDrawable(Ring)));
        _loaders.Add(Counter());
        Retune();
    }

    private void Play(object targets, AnimationBuilder loader)
    {
        if (RenderThread.IsChecked == true) _onCompositor.Add(PrestoAvalonia.AnimateOnCompositor(targets, loader));
        else _loaders.Add(loader.Animate(targets));
    }

    private void SpeedChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        SpeedLabel.Text = $"{e.NewValue:0.00}×";
        Retune();
    }

    private void PauseAllChanged(object? sender, RoutedEventArgs e) => Retune();

    private void Retune()
    {
        var paused = PauseAll.IsChecked == true;
        foreach (var loader in _loaders)
        {
            loader.Speed = Speed.Value;
            if (paused) loader.Pause();
            else loader.Resume();
        }
        foreach (var loader in _onCompositor)
        {
            // Setting the speed recompiles every composition animation, even to the same value.
            if (loader.Speed != Speed.Value) loader.Speed = Speed.Value;
            if (paused) loader.Pause();
            else loader.Resume();
        }
    }

    private void RenderThreadChanged(object? sender, RoutedEventArgs e) => Scope.Refresh();

    private void Freeze(object? sender, RoutedEventArgs e) => UiThread.Freeze(TimeSpan.FromSeconds(2));
}
