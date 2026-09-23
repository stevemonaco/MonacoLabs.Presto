using Monaco.Presto.Core;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>Entry point mirroring the anime.js module exports: the engine singleton, global defaults and factory methods.</summary>
public static class Presto
{
    private static readonly ObjectTargetAdapter s_objectAdapter = new();

    [ThreadStatic] private static DecomposedValue? s_getValue;

    /// <summary>The main engine, which timers run on unless <see cref="Engine.MakeCurrent"/> installed another (upstream <c>engine</c>); its <see cref="Engine.FrameSource"/> is what a host such as the Avalonia bridge replaces.</summary>
    public static Engine Engine { get; } = new(new StopwatchFrameSource(), new PrestoDefaults());

    /// <summary>The main engine's defaults, which new timers inherit and further engines start from, mirroring the upstream <c>defaults</c> object.</summary>
    public static PrestoDefaults Defaults => Engine.Defaults;

    /// <summary>Target adapters consulted in order; the built-in object and reflection adapter handles whatever none of them claims.</summary>
    /// <remarks>Read from every engine's thread, so populate it before any animation starts — the Avalonia bridge registers its adapters from <c>PrestoAvalonia.EnsureInitialized</c>.</remarks>
    public static IList<ITargetAdapter> Adapters { get; } = new List<ITargetAdapter>();

    /// <summary>Codecs for property types the built-in adapter cannot tween natively, keyed by the property type.</summary>
    /// <remarks>Read from every engine's thread, so populate it before any animation starts — the Avalonia bridge registers its codecs from <c>PrestoAvalonia.EnsureInitialized</c>.</remarks>
    public static IDictionary<Type, IValueCodec> Codecs { get; } = new Dictionary<Type, IValueCodec>();

    /// <summary>Creates a timer that fires callbacks over time without animating anything (upstream <c>createTimer()</c>).</summary>
    public static Timer CreateTimer(TimerParams? parameters = null) => new Timer(parameters ?? new(), null, 0).Init();

    /// <summary>Creates a timeline that sequences animations, timers and callbacks on a shared playhead (upstream <c>createTimeline()</c>).</summary>
    public static Timeline CreateTimeline(TimelineParams? parameters = null) => new Timeline(parameters ?? new()).Init();

    /// <summary>Creates the paused per-property animations that <see cref="Animatable.Set(string, double, double?, EaseValue?)"/> drives.</summary>
    public static Animatable CreateAnimatable(object? targets, AnimatableParams parameters) => new(targets, parameters);

    /// <summary>Creates a lifetime that everything constructed inside it registers with, so one <see cref="Scope.Revert"/> undoes it all.</summary>
    public static Scope CreateScope(ScopeParams? parameters = null) => new(parameters);

    /// <summary>
    /// Spaces <paramref name="value"/> across the targets: a step added per target, or a <c>[from, to]</c> range
    /// distributed evenly between them.
    /// </summary>
    public static Stagger Stagger(TweenValue value, StaggerParams? parameters = null) => new(value, parameters ?? new());

    /// <summary>Animates the properties named in <paramref name="parameters"/> on every one of <paramref name="targets"/> (upstream <c>animate()</c>).</summary>
    public static Animation Animate(object? targets, AnimationParams parameters) =>
        (Animation)new Animation(targets, parameters, null, 0).Init();

    /// <summary>Writes values to targets immediately; returns the zero duration animation that wrote them, as upstream <c>utils.set()</c> does.</summary>
    public static Animation Set(object? targets, AnimationParams parameters) =>
        (Animation)new Animation(targets, parameters, null, 0, fastSet: true).Resume();

    /// <summary>Reads <paramref name="property"/> from the first of <paramref name="targets"/>, or null when none resolve.</summary>
    public static object? Get(object? targets, string property)
    {
        var parsed = TargetParser.Parse(targets);
        return parsed.Count == 0 ? null : ResolveAccessor(parsed[0], property).GetValue(parsed[0]);
    }

    /// <summary>Reads <paramref name="property"/>, dropping the unit it is stored with unless <paramref name="withUnit"/> is true.</summary>
    public static object? Get(object? targets, string property, bool withUnit)
    {
        if (withUnit) return Get(targets, property);
        return Decompose(targets, property) is { Kind: ValueKind.Number or ValueKind.Unit } value ? value.Number : null;
    }

    /// <summary>Reads <paramref name="property"/> converted to <paramref name="unit"/>, or null when it holds no single number.</summary>
    public static string? Get(object? targets, string property, string unit)
    {
        if (Decompose(targets, property) is not { Kind: ValueKind.Number or ValueKind.Unit } value) return null;
        UnitConverter.Convert(value, unit);
        return JsNumber.Format(Helpers.Round(value.Number, Globals.Precision)) + value.Unit;
    }

    /// <summary>
    /// Stops <paramref name="renderable"/>, or every running animation, from animating <paramref name="targets"/> — only their
    /// <paramref name="property"/> tweens when one is named — cancelling the animations left with no tweens.
    /// </summary>
    /// <returns>The parsed targets.</returns>
    public static IReadOnlyList<object> Remove(object? targets, Timer? renderable = null, string? property = null)
    {
        var parsed = TargetParser.Parse(targets);
        Composer.RemoveTargetsFromRenderable(parsed, (Clock?)renderable ?? Engine.Current, property);
        // Naming a renderable removes that one timer's tweens, which is no reason to stop what an adapter drives elsewhere.
        if (renderable == null)
        {
            for (var i = 0; i < Adapters.Count; i++) Adapters[i].Removed(parsed, property);
        }
        return parsed;
    }

    private static DecomposedValue? Decompose(object? targets, string property)
    {
        var parsed = TargetParser.Parse(targets);
        if (parsed.Count == 0) return null;
        var accessor = ResolveAccessor(parsed[0], property);
        var value = s_getValue ??= new();
        ValueParser.Decompose(accessor.GetValue(parsed[0]), value, accessor.Codec);
        return value;
    }

    internal static IPropertyAccessor ResolveAccessor(object target, string property)
    {
        for (var i = 0; i < Adapters.Count; i++)
        {
            if (Adapters[i].Resolve(target, property) is { } accessor) return accessor;
        }
        return s_objectAdapter.Resolve(target, property);
    }

    /// <summary>
    /// The name a tween on <paramref name="property"/> is keyed by, so that every spelling an adapter accepts for one
    /// property — a bare name, a qualified name, a typed handle — composes against the others.
    /// </summary>
    internal static string Canonicalise(object target, string property)
    {
        for (var i = 0; i < Adapters.Count; i++)
        {
            if (Adapters[i].Canonicalise(target, property) is { } canonical) return canonical;
        }
        return property;
    }
}
