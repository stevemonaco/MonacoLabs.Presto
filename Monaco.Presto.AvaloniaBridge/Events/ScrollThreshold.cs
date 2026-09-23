namespace Monaco.Presto.AvaloniaBridge.Events;

/// <summary>
/// Where the container and the target have to meet for the target to count as entering or leaving the view:
/// <c>"end start"</c> is the container's end reaching the target's start.
/// </summary>
/// <remarks>
/// Each half is <c>"start"</c>/<c>"top"</c>/<c>"left"</c>, <c>"end"</c>/<c>"bottom"</c>/<c>"right"</c>, <c>"center"</c>,
/// <c>"min"</c>, <c>"max"</c>, a percentage of the container or target size, a number of pixels, or one of those with a
/// relative operator (<c>"bottom-=100"</c>). A single value is the container half, leaving the target half at its
/// default — the target's start on enter and its end on leave — so upstream's <c>{ container, target }</c> object is
/// written as the two-value string. CSS length units other than <c>%</c> have no Avalonia equivalent and are read as
/// their bare number.
/// </remarks>
public readonly struct ScrollThreshold
{
    private ScrollThreshold(object? container, object? target)
    {
        Container = container;
        Target = target;
    }

    internal object? Container { get; }

    internal object? Target { get; }

    /// <summary>The container half followed by the target half, or a single value leaving the target half at its default.</summary>
    public static implicit operator ScrollThreshold(string value)
    {
        var halves = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new(halves.Length > 0 ? halves[0] : null, halves.Length > 1 ? halves[1] : null);
    }

    /// <summary>A number of pixels into the container, leaving the target half at its default.</summary>
    public static implicit operator ScrollThreshold(double value) => new(value, null);
}
