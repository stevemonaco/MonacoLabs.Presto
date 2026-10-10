using Avalonia;
using Avalonia.Media;

namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// Text for a <see cref="CompositorScene"/>, whose <see cref="ImmediateDrawingContext"/> offers <c>DrawGlyphRun</c> and no
/// <c>DrawText</c>. One immutable glyph run is built per printable ASCII character, so drawing a readout every frame
/// costs a translate and a draw per character and allocates nothing.
/// </summary>
/// <remarks>
/// Construct on the UI thread — a scene's own constructor is the right place — and draw only from the render thread:
/// the glyph typeface comes from the font manager and every run is built up front, so <see cref="Draw"/> touches
/// nothing but the cache. Characters outside U+0020..U+007E are skipped, and each character is advanced by its own
/// width, so there is no kerning or shaping.
/// </remarks>
public sealed class SceneText
{
    private const char First = ' ';
    private const char Last = '~';

    private readonly IImmutableGlyphRunReference?[] _runs = new IImmutableGlyphRunReference?[Last - First + 1];
    private readonly double[] _advances = new double[Last - First + 1];

    /// <summary>Builds the glyph runs for the default typeface at <paramref name="fontSize"/>.</summary>
    public SceneText(double fontSize)
    {
        var typeface = Typeface.Default.GlyphTypeface;
        Height = typeface.Metrics.LineSpacing * fontSize / typeface.Metrics.DesignEmHeight;
        for (var c = First; c <= Last; c++)
        {
            if (!typeface.CharacterToGlyphMap.TryGetGlyph(c, out var glyph)) continue;
            using var run = new GlyphRun(typeface, fontSize, c.ToString().AsMemory(), new[] { glyph });
            _runs[c - First] = run.TryCreateImmutableGlyphRunReference();
            _advances[c - First] = run.Metrics.WidthIncludingTrailingWhitespace;
        }
    }

    /// <summary>The line height, which is also the height of the box <see cref="Draw"/> fills.</summary>
    public double Height { get; }

    /// <summary>The width <paramref name="text"/> takes when drawn.</summary>
    public double Measure(string text)
    {
        var width = 0d;
        foreach (var c in text) width += AdvanceOf(c);
        return width;
    }

    /// <summary>Draws <paramref name="text"/> with its top-left corner at <paramref name="origin"/>.</summary>
    public void Draw(ImmediateDrawingContext context, string text, Point origin, IImmutableBrush foreground)
    {
        var x = origin.X;
        foreach (var c in text)
        {
            var index = c - First;
            if ((uint)index >= (uint)_runs.Length) continue;
            if (_runs[index] is { } run)
            {
                using (context.PushPreTransform(Matrix.CreateTranslation(x, origin.Y))) context.DrawGlyphRun(foreground, run);
            }
            x += _advances[index];
        }
    }

    private double AdvanceOf(char c)
    {
        var index = c - First;
        return (uint)index < (uint)_advances.Length ? _advances[index] : 0;
    }
}
