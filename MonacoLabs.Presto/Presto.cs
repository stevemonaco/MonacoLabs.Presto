using System.Runtime.CompilerServices;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary>Entry point: the engine singleton, global defaults and factory methods.</summary>
public static class Presto
{
    private static readonly ObjectTargetAdapter s_objectAdapter = new();

    [ThreadStatic] private static DecomposedValue? s_getValue;

    /// <summary>The main engine, which timers run on unless <see cref="Engine.MakeCurrent"/> installed another; its <see cref="Engine.FrameSource"/> is what a host such as the Avalonia bridge replaces.</summary>
    public static Engine Engine { get; } = new(new StopwatchFrameSource(), new PrestoDefaults());

    /// <summary>The main engine's defaults, which new timers inherit and further engines start from.</summary>
    public static PrestoDefaults Defaults => Engine.Defaults;

    /// <summary>Target adapters consulted in order; the built-in object and reflection adapter handles whatever none of them claims.</summary>
    /// <remarks>Read from every engine's thread, so populate it before any animation starts — the Avalonia bridge registers its adapters from <c>PrestoAvalonia.EnsureInitialized</c>.</remarks>
    public static IList<ITargetAdapter> Adapters { get; } = new List<ITargetAdapter>();

    /// <summary>Codecs for property types the built-in adapter cannot tween natively, keyed by the property type.</summary>
    /// <remarks>Read from every engine's thread, so populate it before any animation starts — the Avalonia bridge registers its codecs from <c>PrestoAvalonia.EnsureInitialized</c>.</remarks>
    public static IDictionary<Type, IValueCodec> Codecs { get; } = new Dictionary<Type, IValueCodec>();

    /// <summary>Creates a timer that fires callbacks over time without animating anything.</summary>
    public static Timer CreateTimer(TimerParams? parameters = null) => Created(new Timer(parameters ?? new(), null, 0).Init());

    /// <summary>
    /// Waits <paramref name="duration"/> of the current engine's time, so unlike <see cref="Task.Delay(int)"/> the wait
    /// follows <see cref="Engine.Speed"/> and a manually advanced frame source.
    /// </summary>
    /// <remarks>Cancel <paramref name="cancellationToken"/> on the engine's thread, as with every other control.</remarks>
    public static Task Delay(double duration, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        if (duration <= 0) return Task.CompletedTask;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        var timer = CreateTimer(new()
        {
            Duration = duration,
            OnComplete = _ =>
            {
                registration.Dispose();
                completion.TrySetResult();
            },
        });
        registration = cancellationToken.Register(() =>
        {
            timer.Cancel();
            completion.TrySetCanceled(cancellationToken);
        });
        return completion.Task;
    }

    /// <summary>Creates a source <see cref="MotionValue"/> that derived values follow (Motion's <c>motionValue()</c>).</summary>
    public static MotionValue CreateMotionValue(double initial = 0) => new(initial);

    /// <summary>Creates a timeline that sequences animations, timers and callbacks on a shared playhead.</summary>
    public static Timeline CreateTimeline(TimelineParams? parameters = null) => Created(new Timeline(parameters ?? new()).Init());

    /// <summary>Creates the paused per-property animations that <see cref="Animatable.Set(PropertyKey, double, double?, EaseValue?)"/> drives.</summary>
    public static Animatable CreateAnimatable(object? targets, AnimatableParams parameters) => new(targets, parameters);

    /// <summary>Creates a lifetime that everything constructed inside it registers with, so one <see cref="Scope.Revert"/> undoes it all.</summary>
    public static Scope CreateScope(ScopeParams? parameters = null) => new(parameters);

    /// <summary>
    /// Spaces <paramref name="value"/> across the targets: a step added per target, or a <c>[from, to]</c> range
    /// distributed evenly between them.
    /// </summary>
    public static Stagger Stagger(TweenValue value, StaggerParams? parameters = null) => new(value, parameters ?? new());

    /// <summary>
    /// The value of a string property (<c>["Text"] = Presto.ScrambleText()</c>) that
    /// scrambles the target's text with random characters and settles it character by character.
    /// </summary>
    /// <remarks>
    /// The scramble chains off the tween it follows, so one added to a timeline, or a later keyframe, starts from the text
    /// the previous one ended on; without <see cref="ScrambleTextParams.Text"/> it then settles back on that same text.
    /// </remarks>
    public static ChainedFunctionValue ScrambleText(ScrambleTextParams? parameters = null) => TextScrambler.Create(parameters ?? new());

    /// <summary>Animates the properties named in <paramref name="parameters"/> on every one of <paramref name="targets"/>.</summary>
    [OverloadResolutionPriority(1)]
    public static Animation Animate(object? targets, AnimationParams parameters) =>
        Created((Animation)new Animation(targets, parameters, null, 0).Init());

    /// <summary>Animates <paramref name="targets"/> with the fresh builder <paramref name="build"/> configures.</summary>
    public static Animation Animate(object? targets, Action<AnimationBuilder> build) => Animate(targets, Build(build).ToParams());

    /// <summary>Starts a fluent, reusable <see cref="AnimationBuilder"/>, a typed alternative to the <see cref="AnimationParams"/> indexer.</summary>
    public static AnimationBuilder Build() => new();

    /// <summary>Writes values to targets immediately; returns the zero duration animation that wrote them.</summary>
    [OverloadResolutionPriority(1)]
    public static Animation Set(object? targets, AnimationParams parameters) =>
        (Animation)new Animation(targets, parameters, null, 0, fastSet: true).Resume();

    /// <summary>Writes the values of the fresh builder <paramref name="build"/> configures to <paramref name="targets"/> immediately.</summary>
    public static Animation Set(object? targets, Action<AnimationBuilder> build) => Set(targets, Build(build).ToParams());

    private static T Created<T>(T timer) where T : Timer
    {
        timer.OwnerEngine.TimerCreated?.Invoke(timer);
        return timer;
    }

    internal static AnimationBuilder Build(Action<AnimationBuilder> build)
    {
        var builder = new AnimationBuilder();
        build(builder);
        return builder;
    }

    /// <summary>Reads <paramref name="property"/> from the first of <paramref name="targets"/>, or null when none resolve.</summary>
    public static object? Get(object? targets, PropertyKey property)
    {
        var parsed = TargetParser.Parse(targets);
        return parsed.Count == 0 ? null : ResolveAccessor(parsed[0], property.Unwrap()).GetValue(parsed[0]);
    }

    /// <summary>Reads <paramref name="property"/>, dropping the unit it is stored with unless <paramref name="withUnit"/> is true.</summary>
    public static object? Get(object? targets, PropertyKey property, bool withUnit)
    {
        if (withUnit) return Get(targets, property);
        return Decompose(targets, property.Unwrap()) is { Kind: ValueKind.Number or ValueKind.Unit } value ? value.Number : null;
    }

    /// <summary>Reads <paramref name="property"/> converted to <paramref name="unit"/>, or null when it holds no single number.</summary>
    public static string? Get(object? targets, PropertyKey property, string unit)
    {
        if (Decompose(targets, property.Unwrap()) is not { Kind: ValueKind.Number or ValueKind.Unit } value) return null;
        UnitConverter.Convert(value, unit);
        return JsNumber.Format(Helpers.Round(value.Number, Globals.Precision)) + value.Unit;
    }

    /// <summary>
    /// Stops <paramref name="renderable"/>, or every running animation, from animating <paramref name="targets"/> — only their
    /// <paramref name="property"/> tweens when one is named — canceling the animations left with no tweens.
    /// </summary>
    /// <returns>The parsed targets.</returns>
    public static IReadOnlyList<object> Remove(object? targets, Timer? renderable = null, PropertyKey? property = null)
    {
        var parsed = TargetParser.Parse(targets);
        var name = property?.Unwrap();
        Composer.RemoveTargetsFromRenderable(parsed, (Clock?)renderable ?? Engine.Current, name);
        // Naming a renderable removes that one timer's tweens, which is no reason to stop what an adapter drives elsewhere.
        if (renderable == null)
        {
            for (var i = 0; i < Adapters.Count; i++) Adapters[i].Removed(parsed, name);
        }
        return parsed;
    }

    /// <summary>
    /// The running animations on the current engine, timeline children included, that tween <paramref name="target"/> —
    /// only those tweening its <paramref name="property"/> when one is named.
    /// </summary>
    /// <remarks>Completed and canceled animations are skipped, and a paused one drops out once the engine releases it on its next frame.</remarks>
    public static IReadOnlyList<Animation> GetAnimations(object target, PropertyKey? property = null)
    {
        var animations = new List<Animation>();
        var name = property?.Unwrap();
        CollectAnimations(Engine.Current, target, string.IsNullOrEmpty(name) ? null : Canonicalize(target, name), animations);
        return animations;
    }

    private static void CollectAnimations(Clock parent, object target, string? property, List<Animation> animations)
    {
        for (var child = parent._head; child != null; child = child._next)
        {
            if (child.Completed || child.Canceled) continue;
            if (child._head != null) CollectAnimations(child, target, property, animations);
            else if (child is Animation animation && Tweens(animation, target, property)) animations.Add(animation);
        }
    }

    private static bool Tweens(Animation animation, object target, string? property)
    {
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            if (ReferenceEquals(tween.Target, target) && (property == null || tween.Property == property)) return true;
        }
        return false;
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
    internal static string Canonicalize(object target, string property)
    {
        for (var i = 0; i < Adapters.Count; i++)
        {
            if (Adapters[i].Canonicalize(target, property) is { } canonical) return canonical;
        }
        return property;
    }
}
