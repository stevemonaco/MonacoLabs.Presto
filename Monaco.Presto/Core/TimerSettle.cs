namespace Monaco.Presto.Core;

/// <summary>A running <see cref="Timer.Settle"/>: a spring follower whose every change seeks the timer.</summary>
internal sealed class TimerSettle
{
    private readonly Timer _timer;
    private readonly double _target;
    private readonly MotionValue? _spring;
    private readonly TaskCompletionSource<double> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TimerSettle(Timer timer, Spring spring, double target, double velocityPerSecond)
    {
        _timer = timer;
        _target = target;
        var from = timer.Progress;
        if (from == target && velocityPerSecond == 0)
        {
            timer.SeekTime(target * timer.Duration);
            _completion.SetResult(target);
            return;
        }
        _spring = MotionValue.Release(timer.Engine, spring, from, velocityPerSecond, target);
        _spring.Changed += OnChanged;
    }

    public Task<double> Task => _completion.Task;

    public static void Validate(SettleParams settings)
    {
        if (settings.Points is { } points)
        {
            if (points.Length == 0) throw new ArgumentException("Points must not be empty.", nameof(settings));
            foreach (var point in points)
                if (!double.IsFinite(point)) throw new ArgumentException("Points must be finite.", nameof(settings));
        }

        if (!double.IsFinite(settings.Projection) || settings.Projection < 0)
            throw new ArgumentException("Projection must be finite and not negative.", nameof(settings));
        if (settings.FlickVelocity is { } flick && (!double.IsFinite(flick) || flick < 0))
            throw new ArgumentException("FlickVelocity must be finite and not negative.", nameof(settings));
    }

    /// <summary>The sorted, distinct resting progresses of <paramref name="timer"/>, clamped to [0, 1].</summary>
    public static double[] RestingPoints(Timer timer, SettleParams settings)
    {
        var points = new SortedSet<double>();
        foreach (var point in settings.Points ?? [0, 1]) points.Add(Math.Clamp(point, 0, 1));
        if (settings.Labels && timer is Timeline timeline && timeline.Duration > 0)
            foreach (var time in timeline.Labels.Values) points.Add(Math.Clamp(time / timeline.Duration, 0, 1));
        return [.. points];
    }

    /// <summary>The settle target among <paramref name="points"/>, which must be sorted ascending.</summary>
    public static double Choose(double progress, double velocity, double[] points, double projection, double? flickVelocity)
    {
        if (velocity != 0 && Math.Abs(velocity) >= flickVelocity)
        {
            if (velocity > 0)
            {
                foreach (var point in points)
                    if (point > progress) return point;
                return points[^1];
            }

            for (var i = points.Length - 1; i >= 0; i--)
                if (points[i] < progress) return points[i];
            return points[0];
        }

        var projected = progress + velocity * projection;
        var nearest = points[0];
        foreach (var point in points)
            if (Math.Abs(point - projected) < Math.Abs(nearest - projected)) nearest = point;
        return nearest;
    }

    public void End(double progress)
    {
        if (_spring != null)
        {
            _spring.Changed -= OnChanged;
            _spring.Stop();
        }
        _completion.TrySetResult(progress);
    }

    private void OnChanged(MotionValue spring)
    {
        var value = spring.Value;
        _timer.SeekTime(Math.Clamp(value, 0, 1) * _timer.Duration);
        if (value == _target) End(_target);
    }
}
