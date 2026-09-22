namespace Monaco.Presto;

/// <summary>Frame source driven explicitly through <see cref="Advance(double)"/>, for deterministic tests.</summary>
public sealed class ManualFrameSource : IFrameSource
{
    private Action? _onFrame;

    public double Now { get; private set; }

    public bool IsRunning => _onFrame is not null;

    public void Start(Action onFrame) => _onFrame ??= onFrame;

    public void Stop() => _onFrame = null;

    /// <summary>Adds <paramref name="ms"/> to <see cref="Now"/> and invokes the frame callback once if running.</summary>
    public void Advance(double ms)
    {
        Now += ms;
        _onFrame?.Invoke();
    }

    /// <summary>Advances by <paramref name="ms"/> in <paramref name="step"/>-sized frames (the last one partial), invoking the frame callback per frame.</summary>
    public void Advance(double ms, double step)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        var target = Now + ms;
        while (Now + step < target) Advance(step);
        Advance(target - Now);
    }
}
