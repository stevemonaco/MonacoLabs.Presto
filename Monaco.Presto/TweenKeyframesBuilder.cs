namespace Monaco.Presto;

/// <summary>Adds one property's keyframes, in order, for <see cref="PropertyBuilder{TSelf}.Property(string, Action{TweenKeyframesBuilder})"/>.</summary>
public sealed class TweenKeyframesBuilder
{
    private readonly List<TweenValue> _frames = [];

    internal TweenKeyframesBuilder() { }

    /// <summary>Adds a frame ending on <paramref name="to"/>; a frame given only <c>to</c> is a bare value, otherwise a <see cref="TweenParams"/>.</summary>
    public TweenKeyframesBuilder To(TweenValue to, TweenValue? duration = null, TweenValue? delay = null, EaseValue? ease = null)
    {
        _frames.Add(duration == null && delay == null && ease == null
            ? to
            : new TweenParams { To = to, Duration = duration, Delay = delay, Ease = ease });
        return this;
    }

    internal TweenValue[] ToArray() => _frames.ToArray();
}
