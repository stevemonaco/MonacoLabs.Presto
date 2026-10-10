namespace MonacoLabs.Presto;

/// <summary><c>{presto:PressIn}</c>: <see cref="Recipes.PressIn"/>, which has no exit form.</summary>
public sealed class PressInExtension : RecipeExtension
{
    /// <summary>The factor the change is multiplied by.</summary>
    public double Intensity { get; set; } = 1;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.PressIn(Intensity, Duration, Delay, ease);
}
