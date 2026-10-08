using System.Runtime.CompilerServices;
using Avalonia;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// The particles one <see cref="Recipes.Confetti"/>, <see cref="Recipes.Sparkles"/> or <see cref="Recipes.Burst"/> call
/// emitted into its window's overlay, resolved on the UI thread once the last of them dies.
/// </summary>
/// <remarks>Emitted particles cannot be sought or reversed; pausing freezes only this effect's emitters.</remarks>
public sealed class ParticleEffect : IRevertible
{
    private readonly TaskCompletionSource _done = new();
    private readonly CompositorCanvas? _canvas;
    private int _pending;

    private ParticleEffect() => _done.SetResult();

    internal ParticleEffect(CompositorCanvas canvas, IReadOnlyList<(EmitterParams Params, Point Origin)> emitters, Action systemFinished)
    {
        _canvas = canvas;
        _pending = emitters.Count;
        foreach (var (parameters, origin) in emitters)
        {
            canvas.Invoke<ParticleScene>(scene => Systems.Add(scene.Add(parameters, origin, () =>
            {
                systemFinished();
                if (--_pending == 0) _done.TrySetResult();
            })));
        }
    }

    /// <summary>An effect that emitted nothing and is already complete.</summary>
    internal static ParticleEffect None { get; } = new();

    /// <summary>This effect's systems, touched only on the render thread.</summary>
    internal List<ParticleSystem> Systems { get; } = [];

    /// <summary>Stops emitting and lets the live particles finish.</summary>
    public void Stop() => Invoke(static system => system.Stop());

    /// <summary>Removes every particle now and completes <see cref="Then"/>.</summary>
    public void Cancel()
    {
        Invoke(static system =>
        {
            system.Paused = false;
            system.Stop();
            system.Clear();
        });
        _done.TrySetResult();
    }

    /// <summary>Freezes this effect's particles and emission where they are.</summary>
    public void Pause() => Invoke(static system => system.Paused = true);

    /// <summary>Continues after <see cref="Pause"/>.</summary>
    public void Resume() => Invoke(static system => system.Paused = false);

    /// <summary>Same as <see cref="Cancel"/>.</summary>
    public void Revert() => Cancel();

    /// <summary>Completes on the UI thread once the last particle dies, or at once after <see cref="Cancel"/> or when nothing was emitted.</summary>
    public Task Then() => _done.Task;

    /// <summary>Lets the effect be awaited, as <see cref="Then"/>.</summary>
    public TaskAwaiter GetAwaiter() => _done.Task.GetAwaiter();

    private void Invoke(Action<ParticleSystem> action)
    {
        if (_done.Task.IsCompleted) return;
        _canvas!.Invoke<ParticleScene>(_ =>
        {
            foreach (var system in Systems) action(system);
        });
    }
}
