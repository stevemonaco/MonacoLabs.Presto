using SkiaSharp;
using SkottieAnimation = SkiaSharp.Skottie.Animation;

namespace MonacoLabs.Presto.Lottie;

/// <summary>
/// One view's Skottie animation, built on first render and disposed once the view and every draw operation still holding
/// it let go.
/// </summary>
internal sealed class LottiePlayerHandle : IDisposable
{
    private static int s_live;

    private readonly LottieComposition _composition;
    // Offscreen snapshots render on the UI thread while the render thread may be drawing the same animation.
    private readonly Lock _gate = new();
    private SkottieAnimation? _animation;
    private int _references = 1;

    public LottiePlayerHandle(LottieComposition composition)
    {
        _composition = composition;
        Interlocked.Increment(ref s_live);
    }

    /// <summary>Handles not yet disposed, across every view.</summary>
    internal static int Live => Volatile.Read(ref s_live);

    public LottiePlayerHandle Retain()
    {
        Interlocked.Increment(ref _references);
        return this;
    }

    /// <summary>Draws composition <paramref name="frame"/> scaled into <paramref name="destination"/>.</summary>
    public void Render(SKCanvas canvas, double frame, SKRect destination)
    {
        var size = _composition.Size;
        lock (_gate)
        {
            _animation ??= _composition.CreateAnimation();
            // Skottie seeks relative to the in point, and its own destination scaling keeps the aspect ratio.
            _animation.SeekFrame(frame - _composition.InPoint);
            canvas.Translate(destination.Left, destination.Top);
            canvas.Scale((float)(destination.Width / size.Width), (float)(destination.Height / size.Height));
            _animation.Render(canvas, new SKRect(0, 0, (float)size.Width, (float)size.Height));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _references) != 0) return;
        _animation?.Dispose();
        Interlocked.Decrement(ref s_live);
    }
}
