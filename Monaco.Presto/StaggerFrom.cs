namespace Monaco.Presto;

/// <summary>
/// Where a stagger measures its distances from: one of the named origins, a target index, or a normalized
/// <c>[x, y(, z)]</c> position inside the grid. The default is <see cref="First"/>.
/// </summary>
public readonly struct StaggerFrom
{
    private enum Origin
    {
        First,
        Center,
        Last,
        Random,
        Index,
        Position,
    }

    private readonly Origin _origin;
    private readonly int _index;
    private readonly double[]? _position;

    private StaggerFrom(Origin origin, int index = 0, double[]? position = null)
    {
        _origin = origin;
        _index = index;
        _position = position;
    }

    /// <summary>The first target, so the stagger runs front to back.</summary>
    public static StaggerFrom First => new(Origin.First);

    /// <summary>The middle target, so the stagger spreads outwards in both directions.</summary>
    public static StaggerFrom Center => new(Origin.Center);

    /// <summary>The last target, so the stagger runs back to front.</summary>
    public static StaggerFrom Last => new(Origin.Last);

    /// <summary>Shuffles the computed values, so the targets are staggered in a random order.</summary>
    public static StaggerFrom Random => new(Origin.Random);

    /// <summary>A normalized position in the grid, where <c>(0, 0)</c> is its first cell and <c>(1, 1)</c> its last.</summary>
    public static StaggerFrom At(double x, double y, double? z = null) =>
        new(Origin.Position, position: z is { } depth ? [x, y, depth] : [x, y]);

    /// <summary>The target at this index, which the stagger spreads outwards from.</summary>
    public static implicit operator StaggerFrom(int index) => new(Origin.Index, index);

    internal bool IsCenter => _origin == Origin.Center;

    internal bool IsLast => _origin == Origin.Last;

    internal bool IsRandom => _origin == Origin.Random;

    internal int? Index => _origin == Origin.Index ? _index : null;

    internal double[]? Position => _position;
}
