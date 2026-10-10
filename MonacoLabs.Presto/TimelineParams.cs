namespace MonacoLabs.Presto;

/// <summary>
/// Parameters for <see cref="Presto.CreateTimeline"/>; a timeline's own duration comes from its children, never from
/// <see cref="TimerParams.Duration"/>. There is no reduced-motion form: each child picks its own through <see cref="AnimationParams.WhenReduced"/>.
/// </summary>
public record TimelineParams : TimerParams
{
    /// <summary>Overrides of <see cref="Presto.Defaults"/> inherited by every child added to the timeline.</summary>
    public DefaultsParams? Defaults { get; init; }

    /// <summary>
    /// Whether adding a child composes it against the timeline's state at the child's position, so its from values are the
    /// ones the earlier children leave behind. This is not the per-tween <see cref="MonacoLabs.Presto.Composition"/> mode.
    /// </summary>
    public bool? Composition { get; init; }

    /// <summary>An ease applied to the timeline's playhead, so every child is driven through the eased time rather than the raw one.</summary>
    public EaseValue? PlaybackEase { get; init; }

    /// <summary>Called after the children have been rendered each frame.</summary>
    public Action<Timer>? OnRender { get; init; }
}
