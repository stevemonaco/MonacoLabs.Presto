using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Draws one host's <see cref="Ripple"/> state-layer tint and ink on the render thread, each ripple growing from its
/// center and fading once released, and asks for animation frames only while a ripple is live or the tint is fading.
/// </summary>
internal sealed class RippleHandler : CompositionCustomVisualHandler
{
    internal static readonly object ReleaseMessage = new();

    private readonly List<Ink> _ripples = [];
    private readonly Dictionary<Color, ImmutableSolidColorBrush> _brushes = [];
    private bool _ticking;
    private Tint? _tint;
    private double _tintFrom;
    private double _tintSince;

    internal int Live => _ripples.Count;

    private double Now => CompositionNow.TotalMilliseconds;

    public override void OnMessage(object message)
    {
        if (message is Press press)
        {
            _ripples.Add(new Ink(press, Now));
            Wake();
        }
        else if (message is Tint tint)
        {
            _tintFrom = TintOpacity(Now);
            _tintSince = Now;
            _tint = tint;
            if (tint.FadeMs > 0) Wake();
        }
        else if (ReferenceEquals(message, ReleaseMessage))
        {
            foreach (ref var ripple in CollectionsMarshal.AsSpan(_ripples))
            {
                if (double.IsNaN(ripple.Released)) ripple.Released = Now;
            }
        }
        Invalidate();
    }

    public override void OnAnimationFrameUpdate()
    {
        var now = Now;
        for (var i = _ripples.Count - 1; i >= 0; i--)
        {
            if (!_ripples[i].At(now, out _, out _)) _ripples.RemoveAt(i);
        }
        Invalidate();
        _ticking = _ripples.Count > 0 || (_tint is { } tint && now - _tintSince < tint.FadeMs);
        if (_ticking) RegisterForNextAnimationFrameUpdate();
    }

    public override void OnRender(ImmediateDrawingContext context)
    {
        var now = Now;
        if (_tint is { } tint && TintOpacity(now) is > 0 and var tinted)
        {
            var box = new Rect(0, 0, EffectiveSize.X, EffectiveSize.Y);
            using (context.PushClip(new RoundedRect(box, tint.Corners))) context.FillRectangle(Brush(tint.Color, tinted), box);
        }
        foreach (ref readonly var ripple in CollectionsMarshal.AsSpan(_ripples))
        {
            if (!ripple.At(now, out var radius, out var opacity)) continue;
            var brush = Brush(ripple.Press.Color, opacity);
            if (ripple.Press.Clip is { } clip)
            {
                using (context.PushClip(clip)) context.DrawEllipse(brush, null, ripple.Press.Center, radius, radius);
            }
            else
            {
                context.DrawEllipse(brush, null, ripple.Press.Center, radius, radius);
            }
        }
    }

    public override Rect GetRenderBounds()
    {
        var bounds = base.GetRenderBounds();
        foreach (ref readonly var ripple in CollectionsMarshal.AsSpan(_ripples))
        {
            if (ripple.Press.Clip is null) bounds = bounds.Union(new Rect(ripple.Press.Center, ripple.Press.Center).Inflate(ripple.Press.Reach));
        }
        return bounds;
    }

    private void Wake()
    {
        if (!_ticking) RegisterForNextAnimationFrameUpdate();
        _ticking = true;
    }

    private double TintOpacity(double now)
    {
        if (_tint is not { } tint) return 0;
        var progress = tint.FadeMs > 0 ? Math.Clamp((now - _tintSince) / tint.FadeMs, 0, 1) : 1;
        return _tintFrom + ((tint.Opacity - _tintFrom) * progress);
    }

    // Fading through the brush's alpha rather than PushOpacity keeps a frame free of allocations once each step is cached.
    private ImmutableSolidColorBrush Brush(Color color, double opacity)
    {
        var faded = Color.FromArgb((byte)Math.Round(color.A * opacity), color.R, color.G, color.B);
        if (_brushes.TryGetValue(faded, out var brush)) return brush;
        if (_brushes.Count >= CompositorScene.BrushCacheLimit) _brushes.Clear();
        _brushes[faded] = brush = new ImmutableSolidColorBrush(faded);
        return brush;
    }

    /// <summary>A ripple at <paramref name="Center"/> growing to <paramref name="Reach"/>, under <paramref name="Clip"/> when bounded, all in host coordinates.</summary>
    internal sealed record Press(Point Center, double Reach, RoundedRect? Clip, Color Color, double GrowMs, Ease Ease, double FadeMs);

    /// <summary>The state layer fading from where it stands to <paramref name="Opacity"/> of the opaque <paramref name="Color"/>, under the host's <paramref name="Corners"/>.</summary>
    internal sealed record Tint(Color Color, double Opacity, CornerRadius Corners, double FadeMs);

    private struct Ink(Press press, double start)
    {
        public readonly Press Press = press;
        public double Released = double.NaN;

        public readonly bool At(double now, out double radius, out double opacity)
        {
            radius = Press.Reach * Press.Ease(Math.Clamp((now - start) / Press.GrowMs, 0, 1));
            opacity = double.IsNaN(Released) ? 1 : Math.Clamp(1 - ((now - Released) / Press.FadeMs), 0, 1);
            return opacity > 0;
        }
    }
}
