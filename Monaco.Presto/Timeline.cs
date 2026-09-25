using Monaco.Presto.Core;
using Monaco.Presto.Targets;

namespace Monaco.Presto;

/// <summary>
/// A timer whose children are other timers and animations. Port of anime.js <c>Timeline</c>: its duration starts at 0 and
/// grows as children are added at the positions <see cref="TimelinePosition"/> resolves.
/// </summary>
public sealed class Timeline : Timer
{
    private readonly PrestoDefaults _defaults;

    internal Timeline(TimelineParams parameters) : base(parameters, null, 0)
    {
        var engineDefaults = OwnerEngine.Defaults;
        var playbackEase = parameters.PlaybackEase ?? engineDefaults.PlaybackEase;
        Duration = 0;
        IterationDuration = 0;
        Labels = new();
        _defaults = parameters.Defaults?.Merge(engineDefaults) ?? engineDefaults;
        Composition = parameters.Composition ?? true;
        _onRender = parameters.OnRender ?? engineDefaults.OnRender;
        _ease = playbackEase is { } ease ? ease.Resolve(this, 0, 1, out _) : null;
    }

    /// <summary>Named positions children can be added at.</summary>
    /// <remarks>Labels are raw times on the timeline, so seeking to one lands on it only when no <see cref="TimelineParams.PlaybackEase"/> or <see cref="Timer.Reversed"/> reshapes the playhead.</remarks>
    public Dictionary<string, double> Labels { get; }

    /// <inheritdoc cref="TimelineParams.Composition"/>
    public bool Composition { get; }

    internal override PrestoDefaults ChildDefaults => _defaults;

    /// <summary>Adds an animation of <paramref name="targets"/> at <paramref name="position"/>, which defaults to the end of the timeline.</summary>
    public Timeline Add(object? targets, AnimationParams parameters, TimelinePosition position = default)
    {
        _hasChildren = true;
        var childPosition = ChildPosition(position, parameters.Duration?.AsDouble);
        AddChild(new Animation(targets, parameters, this, childPosition));
        return this;
    }

    /// <summary>Adds one child per target, each at the position <paramref name="position"/> staggers that target to.</summary>
    public Timeline Add(object? targets, AnimationParams parameters, Stagger position)
    {
        _hasChildren = true;
        var parsedTargets = TargetParser.Parse(targets);
        var duration = Duration;
        var iterationDuration = IterationDuration;
        for (var i = 0; i < parsedTargets.Count; i++)
        {
            // Every child is positioned against the timeline as it was before the group, not against the child before it.
            Duration = duration;
            IterationDuration = iterationDuration;
            var childParameters = parameters.Id is { } id ? parameters with { Id = $"{id}-{i}" } : parameters;
            var staggered = position.Invoke(parsedTargets[i], i, parsedTargets, this);
            TimelinePosition offset = staggered.Raw is string text ? text : staggered.Raw as double? ?? 0;
            var childPosition = ChildPosition(offset, childParameters.Duration?.AsDouble);
            AddChild(new Animation(parsedTargets[i], childParameters, this, childPosition, index: i, allTargets: parsedTargets));
        }
        return this;
    }

    /// <summary>
    /// Adds a plain timer at <paramref name="position"/>. The parameters must be written as <c>new TimerParams { … }</c>,
    /// since a bare <c>new() { … }</c> is ambiguous with the targets overload.
    /// </summary>
    public Timeline Add(TimerParams parameters, TimelinePosition position = default)
    {
        _hasChildren = true;
        var childPosition = ChildPosition(position, parameters.Duration);
        AddChild(new Timer(parameters, this, childPosition));
        return this;
    }

    /// <summary>Adds an animation that writes its values instantly at <paramref name="position"/>, replacing whatever the earlier children left there.</summary>
    public Timeline Set(object? targets, AnimationParams parameters, TimelinePosition position = default) =>
        Add(targets, parameters with { Duration = Constants.MinValue, Composition = Monaco.Presto.Composition.Replace }, position);

    /// <summary>Invokes <paramref name="callback"/> when the playhead reaches <paramref name="position"/>, in either direction.</summary>
    public Timeline Call(Action<Timeline> callback, TimelinePosition position = default) =>
        Add(new TimerParams { Duration = 0, Delay = 0, OnComplete = _ => callback(this) }, position);

    /// <summary>Names the time <paramref name="position"/> resolves to, so later children can be added at <paramref name="name"/>.</summary>
    public Timeline Label(string name, TimelinePosition position = default)
    {
        Labels[name] = position.Parse(this);
        return this;
    }

    /// <summary>Pauses an existing timer or animation and drives its <see cref="Timer.CurrentTime"/> from the timeline instead.</summary>
    public Timeline Sync(Timer synced, TimelinePosition position = default)
    {
        synced.Pause();
        var duration = synced.Duration;
        return Add(synced, new AnimationParams
        {
            ["CurrentTime"] = TweenValue.FromTo(0, duration),
            Duration = duration,
            Delay = 0,
            Ease = "linear",
            PlaybackEase = "linear",
        }, position);
    }

    /// <summary>Stops this timeline from animating <paramref name="targets"/>, or only their <paramref name="property"/> tweens when one is named.</summary>
    public Timeline Remove(object? targets, string? property = null)
    {
        Presto.Remove(targets, this, property);
        return this;
    }

    /// <inheritdoc cref="Timer.Init"/>
    public override Timeline Init(bool internalRender = false)
    {
        base.Init(internalRender);
        return this;
    }

    /// <inheritdoc cref="Timer.Stretch"/>
    public override Timeline Stretch(double newDuration)
    {
        var currentDuration = Duration;
        if (currentDuration == Helpers.NormalizeTime(newDuration)) return this;
        var timeScale = newDuration / currentDuration;
        Helpers.ForEachChildren(this, child => child.Stretch(child.Duration * timeScale));
        foreach (var name in Labels.Keys) Labels[name] *= timeScale;
        base.Stretch(newDuration);
        return this;
    }

    /// <summary>Re-evaluates the function based values of every child animation against the targets' current state.</summary>
    public Timeline Refresh()
    {
        Helpers.ForEachChildren(this, static child => (child as Animation)?.Refresh());
        return this;
    }

    /// <inheritdoc cref="Timer.Revert"/>
    public override Timeline Revert()
    {
        base.Revert();
        // Upstream passes the revert method to forEachChildren without calling it, so its children keep their animated values.
        Helpers.ForEachChildren(this, static child => child.Revert(), true);
        return this;
    }

    // Renders the timeline at the resolved time so the child starts from the values its siblings leave behind.
    private double ChildPosition(TimelinePosition position, double? childDuration)
    {
        var time = position.Parse(this);
        // A setter or 0 duration child starts one minValue early so its end value lands on the requested position.
        if (childDuration <= Constants.MinValue) time -= Constants.MinValue;
        if (Composition) Renderer.Tick(this, time, true, true, TickMode.Auto);
        return time;
    }

    private void AddChild(Timer child)
    {
        if (Composition) child.Init(true);
        Helpers.AddChild(this, child);
        FitChildren();
        if (Composition) Init(true);
    }

    internal void FitChildren()
    {
        for (var sibling = _head; sibling != null; sibling = sibling._next)
        {
            var siblingEnd = sibling._offset + sibling._delay + sibling.Duration;
            if (siblingEnd > IterationDuration) IterationDuration = siblingEnd;
        }
        UpdateDuration();
    }
}
