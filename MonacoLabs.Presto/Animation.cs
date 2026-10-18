using System.Runtime.CompilerServices;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary>
/// A timer that drives tweens: the parameters are resolved into one <see cref="Tween"/>
/// per target and property, and the animation's own delay and duration are derived from the tweens that result.
/// </summary>
public sealed class Animation : Timer
{
    // Mutated in place and read back immediately, so building tweens allocates nothing extra.
    [ThreadStatic] private static DecomposedValue? s_fromValue;
    [ThreadStatic] private static DecomposedValue? s_toValue;
    [ThreadStatic] private static DecomposedValue? s_originalValue;

    private static int s_animationId;

    private static DecomposedValue ScratchFrom => s_fromValue ??= new();
    private static DecomposedValue ScratchTo => s_toValue ??= new();
    private static DecomposedValue ScratchOriginal => s_originalValue ??= new();

    internal Animation(
        object? targets,
        AnimationParams parameters,
        Timer? parent,
        double parentPosition,
        bool fastSet = false,
        int index = 0,
        IReadOnlyList<object>? allTargets = null)
        : base(parameters = parameters.Resolve(parent?.OwnerEngine ?? Engine.Current, parent?.ChildDefaults), parent, parentPosition)
    {
        var animationId = Interlocked.Increment(ref s_animationId);

        Targets = TargetParser.Parse(targets);

        var animDefaults = parent?.ChildDefaults ?? OwnerEngine.Defaults;
        var reduceAll = !fastSet && !parameters.IsReducedMotion && OwnerEngine.Defaults.ReduceAll;
        if (reduceAll) IterationCount = 1;

        var properties = parameters.Keyframes is { } keyframes
            // Keyframes fall back to the engine's default duration, not the parent timeline's.
            ? KeyframeExpander.Expand(keyframes, parameters, OwnerEngine.Defaults.Duration.AsDouble ?? 0)
            : (IReadOnlyList<KeyValuePair<string, TweenValue>>)parameters.Properties;

        var animSpring = (parameters.Ease ?? animDefaults.Ease).Raw as Spring;
        var animPlaybackEase = parameters.PlaybackEase ?? animDefaults.PlaybackEase;
        var playbackEase = animPlaybackEase is { } value ? value.Resolve(this, 0, 1, out _) : null;

        EaseValue tEasing;
        TweenValue tDuration;
        if (animSpring != null)
        {
            // Lets the spring's onComplete callback reach the animation it settles.
            animSpring.Parent = this;
            tEasing = animSpring.Ease;
            tDuration = animSpring.SettlingDuration;
        }
        else
        {
            tEasing = parameters.Ease ?? (playbackEase != null ? (EaseValue)Eases.None : animDefaults.Ease);
            tDuration = fastSet ? (TweenValue)Constants.MinValue : parameters.Duration ?? animDefaults.Duration;
        }

        var tDelay = parameters.Delay ?? animDefaults.Delay;
        var tModifier = parameters.Modifier ?? animDefaults.Modifier;
        // A large target count defaults to no composition, which skips the sibling bookkeeping entirely.
        var tComposition = parameters.Composition ?? (fastSet || Targets.Count >= Constants.K ? Composition.Overlap : animDefaults.Composition);
        var absoluteOffsetTime = _offset + (parent?._offset ?? 0);
        var groupTargets = allTargets ?? Targets;
        var total = groupTargets.Count;

        var iterationDuration = double.NaN;
        var iterationDelay = double.NaN;
        var shouldTriggerRender = false;

        for (var targetIndex = 0; targetIndex < Targets.Count; targetIndex++)
        {
            var target = Targets[targetIndex];
            var ti = index != 0 ? index : targetIndex;

            for (var propertyIndex = 0; propertyIndex < properties.Count; propertyIndex++)
            {
                var (declaredName, propertyValue) = properties[propertyIndex];
                // Keyed by the canonical name so the other spellings of this property compose against these tweens.
                var propertyName = Presto.Canonicalize(target, declaredName);
                var accessor = Presto.ResolveAccessor(target, propertyName);
                var codec = accessor.Codec;
                var frameValue = propertyValue;
                // Unlike upstream utils.set, a function resolving to a from/to pair or keyframes sets their last value.
                if (fastSet && ValueFunction(propertyValue, target, ti, groupTargets, null) is { } setFunction)
                {
                    frameValue = setFunction();
                    if (frameValue.Raw is TweenValue[] { Length: > 0 } items) frameValue = items[^1];
                }
                var frames = NormalizeKeyframes(frameValue, fastSet);

                object? inlineValue = null;
                var hasInlineValue = false;
                TweenSiblings? siblings = null;
                Tween? prevTween = null;
                var firstTweenChangeStartTime = double.NaN;
                var lastTweenChangeEndTime = 0d;

                object? ReadOriginalValue()
                {
                    // Deviation from upstream, which skips nil and empty originals: any read original, null included, is restored by Revert.
                    hasInlineValue = true;
                    return inlineValue = accessor.GetValue(target);
                }

                for (var tweenIndex = 0; tweenIndex < frames.Length; tweenIndex++)
                {
                    var frame = frames[tweenIndex];
                    var key = frame.Raw as TweenParams;

                    Func<TweenValue>? toFunc = null;
                    Func<TweenValue>? fromFunc = null;

                    var tweenComposition = key?.Composition ?? tComposition;
                    if (codec is DiscreteCodec && tweenComposition == Composition.Additive) throw new ArgumentException($"'{declaredName}' is discrete, so it cannot be additive.", nameof(parameters));
                    if (siblings == null && tweenComposition != Composition.Overlap) siblings = Composer.GetTweenSiblings(target, propertyName, OwnerEngine);
                    // A timeline child chains off the tail sibling of the same timeline; anything else off its own previous keyframe.
                    var tailTween = siblings?._tail;
                    var chainedTween = parent != null && tailTween != null && tailTween.Parent.Parent == parent ? tailTween : prevTween;

                    var computedToValue = GetFunctionValue(key != null ? key.To : frame, target, ti, groupTargets, chainedTween, ref toFunc);
                    TweenValue? tweenToValue;
                    // Allows function based values to return an object syntax value.
                    if (computedToValue?.Raw is TweenParams { To: not null } computedKey)
                    {
                        key = computedKey;
                        tweenToValue = computedKey.To;
                    }
                    else
                    {
                        tweenToValue = computedToValue;
                    }

                    var physics = tweenToValue?.Raw is Inertia or Projectile ? tweenToValue.Value.Raw : null;
                    if (physics != null)
                    {
                        // Its duration is only known once the previous keyframe resolves (INERTIA-PLAN §7).
                        if (propertyValue.IsArray) throw new ArgumentException($"An {physics.GetType().Name} cannot be a keyframe or part of a from/to pair ('{declaredName}').", nameof(parameters));
                        tweenToValue = null;
                        toFunc = null;
                    }

                    var tweenFromValue = GetFunctionValue(key?.From, target, ti, groupTargets, chainedTween, ref fromFunc);
                    var tweenEase = (key?.Ease ?? tEasing).Resolve(target, ti, total, out var keySpring);

                    TweenValue tweenDurationValue;
                    if (key?.Duration is { } keyDuration) tweenDurationValue = keyDuration;
                    // Keyframes split the animation duration between them.
                    else if (frames.Length > 1) tweenDurationValue = JsNumber.ToNumber(GetFunctionValue(tDuration, target, ti, groupTargets, chainedTween)?.Raw, 0) / frames.Length;
                    else tweenDurationValue = tDuration;

                    var tweenDuration = reduceAll && physics is not Projectile ? Constants.MinValue
                        : keySpring != null ? keySpring.SettlingDuration
                        : JsNumber.ToNumber(GetFunctionValue(tweenDurationValue, target, ti, groupTargets, chainedTween)?.Raw, 0);
                    // The animation delay only applies to the first keyframe.
                    var tweenDelay = reduceAll ? 0 : JsNumber.ToNumber(GetFunctionValue(key?.Delay ?? (tweenIndex == 0 ? tDelay : (TweenValue)0), target, ti, groupTargets, chainedTween)?.Raw, 0);
                    var tweenModifier = key?.Modifier ?? tModifier;
                    var tweenTextModifier = key?.TextModifier;

                    var hasFromValue = tweenFromValue != null;
                    var isFromToArray = tweenToValue?.IsArray == true;
                    var isFromToValue = isFromToArray || (hasFromValue && tweenToValue != null);
                    // The update start in local time: the previous keyframe's end, or zero for the first one.
                    var tweenUpdateStartLocal = prevTween != null ? lastTweenChangeEndTime : 0;
                    var tweenStartTime = prevTween != null ? lastTweenChangeEndTime + tweenDelay : tweenDelay;
                    // Rounding minimizes the floating point errors of working in seconds.
                    var absoluteStartTime = Helpers.Round(absoluteOffsetTime + tweenStartTime, 12);
                    // Matches the rounding of prevSibling._absoluteEndTime so keyframes touching at a boundary compare cleanly.
                    var absoluteUpdateStartTime = Helpers.Round(absoluteOffsetTime + tweenUpdateStartLocal, 12);

                    if (hasFromValue || isFromToArray) shouldTriggerRender = true;

                    var prevSibling = prevTween;

                    if (tweenComposition != Composition.Overlap)
                    {
                        var nextSibling = siblings!._head;
                        // Overridden siblings are skipped so the chain resolves to the latest live value.
                        while (nextSibling != null && nextSibling._absoluteStartTime <= absoluteStartTime)
                        {
                            if (!nextSibling._isOverridden) prevSibling = nextSibling;
                            nextSibling = nextSibling._nextRep;
                            if (nextSibling != null && nextSibling._absoluteStartTime >= absoluteStartTime)
                            {
                                while (nextSibling != null)
                                {
                                    Composer.OverrideTween(nextSibling);
                                    nextSibling = nextSibling._nextRep;
                                }
                            }
                        }
                    }

                    if (isFromToValue)
                    {
                        ValueParser.Decompose(
                            isFromToArray ? GetFunctionValue(ArrayItem(tweenToValue!.Value, 0), target, ti, groupTargets, chainedTween, ref fromFunc)?.Raw : tweenFromValue?.Raw,
                            ScratchFrom,
                            codec);
                        ValueParser.Decompose(
                            isFromToArray ? GetFunctionValue(ArrayItem(tweenToValue!.Value, 1), target, ti, groupTargets, chainedTween, ref toFunc)?.Raw : tweenToValue?.Raw,
                            ScratchTo,
                            codec);
                        var originalValue = ReadOriginalValue();
                        if (ScratchFrom.Kind == ValueKind.Number)
                        {
                            if (prevSibling != null)
                            {
                                if (prevSibling._valueType == ValueKind.Unit)
                                {
                                    ScratchFrom.Kind = ValueKind.Unit;
                                    ScratchFrom.Unit = prevSibling._unit;
                                }
                            }
                            else
                            {
                                ValueParser.Decompose(originalValue, ScratchOriginal, codec);
                                if (ScratchOriginal.Kind == ValueKind.Unit)
                                {
                                    ScratchFrom.Kind = ValueKind.Unit;
                                    ScratchFrom.Unit = ScratchOriginal.Unit;
                                }
                            }
                        }
                    }
                    else
                    {
                        // A tween whose previous sibling belongs to the same timeline continues from that sibling's end value.
                        var inheritsFromSibling = parent != null && prevSibling != null && prevSibling.Parent.Parent == parent;
                        if (tweenToValue != null) ValueParser.Decompose(tweenToValue.Value.Raw, ScratchTo, codec);
                        else if (prevTween != null) ValueParser.DecomposeTween(prevTween, ScratchTo);
                        else ValueParser.Decompose(inheritsFromSibling ? prevSibling!._value : ReadOriginalValue(), ScratchTo, codec);

                        if (hasFromValue) ValueParser.Decompose(tweenFromValue!.Value.Raw, ScratchFrom, codec);
                        else if (prevTween != null) ValueParser.DecomposeTween(prevTween, ScratchFrom);
                        else ValueParser.Decompose(inheritsFromSibling ? prevSibling!._value : ReadOriginalValue(), ScratchFrom, codec);
                    }

                    if (ScratchFrom.Operator != '\0')
                    {
                        double relativeTo;
                        if (prevSibling != null)
                        {
                            relativeTo = prevSibling._toNumber;
                        }
                        else
                        {
                            ValueParser.Decompose(ReadOriginalValue(), ScratchOriginal, codec);
                            relativeTo = ScratchOriginal.Number;
                        }
                        ScratchFrom.Number = ValueParser.GetRelativeValue(relativeTo, ScratchFrom.Number, ScratchFrom.Operator);
                    }

                    Reconcile(ScratchFrom, ScratchTo, codec);

                    if (physics != null)
                    {
                        if (ScratchFrom.Kind is ValueKind.Color or ValueKind.Complex) throw new ArgumentException($"An {physics.GetType().Name} needs a single number property, not '{declaredName}'.", nameof(parameters));
                        var plan = physics is Inertia inertia
                            ? InertiaSolver.Resolve(ScratchFrom.Number, inertia.Velocity, inertia)
                            : ProjectileSolver.Resolve(ScratchFrom.Number, (Projectile)physics, tweenDuration);
                        ScratchTo.Number = plan.To;
                        tweenEase = plan.Ease;
                        tweenDuration = reduceAll ? Constants.MinValue : plan.Duration;
                        if (plan.Then is { } then) frames = [frame, new TweenParams { To = then.To, Duration = then.Duration, Ease = then.Ease }];
                    }

                    // Rounding minimizes the floating point errors of working in seconds.
                    var tweenUpdateDuration = Helpers.Round(tweenDuration == 0 || double.IsNaN(tweenDuration) ? Constants.MinValue : tweenDuration, 12);
                    lastTweenChangeEndTime = Helpers.Round(tweenStartTime + tweenUpdateDuration, 12);

                    var tween = new Tween(this, propertyName, target, accessor)
                    {
                        _toFunc = toFunc,
                        _fromFunc = fromFunc,
                        _ease = tweenEase,
                        _fromNumbers = Helpers.CloneNumbers(ScratchFrom.Numbers),
                        _toNumbers = Helpers.CloneNumbers(ScratchTo.Numbers),
                        _strings = ScratchTo.Strings is { } strings ? (string[])strings.Clone() : [],
                        _fromNumber = ScratchFrom.Number,
                        _toNumber = ScratchTo.Number,
                        _numbers = Helpers.CloneNumbers(ScratchFrom.Numbers),
                        _number = ScratchFrom.Number,
                        _unit = ScratchTo.Unit,
                        _modifier = tweenModifier,
                        _textModifier = tweenTextModifier,
                        _startTime = tweenStartTime,
                        _delay = tweenDelay,
                        _updateDuration = tweenUpdateDuration,
                        _changeDuration = tweenUpdateDuration,
                        _absoluteStartTime = absoluteStartTime,
                        _absoluteUpdateStartTime = absoluteUpdateStartTime,
                        _absoluteEndTime = Helpers.Round(absoluteOffsetTime + lastTweenChangeEndTime, 12),
                        _hasFromValue = hasFromValue || isFromToArray,
                        _valueType = ScratchTo.Kind,
                        _composition = tweenComposition,
                        _inlineValue = inlineValue,
                        _hasInlineValue = hasInlineValue,
                        _discrete = codec is DiscreteCodec discrete ? key?.Discrete ?? discrete.Rule : null,
                        _fromObject = ScratchFrom.Discrete,
                        _toObject = ScratchTo.Discrete,
                    };
                    inlineValue = null;
                    hasInlineValue = false;

                    if (tweenComposition != Composition.Overlap) Composer.ComposeTween(tween, siblings!);

                    // Pre-computed so a later function based value can chain off this tween's end value.
                    if (tween._discrete != null)
                    {
                        tween._value = tween._toObject;
                    }
                    else if (tweenTextModifier != null)
                    {
                        tween._value = tweenTextModifier(tween._toNumber);
                    }
                    else
                    {
                        switch (tween._valueType)
                        {
                            case ValueKind.Complex:
                                for (var i = 0; i < tween._numbers.Length; i++) tween._numbers[i] = tweenModifier(tween._toNumbers[i]);
                                tween._value = tween.ComposeComplexString();
                                break;
                            case ValueKind.Unit:
                                tween._value = JsNumber.Format(tweenModifier(tween._toNumber)) + tween._unit;
                                break;
                            case ValueKind.Color:
                                tween._value = PrestoColor.FormatRgba(ScratchTo.Numbers!);
                                break;
                            default:
                                tween._value = tweenModifier(tween._toNumber);
                                break;
                        }
                    }

                    if (double.IsNaN(firstTweenChangeStartTime)) firstTweenChangeStartTime = tween._startTime;

                    prevTween = tween;
                    TweenList.AddChild(this, tween);
                }

                if (double.IsNaN(iterationDelay) || firstTweenChangeStartTime < iterationDelay) iterationDelay = firstTweenChangeStartTime;
                if (double.IsNaN(iterationDuration) || lastTweenChangeEndTime > iterationDuration) iterationDuration = lastTweenChangeEndTime;
            }
        }

        if (!double.IsNaN(iterationDelay) && iterationDelay != 0)
        {
            for (var tween = _headTween; tween != null; tween = tween._next)
            {
                // A tween starting at the very beginning of the animation has its delay trimmed with the start time.
                if (tween._startTime - tween._delay == 0) tween._delay -= iterationDelay;
                tween._startTime -= iterationDelay;
            }
            iterationDuration -= iterationDelay;
            // Float noise can leave a delayed zero-duration iteration under MinValue, so its tweens would never reach their end.
            if (iterationDuration < Constants.MinValue) iterationDuration = Constants.MinValue;
        }
        else
        {
            iterationDelay = 0;
        }

        // Keeps the duration and iteration count finite when no animatable property was provided.
        if (iterationDuration == 0 || double.IsNaN(iterationDuration))
        {
            iterationDuration = Constants.MinValue;
            IterationCount = 0;
        }

        var duration = Helpers.ClampInfinity(((iterationDuration + _loopDelay) * IterationCount) - _loopDelay);

        Id = parameters.Id ?? animationId.ToString();
        Duration = iterationDuration == Constants.MinValue || duration == 0 || double.IsNaN(duration) ? Constants.MinValue : duration;
        IterationDuration = iterationDuration;
        _onRender = parameters.OnRender ?? animDefaults.OnRender;
        _ease = playbackEase;
        _delay = iterationDelay;

        if (!_autoplay && shouldTriggerRender) _onRender?.Invoke(this);
    }

    /// <summary>The targets this animation tweens, flattened out of the targets parameter with duplicates dropped.</summary>
    public IReadOnlyList<object> Targets { get; }

    /// <inheritdoc cref="Timer.Stretch"/>
    public override Animation Stretch(double newDuration)
    {
        var currentDuration = Duration;
        if (currentDuration == Helpers.NormalizeTime(newDuration)) return this;
        var timeScale = newDuration / currentDuration;
        for (var tween = _headTween; tween != null; tween = tween._next)
        {
            tween._updateDuration = Helpers.NormalizeTime(tween._updateDuration * timeScale);
            tween._changeDuration = Helpers.NormalizeTime(tween._changeDuration * timeScale);
            tween._currentTime *= timeScale;
            tween._delay *= timeScale;
            tween._startTime *= timeScale;
            if (Parent == null)
            {
                // A top level animation keeps its offset (Timer.Stretch), so the absolute times are rebuilt from the scaled local
                // ones as the constructor builds them; scaling them instead would multiply their rounding noise as well.
                tween._absoluteStartTime = Helpers.Round(_offset + tween._startTime, 12);
                tween._absoluteUpdateStartTime = Helpers.Round(_offset + tween._startTime - tween._delay, 12);
                tween._absoluteEndTime = Helpers.Round(_offset + Helpers.Round(tween._startTime + tween._updateDuration, 12), 12);
            }
            else
            {
                tween._absoluteStartTime *= timeScale;
                tween._absoluteUpdateStartTime *= timeScale;
                tween._absoluteEndTime *= timeScale;
            }
        }
        base.Stretch(newDuration);
        return this;
    }

    internal void ShiftOffset(double delta)
    {
        _offset += delta;
        for (var tween = _headTween; tween != null; tween = tween._next)
        {
            tween._absoluteStartTime = Helpers.Round(tween._absoluteStartTime + delta, 12);
            tween._absoluteUpdateStartTime = Helpers.Round(tween._absoluteUpdateStartTime + delta, 12);
            tween._absoluteEndTime = Helpers.Round(tween._absoluteEndTime + delta, 12);
        }
    }

    /// <summary>Re-evaluates the function based from and to values of every tween against the targets' current state.</summary>
    public Animation Refresh()
    {
        for (var tween = _headTween; tween != null; tween = tween._next)
        {
            var toFunc = tween._toFunc;
            var fromFunc = tween._fromFunc;
            if (toFunc == null && fromFunc == null) continue;

            var codec = tween.Accessor.Codec;
            ValueParser.Decompose(fromFunc != null ? fromFunc().Raw : tween.Accessor.GetValue(tween.Target), ScratchFrom, codec);
            if (toFunc != null) ValueParser.Decompose(toFunc().Raw, ScratchTo, codec);
            else ValueParser.DecomposeTween(tween, ScratchTo);
            // A unitless end keeps the tween's unit, and the from value is in it before a relative end applies (anime issue #1025).
            if (ScratchFrom.Kind is ValueKind.Number or ValueKind.Unit && (ScratchTo.Unit ?? tween._unit) is { } unit && ScratchFrom.Unit != unit) UnitConverter.Convert(ScratchFrom, unit);
            Reconcile(ScratchFrom, ScratchTo, codec);

            tween._fromNumbers = Helpers.CloneNumbers(ScratchFrom.Numbers);
            tween._fromNumber = ScratchFrom.Number;
            tween._fromObject = ScratchFrom.Discrete;
            tween._toNumbers = Helpers.CloneNumbers(ScratchTo.Numbers);
            tween._toNumber = ScratchTo.Number;
            tween._toObject = ScratchTo.Discrete;
            tween._strings = ScratchTo.Strings is { } strings ? (string[])strings.Clone() : [];
            tween._valueType = ScratchTo.Kind;
            tween._unit = ScratchTo.Unit;
            if (tween._numbers.Length != tween._toNumbers.Length) tween._numbers = Helpers.CloneNumbers(ScratchFrom.Numbers);
        }
        // Forces setter animations to render the refreshed values once.
        if (Duration == Constants.MinValue) Restart();
        return this;
    }

    /// <summary>Cancels the animation and writes every animated property back to the value it held before the animation ran.</summary>
    public override Animation Revert()
    {
        base.Revert();
        for (var tween = _headTween; tween != null; tween = tween._next)
        {
            if (tween._hasInlineValue) tween.Accessor.Revert(tween.Target, tween._inlineValue);
        }
        return this;
    }

    /// <inheritdoc cref="Timer.Then"/>
    public async Task<Animation> Then(Action<Animation>? callback = null)
    {
        await base.Then(callback == null ? null : _ => callback(this));
        return this;
    }

    /// <inheritdoc cref="Timer.ThenOrCancel"/>
    public new async Task<Animation> ThenOrCancel(CancellationToken cancellationToken = default)
    {
        await base.ThenOrCancel(cancellationToken);
        return this;
    }

    /// <inheritdoc cref="Timer.GetAwaiter"/>
    public new TaskAwaiter<Animation> GetAwaiter() => Then().GetAwaiter();

    // [x,y] -> one from/to frame; [x,y,z] -> [[x,y], z]
    private static TweenValue[] NormalizeKeyframes(TweenValue propertyValue, bool fastSet)
    {
        if (propertyValue.Raw is not TweenValue[] items)
        {
            return fastSet ? [new TweenParams { To = TweenValue.FromTo(propertyValue, propertyValue) }] : [propertyValue];
        }
        if (Array.Exists(items, static item => item.Raw is Inertia or Projectile)) throw new ArgumentException("An Inertia or Projectile cannot be a keyframe or part of a from/to pair.");
        if (items.Length < 2 || items[0].IsParams) return items;
        if (items.Length == 2) return [new TweenParams { To = propertyValue }];
        var frames = new TweenValue[items.Length - 1];
        frames[0] = TweenValue.FromTo(items[0], items[1]);
        for (var i = 2; i < items.Length; i++) frames[i - 1] = items[i];
        return frames;
    }

    private static TweenValue? ArrayItem(TweenValue value, int index)
    {
        var items = (TweenValue[])value.Raw!;
        return index < items.Length ? items[index] : (TweenValue?)null;
    }

    internal static TweenValue? GetFunctionValue(TweenValue? value, object target, int index, IReadOnlyList<object> targets, Tween? chained) =>
        ValueFunction(value, target, index, targets, chained) is { } function ? function() : value;

    private static TweenValue? GetFunctionValue(TweenValue? value, object target, int index, IReadOnlyList<object> targets, Tween? chained, ref Func<TweenValue>? store)
    {
        if (ValueFunction(value, target, index, targets, chained) is not { } function) return value;
        store = function;
        return function();
    }

    private static Func<TweenValue>? ValueFunction(TweenValue? value, object target, int index, IReadOnlyList<object> targets, Tween? chained) => value?.Raw switch
    {
        FunctionValue function => () => function(target, index, targets.Count).Coerce(),
        ChainedFunctionValue function => () => function(target, index, targets.Count, chained).Coerce(),
        Stagger stagger => () => stagger.Invoke(target, index, targets, stagger.StartOffset(null)).Coerce(),
        _ => null,
    };

    private static void Reconcile(DecomposedValue from, DecomposedValue to, IValueCodec? codec)
    {
        if (to.Operator != '\0') to.Number = ValueParser.GetRelativeValue(from.Number, to.Number, to.Operator);
        if (codec is DiscreteCodec) (from.Number, to.Number) = (0, 1);
        if (codec is IReconcilingValueCodec reconciling) reconciling.Reconcile(from, to);
        if (from.Kind != to.Kind) Homogenize(from, to);
        if (from.Unit != to.Unit) UnitConverter.Convert(to.Unit != null ? from : to, to.Unit ?? from.Unit!);
        if (to.Numbers != null && from.Numbers != null && to.Numbers.Length != from.Numbers.Length) PadShorterValue(from, to);
    }

    private static void Homogenize(DecomposedValue from, DecomposedValue to)
    {
        if (from.Kind == ValueKind.Complex || to.Kind == ValueKind.Complex)
        {
            var complex = from.Kind == ValueKind.Complex ? from : to;
            var notComplex = from.Kind == ValueKind.Complex ? to : from;
            var numbers = new double[complex.Numbers!.Length];
            Array.Fill(numbers, notComplex.Number);
            notComplex.Kind = ValueKind.Complex;
            notComplex.Strings = (string[])complex.Strings!.Clone();
            notComplex.Numbers = numbers;
        }
        else if (from.Kind == ValueKind.Unit || to.Kind == ValueKind.Unit)
        {
            var notUnit = from.Kind == ValueKind.Unit ? to : from;
            notUnit.Unit = (from.Kind == ValueKind.Unit ? from : to).Unit;
            notUnit.Kind = ValueKind.Unit;
        }
        else if (from.Kind == ValueKind.Color || to.Kind == ValueKind.Color)
        {
            var color = from.Kind == ValueKind.Color ? from : to;
            var notColor = from.Kind == ValueKind.Color ? to : from;
            notColor.Kind = ValueKind.Color;
            notColor.Numbers = new double[color.Numbers!.Length];
        }
    }

    private static void PadShorterValue(DecomposedValue from, DecomposedValue to)
    {
        var longest = from.Numbers!.Length > to.Numbers!.Length ? from : to;
        var shortest = longest == from ? to : from;
        var numbers = new double[longest.Numbers!.Length];
        Array.Copy(shortest.Numbers!, numbers, shortest.Numbers!.Length);
        shortest.Numbers = numbers;
        shortest.Strings = longest.Strings is { } strings ? (string[])strings.Clone() : null;
    }
}
