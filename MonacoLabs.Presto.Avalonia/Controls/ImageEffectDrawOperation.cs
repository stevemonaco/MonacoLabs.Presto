using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// Draws a bitmap into a Skia layer whose image filter applies an <see cref="ImageEffect"/>, leasing the canvas only to push
/// and pop it so Avalonia's opacity, clip and transform still apply.
/// </summary>
internal sealed class ImageEffectDrawOperation : ICustomDrawOperation
{
    // Two octaves of fractal noise put the red channel between about 0.1 and 0.9 (measured), which the dissolve stretches to 0..1.
    private const float NoiseLow = 0.1f, NoiseRange = 0.8f;

    private readonly Bitmap _bitmap;
    private readonly Rect _source;
    private readonly ImageEffect _effect;
    private readonly IDisposable? _owner;
    private readonly SKPaint _layerPaint;

    public ImageEffectDrawOperation(Bitmap bitmap, Rect source, Rect destination, in ImageEffect effect, IDisposable? owner = null)
    {
        _bitmap = bitmap;
        _source = source;
        Bounds = destination;
        _effect = effect;
        _owner = owner;
        _layerPaint = new SKPaint { ImageFilter = CreateFilter(_effect, destination.ToSKRect()) };
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => Bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) =>
        other is ImageEffectDrawOperation o && o._bitmap == _bitmap && o._source == _source && o.Bounds == Bounds && o._effect.Equals(_effect);

    public void Render(ImmediateDrawingContext context) => DrawInLayer(context, _bitmap, _source, Bounds, Bounds, _layerPaint);

    /// <summary>Draws <paramref name="bitmap"/> into a Skia layer over <paramref name="layer"/> painted by <paramref name="paint"/>, or plainly without a Skia lease.</summary>
    internal static void DrawInLayer(ImmediateDrawingContext context, Bitmap bitmap, Rect source, Rect destination, Rect layer, SKPaint paint)
    {
        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } skia)
        {
            context.DrawBitmap(bitmap, source, destination);
            return;
        }
        using (var lease = skia.Lease()) lease.SkCanvas.SaveLayer(layer.ToSKRect(), paint);
        context.DrawBitmap(bitmap, source, destination);
        using (var lease = skia.Lease()) lease.SkCanvas.Restore();
    }

    // Each stage takes the previous one as input (null is the layer itself), which holds its own native reference.
    internal static SKImageFilter? CreateFilter(in ImageEffect effect, SKRect bounds)
    {
        SKImageFilter? filter = null;
        if (!effect.Matrix.IsIdentity)
        {
            using var matrix = SKColorFilter.CreateColorMatrix(effect.Matrix.AsSpan());
            filter = SKImageFilter.CreateColorFilter(matrix);
        }
        if (effect.Ripple != 0) filter = Then(filter, Ripple(effect, filter, bounds));
        if (effect.Pixelate > 1) filter = Then(filter, Pixelate((float)effect.Pixelate, filter, bounds));
        if (effect.Dissolve > 0) filter = Then(filter, Dissolve(effect, filter, bounds));
        if (effect.Wipe < 1) filter = Then(filter, Wipe(effect, filter, bounds));
        return filter;
    }

    private static SKImageFilter Then(SKImageFilter? input, SKImageFilter output)
    {
        input?.Dispose();
        return output;
    }

    // Skia maps the displacement scale and the noise through the canvas matrix, so both are in DIPs at any render scaling.
    private static SKImageFilter Ripple(in ImageEffect effect, SKImageFilter? input, SKRect bounds) =>
        Displace((float)effect.Ripple, effect.RippleScale, effect.Seed, 0, input, bounds);

    /// <summary>Displaces by up to <paramref name="amount"/> DIPs through seeded fractal noise of feature size <paramref name="scale"/>, moved <paramref name="shift"/> DIPs along x.</summary>
    internal static SKImageFilter Displace(float amount, double scale, int seed, float shift, SKImageFilter? input, SKRect bounds)
    {
        var frequency = (float)(1 / Math.Max(scale, 1e-3));
        using var fractal = SKShader.CreatePerlinNoiseFractalNoise(frequency, frequency, 2, seed);
        using var noise = fractal.WithLocalMatrix(SKMatrix.CreateTranslation(shift, 0));
        using var map = SKImageFilter.CreateShader(noise);
        using var clamped = Clamp(input, bounds, Math.Abs(amount));
        return SKImageFilter.CreateDisplacementMapEffect(SKColorChannel.R, SKColorChannel.G, amount, map, clamped, bounds);
    }

    // The clamp makes a partial last block cover its whole intermediate pixel, which would otherwise be antialiased translucent.
    private static SKImageFilter Pixelate(float size, SKImageFilter? input, SKRect bounds)
    {
        var nearest = new SKSamplingOptions(SKFilterMode.Nearest);
        var down = SKMatrix.CreateScale(1 / size, 1 / size, bounds.Left, bounds.Top);
        var up = SKMatrix.CreateScale(size, size, bounds.Left, bounds.Top);
        using var clamped = Clamp(input, bounds, size);
        using var small = SKImageFilter.CreateMatrix(in down, nearest, clamped);
        using var large = SKImageFilter.CreateMatrix(in up, nearest, small);
        return SKImageFilter.CreateOffset(0, 0, large, bounds);
    }

    // Extends the input past bounds by repeating its outermost DIP column, then row, as a clamp tile mode would.
    private static SKImageFilter Clamp(SKImageFilter? input, SKRect bounds, float pad)
    {
        var (l, t, r, b) = (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        // CreateTile rejects a null input, so the layer itself is passed through an identity offset.
        using var layer = input is null ? SKImageFilter.CreateOffset(0, 0) : null;
        var from = input ?? layer!;
        using var left = SKImageFilter.CreateTile(new SKRect(l, t, l + 1, b), new SKRect(l - pad, t, l, b), from);
        using var right = SKImageFilter.CreateTile(new SKRect(r - 1, t, r, b), new SKRect(r, t, r + pad, b), from);
        using var wide = SKImageFilter.CreateMerge([from, left, right]);
        using var top = SKImageFilter.CreateTile(new SKRect(l - pad, t, r + pad, t + 1), new SKRect(l - pad, t - pad, r + pad, t), wide);
        using var bottom = SKImageFilter.CreateTile(new SKRect(l - pad, b - 1, r + pad, b), new SKRect(l - pad, b, r + pad, b + pad), wide);
        return SKImageFilter.CreateMerge([wide, top, bottom]);
    }

    // Alpha = clamp((noise - threshold) / softness) over the stretched noise; the threshold runs from -softness (all kept) to 1 (all gone).
    private static SKImageFilter Dissolve(in ImageEffect effect, SKImageFilter? input, SKRect bounds)
    {
        var softness = (float)Math.Max(effect.DissolveSoftness, 1e-3);
        var threshold = (float)effect.Dissolve * (1 + softness) - softness;
        var gone = effect.Dissolve >= 1;
        var gain = gone ? 0 : 1 / (NoiseRange * softness);
        var offset = gone ? 0 : -(NoiseLow / NoiseRange + threshold) / softness;
        var frequency = (float)(1 / Math.Max(effect.DissolveScale, 1e-3));
        using var noise = SKShader.CreatePerlinNoiseFractalNoise(frequency, frequency, 2, effect.Seed);
        using var alpha = SKColorFilter.CreateColorMatrix([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, gain, 0, 0, 0, offset]);
        using var shader = noise.WithColorFilter(alpha);
        using var mask = SKImageFilter.CreateShader(shader);
        return SKImageFilter.CreateBlendMode(SKBlendMode.DstIn, input, mask, bounds);
    }

    // The edge geometry of MaskComposer: opaque behind the edge, transparent ahead of it, in the bounds' relative units.
    private static SKImageFilter Wipe(in ImageEffect effect, SKImageFilter? input, SKRect bounds)
    {
        var feather = Math.Max(effect.WipeFeather, 1e-3);
        var edge = effect.Wipe * (1 + feather) - feather / 2;
        var (sin, cos) = Math.SinCos(effect.WipeAngle * Math.PI / 180);
        var extent = Math.Abs(cos) + Math.Abs(sin);
        using var gradient = SKShader.CreateLinearGradient(At(edge - feather / 2), At(edge + feather / 2),
            [SKColors.Black, SKColors.Transparent], SKShaderTileMode.Clamp);
        using var mask = SKImageFilter.CreateShader(gradient);
        return SKImageFilter.CreateBlendMode(SKBlendMode.DstIn, input, mask, bounds);

        SKPoint At(double along) => new(
            bounds.Left + bounds.Width * (float)(0.5 + cos * (along - 0.5) * extent),
            bounds.Top + bounds.Height * (float)(0.5 + sin * (along - 0.5) * extent));
    }

    public void Dispose()
    {
        _layerPaint.ImageFilter?.Dispose();
        _layerPaint.Dispose();
        _owner?.Dispose();
    }
}
