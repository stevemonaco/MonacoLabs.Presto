using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Shapes;
using Monaco.Presto.AvaloniaBridge.Text;
using Monaco.Presto.Diagnostics;
using Monaco.Presto.Values;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge;

public static partial class Recipes
{
    private const int ShardsPerGlyph = 8;

    private static readonly ConditionalWeakTable<TextBlock, ParticleEffect> ParticleTexts = new();

    // DIPs per ms: rightwards and a little up, as if blown.
    private static readonly Vector ParticleDrift = new(0.12, -0.06);

    /// <summary>
    /// Writes <paramref name="textBlock"/>'s text in: it is split into outline pieces with no fill whose contours are traced with
    /// <c>"Draw"</c> in reading order, each letter <see cref="MotionTokens.StaggerStep"/> after the one before starting at
    /// <paramref name="delay"/>, over <paramref name="duration"/> (default <see cref="MotionTokens.Long"/>); then the fill fades in
    /// and the stroke thins away over <see cref="MotionTokens.Medium"/>, and the block ends as plain text. Reduced, the block fades in.
    /// </summary>
    /// <remarks>The block has to be laid out. A character with no outline, such as an emoji, fades in with the fill.</remarks>
    public static Timeline WriteIn(TextBlock textBlock, double? duration = null, double? delay = null, EaseValue? ease = null)
    {
        if (Morphs.TryGetValue(textBlock, out var running)) running.Complete();
        var tokens = Tokens;
        using var origin = PrestoDiagnostics.Origin("Recipe");
        if (Engine.Current.Defaults.ReducedMotion)
        {
            var fade = Presto.CreateTimeline(new() { OnComplete = _ => Morphs.Remove(textBlock) })
                .Add(textBlock, b => b.Opacity(from: 0).Duration(tokens.Short), delay ?? 0);
            Morphs.AddOrUpdate(textBlock, fade);
            return fade;
        }

        var hold = textBlock.SetValue(Layoutable.WidthProperty, textBlock.Bounds.Width, BindingPriority.Animation);
        var split = PrestoAvalonia.SplitText(textBlock, new() { Words = null, Chars = new(), Outlines = true });
        var outlines = split.Chars.OfType<Path>().ToList();
        var others = split.Chars.Where(c => c is not Path).ToList();
        var foreground = textBlock.Foreground;
        foreach (var piece in outlines)
        {
            piece.Fill = Brushes.Transparent;
            piece.Stroke = foreground;
            piece.StrokeThickness = textBlock.FontSize / 32;
            // A letter's trace starts after its stagger delay, and until then it must not show its whole stroke.
            DrawAccessor.Apply(piece, 0, 0);
        }
        foreach (var piece in others) piece.Opacity = 0;

        var write = Presto.CreateTimeline(new()
        {
            OnComplete = _ =>
            {
                Morphs.Remove(textBlock);
                split.Revert();
                hold?.Dispose();
            },
        });
        if (outlines.Count > 0)
        {
            write.Add(outlines, b => b
                    .Property("Draw", TweenValue.FromTo("0 0", "0 1"))
                    .Duration(duration ?? tokens.Long).Ease(ease ?? tokens.Ease)
                    .Delay(Presto.Stagger(tokens.StaggerStep, new() { Start = delay })), 0)
                .Add(outlines, b => b.Property("Fill", TweenValue.Of(foreground)).Property("StrokeThickness", 0).Duration(tokens.Medium).Ease(tokens.Ease));
        }
        TimelinePosition withFill = outlines.Count > 0 ? "<<" : delay ?? 0;
        if (others.Count > 0) write.Add(others, b => b.Opacity(1).Duration(tokens.Medium).Ease(tokens.Ease), withFill);
        Morphs.AddOrUpdate(textBlock, write);
        return write;
    }

    /// <summary>
    /// Turns <paramref name="textBlock"/>'s text into <paramref name="text"/> by morphing each character's outline into the
    /// character at the same position of <paramref name="text"/>, counted from the end when both texts are numbers so a
    /// counter's units column stays put, over <see cref="MotionTokens.Long"/> with <see cref="MotionTokens.EmphasizedEase"/>.
    /// Characters with no partner grow from or collapse into the center of their slot, and a pair where either character has no
    /// outline crossfades. Reduced, the block crossfades as <see cref="MorphText"/> does.
    /// </summary>
    /// <remarks>The block has to be laid out. It is split into outline pieces while it morphs and ends as the plain new text.</remarks>
    public static Timeline MorphGlyphs(TextBlock textBlock, string text)
    {
        if (Morphs.TryGetValue(textBlock, out var running)) running.Complete();
        var tokens = Tokens;
        using var origin = PrestoDiagnostics.Origin("Recipe");
        if (Engine.Current.Defaults.ReducedMotion) return Crossfade(textBlock, text, tokens);

        var oldOrigin = OriginInWindow(textBlock);
        var numeric = IsNumber(textBlock.Text) && IsNumber(text);
        var before = PieceGhosts.Split(textBlock, TextPieces.Chars);
        var slots = before.Chars.Select(before.SlotOf).ToArray();
        var oldOutlines = before.Chars.Select(c => OutlineOf(textBlock, TextSplitter.GraphemeOf(c))).ToArray();
        var newOutlines = TextSplitter.CharsOf(text).Select(g => OutlineOf(textBlock, g)).ToArray();
        // The old character at index j + skip is the new one at j's partner.
        var skip = numeric ? slots.Length - newOutlines.Length : 0;
        bool Collapses(int i) => i - skip < 0 || i - skip >= newOutlines.Length;
        bool Pairs(int i) => !Collapses(i) && oldOutlines[i] != null && newOutlines[i - skip] != null;
        var (host, ghosts) = Ghosts(textBlock, before, Enumerable.Range(0, slots.Length).Where(i => !Pairs(i)));
        before.Revert();

        var (morph, after, shift) = Retext(textBlock, text, oldOrigin, outlines: true, host, ghosts);
        var morphing = new List<(Path, Geometry, Vector)>();
        var growing = new List<Control>();
        var fading = new List<Control>();
        for (var j = 0; j < after.Chars.Count; j++)
        {
            var piece = after.Chars[j];
            var i = j + skip;
            if (i < 0 || i >= slots.Length) growing.Add(piece);
            else if (Pairs(i)) morphing.Add(((Path)piece, oldOutlines[i]!, slots[i].Position + shift - after.SlotOf(piece).Position));
            else fading.Add(piece);
        }

        MorphOutlines(morph, morphing, tokens);
        var collapsing = ghosts.Where(g => Collapses(g.Key)).Select(g => g.Value).ToList();
        var leaving = ghosts.Where(g => !Collapses(g.Key)).Select(g => g.Value).ToList();
        if (growing.Count > 0) morph.Add(growing, b => b.Scale(from: 0).Duration(tokens.Long).Ease(tokens.EmphasizedEase), 0);
        if (collapsing.Count > 0) morph.Add(collapsing, b => b.Scale(0).Duration(tokens.Long).Ease(tokens.EmphasizedEase), 0);
        if (fading.Count > 0) morph.Add(fading, b => b.Opacity(from: 0).Duration(tokens.Long).Ease(tokens.Ease), 0);
        if (leaving.Count > 0) morph.Add(leaving, b => b.Opacity(0).Duration(tokens.Long).Ease(tokens.Ease), 0);
        return morph;
    }

    /// <summary>
    /// Shatters <paramref name="textBlock"/>: it is hidden and each character's outline, cut into about <paramref name="count"/>
    /// (default 8) <see cref="GlyphShards"/> cut with <paramref name="seed"/> (default 0) plus its index, flies apart from its slot
    /// in the window's overlay layer as <see cref="Scatter"/> flies elements, filled with the block's <c>Foreground</c>. Reversed
    /// mid-play, the shards re-form on their slots and the block comes back. Reduced, the block fades out.
    /// </summary>
    /// <remarks>
    /// The block has to be laid out. The shards leave the overlay when the timeline completes, in either direction, and the
    /// block is left at <c>Opacity</c> 0 as exits leave an element, or at its own opacity when reversed. A character with no
    /// outline, such as an emoji, scatters as a snapshot.
    /// </remarks>
    public static Timeline Shatter(TextBlock textBlock, int? count = null, int? seed = null, double? distance = null, double? spin = null)
    {
        if (Morphs.TryGetValue(textBlock, out var running)) running.Complete();
        var tokens = Tokens;
        using var origin = PrestoDiagnostics.Origin("Recipe");
        if (Engine.Current.Defaults.ReducedMotion || OverlayLayer.GetOverlayLayer(textBlock) is null)
        {
            var fade = Presto.CreateTimeline(new() { OnComplete = _ => Morphs.Remove(textBlock) })
                .Add(textBlock, b => b.Opacity(0).Duration(tokens.Short).Ease(tokens.Ease), 0);
            Morphs.AddOrUpdate(textBlock, fade);
            return fade;
        }

        var split = PieceGhosts.Split(textBlock, TextPieces.Chars);
        var outlines = split.Chars.Select(c => OutlineOf(textBlock, TextSplitter.GraphemeOf(c))).ToArray();
        var (host, ghosts) = Ghosts(textBlock, split, Enumerable.Range(0, outlines.Length).Where(i => outlines[i] == null));
        var pieces = new List<Control>(ghosts.Values);
        for (var i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] is { } outline) pieces.AddRange(Shards(host, outline, split.SlotOf(split.Chars[i]).Position, textBlock.Foreground, count ?? ShardsPerGlyph, (seed ?? 0) + i));
        }
        split.Revert();

        var shatter = Presto.CreateTimeline(new()
            {
                OnComplete = _ =>
                {
                    Morphs.Remove(textBlock);
                    Dismiss(host, ghosts);
                },
            })
            // A 1 ms tween, not Set: a reversed timeline that ends at 0 leaves a Set at 0 applied.
            .Add(textBlock, b => b.Opacity(0).Duration(1), 0)
            .Add(pieces, Scatter(distance, spin, seed), 0);
        Morphs.AddOrUpdate(textBlock, shatter);
        return shatter;
    }

    /// <summary>
    /// Disintegrates <paramref name="textBlock"/>: it is hidden and its letters become particles of its <c>Foreground</c> in
    /// the window's particle overlay, one per <see cref="GlyphSampling.Fill"/> point of each character's outline, which blow
    /// away left to right on a drift with turbulence and fade out over twice <see cref="MotionTokens.Long"/>. Reduced, the
    /// block fades out and nothing spawns.
    /// </summary>
    /// <remarks>The block has to be laid out, and is left at <c>Opacity</c> 0. A character with no outline, such as an emoji, spawns nothing.</remarks>
    public static ParticleEffect Disintegrate(TextBlock textBlock)
    {
        var tokens = Tokens;
        return TextParticles(textBlock, 0, (points, color, size) => (scene, finished) =>
        {
            var formation = scene.AddFormation(points, null, color, size, null, finished);
            formation.Disperse(ParticleDrift, 2 * tokens.Long);
            return formation;
        });
    }

    /// <summary>
    /// The reverse of <see cref="Disintegrate"/>: <paramref name="textBlock"/> is hidden, particles of its <c>Foreground</c> fade
    /// in scattered along the drift and spring onto the fill points of its letters, and once they rest the block fades in to
    /// <c>Opacity</c> 1 as they fade out. Reduced, the block fades in and nothing spawns.
    /// </summary>
    /// <remarks>The block has to be laid out. The effect completes once the particles have faded out.</remarks>
    public static ParticleEffect Assemble(TextBlock textBlock)
    {
        var tokens = Tokens;
        return TextParticles(textBlock, TweenValue.FromTo(0, 1), (points, color, size) =>
        {
            var random = Utils.CreateSeededRandom(0);
            var starts = points.ConvertAll(p => p + ParticleDrift * tokens.Long * (1 + random(0, 1, 9)) + new Vector(random(-1, 1, 9), random(-1, 1, 9)) * tokens.Distance);
            return (scene, finished) => scene.AddFormation(points, starts, color, size,
                () => Presto.Build().Opacity(1).Duration(ParticleFormation.FadeTime).Ease(tokens.Ease).Animate(textBlock), finished);
        });
    }

    private static ParticleEffect TextParticles(TextBlock block, TweenValue reduced, Func<List<Point>, Color, double, Func<ParticleScene, Action, ParticleFormation>> add)
    {
        if (ParticleTexts.TryGetValue(block, out var running)) running.Cancel();
        var tokens = Tokens;
        using var origin = PrestoDiagnostics.Origin("Recipe");
        if (Engine.Current.Defaults.ReducedMotion || ParticleOverlay.For(block) is not { } overlay)
        {
            Presto.Build().Property("Opacity", reduced).Duration(tokens.Short).Ease(tokens.Ease).Animate(block);
            return ParticleEffect.None;
        }

        var (points, color, size) = FillPoints(block, overlay.Top);
        var formation = add(points, color, size);
        Presto.Set(block, new() { ["Opacity"] = 0 });
        var effect = overlay.Play(formation);
        ParticleTexts.AddOrUpdate(block, effect);
        effect.Then().ContinueWith(_ =>
        {
            if (ParticleTexts.TryGetValue(block, out var held) && held == effect) ParticleTexts.Remove(block);
        }, TaskContinuationOptions.ExecuteSynchronously);
        return effect;
    }

    private static bool IsNumber(string? text) => double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out _);

    private static Point OriginInWindow(TextBlock block) =>
        TopLevel.GetTopLevel(block) is { } top ? block.TranslatePoint(default, top) ?? default : default;

    private static Geometry? OutlineOf(TextBlock block, string grapheme)
    {
        using var path = GlyphOutlines.Path(grapheme, block);
        return path.IsEmpty ? null : GlyphOutlines.Geometry(path);
    }

    // One particle per fill point, as wide as the spacing so the particles tile the letters.
    private static (List<Point> Points, Color Color, double Size) FillPoints(TextBlock block, TopLevel top)
    {
        var spacing = Math.Max(2, block.FontSize / 24);
        var at = block.TranslatePoint(default, top) ?? default;
        var split = PieceGhosts.Split(block, TextPieces.Chars);
        var points = new List<Point>();
        for (var i = 0; i < split.Chars.Count; i++)
        {
            if (OutlineOf(block, TextSplitter.GraphemeOf(split.Chars[i])) is not { } outline) continue;
            var slot = split.SlotOf(split.Chars[i]).Position;
            foreach (var point in GlyphPoints.Sample(outline, spacing, GlyphSampling.Fill, i)) points.Add(at + slot + point);
        }
        split.Revert();
        return (points, (block.Foreground as ISolidColorBrush)?.Color ?? Colors.Black, spacing);
    }

    private static Timeline Crossfade(TextBlock textBlock, string text, MotionTokens tokens)
    {
        var opacity = textBlock.Opacity;
        var fade = Presto.CreateTimeline(new() { OnComplete = _ => Morphs.Remove(textBlock) })
            .Add(textBlock, b => b.Opacity(to: 0).Duration(tokens.Short))
            .Call(_ => textBlock.Text = text)
            .Add(textBlock, b => b.Opacity(from: 0, to: opacity).Duration(tokens.Short));
        Morphs.AddOrUpdate(textBlock, fade);
        return fade;
    }

    private static (Canvas Host, Dictionary<int, Image> Ghosts) Ghosts(TextBlock block, TextSplitter split, IEnumerable<int> indices)
    {
        var host = new Canvas { IsHitTestVisible = false };
        var ghosts = new Dictionary<int, Image>();
        if (TopLevel.GetTopLevel(block) is not { } top || OverlayLayer.GetOverlayLayer(block) is not { } overlay || block.TranslatePoint(default, overlay) is not { } at) return (host, ghosts);
        foreach (var i in indices)
        {
            var ghost = ghosts[i] = Presence.Snapshot(split.Chars[i], top.RenderScaling);
            var slot = split.SlotOf(split.Chars[i]);
            Canvas.SetLeft(ghost, slot.X);
            Canvas.SetTop(ghost, slot.Y);
            host.Children.Add(ghost);
        }
        Canvas.SetLeft(host, at.X);
        Canvas.SetTop(host, at.Y);
        overlay.Children.Add(host);
        return (host, ghosts);
    }

    private static bool DropGhost(Canvas host, Dictionary<int, Image> ghosts, int index)
    {
        if (!ghosts.Remove(index, out var ghost)) return false;
        host.Children.Remove(ghost);
        ((Bitmap)ghost.Source!).Dispose();
        return true;
    }

    private static void Dismiss(Canvas host, Dictionary<int, Image> ghosts)
    {
        (host.Parent as Panel)?.Children.Remove(host);
        foreach (var ghost in ghosts.Values) ((Bitmap)ghost.Source!).Dispose();
    }

    // Each shard turns about its own center.
    private static List<Path> Shards(Canvas host, Geometry outline, Point at, IBrush? fill, int count, int seed)
    {
        var shards = new List<Path>();
        foreach (var shard in GlyphShards.Of(outline, count, seed))
        {
            var path = new Path { Data = shard, Fill = fill, RenderTransformOrigin = new RelativePoint(shard.Bounds.Center, RelativeUnit.Absolute) };
            Canvas.SetLeft(path, at.X);
            Canvas.SetTop(path, at.Y);
            host.Children.Add(path);
            shards.Add(path);
        }
        return shards;
    }

    // Completing the timeline unsplits the block and dismisses the ghosts.
    private static (Timeline Morph, TextSplitter After, Vector Shift) Retext(TextBlock textBlock, string text, Point oldOrigin, bool outlines, Canvas host, Dictionary<int, Image> ghosts)
    {
        textBlock.Text = text;
        textBlock.UpdateLayout();
        var shift = oldOrigin - OriginInWindow(textBlock);
        // A re-split detaches the pieces the morph plays on, which pauses it, so the width cannot change until it ends.
        var hold = textBlock.SetValue(Layoutable.WidthProperty, textBlock.Bounds.Width, BindingPriority.Animation);
        var after = PrestoAvalonia.SplitText(textBlock, new() { Words = null, Chars = new(), Outlines = outlines });
        var morph = Presto.CreateTimeline(new()
        {
            OnComplete = _ =>
            {
                Morphs.Remove(textBlock);
                after.Revert();
                hold?.Dispose();
                Dismiss(host, ghosts);
            },
        });
        Morphs.AddOrUpdate(textBlock, morph);
        return (morph, after, shift);
    }

    private static void MorphOutlines(Timeline morph, List<(Path Piece, Geometry From, Vector Offset)> pairs, MotionTokens tokens)
    {
        if (pairs.Count == 0) return;
        var shapes = new ChainedFunctionValue[pairs.Count];
        for (var i = 0; i < pairs.Count; i++)
        {
            shapes[i] = PrestoAvalonia.MorphTo(pairs[i].Piece.Data!);
            pairs[i].Piece.Data = pairs[i].From;
        }
        morph.Add(pairs.ConvertAll(p => p.Piece), b => b
            .X(from: (FunctionValue)((_, i, _) => pairs[i].Offset.X), to: 0)
            .Y(from: (FunctionValue)((_, i, _) => pairs[i].Offset.Y), to: 0)
            .Property("Data", (ChainedFunctionValue)((target, i, total, previous) => shapes[i](target, i, total, previous)))
            .Duration(tokens.Long).Ease(tokens.EmphasizedEase), 0);
    }
}
