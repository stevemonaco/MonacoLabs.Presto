using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Text;

/// <summary>
/// The ghosts a leaving <see cref="TextBlock"/> with <see cref="TextReveal.PiecesProperty"/> exits as: one snapshot per
/// split piece, tagged with its line so <see cref="TextSplitter.StaggerByLine"/> and <see cref="TextSplitter.LineHeightOf"/> work on it.
/// </summary>
internal static class PieceGhosts
{
    /// <summary>The most ghosts one block leaves as; more pieces coarsen chars to words to lines, then to one whole ghost.</summary>
    internal static int Max = 256;

    /// <summary>
    /// Snapshots the pieces of <paramref name="block"/>, split for the call unless it is still split, into a canvas at
    /// the block's origin; null when every level has more pieces than <see cref="Max"/>.
    /// </summary>
    /// <returns>The canvas and the ghosts in reading order, which the exit animates.</returns>
    public static (Canvas Frame, Image[] Ghosts)? Snapshot(TextBlock block, TextPieces pieces, double scaling)
    {
        var split = TextSplitter.Of(block);
        var fresh = split is null;
        split ??= Split(block, pieces);
        try
        {
            IReadOnlyList<Control>[] levels = [split.Chars, split.Words, split.Lines];
            var level = Array.FindIndex(levels, (int)TextPieces.Chars - (int)pieces, list => list.Count > 0 && list.Count <= Max);
            if (level < 0) return null;

            var mask = TextReveal.GetMask(block);
            var frame = new Canvas();
            var ghosts = new Image[levels[level].Count];
            for (var i = 0; i < ghosts.Length; i++)
            {
                var piece = levels[level][i];
                var ghost = ghosts[i] = Presence.Snapshot(piece, scaling);
                ghost.SetValue(TextSplitter.LineIndexProperty, TextSplitter.GetLineIndex(piece));
                ghost.SetValue(TextSplitter.IndexInLineProperty, piece.GetValue(TextSplitter.IndexInLineProperty));
                ghost.SetValue(TextSplitter.LineHeightProperty, split.LineHeight);
                Control placed = mask ? new Canvas { Width = ghost.Width, Height = ghost.Height, ClipToBounds = true, Children = { ghost } } : ghost;
                var offset = OffsetIn(piece, block);
                Canvas.SetLeft(placed, offset.X);
                Canvas.SetTop(placed, offset.Y);
                frame.Children.Add(placed);
            }
            return (frame, ghosts);
        }
        finally
        {
            if (fresh) split.Revert();
        }
    }

    private static TextSplitter Split(TextBlock block, TextPieces pieces)
    {
        // Reverted before this returns, so neither it nor its resize timer belongs to the scope the exit started in.
        var scope = Engine.Current.CurrentScope;
        Engine.Current.CurrentScope = null;
        TextSplitter split;
        try
        {
            split = new TextSplitter(block, new SplitTextParams
            {
                Lines = new(),
                Words = pieces == TextPieces.Lines ? null : new(),
                Chars = pieces == TextPieces.Chars ? new() : null,
            });
        }
        finally
        {
            Engine.Current.CurrentScope = scope;
        }
        // A detached block keeps the bounds it was last arranged with, which a layout pass would no longer give it.
        var slot = block.Bounds.Inflate(block.Margin);
        block.Measure(slot.Size);
        block.Arrange(slot);
        return split;
    }

    // Layout offsets, since a live piece's render transform moves onto its ghost.
    private static Point OffsetIn(Visual piece, Visual block)
    {
        var offset = default(Point);
        for (var visual = piece; visual != block; visual = visual.GetVisualParent()!) offset += visual.Bounds.Position;
        return offset;
    }
}
