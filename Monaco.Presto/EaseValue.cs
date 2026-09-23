namespace Monaco.Presto;

/// <summary>
/// An ease parameter: an <see cref="Ease"/>, a <see cref="Spring"/>, an <see cref="EaseParser"/> name such as
/// <c>"inOutQuad"</c>, or a <see cref="FunctionValue"/> returning one of those per target.
/// </summary>
public readonly struct EaseValue
{
    internal readonly object? Raw;

    private EaseValue(object raw) => Raw = raw;

    /// <summary>An easing function, either one of the <see cref="Eases"/> or any <c>double -> double</c> of your own.</summary>
    public static implicit operator EaseValue(Ease ease) => new(ease);

    /// <summary>A spring, whose computed <see cref="Spring.SettlingDuration"/> also becomes the tween's duration.</summary>
    public static implicit operator EaseValue(Spring spring) => new(spring);

    /// <summary>An ease name in the <see cref="EaseParser"/> grammar, such as <c>"inOutQuad"</c> or <c>"out(2)"</c>.</summary>
    public static implicit operator EaseValue(string name) => new(name);

    /// <summary>A delegate evaluated once per target, for animations whose targets ease differently.</summary>
    public static implicit operator EaseValue(FunctionValue function) => new(function);

    /// <summary>Resolves the ease for one target; <paramref name="spring"/> is set when it is a <see cref="Spring"/>, whose settling duration then drives the tween.</summary>
    internal Ease Resolve(object target, int index, int total, out Spring? spring)
    {
        var raw = Raw is FunctionValue function ? function(target, index, total).Raw : Raw;
        spring = raw as Spring;
        return raw switch
        {
            Spring s => s.Ease,
            Ease ease => ease,
            string name => EaseParser.Parse(name),
            _ => Eases.None,
        };
    }
}
