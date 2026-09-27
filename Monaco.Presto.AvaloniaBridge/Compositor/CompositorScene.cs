using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>
/// A scene animated and drawn on Avalonia's render thread by a <see cref="CompositorCanvas"/>, so it keeps running
/// while the UI thread is blocked.
/// </summary>
/// <remarks>
/// The scene is constructed on the UI thread, but from the moment its canvas attaches it belongs to the render thread:
/// <see cref="OnAttached"/>, <see cref="OnMessage"/>, <see cref="OnRender"/> and the callbacks of everything they create
/// run there, and none of them may touch an <see cref="AvaloniaObject"/> except through <see cref="PostToUI"/>. The UI
/// thread reaches an attached scene only through <see cref="CompositorCanvas.Post"/> and
/// <see cref="CompositorCanvas.Invoke{TScene}"/>. <see cref="Now"/>, <see cref="Size"/> and <see cref="Invalidate()"/>
/// hold the compositor lock and are only valid from those callbacks.
/// </remarks>
public abstract class CompositorScene
{
    internal const int BrushCacheLimit = 1024;

    private readonly Dictionary<(uint Color, byte Alpha), ImmutableSolidColorBrush> _brushes = [];

    private CompositorSceneHandler? _handler;

    /// <summary>The engine the render loop ticks this scene with, started from a copy of <see cref="Presto.Defaults"/>.</summary>
    public Engine Engine { get; private set; } = null!;

    /// <summary>The scope everything created in <see cref="OnAttached"/> registers with; the canvas reverts it on detach.</summary>
    public Scope Scope { get; private set; } = null!;

    /// <summary>The compositor's clock in milliseconds, which is also the scene engine's time.</summary>
    protected double Now => Handler.Now;

    /// <summary>The size the canvas gave the scene, which <see cref="OnRender"/> is also clipped to.</summary>
    protected Size Size => Handler.Size;

    private CompositorSceneHandler Handler
    {
        get
        {
            var handler = _handler ?? throw new InvalidOperationException("Object is not yet attached to the compositor");
            Engine.AssertThreadAffinity();
            return handler;
        }
    }

    /// <summary>Builds the scene's animations; runs with <see cref="Engine"/> ambient and <see cref="Scope"/> current, so <see cref="Presto.Animate"/> and friends need no arguments of their own.</summary>
    protected virtual void OnAttached()
    {
    }

    /// <summary>Reacts to a message from <see cref="CompositorCanvas.Post"/>; runs exactly as <see cref="OnAttached"/> does.</summary>
    protected virtual void OnMessage(object message)
    {
    }

    /// <summary>Draws the scene, after every frame its engine ticked.</summary>
    protected abstract void OnRender(ImmediateDrawingContext context, Size size);

    /// <summary>Runs <paramref name="action"/> on the UI thread, which is the only way a scene may reach a control.</summary>
    /// <remarks>
    /// Everything else a scene starts stays on the render thread: <c>OnUpdate</c>, <c>OnComplete</c> and the rest of the
    /// callbacks run there and must not touch an <see cref="AvaloniaObject"/>, and <c>Then()</c> on a scene timer
    /// completes on the thread pool, since the render thread has no synchronization context and the task is created
    /// with <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>.
    /// </remarks>
    protected void PostToUI(Action action) => Dispatcher.UIThread.Post(action);

    /// <summary>Marks the whole scene for redrawing.</summary>
    protected void Invalidate() => Handler.InvalidateScene();

    /// <summary>A brush this scene caches: the same colour and opacity hand back the same instance, opacity rounded to 256 steps.</summary>
    /// <remarks>
    /// Meant for a small palette. The cache is cleared once it holds 1 024 brushes, so a scene
    /// that tweens colours through more distinct values than that allocates again instead of growing without bound.
    /// </remarks>
    protected IImmutableBrush Brush(Color color, double opacity = 1)
    {
        var key = (color.ToUInt32(), Alpha: Quantize(opacity));
        if (_brushes.TryGetValue(key, out var brush)) return brush;
        if (_brushes.Count >= BrushCacheLimit) _brushes.Clear();
        return _brushes[key] = new ImmutableSolidColorBrush(color, key.Alpha / 255d);
    }

    internal int CachedBrushCount => _brushes.Count;

    // Scenes tween opacity every frame, so it is rounded to the 256 steps the framebuffer has anyway; unrounded, the cache would grow without bound.
    private static byte Quantize(double opacity) => (byte)Math.Clamp(Math.Round(opacity * 255), 0, 255);

    internal void Attach(CompositorSceneHandler handler, Engine engine, Scope scope)
    {
        _handler = handler;
        Engine = engine;
        Scope = scope;
    }

    internal void RaiseAttached() => OnAttached();

    internal void RaiseMessage(object message) => OnMessage(message);

    internal void Render(ImmediateDrawingContext context) => OnRender(context, Size);
}
