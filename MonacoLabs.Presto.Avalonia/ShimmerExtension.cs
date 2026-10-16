namespace MonacoLabs.Presto;

/// <summary><c>{presto:Shimmer}</c>: <see cref="Recipes.Shimmer"/>, which has no exit form.</summary>
public sealed class ShimmerExtension : RecipeExtension
{
    /// <inheritdoc/>
    protected override bool HasExitForm => false;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.Shimmer(Duration, Delay, ease);
}
