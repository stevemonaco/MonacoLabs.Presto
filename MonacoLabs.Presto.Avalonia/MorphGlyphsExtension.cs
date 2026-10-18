using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MonacoLabs.Presto;

/// <summary>
/// <c>{presto:MorphGlyphs}</c>: a <see cref="CueAction"/> that shows each value's text on its <see cref="TextBlock"/> through
/// <see cref="Recipes.MorphGlyphs"/>, setting it at once for a first value and while the block is not loaded.
/// </summary>
public sealed class MorphGlyphsExtension : MarkupExtension
{
    /// <summary>What the morph does under reduced motion.</summary>
    public ReducedMorph Reduced { get; set; } = ReducedMorph.Crossfade;

    /// <summary>Whether a change to or from empty text morphs; when false it is set at once.</summary>
    public bool MorphBlank { get; set; } = true;

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => new MorphGlyphsAction(Reduced, MorphBlank);

    private sealed class MorphGlyphsAction(ReducedMorph reduced, bool morphBlank) : CueAction
    {
        protected internal override void Invoke(Visual target, object? value, bool first)
        {
            var block = Require<TextBlock>(target, "{presto:MorphGlyphs}");
            var text = value?.ToString() ?? "";
            // A running morph lays the block out as Inlines, leaving Text null until it completes.
            Recipes.CompleteTextMorph(block);
            if (first || !block.IsLoaded || (!morphBlank && (text.Length == 0 || string.IsNullOrEmpty(block.Text))))
            {
                block.SetCurrentValue(TextBlock.TextProperty, text);
            }
            else
            {
                Recipes.MorphGlyphs(block, text, reduced);
            }
        }
    }
}
