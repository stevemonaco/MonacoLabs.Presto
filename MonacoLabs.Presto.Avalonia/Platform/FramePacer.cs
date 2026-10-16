namespace MonacoLabs.Presto.Platform;

/// <summary>
/// Snaps unevenly dispatched frame callbacks onto a grid of the display's refresh interval, estimated from the callbacks
/// themselves, so consecutive frame times differ by whole intervals, never lag the wall clock and lead it by at most one interval.
/// </summary>
internal sealed class FramePacer
{
    private const int Window = 32;
    private const int SettledAfter = 8;
    private const double OutlierFactor = 1.5;
    private const double GapIntervals = 4;

    private readonly double[] _intervals = new double[Window];
    private int _next;
    private int _count;
    private int _rejected;
    private double _previousNow;
    private double _last = double.NegativeInfinity;
    private bool _restart = true;

    /// <summary>The estimated refresh interval in milliseconds, or NaN before any interval was recorded.</summary>
    internal double Interval { get; private set; } = double.NaN;

    internal bool IsSettled => _count >= SettledAfter;

    /// <summary>Returns the paced time of a frame whose callback ran at <paramref name="now"/> on the wall clock.</summary>
    public double Frame(double now)
    {
        if (!_restart) Record(now - _previousNow);
        _previousNow = now;
        var restart = _restart;
        _restart = false;

        if (!IsSettled || restart) return _last = Math.Max(now, _last);
        var interval = Interval;
        var elapsed = now - _last;
        if (elapsed > GapIntervals * interval) return _last = now;
        var n = Math.Max(1, Math.Round(elapsed / interval));
        // The upper bound stops the grid running ahead while the estimate still trails a faster refresh rate.
        return _last = Math.Max(Math.Clamp(_last + n * interval, now, now + interval), _last);
    }

    /// <summary>Starts the grid again at the next frame's wall clock, keeping the interval estimate.</summary>
    public void Restart() => _restart = true;

    private void Record(double interval)
    {
        if (IsSettled && interval > OutlierFactor * Interval)
        {
            // A cadence that stays this slow is a new refresh rate, not a run of hitches.
            if (++_rejected < SettledAfter) return;
            _count = 0;
        }
        _rejected = 0;
        _intervals[_next] = interval;
        _next = (_next + 1) % Window;
        if (_count < Window) _count++;

        // The least-squares slope of the callback times: the m-th oldest of c intervals weighs m(c + 1 - m), which damps
        // the bias of a window holding a partial jitter cycle far better than a plain mean.
        double sum = 0, weights = 0;
        for (var m = 1; m <= _count; m++)
        {
            var weight = m * (_count + 1.0 - m);
            sum += weight * _intervals[(_next - _count + m - 1 + Window) % Window];
            weights += weight;
        }
        Interval = sum / weights;
    }
}
