namespace MonacoLabs.Presto;

/// <summary><c>{presto:Popover}</c>: <see cref="Recipes.PopoverIn"/>, or <see cref="Recipes.PopoverOut"/> as an exit, such as on <see cref="Popover.CloseProperty"/>.</summary>
public sealed class PopoverExtension : RecipeExtension
{
    /// <summary>How far it slides from its anchor in DIPs, or half of <see cref="MotionTokens.Distance"/> when unset.</summary>
    public double? Distance { get; set; }

    /// <summary>The scale it grows from, or shrinks to as an exit; <see cref="MotionTokens.Scale"/> when unset.</summary>
    public double? Scale { get; set; }

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.PopoverOut(Distance, Scale, Duration, Delay, ease) : Recipes.PopoverIn(Distance, Scale, Duration, Delay, ease);
}
