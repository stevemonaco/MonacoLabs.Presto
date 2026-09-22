namespace Monaco.Presto;

/// <summary>
/// A composed numeric transform, built by the <see cref="Utils"/> overloads that omit the value
/// (<c>Utils.MapRange(0, 100, 0, 1000).Clamp(0, 100)</c>). Stands in for anime.js's <c>Proxy</c> based chainable utilities.
/// </summary>
public readonly struct ChainedUtil
{
    private readonly Func<double, double> _fn;

    internal ChainedUtil(Func<double, double> fn) => _fn = fn;

    public double Invoke(double v) => _fn(v);

    public ChainedUtil Clamp(double min, double max) => Then(v => Utils.Clamp(v, min, max));

    public ChainedUtil Round(int decimals) => Then(v => Utils.Round(v, decimals));

    public ChainedUtil Snap(double increment) => Then(v => Utils.Snap(v, increment));

    public ChainedUtil Snap(double[] points) => Then(v => Utils.Snap(v, points));

    public ChainedUtil Wrap(double min, double max) => Then(v => Utils.Wrap(v, min, max));

    public ChainedUtil MapRange(double inLow, double inHigh, double outLow, double outHigh) =>
        Then(v => Utils.MapRange(v, inLow, inHigh, outLow, outHigh));

    public ChainedUtil DegToRad() => Then(Utils.DegToRad);

    public ChainedUtil RadToDeg() => Then(Utils.RadToDeg);

    /// <summary>Chains a lerp whose factor is the chained value, matching upstream's right-curried <c>lerp</c>.</summary>
    public ChainedUtil Lerp(double start, double end) => Then(v => Utils.Lerp(start, end, v));

    /// <summary>Chains a damp whose factor is the chained value, matching upstream's right-curried <c>damp</c>.</summary>
    public ChainedUtil Damp(double start, double end, double deltaTime) => Then(v => Utils.Damp(start, end, deltaTime, v));

    /// <summary>Ends the chain: <see cref="Utils.RoundPad(double, int)"/> produces a string, which nothing further can consume.</summary>
    public Func<double, string> RoundPad(int decimals)
    {
        var fn = _fn;
        return v => Utils.RoundPad(fn(v), decimals);
    }

    public static implicit operator Func<double, double>(ChainedUtil chain) => chain.Invoke;

    private ChainedUtil Then(Func<double, double> next)
    {
        var fn = _fn;
        return new(v => next(fn(v)));
    }
}
