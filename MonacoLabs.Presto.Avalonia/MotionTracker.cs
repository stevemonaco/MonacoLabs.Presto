using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Effects;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Text;
using MonacoLabs.Presto.Transforms;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Diagnostics;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary>
/// An element's motion states at run time: which hold, which one wins each property, and the animations that move each
/// property to its winner or back to rest. It acts only when a condition changes, never per frame.
/// </summary>
internal sealed class MotionTracker
{
    private static readonly char[] s_separators = [' ', ',', '\t', '\r', '\n'];

    private readonly Control _control;
    private readonly List<Entry> _entries = [];
    private readonly List<Entry> _added = [];
    private readonly HashSet<string> _tokens = [];
    // The animation that last moved each property; a return to rest reverts the property only while it is still the driver.
    private readonly Dictionary<string, Animation> _drivers = [];
    private Dictionary<string, Entry> _winners = [];
    private Dictionary<string, Entry> _resolved = [];
    private Dictionary<Entry, List<string>>? _entering;
    private Dictionary<Entry, List<string>>? _leaving;
    private MotionStates? _states;
    private bool _listening;
    private bool _pressed;
    private bool _hoverable;
    private bool _hovered;
    private TopLevel? _watched;
    private readonly Action _follow;
    private Engine? _engine;

    public MotionTracker(Control control)
    {
        PrestoAvalonia.EnsureInitialized();
        _control = control;
        _follow = HandleReducedMotionChanged;
        control.Classes.CollectionChanged += HandleClassesChanged;
        control.AttachedToVisualTree += HandleAttached;
        control.DetachedFromVisualTree += HandleDetached;
        ResolveSource();
        ReadTokens();
        Rebuild();
    }

    public bool HasAdded => _added.Count > 0;

    public void Release()
    {
        _control.Classes.CollectionChanged -= HandleClassesChanged;
        _control.AttachedToVisualTree -= HandleAttached;
        _control.DetachedFromVisualTree -= HandleDetached;
        if (_states != null) _states.CollectionChanged -= HandleStatesChanged;
        Listen(false);
        ListenForHover(false);
        Stop();
        Source = null;
    }

    /// <summary>Re-reads the sugar properties and <see cref="Motion.StatesProperty"/> into the ordered state list.</summary>
    public void Rebuild()
    {
        foreach (var entry in _entries) entry.Active = false;
        _entries.Clear();
        AddSugar(":pointerover", Motion.GetHover(_control));
        AddSugar(":focus-visible", Motion.GetFocus(_control));
        AddSugar(":pressed", Motion.GetPress(_control));
        var states = Motion.GetStates(_control);
        if (states != _states)
        {
            if (_states != null) _states.CollectionChanged -= HandleStatesChanged;
            if (states != null) states.CollectionChanged += HandleStatesChanged;
            _states = states;
        }
        if (states != null) foreach (var state in states) _entries.Add(Create(state));
        _entries.AddRange(_added);
        ListenIfPressable();
        Evaluate();
    }

    /// <summary>The ancestor whose <c>Motion.State</c> this element follows, if any.</summary>
    public Control? Source { get; private set; }

    /// <summary>Re-reads the tokens and plans the move to the states that now hold, returning whether <see cref="Play"/> has anything to play.</summary>
    public bool Prepare()
    {
        ReadTokens();
        return Plan();
    }

    /// <summary>Adds <paramref name="state"/> after every declared state, so it wins the properties it shares with them.</summary>
    public Entry Add(MotionState state)
    {
        var entry = Create(state);
        _added.Add(entry);
        _entries.Add(entry);
        ListenIfPressable();
        Evaluate();
        return entry;
    }

    public void Remove(Entry entry)
    {
        if (!_added.Remove(entry)) return;
        _entries.Remove(entry);
        entry.Active = false;
        ListenIfPressable();
        Evaluate();
    }

    private void ListenIfPressable()
    {
        var pressable = false;
        var hoverable = false;
        foreach (var entry in _entries)
        {
            pressable |= entry.Condition.Pressed;
            hoverable |= entry.Condition.Hovered;
        }
        Listen(pressable);
        ListenForHover(hoverable);
    }

    private void ListenForHover(bool listen)
    {
        if (listen == _hoverable) return;
        _hoverable = listen;
        if (listen)
        {
            _control.PointerEntered += HandlePointerEntered;
            _control.PointerExited += HandlePointerExited;
        }
        else
        {
            _control.PointerEntered -= HandlePointerEntered;
            _control.PointerExited -= HandlePointerExited;
            Watch(null);
        }
        _hovered = listen && _control.IsPointerOver;
    }

    private void Listen(bool listen)
    {
        if (listen == _listening) return;
        _listening = listen;
        _pressed = false;
        const RoutingStrategies routes = RoutingStrategies.Tunnel | RoutingStrategies.Bubble;
        if (listen)
        {
            _control.AddHandler(InputElement.PointerPressedEvent, HandlePointerPressed, routes, handledEventsToo: true);
            _control.AddHandler(InputElement.PointerReleasedEvent, HandlePointerReleased, routes, handledEventsToo: true);
            // A touch that becomes a scroll loses capture to the scroller, ending the press.
            _control.AddHandler(InputElement.PointerCaptureLostEvent, HandlePointerCaptureLost, handledEventsToo: true);
        }
        else
        {
            _control.RemoveHandler(InputElement.PointerPressedEvent, HandlePointerPressed);
            _control.RemoveHandler(InputElement.PointerReleasedEvent, HandlePointerReleased);
            _control.RemoveHandler(InputElement.PointerCaptureLostEvent, HandlePointerCaptureLost);
        }
    }

    private void AddSugar(string when, AnimationBuilder? motion)
    {
        if (motion != null) _entries.Add(Create(new MotionState { When = when, Motion = motion }));
    }

    private Entry Create(MotionState state)
    {
        var condition = MotionCondition.Parse(state.When);
        state.Validate();
        return new Entry(state, condition, Read(state), Engine.Current.Defaults.ReducedMotion);
    }

    private Dictionary<string, KeyValuePair<string, TweenValue>> Read(MotionState state)
    {
        var properties = new Dictionary<string, KeyValuePair<string, TweenValue>>();
        if (state.Motion != null)
        {
            foreach (var pair in Expand(state.Motion.Resolve()))
            {
                var property = Presto.Canonicalize(_control, pair.Key);
                // Throws now, from the setter, for a property with no rest, rather than later from an input handler.
                Rest(property);
                properties[property] = pair;
            }
        }
        return properties;
    }

    private void ReadTokens()
    {
        _tokens.Clear();
        if (Motion.GetState(Source ?? _control)?.ToString() is { } text) _tokens.UnionWith(text.Split(s_separators, StringSplitOptions.RemoveEmptyEntries));
    }

    public void ResolveSource()
    {
        Source = null;
        if (!_control.IsAttachedToVisualTree() || _control.IsSet(Motion.StateProperty)) return;
        foreach (var ancestor in _control.GetVisualAncestors())
        {
            if (ancestor is not Control control || !control.IsSet(Motion.StateProperty)) continue;
            Source = control;
            return;
        }
    }

    private void Resolve(Dictionary<string, Entry> winners, List<Entry>? reread = null)
    {
        var reduced = Engine.Current.Defaults.ReducedMotion;
        foreach (var entry in _entries)
        {
            // Re-read here so toggling reduced motion takes effect at the next transition; a replay re-reads only what it replays.
            if (entry.Reduced != reduced && (reread == null || reread.Contains(entry))) (entry.Properties, entry.Reduced) = (Read(entry.State), reduced);
            entry.WasActive = entry.Active;
            entry.Active = entry.Condition.Matches(_control.Classes, _tokens, _pressed, _hovered);
            if (!entry.Active) continue;
            foreach (var property in entry.Properties.Keys) winners[property] = entry;
        }
    }

    private void Evaluate()
    {
        if (Plan()) Play(0);
    }

    /// <summary>
    /// Groups every property whose winner changed: one animation per entering state for the properties it takes, and one per
    /// leaving state, on its own timing, for the properties it hands to the next winner or to rest.
    /// </summary>
    private bool Plan(List<Entry>? reread = null)
    {
        if (!_control.IsAttachedToVisualTree()) return false;
        Resolve(_resolved, reread);
        foreach (var (property, winner) in _resolved)
        {
            var previous = _winners.GetValueOrDefault(property);
            if (previous == winner) continue;
            if (previous is { Active: false } && winner.WasActive && !Resumes(winner, property)) Group(ref _leaving, previous, property);
            else Group(ref _entering, winner, property);
        }
        foreach (var (property, previous) in _winners)
        {
            if (!_resolved.ContainsKey(property)) Group(ref _leaving, previous, property);
        }
        (_winners, _resolved) = (_resolved, _winners);
        _resolved.Clear();
        return _entering != null || _leaving != null;
    }

    /// <summary>Plays what <see cref="Plan"/> grouped, <paramref name="delay"/> later, and returns when the last of it ends.</summary>
    public double Play(double delay)
    {
        var end = 0d;
        if (_entering != null) foreach (var (entry, properties) in _entering) end = Math.Max(end, End(Enter(entry, properties, delay)));
        if (_leaving != null) foreach (var (entry, properties) in _leaving) end = Math.Max(end, End(Leave(entry, properties, delay)));
        _entering = _leaving = null;
        return end;
    }

    private static double End(Animation animation) => animation._delay + animation.Duration;

    private static void Group(ref Dictionary<Entry, List<string>>? groups, Entry entry, string property)
    {
        groups ??= [];
        if (!groups.TryGetValue(entry, out var properties)) groups[entry] = properties = [];
        properties.Add(property);
    }

    private Animation Enter(Entry entry, List<string> properties, double delay)
    {
        var chosen = new OrderedDictionary<string, TweenValue>();
        foreach (var property in properties)
        {
            var (name, value) = entry.Properties[property];
            chosen[name] = value;
        }
        var parameters = entry.State.Motion!.Resolve();
        entry.Form = ReducedMotionFollowers.Form.Of(parameters);
        return Animate(parameters with
        {
            Properties = chosen, Keyframes = null, Composition = Composition.Replace, Delay = Delayed(parameters.Delay, delay),
        }, properties);
    }

    private Animation Leave(Entry entry, List<string> properties, double delay)
    {
        var timing = (entry.State.Leave ?? entry.State.Motion!).Resolve();
        var values = new OrderedDictionary<string, TweenValue>();
        List<string>? resting = null;
        foreach (var property in properties)
        {
            TweenValue to;
            if (_winners.TryGetValue(property, out var next))
            {
                to = Target(next, property);
            }
            else
            {
                to = Rest(property);
                // A geometry tweened plainly is one resampled polyline, losing all but its first figure until the return settles.
                if (to.Raw is Geometry geometry) to = PrestoAvalonia.MorphTo(geometry);
                (resting ??= []).Add(property);
            }
            values[property] = Timing(timing, property) is { } tween
                ? new TweenParams { To = to, Duration = tween.Duration, Delay = tween.Delay is { } own ? Delayed(own, delay) : null, Ease = tween.Ease }
                : to;
        }
        var animation = Animate(new AnimationParams
        {
            Duration = timing.Duration, Delay = Delayed(timing.Delay, delay), Ease = timing.Ease, Composition = Composition.Replace, Properties = values,
        }, properties);
        if (resting == null) return animation;
        if (animation.Completed) Settle(animation, resting);
        else animation.OnComplete = _ => Settle(animation, resting);
        return animation;
    }

    private TweenValue? Delayed(TweenValue? delay, double extra) =>
        extra == 0 ? delay : JsNumber.ToNumber(Animation.GetFunctionValue(delay ?? Presto.Defaults.Delay, _control, 0, [_control], null)?.Raw, 0) + extra;

    private Animation Animate(AnimationParams parameters, List<string> properties)
    {
        foreach (var property in properties)
        {
            // Replacement only trims a tween that has begun; one still waiting out its delay would start later over this one.
            if (_drivers.GetValueOrDefault(property) is { Began: false, Completed: false, Canceled: false } pending) Presto.Remove(_control, pending, property);
        }
        using var origin = PrestoDiagnostics.Origin("Motion");
        var animation = Presto.Animate(_control, parameters with { HostLifetime = WhenDetached.Continue });
        foreach (var property in properties) _drivers[property] = animation;
        Follow(true);
        return animation;
    }

    // Registered only while a property is driven, so the engine never roots a resting tracker.
    private void Follow(bool follow)
    {
        if (follow == (_engine != null)) return;
        if (follow)
        {
            _engine = ReducedMotionFollowers.Add(_follow);
            return;
        }
        ReducedMotionFollowers.Remove(_engine!, _follow);
        _engine = null;
    }

    private void HandleReducedMotionChanged()
    {
        List<Entry>? replays = null;
        foreach (var entry in _entries)
        {
            if (entry is { Active: true, State.Motion: { } motion } && entry.Form.Replays(ReducedMotionFollowers.Form.Of(motion.Resolve())))
                (replays ??= []).Add(entry);
        }
        if (replays == null) return;
        Plan(replays);
        foreach (var (property, winner) in _winners)
        {
            if (replays.Contains(winner) && _entering?.GetValueOrDefault(winner)?.Contains(property) != true) Group(ref _entering, winner, property);
        }
        Play(0);
    }

    /// <summary>Drops the animation-priority values a finished return to rest left, uncovering the equal values beneath.</summary>
    private void Settle(Animation animation, List<string> properties)
    {
        foreach (var property in properties)
        {
            if (!_drivers.TryGetValue(property, out var driver) || driver != animation) continue;
            _drivers.Remove(property);
            Presto.ResolveAccessor(_control, property).Revert(_control, null);
        }
        if (_drivers.Count == 0) Follow(false);
    }

    private void Stop()
    {
        foreach (var (property, animation) in _drivers)
        {
            // Canceling twice would unlink the tweens' siblings a second time.
            if (!animation.Canceled) animation.Cancel();
            Presto.ResolveAccessor(_control, property).Revert(_control, null);
        }
        _drivers.Clear();
        _winners.Clear();
        Follow(false);
    }

    /// <summary>Where <paramref name="entry"/> holds <paramref name="property"/>: its value, or its last keyframe's.</summary>
    private static TweenValue Target(Entry entry, string property) => Final(entry.Properties[property].Value);

    private static bool Resumes(Entry entry, string property) =>
        entry.Properties[property].Value.Raw is TweenValue[] { Length: > 2 } or TweenValue[] and [{ IsParams: true }, ..]
        || entry.State.Motion!.Resolve().Loop is { Value: not 0 };

    private static TweenValue Final(TweenValue value)
    {
        if (value.Raw is TweenValue[] frames)
        {
            for (var i = frames.Length - 1; i >= 0; i--)
            {
                if (frames[i].Raw is not TweenParams { To: null }) return Final(frames[i]);
            }
        }
        return value.Raw is TweenParams { To: { } to } ? Final(to) : value;
    }

    /// <summary>A transform, effect, reveal or warp shorthand's identity, or a property's value beneath the animations, read now so a local value or style set meanwhile is honored.</summary>
    private TweenValue Rest(string property)
    {
        if (TransformShorthands.Parse(property) is { } shorthand) return RenderTransformComposer.Identity(shorthand);
        if (EffectShorthands.Parse(property) is { } effect) return TweenValue.Of(EffectComposer.Identity(effect));
        if (EffectShorthands.ParseClip(property) is { } clip) return TweenValue.Of(ClipComposer.Identity(clip));
        if (EffectShorthands.ParseMask(property) is { } mask) return TweenValue.Of(MaskComposer.Identity(mask));
        if (_control is TextBlock && WarpComposer.Parse(property) is { } warp) return TweenValue.Of(WarpComposer.Identity(_control, warp));
        var avaloniaProperty = AvaloniaObjectTargetAdapter.ResolveProperty(_control.GetType(), property)
            ?? throw new NotSupportedException($"{property} has no rest value for a motion state to return to.");
        var value = _control.GetBaseValue(avaloniaProperty);
        return TweenValue.Of(value == AvaloniaProperty.UnsetValue ? (avaloniaProperty.GetMetadata(_control) as IStyledPropertyMetadata)?.DefaultValue : value);
    }

    private TweenParams? Timing(AnimationParams timing, string property)
    {
        foreach (var (name, value) in timing.Properties)
        {
            if (value.Raw is TweenParams tween && Presto.Canonicalize(_control, name) == property) return tween;
        }
        return null;
    }

    private static IReadOnlyList<KeyValuePair<string, TweenValue>> Expand(AnimationParams parameters) =>
        parameters.Keyframes is { } keyframes
            ? KeyframeExpander.Expand(keyframes, parameters, Presto.Defaults.Duration.AsDouble ?? 0)
            : parameters.Properties;

    private void HandleClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Evaluate();

    private void HandleStatesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_control).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed) Press(true);
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left) Press(false);
    }

    private void HandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Press(false);

    private void Press(bool pressed)
    {
        // Tunnel and bubble both deliver each press.
        if (pressed == _pressed) return;
        _pressed = pressed;
        Evaluate();
    }

    private void HandlePointerEntered(object? sender, PointerEventArgs e)
    {
        Watch(null);
        Hover(true);
    }

    /// <remarks>
    /// A state that moves the element (a hover lift) can slide it out from under a pointer resting on its edge, so hover
    /// lasts while the pointer stays in the element's layout slot, watched from the top level until it leaves.
    /// </remarks>
    private void HandlePointerExited(object? sender, PointerEventArgs e)
    {
        if (InSlot(e)) Watch(TopLevel.GetTopLevel(_control));
        else Hover(false);
    }

    private void HandleWatchedMoved(object? sender, PointerEventArgs e)
    {
        if (_control.IsPointerOver || InSlot(e)) return;
        Watch(null);
        Hover(false);
    }

    private void HandleWatchedExited(object? sender, PointerEventArgs e)
    {
        Watch(null);
        Hover(false);
    }

    private bool InSlot(PointerEventArgs e) =>
        _control.GetVisualParent() is { } parent && _control.Bounds.Contains(e.GetPosition(parent));

    private void Watch(TopLevel? topLevel)
    {
        if (topLevel == _watched) return;
        if (_watched != null)
        {
            _watched.RemoveHandler(InputElement.PointerMovedEvent, HandleWatchedMoved);
            _watched.PointerExited -= HandleWatchedExited;
        }
        _watched = topLevel;
        if (topLevel == null) return;
        topLevel.AddHandler(InputElement.PointerMovedEvent, HandleWatchedMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.PointerExited += HandleWatchedExited;
    }

    private void Hover(bool hovered)
    {
        if (hovered == _hovered) return;
        _hovered = hovered;
        Evaluate();
    }

    /// <summary>
    /// Applies the states that hold instantly, so a recycled or re-shown element arrives as it was rather than replaying
    /// them; a looping state then plays on from there.
    /// </summary>
    private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _hovered = _hoverable && _control.IsPointerOver;
        ResolveSource();
        ReadTokens();
        Resolve(_winners);
        if (_winners.Count == 0) return;
        var values = new OrderedDictionary<string, TweenValue>();
        Dictionary<Entry, List<string>>? looping = null;
        foreach (var (property, entry) in _winners)
        {
            values[property] = Target(entry, property);
            var parameters = entry.State.Motion!.Resolve();
            entry.Form = ReducedMotionFollowers.Form.Of(parameters);
            if (parameters.Loop is { Value: not 0 }) Group(ref looping, entry, property);
        }
        var set = Presto.Set(_control, new AnimationParams { Properties = values });
        foreach (var property in _winners.Keys) _drivers[property] = set;
        Follow(true);
        if (looping != null) foreach (var (entry, properties) in looping) Enter(entry, properties, 0);
    }

    private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Watch(null);
        Stop();
        Source = null;
    }

    /// <summary>One state in the tracker's order, with the properties it moves keyed by canonical name.</summary>
    internal sealed class Entry(MotionState state, MotionCondition condition, Dictionary<string, KeyValuePair<string, TweenValue>> properties, bool reduced)
    {
        public MotionState State { get; } = state;

        public MotionCondition Condition { get; } = condition;

        /// <summary>Canonical name to the name and value as the state wrote them.</summary>
        public Dictionary<string, KeyValuePair<string, TweenValue>> Properties { get; set; } = properties;

        /// <summary>Whether <see cref="Properties"/> were read from the <see cref="AnimationBuilder.WhenReduced"/> form.</summary>
        public bool Reduced { get; set; } = reduced;

        /// <summary>The form this state last played or arrived in, which a flag change compares against.</summary>
        public ReducedMotionFollowers.Form Form { get; set; }

        public bool Active { get; set; }

        public bool WasActive { get; set; }
    }
}
