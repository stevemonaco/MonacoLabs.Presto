namespace Monaco.Presto;

/// <summary>Adds one property's keyframes, in order, for <see cref="PropertyBuilder{TSelf}.Property(string, Action{TweenKeyframesBuilder})"/>.</summary>
public sealed class TweenKeyframesBuilder
{
    private readonly List<TweenValue> _frames = [];

    internal TweenKeyframesBuilder() { }

    /// <summary>Adds a frame ending on <paramref name="to"/>; the first frame starts from the property's current value.</summary>
    /// <remarks>
    /// Every frame is a <see cref="TweenParams"/>, never a bare value: a bare array reads as <c>[from, to]</c>, which would
    /// turn the first frame into a start value.
    /// </remarks>
    public TweenKeyframesBuilder To(TweenValue to, TweenValue? duration = null, TweenValue? delay = null, EaseValue? ease = null)
    {
        _frames.Add(new TweenParams { To = to, Duration = duration, Delay = delay, Ease = ease });
        return this;
    }

    internal TweenValue[] ToArray() => _frames.ToArray();
}
