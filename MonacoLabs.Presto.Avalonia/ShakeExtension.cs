namespace MonacoLabs.Presto;

/// <summary><c>{presto:Shake}</c>: <see cref="Recipes.Shake"/>, which has no exit form.</summary>
public sealed class ShakeExtension : RecipeExtension
{
    /// <summary>The factor the travel is multiplied by.</summary>
    public double Intensity { get; set; } = 1;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.Shake(Intensity, Duration, Delay, ease);
}
