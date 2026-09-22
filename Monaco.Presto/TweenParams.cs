namespace Monaco.Presto;

/// <summary>Per-property tween values and settings (upstream <c>TweenKeyValue</c>): <c>["Rotate"] = new TweenParams { From = -180 }</c>.</summary>
public sealed class TweenParams
{
    public TweenValue? From { get; init; }
    public TweenValue? To { get; init; }
    public TweenValue? Duration { get; init; }
    public TweenValue? Delay { get; init; }
    public EaseValue? Ease { get; init; }
    public Func<double, double>? Modifier { get; init; }
    public Composition? Composition { get; init; }
}
