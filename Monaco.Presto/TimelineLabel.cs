namespace Monaco.Presto;

/// <summary>
/// A named handle to a timeline label, usable as a <see cref="TimelinePosition"/> (<c>label</c>, <c>label + 100</c>) and as a
/// <see cref="Timeline.Seek(TimelineLabel, bool)"/> target.
/// </summary>
public sealed class TimelineLabel
{
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public TimelineLabel(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>The key the label is stored under in <see cref="Timeline.Labels"/>.</summary>
    public string Name { get; }

    /// <summary>The label's time, like the string <c>"name"</c>; a label not on the timeline resolves to its end.</summary>
    public static implicit operator TimelinePosition(TimelineLabel label) => TimelinePosition.OfLabel(label.Name);

    /// <summary><paramref name="offset"/> milliseconds after the label, like <c>"name+=offset"</c>.</summary>
    public static TimelinePosition operator +(TimelineLabel label, double offset) => TimelinePosition.OfLabel(label.Name) + offset;

    /// <summary><paramref name="offset"/> milliseconds before the label, like <c>"name-=offset"</c>.</summary>
    public static TimelinePosition operator -(TimelineLabel label, double offset) => TimelinePosition.OfLabel(label.Name) - offset;
}
