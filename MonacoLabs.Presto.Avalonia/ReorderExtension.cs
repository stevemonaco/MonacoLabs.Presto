namespace MonacoLabs.Presto;

/// <summary><c>{presto:Reorder}</c>: <see cref="Recipes.ReorderLift"/>, which has no exit form.</summary>
public sealed class ReorderExtension : RecipeExtension
{
    /// <summary>The factor the change is multiplied by.</summary>
    public double Intensity { get; set; } = 1;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.ReorderLift(null, Intensity, Duration, Delay, ease);
}
