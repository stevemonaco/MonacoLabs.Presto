using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace MonacoLabs.Presto.Text;

/// <summary>
/// The characters of a laid-out <see cref="TextBlock"/> as its own text layout draws them, counted as
/// <see cref="TextSplitter.CharsOf"/> counts them, and hiding some of them in place.
/// </summary>
internal static class BlockGlyphs
{
    /// <summary>Where the character at <paramref name="c"/> is laid out, in block coordinates.</summary>
    internal static Rect SlotOf(TextBlock block, (int Start, int Length) c)
    {
        Rect? slot = null;
        foreach (var rect in block.TextLayout.HitTestTextRange(c.Start, c.Length)) slot = slot?.Union(rect) ?? rect;
        return (slot ?? default).Translate(new Vector(block.Padding.Left, block.Padding.Top));
    }

    /// <summary>The glyphs the block's layout shaped for the character at <paramref name="c"/>, relative to its <paramref name="slot"/>; null when it has none.</summary>
    internal static Geometry? OutlineOf(TextBlock block, (int Start, int Length) c, Rect slot)
    {
        using var path = GlyphOutlines.Path(block.TextLayout, c.Start, c.Length);
        if (path.IsEmpty) return null;
        path.Offset((float)(block.Padding.Left - slot.X), (float)(block.Padding.Top - slot.Y));
        return GlyphOutlines.Geometry(path);
    }

    /// <summary>
    /// Shows <paramref name="text"/> with the characters at <paramref name="hidden"/> transparent. A run boundary that only
    /// changes the brush keeps the block's shaping, so its layout is that of the plain text.
    /// </summary>
    internal static void Hide(TextBlock block, string text, IReadOnlySet<int> hidden)
    {
        var inlines = new InlineCollection();
        var plain = 0;
        var index = 0;
        foreach (var (start, length) in TextSplitter.CharRangesOf(text))
        {
            if (hidden.Contains(index++))
            {
                if (start > plain) inlines.Add(new Run(text[plain..start]));
                inlines.Add(new Run(text.Substring(start, length)) { Foreground = Brushes.Transparent });
                plain = start + length;
            }
        }
        if (plain < text.Length) inlines.Add(new Run(text[plain..]));
        block.SetCurrentValue(TextBlock.TextProperty, null);
        block.Inlines = inlines;
    }

    /// <summary>Leaves the block as plain <paramref name="text"/>, any binding on its <c>Text</c> intact.</summary>
    internal static void Restore(TextBlock block, string text)
    {
        block.Inlines = null;
        block.SetCurrentValue(TextBlock.TextProperty, text);
    }
}
