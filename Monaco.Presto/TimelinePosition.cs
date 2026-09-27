using Monaco.Presto.Core;
using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>
/// Where a child lands on a timeline: a number, a numeric string, a label name, a relative offset (<c>"+=100"</c>,
/// <c>"*=2"</c>, <c>"myLabel-=50"</c>), the previous child's end (<c>"&lt;"</c>) or start (<c>"&lt;&lt;"</c>), optionally with a
/// relative offset of its own. The default value appends the child at the end of the timeline.
/// </summary>
public readonly struct TimelinePosition
{
    private readonly object? _raw;

    private TimelinePosition(object raw) => _raw = raw;

    /// <summary>An absolute time on the timeline, in milliseconds from its start.</summary>
    public static implicit operator TimelinePosition(double value) => new(value);

    /// <summary>The string forms listed on <see cref="TimelinePosition"/>; an unknown label falls back to the end of the timeline.</summary>
    public static implicit operator TimelinePosition(string value) => new(value);

    internal double Parse(Timeline timeline)
    {
        var tlDuration = timeline.IterationDuration == Constants.MinValue ? 0 : timeline.IterationDuration;
        if (_raw is null) return tlDuration;
        if (_raw is double number) return number;

        var text = (string)_raw;
        var asNumber = JsNumber.ToNumber(text);
        if (!double.IsNaN(asNumber)) return asNumber;

        var prevOffset = PreviousChildOffset(timeline, text);
        var operatorIndex = ValueParser.IndexOfRelativeOperator(text);
        var name = operatorIndex < 0 ? text : text[..operatorIndex];
        // Upstream resolves an unknown label before an operator to NaN; the duration fallback matches its bare label branch.
        var offset = prevOffset ?? (timeline.Labels.TryGetValue(name, out var labelOffset) ? labelOffset : tlDuration);
        return operatorIndex < 0
            ? offset
            : ValueParser.GetRelativeValue(offset, JsNumber.ToNumber(text.AsSpan(operatorIndex + 2)), text[operatorIndex]);
    }

    private static double? PreviousChildOffset(Timeline timeline, string text)
    {
        if (!text.StartsWith('<')) return null;
        var previous = timeline._tail;
        if (previous == null) return 0;
        var start = previous._offset + previous._delay;
        return text.Length > 1 && text[1] == '<' ? start : start + previous.Duration;
    }
}
