namespace Monaco.Presto;

/// <summary>Supplies the engine with its clock and frame pump, standing in for <c>Date.now()</c> and <c>requestAnimationFrame</c> upstream.</summary>
public interface IFrameSource
{
    /// <summary>Current time in milliseconds, monotonic within a source.</summary>
    double Now { get; }

    /// <summary>Invokes <paramref name="onFrame"/> on every frame until <see cref="Stop"/>. A no-op while already started.</summary>
    void Start(Action onFrame);

    /// <summary>Stops frame callbacks. A no-op when not started; may be called from inside the callback.</summary>
    void Stop();
}
