using System.Globalization;

namespace Monaco.Presto.Core;

/// <summary>Ports upstream <c>generateKeyframes</c>: turns the <c>keyframes</c> parameter into per-property keyframe arrays.</summary>
internal static class KeyframeExpander
{
    /// <summary>
    /// Expands <paramref name="keyframes"/> into property tweens, followed by the properties of <paramref name="parameters"/>
    /// whose names the keyframes did not produce (upstream <c>mergeObjects(generateKeyframes(...), parameters)</c>).
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, TweenValue>> Expand(Keyframes keyframes, AnimationParams parameters)
    {
        var properties = keyframes.DurationBased is { } frames
            ? ExpandDurations(frames)
            : ExpandPercentages(keyframes.PercentageBased!, parameters.Duration?.AsDouble ?? Globals.Defaults.Duration.AsDouble ?? 0);
        foreach (var (name, value) in parameters.Properties) properties.TryAdd(name, value);
        return properties;
    }

    private static OrderedDictionary<string, TweenValue> ExpandDurations(Keyframe[] frames)
    {
        var properties = new OrderedDictionary<string, TweenValue>();
        foreach (var frame in frames)
        {
            foreach (var name in frame.Properties.Keys)
            {
                if (properties.ContainsKey(name)) continue;
                var entries = new TweenValue[frames.Length];
                for (var i = 0; i < frames.Length; i++)
                {
                    var f = frames[i];
                    entries[i] = new TweenParams
                    {
                        To = f.Properties.TryGetValue(name, out var to) ? to : (TweenValue?)null,
                        Duration = f.Duration,
                        Delay = f.Delay,
                        Ease = f.Ease,
                        Modifier = f.Modifier,
                        Composition = f.Composition,
                    };
                }
                properties[name] = entries;
            }
        }
        return properties;
    }

    private static OrderedDictionary<string, TweenValue> ExpandPercentages(Dictionary<string, Keyframe> frames, double totalDuration)
    {
        var entries = new OrderedDictionary<string, List<(TweenValue To, TweenValue? From, double Duration, EaseValue? Ease)>>();
        foreach (var (offset, frame) in frames.Select(f => (Offset: ParseOffset(f.Key), Frame: f.Value)).OrderBy(f => f.Offset))
        {
            var duration = offset * totalDuration;
            foreach (var (name, to) in frame.Properties)
            {
                if (!entries.TryGetValue(name, out var list)) entries[name] = list = [];
                var elapsed = 0.0;
                foreach (var entry in list) elapsed += entry.Duration;
                list.Add((to, list.Count == 1 ? list[0].To : (TweenValue?)null, duration - elapsed, frame.Ease));
            }
        }

        var properties = new OrderedDictionary<string, TweenValue>();
        foreach (var (name, list) in entries)
        {
            // Emulate the WAAPI easing position: a frame's ease applies to the segment that starts at it.
            var shifted = new TweenValue[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                shifted[i] = new TweenParams { To = list[i].To, From = list[i].From, Duration = list[i].Duration, Ease = i > 0 ? list[i - 1].Ease : null };
            }
            properties[name] = list[0].Duration == 0 ? shifted[1..] : shifted;
        }
        return properties;
    }

    private static double ParseOffset(string key) => double.Parse(key.AsSpan().TrimEnd('%'), CultureInfo.InvariantCulture) / 100;
}
