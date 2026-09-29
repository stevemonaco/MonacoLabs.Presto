namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:Pop}</c>: <see cref="Recipes.PopIn"/>, or <see cref="Recipes.PopOut"/> as an exit.</summary>
public sealed class PopExtension : RecipeExtension
{
    /// <summary>The scale it pops from, or shrinks to as an exit; <see cref="MotionTokens.Scale"/> when unset.</summary>
    public double? Scale { get; set; }

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.PopOut(Scale, Duration, Delay, ease) : Recipes.PopIn(Scale, Duration, Delay, ease);
}
