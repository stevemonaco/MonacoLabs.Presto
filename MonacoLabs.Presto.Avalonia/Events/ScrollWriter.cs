using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Events;

/// <summary>
/// Writes a <see cref="ScrollViewer"/>'s offset with <c>SetCurrentValue</c>, so user input keeps working, and remembers
/// per axis who wrote last and what, so the snap, the smooth wheel and the scroll shorthands each tell their own writes
/// from any other offset change (GAPS-2-PLAN §5.3).
/// </summary>
internal sealed class ScrollWriter
{
    private static readonly ConditionalWeakTable<ScrollViewer, ScrollWriter> s_writers = new();

    private readonly ScrollViewer _scroller;
    private Axis _x;
    private Axis _y;

    private ScrollWriter(ScrollViewer scroller) => _scroller = scroller;

    public static ScrollWriter Of(ScrollViewer scroller) => s_writers.GetValue(scroller, static scroller => new ScrollWriter(scroller));

    public void Write(object owner, Vector offset)
    {
        var current = _scroller.Offset;
        _x = new Axis(owner, offset.X, current.X);
        _y = new Axis(owner, offset.Y, current.Y);
        _scroller.SetCurrentValue(ScrollViewer.OffsetProperty, offset);
    }

    /// <summary>Writes one axis, clamped to the scrollable range.</summary>
    public void Write(object owner, bool horizontal, double value)
    {
        value = Math.Clamp(value, 0, Max(_scroller, horizontal));
        var offset = _scroller.Offset;
        if (horizontal) _x = new Axis(owner, value, offset.X);
        else _y = new Axis(owner, value, offset.Y);
        _scroller.SetCurrentValue(ScrollViewer.OffsetProperty, horizontal ? offset.WithX(value) : offset.WithY(value));
    }

    /// <summary>Writes back the axis offset that <paramref name="owner"/>'s last write replaced, if no one has written since.</summary>
    public void Undo(object owner, bool horizontal)
    {
        if (Owns(owner, horizontal)) Write(owner, horizontal, (horizontal ? _x : _y).Replaced);
    }

    public bool Owns(object owner, bool horizontal) => (horizontal ? _x : _y).Owner == owner;

    /// <summary>Whether a live <c>"ScrollX"</c>/<c>"ScrollY"</c> tween holds that axis, from its first write until it ends or is stopped.</summary>
    /// <remarks>A hold left by a canceled tween is released here, since <c>Cancel()</c> never reaches the accessor.</remarks>
    public bool Tweening(bool horizontal)
    {
        if ((horizontal ? _x : _y).Owner is not ScrollOffsetAccessor owner) return false;
        if (Presto.GetAnimations(_scroller, horizontal ? "ScrollX" : "ScrollY").Count > 0) return true;
        owner.Detach(_scroller);
        return false;
    }

    /// <summary>Whether <paramref name="owner"/> wrote <paramref name="value"/> last on that axis.</summary>
    public bool Wrote(object owner, bool horizontal, double value) => Owns(owner, horizontal) && value == (horizontal ? _x : _y).Written;

    /// <summary>Whether a <c>"ScrollX"</c>/<c>"ScrollY"</c> tween wrote <paramref name="value"/> last on that axis.</summary>
    public bool TweenWrote(bool horizontal, double value) => (horizontal ? _x : _y) is { Owner: ScrollOffsetAccessor } axis && value == axis.Written;

    public bool Wrote(object owner, Vector offset) => Wrote(owner, true, offset.X) && Wrote(owner, false, offset.Y);

    /// <summary>Whether <paramref name="value"/> is <paramref name="owner"/>'s last write cut back to the end of an extent that shrank below it.</summary>
    public bool Clamped(object owner, bool horizontal, double value)
        => Owns(owner, horizontal) && value < (horizontal ? _x : _y).Written && value == Max(_scroller, horizontal);

    public void Release(object owner)
    {
        if (_x.Owner == owner) _x = default;
        if (_y.Owner == owner) _y = default;
    }

    public static double Along(Vector vector, bool horizontal) => horizontal ? vector.X : vector.Y;

    public static double Along(Size size, bool horizontal) => horizontal ? size.Width : size.Height;

    public static double Max(ScrollViewer scroller, bool horizontal) => Along(scroller.ScrollBarMaximum, horizontal);

    private readonly record struct Axis(object? Owner, double Written, double Replaced);
}
