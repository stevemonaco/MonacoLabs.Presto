using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// The base of the text reveal markup extensions, such as <c>{presto:LineMask}</c>, which have no exit form. Set on a
/// <see cref="TextBlock"/>, each also sets the <see cref="TextReveal.PiecesProperty"/> and <see cref="TextReveal.MaskProperty"/>
/// its recipe needs unless they are already set; in a style <c>Setter</c> the block has to set <see cref="TextReveal.PiecesProperty"/> itself.
/// </summary>
public abstract class TextRevealExtension(TextPieces pieces, bool mask = false) : RecipeExtension
{
    /// <summary>The ms each line starts after the one before, or the recipe's default when unset.</summary>
    public double? LineStep { get; set; }

    /// <summary>The ms each piece starts after the one before it on its line, or the recipe's default when unset.</summary>
    public double? PieceStep { get; set; }

    /// <inheritdoc/>
    protected override void Configure(IProvideValueTarget? target)
    {
        if (target?.TargetObject is not TextBlock block) return;
        if (!block.IsSet(TextReveal.PiecesProperty)) TextReveal.SetPieces(block, pieces);
        if (mask && !block.IsSet(TextReveal.MaskProperty)) TextReveal.SetMask(block, true);
    }
}
