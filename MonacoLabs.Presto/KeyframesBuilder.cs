using System.Globalization;

namespace MonacoLabs.Presto;

/// <summary>
/// Adds animation-level keyframes for <see cref="AnimationBuilder.Keyframes"/>, either duration-based with
/// <see cref="Frame"/> or percentage-based with <see cref="At"/>; the two forms cannot be mixed.
/// </summary>
public sealed class KeyframesBuilder
{
    private readonly List<KeyframeBuilder> _frames = [];
    private readonly Dictionary<string, KeyframeBuilder> _percentages = [];

    internal KeyframesBuilder() { }

    /// <summary>Adds the next frame; frames without their own duration split the animation's evenly.</summary>
    public KeyframesBuilder Frame(Action<KeyframeBuilder> frame)
    {
        if (_percentages.Count > 0) throw new InvalidOperationException("Frame cannot be mixed with At in the same keyframes.");
        _frames.Add(Build(frame));
        return this;
    }

    /// <summary>Places a frame at <paramref name="percent"/> (0-100) of the animation's duration, replacing any frame already there.</summary>
    public KeyframesBuilder At(double percent, Action<KeyframeBuilder> frame)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        if (_frames.Count > 0) throw new InvalidOperationException("At cannot be mixed with Frame in the same keyframes.");
        _percentages[percent.ToString(CultureInfo.InvariantCulture) + "%"] = Build(frame);
        return this;
    }

    internal Keyframes ToKeyframes() => _frames.Count > 0
        ? _frames.Select(f => f.ToKeyframe()).ToArray()
        : _percentages.ToDictionary(p => p.Key, p => p.Value.ToKeyframe());

    internal KeyframesBuilder Clone()
    {
        var copy = new KeyframesBuilder();
        foreach (var frame in _frames) copy._frames.Add(frame.Clone());
        foreach (var (percent, frame) in _percentages) copy._percentages[percent] = frame.Clone();
        return copy;
    }

    private static KeyframeBuilder Build(Action<KeyframeBuilder> frame)
    {
        var builder = new KeyframeBuilder();
        frame(builder);
        return builder;
    }
}
