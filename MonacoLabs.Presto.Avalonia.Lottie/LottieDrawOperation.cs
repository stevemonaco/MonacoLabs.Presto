using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using MonacoLabs.Presto.Controls;
using SkiaSharp;

namespace MonacoLabs.Presto.Lottie;

/// <summary>
/// Renders one frame of a view's Skottie animation into the leased Skia canvas, through a layer filtered by the view's
/// <see cref="ImageEffect"/> when it has one, so Avalonia's opacity, clip and transform still apply.
/// </summary>
internal sealed class LottieDrawOperation : ICustomDrawOperation
{
    private readonly LottiePlayerHandle _player;
    private readonly double _frame;
    private readonly Rect _placed;
    private readonly ImageEffect _effect;
    private readonly SKPaint? _layerPaint;
    private int _disposed;

    /// <summary>Draws the whole composition into <paramref name="placed"/>, clipped to <paramref name="bounds"/>, and releases <paramref name="player"/> when disposed.</summary>
    public LottieDrawOperation(LottiePlayerHandle player, double frame, Rect placed, Rect bounds, in ImageEffect effect)
    {
        _player = player;
        _frame = frame;
        _placed = placed;
        Bounds = bounds;
        _effect = effect;
        _layerPaint = LayerPaint(effect, bounds);
    }

    public Rect Bounds { get; }

    /// <summary>Whether <see cref="Render"/> has run, which it does synchronously inside an immediate context's <c>Custom</c>.</summary>
    public bool Rendered { get; private set; }

    public bool HitTest(Point p) => Bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) =>
        other is LottieDrawOperation o && o._player == _player && o._frame == _frame && o._placed == _placed && o.Bounds == Bounds
        && o._effect.Equals(_effect);

    public void Render(ImmediateDrawingContext context)
    {
        Rendered = true;
        Draw(context, _player, _frame, _placed, Bounds, _layerPaint);
    }

    internal static SKPaint? LayerPaint(in ImageEffect effect, Rect bounds) =>
        effect.IsIdentity ? null : new SKPaint { ImageFilter = ImageEffectDrawOperation.CreateFilter(effect, bounds.ToSKRect()) };

    internal static void DisposeLayerPaint(SKPaint? layerPaint)
    {
        layerPaint?.ImageFilter?.Dispose();
        layerPaint?.Dispose();
    }

    internal static void Draw(ImmediateDrawingContext context, LottiePlayerHandle player, double frame, Rect placed, Rect bounds, SKPaint? layerPaint)
    {
        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } skia) return;
        using var lease = skia.Lease();
        var canvas = lease.SkCanvas;
        var count = canvas.Save();
        canvas.ClipRect(bounds.ToSKRect());
        if (layerPaint is not null) canvas.SaveLayer(bounds.ToSKRect(), layerPaint);
        player.Render(canvas, frame, placed.ToSKRect());
        canvas.RestoreToCount(count);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        DisposeLayerPaint(_layerPaint);
        _player.Dispose();
    }
}
