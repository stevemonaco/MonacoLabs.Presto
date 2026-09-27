using Avalonia;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Rendering.Composition;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>Owns a <see cref="CompositorScene"/> on the render thread: its engine's frame source, its message sink and its renderer.</summary>
internal sealed class CompositorSceneHandler : CompositionCustomVisualHandler, IFrameSource
{
    internal static readonly object DetachMessage = new();

    private CompositorScene? _scene;
    private Engine? _engine;
    private Scope? _scope;
    private Action? _onFrame;

    /// <inheritdoc/>
    public double Now => CompositionNow.TotalMilliseconds;

    internal Size Size => new(EffectiveSize.X, EffectiveSize.Y);

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
        if (message is AttachMessage attach) Attach(attach.Scene);
        else if (ReferenceEquals(message, DetachMessage)) Detach();
        else Dispatch(message);
    }

    /// <inheritdoc/>
    /// <remarks>Avalonia does not catch what this throws, so a faulted tick would take the render thread down with it.</remarks>
    public override void OnAnimationFrameUpdate()
    {
        if (_engine is not { } engine || _onFrame is not { } onFrame) return;
        try
        {
            using (engine.MakeCurrent()) onFrame();
        }
        catch (Exception e)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Animations)
                ?.Log(this, $"Exception in {nameof(CompositorScene)} tick {{0}}", e);
        }
        // The engine stops its own frame source once it runs out of children, which is the signal to let the render loop sleep.
        if (_onFrame is null) return;
        Invalidate();
        RegisterForNextAnimationFrameUpdate();
    }

    /// <inheritdoc/>
    public override void OnRender(ImmediateDrawingContext drawingContext)
    {
        if (_scene is { } scene) scene.Render(drawingContext);
    }

    internal void InvalidateScene() => Invalidate();

    private void Attach(CompositorScene scene)
    {
        _scene = scene;
        var engine = _engine = new Engine(this);
        using (engine.MakeCurrent())
        {
            var scope = _scope = new Scope();
            scene.Attach(this, engine, scope);
            scope.Execute(_ => scene.RaiseAttached());
        }
    }

    // Scope.Execute installs the scene's engine, defaults and scope, and restores all three in a finally, so a throwing
    // scene leaves nothing behind; Avalonia's DispatchMessages logs it and carries on with the rest of the batch.
    private void Dispatch(object message)
    {
        if (_scene is not { } scene || _scope is not { } scope) return;
        if (message is Action<CompositorScene> invoke) scope.Execute(_ => invoke(scene));
        else scope.Execute(_ => scene.RaiseMessage(message));
    }

    private void Detach()
    {
        if (_engine is not { } engine || _scope is not { } scope) return;
        using (engine.MakeCurrent())
        {
            scope.Revert();
            engine.Pause();
            // Nothing will tick this engine again, so the timers the revert cancelled have to be pruned here.
            engine.DropChildren();
        }
        _scene = null;
        _scope = null;
        _engine = null;
    }

    internal sealed record AttachMessage(CompositorScene Scene);
}
