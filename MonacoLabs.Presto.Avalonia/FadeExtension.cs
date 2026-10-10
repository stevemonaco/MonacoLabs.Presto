namespace MonacoLabs.Presto;

/// <summary><c>{presto:Fade}</c>: <see cref="Recipes.Fade"/> and its directional forms, or <see cref="Recipes.FadeOut"/> and its forms as an exit.</summary>
public sealed class FadeExtension : RecipeExtension
{
    /// <summary>Which way the element moves as it fades.</summary>
    public FadeDirection Direction { get; set; }

    /// <summary>How far it moves in DIPs, or <see cref="MotionTokens.Distance"/> when unset.</summary>
    public double? Distance { get; set; }

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => (Direction, exit) switch
    {
        (FadeDirection.Up, false) => Recipes.FadeUp(Distance, Duration, Delay, ease),
        (FadeDirection.Down, false) => Recipes.FadeDown(Distance, Duration, Delay, ease),
        (FadeDirection.Left, false) => Recipes.FadeLeft(Distance, Duration, Delay, ease),
        (FadeDirection.Right, false) => Recipes.FadeRight(Distance, Duration, Delay, ease),
        (FadeDirection.Up, true) => Recipes.FadeOutUp(Distance, Duration, Delay, ease),
        (FadeDirection.Down, true) => Recipes.FadeOutDown(Distance, Duration, Delay, ease),
        (FadeDirection.Left, true) => Recipes.FadeOutLeft(Distance, Duration, Delay, ease),
        (FadeDirection.Right, true) => Recipes.FadeOutRight(Distance, Duration, Delay, ease),
        (_, true) => Recipes.FadeOut(Duration, Delay, ease),
        _ => Recipes.Fade(Duration, Delay, ease),
    };
}
