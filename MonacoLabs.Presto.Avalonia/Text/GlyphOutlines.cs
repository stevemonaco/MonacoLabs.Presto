using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using SkiaSharp;

namespace MonacoLabs.Presto.Text;

/// <summary>Reads glyph outlines through SkiaSharp, since an Avalonia <see cref="Avalonia.Media.Geometry"/> cannot enumerate its figures.</summary>
internal static class GlyphOutlines
{
    private static readonly ConditionalWeakTable<GlyphTypeface, SKTypeface?> s_faces = new();
    private static readonly uint[] s_colorTables = [Tag("COLR"), Tag("CBDT"), Tag("sbix"), Tag("SVG ")];

    /// <summary>
    /// The outline of <paramref name="text"/> shaped as a <see cref="TextLayout"/> shapes it, with its origin at the top left
    /// of the first line box. Missing glyphs and glyphs of color or bitmap fonts add nothing.
    /// </summary>
    internal static SKPath Path(string text, Typeface typeface, double fontSize, double letterSpacing = 0)
    {
        var path = new SKPath();
        using var layout = new TextLayout(text, typeface, fontSize, null, letterSpacing: letterSpacing);
        var top = 0d;
        foreach (var line in layout.TextLines)
        {
            var x = line.Start;
            foreach (var run in line.TextRuns)
            {
                if (run is ShapedTextRun { GlyphRun: var glyphs } && Face(glyphs.GlyphTypeface) is { } face)
                {
                    var simulations = glyphs.GlyphTypeface.FontSimulations;
                    // As Avalonia.Skia simulates bold and oblique.
                    using var font = new SKFont(face, (float)glyphs.FontRenderingEmSize)
                    {
                        Embolden = simulations.HasFlag(FontSimulations.Bold),
                        SkewX = simulations.HasFlag(FontSimulations.Oblique) ? -0.3f : 0,
                    };
                    var advance = x;
                    foreach (var glyph in glyphs.GlyphInfos)
                    {
                        if (glyph.GlyphIndex != 0 && font.GetGlyphPath(glyph.GlyphIndex) is { } outline)
                        {
                            using (outline) path.AddPath(outline, (float)(advance + glyph.GlyphOffset.X), (float)(top + line.Baseline + glyph.GlyphOffset.Y));
                        }
                        advance += glyph.GlyphAdvance;
                    }
                }
                if (run is DrawableTextRun drawable) x += drawable.Size.Width;
            }
            top += line.Height;
        }
        return path;
    }

    /// <summary><see cref="Path(string, Typeface, double, double)"/> in <paramref name="block"/>'s font, size and letter spacing.</summary>
    internal static SKPath Path(string text, TextBlock block) =>
        Path(text, new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize, block.LetterSpacing);

    /// <summary><see cref="Path(string, Typeface, double, double)"/> as a geometry with the font's nonzero fill rule, curves kept.</summary>
    internal static Geometry Geometry(string text, Typeface typeface, double fontSize, double letterSpacing = 0)
    {
        using var path = Path(text, typeface, fontSize, letterSpacing);
        return Geometry(path);
    }

    internal static Geometry Geometry(SKPath path) =>
        path.IsEmpty ? new StreamGeometry() : Avalonia.Media.Geometry.Parse("F1 " + path.ToSvgPathData());

    private static SKTypeface? Face(GlyphTypeface glyphTypeface) => s_faces.GetValue(glyphTypeface, Load);

    private static SKTypeface? Load(GlyphTypeface glyphTypeface)
    {
        if (!glyphTypeface.PlatformTypeface.TryGetStream(out var stream)) return null;
        using var data = SKData.Create(stream);
        stream.Dispose();
        // A collection's stream holds every face; take the one named like the typeface.
        SKTypeface? first = null;
        for (var i = 0; SKTypeface.FromData(data, i) is { } face; i++)
        {
            if (face.FamilyName == glyphTypeface.FamilyName)
            {
                first?.Dispose();
                first = face;
                break;
            }
            if (first == null) first = face; else face.Dispose();
        }
        if (first == null || !first.GetTableTags().Any(s_colorTables.Contains)) return first;
        first.Dispose();
        return null;
    }

    private static uint Tag(string tag) => (uint)(tag[0] << 24 | tag[1] << 16 | tag[2] << 8 | tag[3]);
}
