namespace MonacoLabs.Presto.Events;

/// <summary>
/// Where a <see cref="ScrollObserver"/> eases the scroller to once scrolling settles between its thresholds: a progress
/// increment (<c>.25</c>), a function of the progress (<c>Utils.Snap([0, .3, 1])</c>), or <see cref="Labels"/>.
/// </summary>
/// <remarks>Upstream has no snapping; this is GSAP ScrollTrigger's <c>snap</c>. Tune a value with <c>with { Duration = 600 }</c>.</remarks>
public sealed record ScrollSnap
{
    private ScrollSnap(Func<ScrollObserver, double, double> map) => Map = map;

    /// <summary>Snaps to the linked <see cref="Timeline"/>'s labels, and to its start and end.</summary>
    /// <remarks>
    /// Labels map linearly onto the scroll progress, so the timeline lands on one exactly only under a linear
    /// <see cref="ScrollObserverParams.Sync"/> (<c>true</c> or <c>"linear"</c>); a smoothed sync converges onto it afterwards.
    /// </remarks>
    public static ScrollSnap Labels { get; } = new(static (observer, progress) => observer.Linked is Timeline { Duration: > 0 } timeline
        ? Utils.Snap(progress, [0, 1, .. timeline.Labels.Values.Select(time => time / timeline.Duration)])
        : progress);

    /// <summary>How long the snap takes; 400 ms by default.</summary>
    public double? Duration { get; init; }

    /// <summary>The snap's ease; <see cref="Eases.OutQuad"/> by default.</summary>
    public Ease? Ease { get; init; }

    /// <summary>How long the offset has to stay still before the snap starts; 150 ms by default.</summary>
    public double? Delay { get; init; }

    internal Func<ScrollObserver, double, double> Map { get; }

    /// <summary>Snaps the progress to the nearest multiple of <paramref name="increment"/>.</summary>
    public static implicit operator ScrollSnap(double increment) => new((_, progress) => Utils.Snap(progress, increment));

    /// <summary>Snaps the progress to what <paramref name="map"/> returns for it.</summary>
    public static implicit operator ScrollSnap(Func<double, double> map) => new((_, progress) => map(progress));

    /// <inheritdoc cref="op_Implicit(Func{double, double})"/>
    public static implicit operator ScrollSnap(ChainedUtil map) => new((_, progress) => map.Invoke(progress));
}
