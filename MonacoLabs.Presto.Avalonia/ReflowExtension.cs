namespace MonacoLabs.Presto;

/// <summary><c>{presto:Reflow}</c>: <see cref="Recipes.Reflow"/>, which has no exit form. <c>{presto:Reflow 320}</c> sets the duration.</summary>
public sealed class ReflowExtension : RecipeExtension
{
    /// <summary>A reflow with the token duration.</summary>
    public ReflowExtension() { }

    /// <summary>A reflow lasting <paramref name="duration"/> ms.</summary>
    public ReflowExtension(double duration) => Duration = duration;

    /// <inheritdoc/>
    protected override bool HasExitForm => false;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.Reflow(Duration, Delay, ease);
}
