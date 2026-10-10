using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Timer = MonacoLabs.Presto.Timer;

namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// The one <see cref="ParticleScene"/> canvas a window's particle recipes share, laid over the window in its
/// <see cref="OverlayLayer"/> in top-level coordinates and removed once it has drawn nothing for a second.
/// </summary>
internal sealed class ParticleOverlay
{
    internal const double IdleTime = 1000;

    private static readonly ConditionalWeakTable<TopLevel, ParticleOverlay> s_overlays = new();

    private readonly OverlayLayer _layer;
    private int _running;
    private Timer? _idle;

    private ParticleOverlay(TopLevel top, OverlayLayer layer)
    {
        Top = top;
        _layer = layer;
        var origin = layer.TranslatePoint(default, top) ?? default;
        Canvas.SetLeft(Host, -origin.X);
        Canvas.SetTop(Host, -origin.Y);
        Resize(null, null);
        top.SizeChanged += Resize;
        layer.Children.Add(Host);
    }

    internal CompositorCanvas Host { get; } = new() { Scene = new ParticleScene(), IsHitTestVisible = false };

    internal TopLevel Top { get; }

    /// <summary>The overlay of <paramref name="anchor"/>'s window, created on first use; null when the anchor is not in one.</summary>
    internal static ParticleOverlay? For(Visual anchor)
    {
        if (TopLevel.GetTopLevel(anchor) is not { } top) return null;
        if (s_overlays.TryGetValue(top, out var overlay)) return overlay;
        if (OverlayLayer.GetOverlayLayer(anchor) is not { } layer) return null;
        overlay = new ParticleOverlay(top, layer);
        s_overlays.Add(top, overlay);
        return overlay;
    }

    /// <summary>Starts each emitter at its origin, in top-level coordinates.</summary>
    internal ParticleEffect Play(IReadOnlyList<(EmitterParams Params, Point Origin)> emitters)
    {
        _idle?.Cancel();
        _running += emitters.Count;
        return new ParticleEffect(Host, emitters, SystemFinished);
    }

    /// <summary>Adds the formation <paramref name="add"/> makes, which it hands the callback to run once the formation is dropped.</summary>
    internal ParticleEffect Play(Func<ParticleScene, Action, ParticleFormation> add)
    {
        _idle?.Cancel();
        _running++;
        return new ParticleEffect(Host, add, SystemFinished);
    }

    private void SystemFinished()
    {
        if (--_running > 0) return;
        _idle = Presto.CreateTimer(new TimerParams { Duration = IdleTime, OnComplete = _ => Remove() });
    }

    private void Remove()
    {
        Top.SizeChanged -= Resize;
        _layer.Children.Remove(Host);
        s_overlays.Remove(Top);
    }

    private void Resize(object? sender, SizeChangedEventArgs? e)
    {
        Host.Width = Top.Bounds.Width;
        Host.Height = Top.Bounds.Height;
    }
}
