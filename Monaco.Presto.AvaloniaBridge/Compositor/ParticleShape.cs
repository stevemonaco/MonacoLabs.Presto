using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>What a <see cref="ParticleScene"/> draws for each particle, set as <see cref="EmitterParams.Shape"/>; unset draws a <see cref="Circle"/>.</summary>
/// <remarks>A particle's size is the shape's width, the diameter of a circle and the font size of a glyph.</remarks>
public sealed record ParticleShape
{
    internal const double GlyphFontSize = 32;

    private ParticleShape(ShapeKind kind) => Kind = kind;

    /// <summary>A square.</summary>
    public static ParticleShape Rect { get; } = new(ShapeKind.Rect);

    /// <summary>A circle.</summary>
    public static ParticleShape Circle { get; } = new(ShapeKind.Circle);

    /// <summary>A rounded line trailing the particle along its velocity, longer the faster it moves.</summary>
    public static ParticleShape Streak { get; } = new(ShapeKind.Streak);

    internal ShapeKind Kind { get; }

    internal double Radius { get; private init; }

    internal SceneText? Text { get; private init; }

    internal string Character { get; private init; } = "";

    internal Bitmap? Image { get; private init; }

    /// <summary>A square with corners of <paramref name="radius"/>.</summary>
    public static ParticleShape RoundedRect(double radius) => new(ShapeKind.RoundedRect) { Radius = radius };

    /// <summary>A printable ASCII character in the default typeface; call on the UI thread, which builds its glyph runs.</summary>
    public static ParticleShape Glyph(char glyph)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(glyph, '!');
        ArgumentOutOfRangeException.ThrowIfGreaterThan(glyph, '~');
        return new(ShapeKind.Glyph) { Text = new SceneText(GlyphFontSize), Character = glyph.ToString() };
    }

    /// <summary>A bitmap scaled to the particle's size, keeping its aspect ratio; opacity applies, color does not.</summary>
    public static ParticleShape Bitmap(Bitmap image) => new(ShapeKind.Bitmap) { Image = image };

    /// <summary>Rasterizes <paramref name="geometry"/>, in a box of <paramref name="size"/>, once and draws it as a <see cref="Bitmap"/>; call on the UI thread.</summary>
    public static ParticleShape FromGeometry(Geometry geometry, Size size)
    {
        var bitmap = new RenderTargetBitmap(PixelSize.FromSize(size, 1));
        using (var context = bitmap.CreateDrawingContext()) context.DrawGeometry(Brushes.White, null, geometry);
        return Bitmap(bitmap);
    }

    internal enum ShapeKind
    {
        Rect,
        RoundedRect,
        Circle,
        Streak,
        Glyph,
        Bitmap,
    }
}
