using System.Runtime.CompilerServices;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>An animation started for an <see cref="AnimationThread"/>: either a UI-thread <see cref="Animation"/> or a <see cref="CompositorAnimation"/>, with the playback they share.</summary>
public sealed class ThreadedAnimation
{
    internal ThreadedAnimation(Animation? uiThread, CompositorAnimation? compositor)
    {
        UIThread = uiThread;
        Compositor = compositor;
    }

    /// <summary>Whether the animation runs on the render thread, so <see cref="Compositor"/> is set rather than <see cref="UIThread"/>.</summary>
    public bool OnCompositor => Compositor != null;

    /// <summary>The animation when it runs on the UI thread, otherwise null.</summary>
    public Animation? UIThread { get; }

    /// <summary>The animation when it runs on the render thread, otherwise null.</summary>
    public CompositorAnimation? Compositor { get; }

    /// <inheritdoc cref="Timer.Play"/>
    public ThreadedAnimation Play()
    {
        if (Compositor != null) Compositor.Play();
        else UIThread!.Play();
        return this;
    }

    /// <inheritdoc cref="Timer.Pause"/>
    public ThreadedAnimation Pause()
    {
        if (Compositor != null) Compositor.Pause();
        else UIThread!.Pause();
        return this;
    }

    /// <inheritdoc cref="Timer.Resume"/>
    public ThreadedAnimation Resume()
    {
        if (Compositor != null) Compositor.Resume();
        else UIThread!.Resume();
        return this;
    }

    /// <inheritdoc cref="Timer.Cancel"/>
    public ThreadedAnimation Cancel()
    {
        if (Compositor != null) Compositor.Cancel();
        else UIThread!.Cancel();
        return this;
    }

    /// <inheritdoc cref="Animation.Revert"/>
    public ThreadedAnimation Revert()
    {
        if (Compositor != null) Compositor.Revert();
        else UIThread!.Revert();
        return this;
    }

    /// <summary>Completes when the live animation does.</summary>
    public async Task<ThreadedAnimation> Then()
    {
        if (Compositor != null) await Compositor.Then();
        else await UIThread!.Then();
        return this;
    }

    /// <inheritdoc cref="Timer.GetAwaiter"/>
    public TaskAwaiter<ThreadedAnimation> GetAwaiter() => Then().GetAwaiter();
}
