using Avalonia;
using Avalonia.Layout;

namespace MonacoLabs.Presto.Gestures;

/// <summary>
/// What a <see cref="GestureArena"/> member needs to claim a press: travel past a slop along its <see cref="Axis"/> (both
/// when null) by a dominance ratio (a mouse always takes 3 DIPs at 1:1, touch or a pen its <see cref="Touch"/> numbers), or an optional touch or pen hold.
/// </summary>
internal sealed record GestureGate(Orientation? Axis, (double Slop, double Dominance) Touch)
{
    internal const double DefaultDragThreshold = 3;

    /// <summary>A finger's slop: touch drifts further than a mouse before it means to move.</summary>
    internal const double TouchSlop = 10;

    /// <summary>Checked once the travel passes the slop along the axis; false drops the member out.</summary>
    public Func<Vector, bool>? Accepts { get; init; }

    /// <summary>The touch or pen hold in ms that wins the press, or 0 for none.</summary>
    public double Hold { get; init; }

    /// <summary>A gesture across a scroller's axis, which touch must mean clearly before the scroller loses it.</summary>
    public static GestureGate Crosswise(Orientation axis) => new(axis, (TouchSlop, 2));

    /// <summary>A gesture along a scroller's axis, which must claim a touch before the scroller's 5 DIPs.</summary>
    public static GestureGate SameAxis(Orientation axis) => new(axis, (DefaultDragThreshold, 1));

    /// <summary>Whether <paramref name="travel"/> since the press claims (true) or rejects (false); null while within the slop.</summary>
    public bool? Judge(Vector travel, bool touch)
    {
        var (slop, dominance) = touch ? Touch : (DefaultDragThreshold, 1);
        var (x, y) = (Math.Abs(travel.X), Math.Abs(travel.Y));
        if (x <= slop && y <= slop) return null;
        var (along, across) = Axis == Orientation.Vertical ? (y, x) : (x, y);
        return (Axis is null || along > across * dominance) && Accepts?.Invoke(travel) != false;
    }
}
