namespace Monaco.Presto;

/// <summary>
/// The property half of the fluent builders. A <c>Property</c> call given only <c>to</c> writes that bare value, as
/// <c>["X"] = 100</c> does, and any other argument writes a <see cref="TweenParams"/>; naming a property again replaces
/// its value but keeps its original position.
/// </summary>
public abstract class PropertyBuilder<TSelf> where TSelf : PropertyBuilder<TSelf>
{
    private protected PropertyBuilder() { }

    internal OrderedDictionary<string, TweenValue> Properties { get; } = new();

    /// <summary>Animates <paramref name="name"/> to <paramref name="to"/>.</summary>
    public TSelf Property(string name, TweenValue to)
    {
        Properties[name] = to;
        return (TSelf)this;
    }

    /// <summary>Animates <paramref name="name"/> with its own tween settings, overriding the animation's for this property.</summary>
    public TSelf Property(string name, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null, Func<double, string>? textModifier = null)
    {
        if (to is { } bare && from == null && duration == null && delay == null && ease == null && composition == null
            && modifier == null && textModifier == null)
        {
            return Property(name, bare);
        }
        return Property(name, new TweenParams
        {
            To = to, From = from, Duration = duration, Delay = delay, Ease = ease,
            Composition = composition, Modifier = modifier, TextModifier = textModifier,
        });
    }

    /// <summary>Animates <paramref name="name"/> to a value computed per target.</summary>
    public TSelf Property(string name, FunctionValue to) => Property(name, (TweenValue)to);

    /// <summary>Animates <paramref name="name"/> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public TSelf Property(string name, Action<TweenKeyframesBuilder> keyframes)
    {
        var builder = new TweenKeyframesBuilder();
        keyframes(builder);
        return Property(name, TweenValue.Keyframes(builder.ToArray()));
    }

    private protected TSelf CopyPropertiesTo(TSelf copy)
    {
        foreach (var (name, value) in Properties) copy.Properties[name] = value;
        return copy;
    }
}
