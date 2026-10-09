using Avalonia;
using Avalonia.Controls;
using Monaco.Presto.AvaloniaBridge.Text;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Picks the warp the <c>"Warp"</c> shorthand applies to a <see cref="TextBlock"/> split with
/// <see cref="SplitTextParams.Outlines"/>: <c>TextWarp.SetKind(Hero, TextWarpKind.Bulge)</c>, then animate <c>"Warp"</c>
/// (the strength, 0 = none) with its companions <c>"WarpPhase"</c>, <c>"WarpOrigin"</c> and <c>"WarpRadius"</c>.
/// </summary>
public static class TextWarp
{
    /// <summary>The warp the block's outline pieces follow; <see cref="TextWarpKind.Wave"/> when not set.</summary>
    public static readonly AttachedProperty<TextWarpKind> KindProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, TextWarpKind>("Kind", typeof(TextWarp));

    static TextWarp() => KindProperty.Changed.AddClassHandler<TextBlock>((block, _) => WarpComposer.KindChanged(block));

    /// <summary>Gets the warp <paramref name="block"/> follows.</summary>
    public static TextWarpKind GetKind(TextBlock block) => block.GetValue(KindProperty);

    /// <summary>Sets the warp <paramref name="block"/> follows.</summary>
    public static void SetKind(TextBlock block, TextWarpKind value) => block.SetValue(KindProperty, value);
}
