using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using MonacoLabs.Presto.Platform;

namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// A control whose content is a <see cref="CompositorScene"/>, drawn into a composition visual of its own and animated
/// by the render loop rather than by the UI thread.
/// </summary>
public sealed class CompositorCanvas : Control
{
    /// <summary>Defines the <see cref="Scene"/> property.</summary>
    public static readonly StyledProperty<CompositorScene?> SceneProperty =
        AvaloniaProperty.Register<CompositorCanvas, CompositorScene?>(nameof(Scene));

    private CompositionCustomVisual? _visual;
    private List<object>? _queued;

    /// <summary>Creates an empty canvas; a <see cref="Scene"/> is handed to the render thread once the canvas enters the visual tree.</summary>
    public CompositorCanvas() => PrestoAvalonia.EnsureInitialized();

    /// <summary>The scene this canvas draws; replacing it while attached reverts the old scene and attaches the new one.</summary>
    public CompositorScene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Hands <paramref name="message"/> to <see cref="CompositorScene.OnMessage"/> on the render thread; call it from the UI thread only.</summary>
    /// <remarks>
    /// A canvas is usually configured on the line after it is constructed, before anything has shown it, so messages
    /// posted while the canvas is outside the visual tree are queued here and sent, in order, behind the attach message.
    /// </remarks>
    public void Post(object message)
    {
        Debug.Assert(Dispatcher.UIThread.CheckAccess(), "A compositor scene may only be posted to from the UI thread.");
        if (_visual is { } visual) visual.SendHandlerMessage(message);
        else (_queued ??= []).Add(message);
    }

    /// <summary>Runs <paramref name="action"/> against the live scene on the render thread, or does nothing when the attached scene is not a <typeparamref name="TScene"/>.</summary>
    /// <inheritdoc cref="Post" path="/remarks"/>
    public void Invoke<TScene>(Action<TScene> action) where TScene : CompositorScene =>
        Post(new Action<CompositorScene>(scene =>
        {
            if (scene is TScene typed) action(typed);
        }));

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ElementComposition.GetElementVisual(this) is not { } element) return;
        _visual = element.Compositor.CreateCustomVisual(new CompositorSceneHandler());
        ElementComposition.SetElementChildVisual(this, _visual);
        _visual.Size = new Vector(Bounds.Width, Bounds.Height);
        HiddenSignal.Changed += SendHidden;
        if (HiddenSignal.Hidden) SendHidden();
        if (Scene is { } scene) _visual.SendHandlerMessage(new CompositorSceneHandler.AttachMessage(scene));
        if (_queued is not { } queued) return;
        foreach (var message in queued) _visual.SendHandlerMessage(message);
        _queued = null;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        HiddenSignal.Changed -= SendHidden;
        _visual?.SendHandlerMessage(CompositorSceneHandler.DetachMessage);
        ElementComposition.SetElementChildVisual(this, null);
        _visual = null;
    }

    private void SendHidden() => _visual?.SendHandlerMessage(new CompositorSceneHandler.HiddenMessage(HiddenSignal.Hidden));

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_visual is not { } visual) return;
        if (change.Property == BoundsProperty)
        {
            visual.Size = new Vector(Bounds.Width, Bounds.Height);
        }
        else if (change.Property == SceneProperty)
        {
            visual.SendHandlerMessage(CompositorSceneHandler.DetachMessage);
            if (change.GetNewValue<CompositorScene?>() is { } scene)
            {
                visual.SendHandlerMessage(new CompositorSceneHandler.AttachMessage(scene));
            }
        }
    }
}
