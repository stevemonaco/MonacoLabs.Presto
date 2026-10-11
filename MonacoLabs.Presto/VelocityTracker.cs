namespace MonacoLabs.Presto;

/// <summary>
/// Measures the velocity of a sampled value: samples at least an interval apart, and the
/// strongest of the last few taken as the velocity, so one slow final frame does not kill a flick.
/// </summary>
public sealed class VelocityTracker
{
    private readonly double _interval;
    private readonly double[] _samples;
    private int _index;
    private double _time = double.NegativeInfinity;
    private double _value = double.NaN;

    /// <param name="interval">The minimum time between accepted samples; closer samples are ignored.</param>
    /// <param name="samples">How many recent samples the velocity is the strongest of.</param>
    public VelocityTracker(double interval = 17, int samples = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 1);
        _interval = interval;
        _samples = new double[samples];
    }

    /// <summary>The signed sample of largest magnitude over the window, in units per time unit.</summary>
    public double Velocity { get; private set; }

    /// <summary>Records <paramref name="value"/> at <paramref name="time"/>; the first value after a reset is the baseline.</summary>
    public void Add(double value, double time)
    {
        if (double.IsNaN(_value))
        {
            _value = value;
            _time = time;
        }
        else if (AddDisplacement(value - _value, time))
        {
            _value = value;
        }
    }

    /// <summary>Clears the samples and the value baseline; the time of the last sample is kept.</summary>
    public void Reset()
    {
        Array.Clear(_samples);
        _index = 0;
        _value = double.NaN;
        Velocity = 0;
    }

    /// <summary>Records a displacement over the time since the last accepted sample, and says whether it was accepted.</summary>
    internal bool AddDisplacement(double distance, double time)
    {
        var elapsed = time - _time;
        if (elapsed < _interval) return false;
        Push(distance / elapsed, time);
        var velocity = 0.0;
        foreach (var sample in _samples)
            if (Math.Abs(sample) > Math.Abs(velocity)) velocity = sample;
        Velocity = velocity;
        return true;
    }

    /// <summary>
    /// Draggable's sampler: each sample is scaled, clamped and rounded when taken,
    /// and the velocity is the plain maximum of the window, zero seeds included.
    /// </summary>
    internal bool AddDisplacement(double distance, double time, double multiplier, double min, double max)
    {
        var elapsed = time - _time;
        if (elapsed < _interval) return false;
        Push(Utils.Round(Utils.Clamp(distance / elapsed * multiplier, min, max), 5), time);
        var velocity = double.NegativeInfinity;
        foreach (var sample in _samples) velocity = Math.Max(velocity, sample);
        Velocity = velocity;
        return true;
    }

    private void Push(double sample, double time)
    {
        _time = time;
        _samples[_index] = sample;
        _index = (_index + 1) % _samples.Length;
    }
}
