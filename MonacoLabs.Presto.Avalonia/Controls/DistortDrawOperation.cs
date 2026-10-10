using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// Draws a snapshot of a <see cref="Distort"/>'s child into a Skia layer over the whole control whose image filter applies, in
/// this order, the turbulence, the goo and the glitch.
/// </summary>
internal sealed class DistortDrawOperation : ICustomDrawOperation
{
    // The classic SVG goo matrix: alpha × 19 − 9 keeps what the blur left above about half opacity, with a hard edge.
    private const float GooGain = 19, GooOffset = -9;
    private const int GlitchBands = 8;

    private readonly Bitmap _bitmap;
    private readonly Rect _destination;
    private readonly (double Turbulence, double Scale, double Phase, double Goo, double Glitch) _effect;
    private readonly IDisposable _owner;
    private readonly SKPaint _layerPaint;

    public DistortDrawOperation(Bitmap bitmap, Rect destination, Rect bounds,
        (double Turbulence, double Scale, double Phase, double Goo, double Glitch) effect, IDisposable owner)
    {
        _bitmap = bitmap;
        _destination = destination;
        Bounds = bounds;
        _effect = effect;
        _owner = owner;
        _layerPaint = new SKPaint { ImageFilter = CreateFilter(effect, bounds.ToSKRect()) };
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => Bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) =>
        other is DistortDrawOperation o && o._bitmap == _bitmap && o._destination == _destination && o.Bounds == Bounds && o._effect == _effect;

    public void Render(ImmediateDrawingContext context) =>
        ImageEffectDrawOperation.DrawInLayer(context, _bitmap, new Rect(_bitmap.Size), _destination, Bounds, _layerPaint);

    private static SKImageFilter? CreateFilter((double Turbulence, double Scale, double Phase, double Goo, double Glitch) effect, SKRect bounds)
    {
        var filter = effect.Turbulence == 0 ? null
            : ImageEffectDrawOperation.Displace((float)effect.Turbulence, effect.Scale, 0, (float)(effect.Phase * effect.Scale), null, bounds);
        if (effect.Goo > 0)
        {
            using var input = filter;
            filter = Goo((float)effect.Goo, input);
        }
        if (effect.Glitch != 0)
        {
            using var input = filter;
            filter = Glitch(effect.Glitch, input, bounds);
        }
        return filter;
    }

    // Avalonia's BlurEffect radius-to-sigma conversion, so a Goo of r blurs as a BlurEffect of r would.
    private static SKImageFilter Goo(float radius, SKImageFilter? input)
    {
        var sigma = 0.288675f * radius + 0.5f;
        using var blur = SKImageFilter.CreateBlur(sigma, sigma, input);
        using var threshold = SKColorFilter.CreateColorMatrix([1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, GooGain, GooOffset]);
        return SKImageFilter.CreateColorFilter(threshold, blur);
    }

    // Bands of seeded heights shift by up to the strength either way, then red moves left and blue right of green and they add back up.
    private static SKImageFilter Glitch(double strength, SKImageFilter? input, SKRect bounds)
    {
        var random = new Random(strength.GetHashCode());
        Span<float> cuts = stackalloc float[GlitchBands + 1];
        for (var i = 1; i < GlitchBands; i++) cuts[i] = random.NextSingle();
        cuts[GlitchBands] = 1;
        cuts.Sort();

        var bands = new SKImageFilter[GlitchBands];
        for (var i = 0; i < GlitchBands; i++)
        {
            var band = new SKRect(bounds.Left, bounds.Top + bounds.Height * cuts[i], bounds.Right, bounds.Top + bounds.Height * cuts[i + 1]);
            bands[i] = SKImageFilter.CreateOffset((float)(strength * (2 * random.NextDouble() - 1)), 0, input, band);
        }
        using var sliced = SKImageFilter.CreateMerge(bands);
        foreach (var band in bands) band.Dispose();

        var shift = (float)strength;
        using var red = Channel(sliced, -shift, [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0]);
        using var green = Channel(sliced, 0, [0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0]);
        using var blue = Channel(sliced, shift, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0]);
        using var redGreen = SKImageFilter.CreateBlendMode(SKBlendMode.Plus, red, green, bounds);
        return SKImageFilter.CreateBlendMode(SKBlendMode.Plus, redGreen, blue, bounds);
    }

    private static SKImageFilter Channel(SKImageFilter input, float dx, float[] matrix)
    {
        using var keep = SKColorFilter.CreateColorMatrix(matrix);
        using var channel = SKImageFilter.CreateColorFilter(keep, input);
        return SKImageFilter.CreateOffset(dx, 0, channel);
    }

    public void Dispose()
    {
        _layerPaint.ImageFilter?.Dispose();
        _layerPaint.Dispose();
        _owner.Dispose();
    }
}
