namespace MonacoLabs.Presto;

/// <summary>
/// A motion state: while <see cref="When"/> holds, the properties <see cref="Motion"/> names go to its values, and when it
/// stops holding they return, timed by <see cref="Leave"/> if given. Immutable, so one instance can serve many controls.
/// </summary>
public sealed class MotionState
{
    /// <summary>
    /// ANDed tokens on the element itself: <c>:pseudo-class</c>, <c>.class</c>, or a leading bare name matched against
    /// <c>Motion.State</c>, as in <c>.selected:pointerover</c> or <c>Error</c>.
    /// </summary>
    public string When { get; init; } = "";

    /// <summary>Where properties go, and how, while <see cref="When"/> holds. Values must be absolute.</summary>
    public AnimationBuilder? Motion { get; init; }

    /// <summary>Timing for the return when <see cref="When"/> stops holding: duration, delay and eases only, no values.</summary>
    public AnimationBuilder? Leave { get; init; }

    /// <exception cref="NotSupportedException"><see cref="Motion"/> has a relative or from-only value, or <see cref="Leave"/> has any value.</exception>
    internal void Validate()
    {
        if (Motion != null) ValidateMotion(Motion.ToParams());
        if (Leave != null) ValidateLeave(Leave.ToParams());
    }

    private void ValidateMotion(AnimationParams p)
    {
        foreach (var (name, value) in p.Properties)
        {
            if (value.Raw is TweenParams { From: not null, To: null })
                throw new NotSupportedException($"A state is a target, so {name} in \"{When}\" needs a To; a From alone has nowhere to go.");
            RejectRelative(name, value);
        }
        if (p.Keyframes?.DurationBased is { } frames)
            foreach (var frame in frames) RejectRelative(frame.Properties);
        if (p.Keyframes?.PercentageBased is { } offsets)
            foreach (var frame in offsets.Values) RejectRelative(frame.Properties);
    }

    private void RejectRelative(OrderedDictionary<string, TweenValue> properties)
    {
        foreach (var (name, value) in properties) RejectRelative(name, value);
    }

    private void RejectRelative(string name, TweenValue value)
    {
        switch (value.Raw)
        {
            case string { Length: > 1 } text when text[1] == '=' && text[0] is '+' or '-' or '*':
                throw new NotSupportedException($"A state re-enters, so a relative {name} of \"{text}\" in \"{When}\" would drift each time; give an absolute value.");
            case TweenParams tween:
                if (tween.From is { } from) RejectRelative(name, from);
                if (tween.To is { } to) RejectRelative(name, to);
                break;
            case TweenValue[] items:
                foreach (var item in items) RejectRelative(name, item);
                break;
        }
    }

    private void ValidateLeave(AnimationParams p)
    {
        if (p.Keyframes != null)
            throw new NotSupportedException($"The Leave of \"{When}\" only times the return to rest, so it cannot have keyframes.");
        foreach (var (name, value) in p.Properties)
        {
            if (value.Raw is not TweenParams tween || tween.To != null || tween.From != null || tween.Delay != null
                || tween.Composition != null || tween.Modifier != null || tween.TextModifier != null)
                throw new NotSupportedException($"The Leave of \"{When}\" only times the return to rest, so give {name} only an ease or duration: .{name}(ease: …).");
        }
    }
}
