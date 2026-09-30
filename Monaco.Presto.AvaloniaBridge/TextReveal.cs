using Avalonia;
using Avalonia.Controls;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Turns the builder a trigger plays on a <see cref="TextBlock"/> into a text reveal: <see cref="Presence"/>'s arrivals,
/// <see cref="Animate.OnAttachedProperty"/> and <see cref="Animate.OnTappedProperty"/> play it on the block's split pieces
/// instead of the block. <see cref="Motion"/> states always animate the block.
/// </summary>
/// <remarks>
/// The block is hidden from the trigger until it has been laid out and split, and a reveal that completes puts its text
/// back together; a looping one stays split. A builder's <see cref="AnimationBuilder.WhenReduced"/> form plays on the
/// block unsplit.
/// </remarks>
public static class TextReveal
{
    /// <summary>Which pieces the triggers animate; <see cref="TextPieces.None"/>, the default, animates the block.</summary>
    public static readonly AttachedProperty<TextPieces> PiecesProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, TextPieces>("Pieces", typeof(TextReveal));

    /// <summary>Whether each animated piece is wrapped in a clipping border, so it is masked outside its own box.</summary>
    public static readonly AttachedProperty<bool> MaskProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, bool>("Mask", typeof(TextReveal));

    /// <summary>Gets which pieces of <paramref name="block"/> the triggers animate.</summary>
    public static TextPieces GetPieces(TextBlock block) => block.GetValue(PiecesProperty);

    /// <summary>Sets which pieces of <paramref name="block"/> the triggers animate.</summary>
    public static void SetPieces(TextBlock block, TextPieces value) => block.SetValue(PiecesProperty, value);

    /// <summary>Gets whether the animated pieces of <paramref name="block"/> are masked.</summary>
    public static bool GetMask(TextBlock block) => block.GetValue(MaskProperty);

    /// <summary>Sets whether the animated pieces of <paramref name="block"/> are masked.</summary>
    public static void SetMask(TextBlock block, bool value) => block.SetValue(MaskProperty, value);
}
