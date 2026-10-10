namespace MonacoLabs.Presto;

/// <summary>Which thread an animation runs on: the UI thread's <see cref="Presto.Engine"/> or the compositor's render thread.</summary>
public enum AnimationThread
{
    /// <summary>The UI thread, even when everything the animation sets has a counterpart on the compositor.</summary>
    UIThread,

    /// <summary>The render thread, throwing <see cref="NotSupportedException"/> for a property or setting the compositor lacks.</summary>
    Compositor,

    /// <summary>The render thread when <see cref="PrestoAvalonia.CanAnimateOnCompositor(AnimationParams, out string?)"/> holds, otherwise the UI thread.</summary>
    PreferCompositor,
}
