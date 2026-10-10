namespace MonacoLabs.Presto;

/// <summary>
/// A composed numeric transform, built by the <see cref="Utils"/> overloads that omit the value
/// (<c>Utils.MapRange(0, 100, 0, 1000).Clamp(0, 100)</c>). Stands in for anime.js's <c>Proxy</c> based chainable utilities.
/// </summary>
public readonly struct ChainedUtil
{
    private readonly Func<double, double> _fn;

    internal ChainedUtil(Func<double, double> fn) => _fn = fn;

    /// <summary>Runs <paramref name="v"/> through the chain.</summary>
    public double Invoke(double v) => _fn(v);

    /// <inheritdoc cref="Utils.Clamp(double, double, double)"/>
    public ChainedUtil Clamp(double min, double max) => Then(v => Utils.Clamp(v, min, max));

    /// <inheritdoc cref="Utils.Round(double, int)"/>
    public ChainedUtil Round(int decimals) => Then(v => Utils.Round(v, decimals));

    /// <inheritdoc cref="Utils.Snap(double, double)"/>
    public ChainedUtil Snap(double increment) => Then(v => Utils.Snap(v, increment));

    /// <inheritdoc cref="Utils.Snap(double, double[])"/>
    public ChainedUtil Snap(double[] points) => Then(v => Utils.Snap(v, points));

    /// <inheritdoc cref="Utils.Wrap(double, double, double)"/>
    public ChainedUtil Wrap(double min, double max) => Then(v => Utils.Wrap(v, min, max));

    /// <inheritdoc cref="Utils.MapRange(double, double, double, double, double)"/>
    public ChainedUtil MapRange(double inLow, double inHigh, double outLow, double outHigh) =>
        Then(v => Utils.MapRange(v, inLow, inHigh, outLow, outHigh));

    /// <inheritdoc cref="Utils.DegToRad(double)"/>
    public ChainedUtil DegToRad() => Then(Utils.DegToRad);

    /// <inheritdoc cref="Utils.RadToDeg(double)"/>
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

    /// <summary>Passes the chain wherever a modifier delegate is expected.</summary>
    public static implicit operator Func<double, double>(ChainedUtil chain) => chain.Invoke;

    private ChainedUtil Then(Func<double, double> next)
    {
        var fn = _fn;
        return new(v => next(fn(v)));
    }
}
