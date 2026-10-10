namespace MonacoLabs.Presto;

/// <summary><c>{presto:Scatter}</c>: <see cref="Recipes.Gather"/>, or <see cref="Recipes.Scatter"/> as an exit.</summary>
public sealed class ScatterExtension : RecipeExtension
{
    /// <summary>How far each element flies at most, in DIPs; four times <see cref="MotionTokens.Distance"/> when unset.</summary>
    public double? Distance { get; set; }

    /// <summary>How far each element spins at most, in degrees either way; 180 when unset.</summary>
    public double? Spin { get; set; }

    /// <summary>Makes the offsets the same on every play; drawn from the shared generator when unset.</summary>
    public int? Seed { get; set; }

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.Scatter(Distance, Spin, Seed, Duration, Delay, ease) : Recipes.Gather(Distance, Spin, Seed, Duration, Delay, ease);
}
