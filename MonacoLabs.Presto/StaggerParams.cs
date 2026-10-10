namespace MonacoLabs.Presto;

/// <summary>Options for <see cref="Presto.Stagger"/>, mirroring anime.js's <c>StaggerParams</c>.</summary>
public sealed record StaggerParams
{
    /// <summary>The target the distances are measured from.</summary>
    public StaggerFrom From { get; init; }

    /// <summary>Plays the stagger back to front.</summary>
    public bool Reversed { get; init; }

    /// <summary>Distributes the values along an ease instead of evenly; a <see cref="Spring"/> contributes its ease curve.</summary>
    public EaseValue? Ease { get; init; }

    /// <summary>The <c>[columns, rows(, layers)]</c> the targets are laid out in, so distances are measured in two or three dimensions.</summary>
    public int[]? Grid { get; init; }

    /// <summary>Derives the grid from the targets' measured positions instead of <see cref="Grid"/>.</summary>
    public bool AutoGrid { get; init; }

    /// <summary>
    /// Measures a target's position for <see cref="AutoGrid"/> as <c>[x, y]</c> or <c>[x, y, z]</c>, for targets that carry
    /// their position somewhere other than <c>X</c>/<c>Y</c>/<c>Z</c> properties, which a null result falls back to.
    /// </summary>
    public Func<object, double[]?>? Coordinates { get; init; }

    /// <summary>Measures along one grid axis, signed, instead of the distance to <see cref="From"/>.</summary>
    public StaggerAxis? Axis { get; init; }

    /// <summary>Spaces the values over this many targets instead of the number actually animated.</summary>
    public int? Total { get; init; }

    /// <summary>Supplies each target's position in the stagger order; a value outside <c>[0, total)</c> falls back to the natural index.</summary>
    public Func<object, int, int, double>? Use { get; init; }

    /// <inheritdoc cref="Use"/>
    /// <remarks>The name of a target property holding the index, for targets that carry their own order.</remarks>
    public string? UseProperty { get; init; }

    /// <summary>The value the first target starts from: a number, or a label when the stagger positions timeline children.</summary>
    public TweenValue? Start { get; init; }

    /// <summary>Makes <see cref="Jitter"/> reproducible; omitting it draws from the shared random generator.</summary>
    public int? Seed { get; init; }

    /// <summary>A random offset added to each value: one magnitude, or <c>[start, end]</c> to ramp it across the stagger order.</summary>
    public double[]? Jitter { get; init; }

    /// <summary>Post-processes every value, as <c>Utils</c>'s chainable helpers do.</summary>
    public Func<double, double>? Modifier { get; init; }
}
