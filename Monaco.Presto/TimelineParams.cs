namespace Monaco.Presto;

/// <summary>Parameters for <see cref="Presto.CreateTimeline"/>; a timeline's own duration comes from its children, never from <see cref="TimerParams.Duration"/>.</summary>
public record TimelineParams : TimerParams
{
    /// <summary>Overrides of <see cref="Presto.Defaults"/> inherited by every child added to the timeline.</summary>
    public DefaultsParams? Defaults { get; init; }

    /// <summary>
    /// Whether adding a child composes it against the timeline's state at the child's position, so its from values are the
    /// ones the earlier children leave behind. This is not the per-tween <see cref="Monaco.Presto.Composition"/> mode.
    /// </summary>
    public bool? Composition { get; init; }

    public EaseValue? PlaybackEase { get; init; }

    public Action<Timer>? OnRender { get; init; }
}
