using Avalonia.Controls;

namespace MonacoLabs.Presto.Text;

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
                var offset = split.SlotOf(piece).Position;
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

    /// <summary>Splits <paramref name="block"/> into lines and <paramref name="pieces"/> outside the current scope, for a caller that reverts it at once.</summary>
    internal static TextSplitter Split(TextBlock block, TextPieces pieces)
    {
        // Reverted by the caller, so neither it nor its resize timer belongs to the scope the caller runs in.
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
        return split;
    }
}
