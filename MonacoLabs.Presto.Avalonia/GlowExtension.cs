namespace MonacoLabs.Presto;

/// <summary><c>{presto:Glow}</c>: <see cref="Recipes.Glow"/>, which has no exit form.</summary>
public sealed class GlowExtension : RecipeExtension
{
    /// <summary>The factor the change is multiplied by.</summary>
    public double Intensity { get; set; } = 1;

    /// <inheritdoc/>
    protected override bool HasExitForm => false;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.Glow(null, Intensity, Duration, Delay, ease);
}
