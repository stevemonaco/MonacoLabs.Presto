using Monaco.Presto.Core;
using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>
/// Where a child lands on a timeline: a number, a numeric string, a label name, a relative offset (<c>"+=100"</c>,
/// <c>"*=2"</c>, <c>"myLabel-=50"</c>), the previous child's end (<c>"&lt;"</c>) or start (<c>"&lt;&lt;"</c>), optionally with a
/// relative offset of its own. The default value appends the child at the end of the timeline. The typed forms
/// (<see cref="TimelineLabel"/>, <see cref="End"/>, <see cref="PreviousEnd"/>, <see cref="PreviousStart"/>, and <c>+</c>/<c>-</c>
/// with a number) resolve as their string twins.
/// </summary>
public readonly struct TimelinePosition
{
    private enum Anchor : byte { Raw, Label, PreviousEnd, PreviousStart }

    private readonly object? _raw;
    private readonly Anchor _anchor;
    private readonly double _offset;

    private TimelinePosition(object? raw, Anchor anchor = Anchor.Raw, double offset = 0)
    {
        _raw = raw;
        _anchor = anchor;
        _offset = offset;
    }

    /// <summary>The timeline's current end, like the default position; <c>End + n</c> is <c>"+=n"</c>.</summary>
    public static TimelinePosition End => default;

    /// <summary>The last added child's end, like <c>"&lt;"</c>; <c>PreviousEnd - n</c> is <c>"&lt;-=n"</c>.</summary>
    public static TimelinePosition PreviousEnd => new(null, Anchor.PreviousEnd);

    /// <summary>The last added child's start, like <c>"&lt;&lt;"</c>; <c>PreviousStart + n</c> is <c>"&lt;&lt;+=n"</c>.</summary>
    public static TimelinePosition PreviousStart => new(null, Anchor.PreviousStart);

    internal static TimelinePosition OfLabel(string name) => new(name, Anchor.Label);

    /// <summary>Moves <paramref name="position"/> <paramref name="offset"/> milliseconds later, folding into any offset it already has.</summary>
    public static TimelinePosition operator +(TimelinePosition position, double offset) =>
        new(position._raw, position._anchor, position._offset + offset);

    /// <summary>Moves <paramref name="position"/> <paramref name="offset"/> milliseconds earlier, folding into any offset it already has.</summary>
    public static TimelinePosition operator -(TimelinePosition position, double offset) =>
        new(position._raw, position._anchor, position._offset - offset);

    /// <summary>An absolute time on the timeline, in milliseconds from its start.</summary>
    public static implicit operator TimelinePosition(double value) => new(value);

    /// <summary>The string forms listed on <see cref="TimelinePosition"/>; an unknown label falls back to the end of the timeline.</summary>
    public static implicit operator TimelinePosition(string value) => new(value);

    internal double Parse(Timeline timeline) => _anchor switch
    {
        Anchor.Label => LabelOrEnd(timeline, (string)_raw!),
        Anchor.PreviousEnd => PreviousChildOffset(timeline, start: false),
        Anchor.PreviousStart => PreviousChildOffset(timeline, start: true),
        _ => ParseRaw(timeline),
    } + _offset;

    private static double TimelineEnd(Timeline timeline) =>
        timeline.IterationDuration == Constants.MinValue ? 0 : timeline.IterationDuration;

    private static double LabelOrEnd(Timeline timeline, string name) =>
        timeline.Labels.TryGetValue(name, out var labelOffset) ? labelOffset : TimelineEnd(timeline);

    private double ParseRaw(Timeline timeline)
    {
        if (_raw is null) return TimelineEnd(timeline);
        if (_raw is double number) return number;

        var text = (string)_raw;
        var asNumber = JsNumber.ToNumber(text);
        if (!double.IsNaN(asNumber)) return asNumber;

        var operatorIndex = ValueParser.IndexOfRelativeOperator(text);
        // Upstream resolves an unknown label before an operator to NaN; the duration fallback matches its bare label branch.
        var offset = text.StartsWith('<')
            ? PreviousChildOffset(timeline, start: text.Length > 1 && text[1] == '<')
            : LabelOrEnd(timeline, operatorIndex < 0 ? text : text[..operatorIndex]);
        return operatorIndex < 0
            ? offset
            : ValueParser.GetRelativeValue(offset, JsNumber.ToNumber(text.AsSpan(operatorIndex + 2)), text[operatorIndex]);
    }

    private static double PreviousChildOffset(Timeline timeline, bool start)
    {
        var previous = timeline._tail;
        if (previous == null) return 0;
        var startTime = previous._offset + previous._delay;
        return start ? startTime : startTime + previous.Duration;
    }
}
