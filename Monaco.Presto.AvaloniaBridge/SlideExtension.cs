namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:Slide}</c>: <see cref="Recipes.SlideIn"/>, or <see cref="Recipes.SlideOut"/> as an exit.</summary>
public sealed class SlideExtension : RecipeExtension
{
    /// <summary>The side it slides in from, or out towards as an exit.</summary>
    public Edge Edge { get; set; }

    /// <summary>How far it travels in DIPs, or four times <see cref="MotionTokens.Distance"/> when unset.</summary>
    public double? Distance { get; set; }

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.SlideOut(Edge, Distance, Duration, Delay, ease) : Recipes.SlideIn(Edge, Distance, Duration, Delay, ease);
}
