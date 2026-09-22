namespace Monaco.Presto;

/// <summary>How a new tween interacts with running tweens on the same target property (upstream <c>compositionTypes</c>).</summary>
public enum Composition
{
    Replace = 0,
    None = 1,
    Blend = 2,
}
