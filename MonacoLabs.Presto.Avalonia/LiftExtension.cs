namespace MonacoLabs.Presto;

/// <summary><c>{presto:Lift}</c>: <see cref="Recipes.Lift"/>, which has no exit form.</summary>
public sealed class LiftExtension : RecipeExtension
{
    /// <summary>The factor the change is multiplied by.</summary>
    public double Intensity { get; set; } = 1;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.Lift(null, Intensity, Duration, Delay, ease);
}
