namespace MonacoLabs.Presto.Events;

/// <summary>
/// How a <see cref="ScrollObserver"/> drives what it is linked to: playback methods called as the target enters and
/// leaves the view, or a mode mapping the scroll position onto the linked object's progress.
/// </summary>
/// <remarks>
/// The default is the two method names <c>"play pause"</c>; four of them (<c>"play pause reverse play"</c>) are the
/// forward and backward pairs instead. <c>true</c> and <c>"linear"</c> map the scroll position straight onto the
/// progress, a number below 1 smooths that mapping, and an <see cref="Ease"/> or an <see cref="EaseParser"/> name eases
/// it. <c>false</c> turns every mode off.
/// </remarks>
public readonly struct ScrollSync
{
    internal readonly object? Raw;

    private ScrollSync(object raw) => Raw = raw;

    /// <summary>Maps the scroll position straight onto the progress, or turns every mode off.</summary>
    public static implicit operator ScrollSync(bool enabled) => new(enabled);

    /// <summary>Maps the scroll position onto the progress, smoothed: 1 follows it exactly and lower values trail it.</summary>
    public static implicit operator ScrollSync(double smoothing) => new(smoothing);

    /// <summary>The playback methods to call (<c>"play pause"</c>), <c>"linear"</c>, or the name of an ease.</summary>
    public static implicit operator ScrollSync(string mode) => new(mode);

    /// <summary>Maps the scroll position onto the progress through this ease.</summary>
    public static implicit operator ScrollSync(Ease ease) => new(ease);
}
