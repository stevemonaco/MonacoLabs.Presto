namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:Blur}</c>: <see cref="Recipes.BlurIn"/>, or <see cref="Recipes.BlurOut"/> as an exit.</summary>
public sealed class BlurExtension : RecipeExtension
{
    /// <summary>The blur radius it sharpens from, or blurs to as an exit; <see cref="MotionTokens.BlurRadius"/> when unset.</summary>
    public double? Radius { get; set; }

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.BlurOut(Radius, Duration, Delay, ease) : Recipes.BlurIn(Radius, Duration, Delay, ease);
}
