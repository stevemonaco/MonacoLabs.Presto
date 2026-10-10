namespace MonacoLabs.Presto;

/// <summary><c>{presto:Scale}</c>: <see cref="Recipes.ScaleIn"/>, or <see cref="Recipes.ScaleOut"/> as an exit.</summary>
public sealed class ScaleExtension : RecipeExtension
{
    /// <summary>The scale it grows from, or shrinks to as an exit; <see cref="MotionTokens.Scale"/> when unset.</summary>
    public double? From { get; set; }

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.ScaleOut(From, Duration, Delay, ease) : Recipes.ScaleIn(From, Duration, Delay, ease);
}
