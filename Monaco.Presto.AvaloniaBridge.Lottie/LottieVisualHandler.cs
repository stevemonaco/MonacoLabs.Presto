using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Monaco.Presto.AvaloniaBridge.Lottie.Prebuilt;
using SkiaSharp;

namespace Monaco.Presto.AvaloniaBridge.Lottie;

/// <summary>
/// Owns a <see cref="LottieView"/>'s Skottie animation on the render thread while <see cref="LottieView.RenderThread"/> is set,
/// and plays it there on an engine of its own, so it keeps moving while the UI thread is blocked.
/// </summary>
internal sealed class LottieVisualHandler : CompositionCustomVisualHandler, IFrameSource
{
    internal static readonly object StopMessage = new(), DetachMessage = new();

    private Engine? _engine;
    private Animation? _playback;
    private Action? _onFrame;
    private SceneMessage? _scene;
    private LottiePlayerHandle? _player;
    private SKPaint? _layerPaint;

    /// <summary>The composition frame drawn, which render-thread playback tweens.</summary>
    public double Frame { get; set; }

    /// <inheritdoc/>
    public double Now => CompositionNow.TotalMilliseconds;

    /// <inheritdoc/>
    public void Start(Action onFrame)
    {
        if (_onFrame is not null) return;
        _onFrame = onFrame;
        RegisterForNextAnimationFrameUpdate();
    }

    /// <inheritdoc/>
    public void Stop() => _onFrame = null;

    /// <inheritdoc/>
    public override void OnMessage(object message)
    {
        if (message is SceneMessage scene) Show(scene);
        else if (message is PlayMessage play) Play(play);
        else if (ReferenceEquals(message, DetachMessage)) Detach();
        else _playback?.Pause();
    }

    /// <inheritdoc/>
    public override void OnAnimationFrameUpdate()
    {
        if (_engine is not { } engine || _onFrame is not { } onFrame) return;
        using (engine.MakeCurrent()) onFrame();
        // The engine stops its own frame source once it runs out of children, which is the signal to let the render loop sleep.
        if (_onFrame is null) return;
        Invalidate();
        RegisterForNextAnimationFrameUpdate();
    }

    /// <inheritdoc/>
    public override void OnRender(ImmediateDrawingContext drawingContext)
    {
        if (_player is { } player && _scene is { } scene)
            LottieDrawOperation.Draw(drawingContext, player, Frame, scene.Placed, scene.Bounds, _layerPaint);
    }

    // A changed frame or composition is a UI-thread seek, which stops playback; the first scene only sets where a queued play starts.
    private void Show(SceneMessage scene)
    {
        if (scene.Composition != _scene?.Composition)
        {
            _player?.Dispose();
            _player = scene.Composition is { } composition ? new LottiePlayerHandle(composition) : null;
        }
        if (_scene is null || scene.Composition != _scene.Composition || scene.Frame != _scene.Frame)
        {
            if (_scene is not null) _playback?.Pause();
            Frame = scene.Frame;
        }
        LottieDrawOperation.DisposeLayerPaint(_layerPaint);
        _layerPaint = LottieDrawOperation.LayerPaint(scene.Effect, scene.Bounds);
        _scene = scene;
        Invalidate();
    }

    private void Play(PlayMessage play)
    {
        _playback?.Pause();
        var engine = _engine ??= new Engine(this);
        using (engine.MakeCurrent()) _playback = LottieRecipes.Frames(play.From, play.To, play.Duration, play.Loop).Animate(this);
    }

    private void Detach()
    {
        _engine?.Pause();
        _player?.Dispose();
        _player = null;
        LottieDrawOperation.DisposeLayerPaint(_layerPaint);
    }

    internal sealed record SceneMessage(LottieComposition? Composition, double Frame, Rect Placed, Rect Bounds, ImageEffect Effect);

    internal sealed record PlayMessage(double From, double To, double Duration, bool Loop);
}
