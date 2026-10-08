using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>
/// A <c>double</c> cell that derived values follow through combinators such as <see cref="Map(double[], double[], EaseValue?, bool)"/>
/// and <see cref="Combine(MotionValue, MotionValue, Func{double, double, double})"/>. Reading a derived value evaluates it lazily,
/// and <see cref="Changed"/> is raised from the engine frame, at most once per frame, however many sources moved.
/// </summary>
public sealed class MotionValue
{
    private readonly Engine _engine;
    private readonly MotionValue[] _sources;
    private readonly int[] _seenVersions;
    private readonly Func<double>? _compute;
    private List<MotionValue>? _dependents;
    private readonly bool _derived;
    private Spring? _spring;
    private VelocityTracker? _tracker;
    private MotionValue? _velocityOf;
    private Action<MotionValue>? _changed;
    private double _value;
    private double _flushedValue;
    private double _sampledValue;
    private double _springVelocity;
    private double[]? _lagTimes;
    private double[]? _lagValues;
    private double _lagDelay;
    private int _lagStart;
    private int _lagCount;
    private int _lagSeen;
    private double _time;
    private int _version;
    private int _observedDependents;
    private bool _dirty;
    internal bool _queued;
    internal bool _active;

    internal MotionValue(double initial)
    {
        _engine = Engine.Current;
        _value = initial;
        _sources = [];
        _seenVersions = [];
    }

    private MotionValue(MotionValue[] sources, Func<double> compute)
    {
        _engine = Engine.Current;
        _sources = sources;
        _seenVersions = new int[sources.Length];
        Array.Fill(_seenVersions, -1);
        _compute = compute;
        _derived = true;
    }

    /// <summary>A value only Presto assigns: a spring follower, a lag, a velocity, a timer's progress or an animatable property.</summary>
    internal MotionValue(Engine engine, MotionValue[] sources, double initial)
    {
        _engine = engine;
        _value = initial;
        _sources = sources;
        _seenVersions = [];
        _derived = true;
    }

    /// <summary>The current value; a derived value is recomputed here if any of its sources moved since it was last read.</summary>
    public double Value
    {
        get
        {
            if (_compute != null && (_dirty || !IsAttached)) Refresh();
            return _value;
        }
    }

    /// <summary>
    /// Raised from the engine frame after every timer has ticked, at most once per frame and only when <see cref="Value"/> differs
    /// from the last raise. A value set from a handler is raised on a later frame rather than looping within this one.
    /// </summary>
    public event Action<MotionValue>? Changed
    {
        add
        {
            if (value == null) return;
            if (!IsObserved) Attach();
            // Reading cleans the chain Attach marked dirty, so the next Set reaches this sink.
            if (_changed == null) _flushedValue = Value;
            _changed += value;
        }
        remove
        {
            if (_changed == null) return;
            _changed -= value;
            if (_changed != null) return;
            _queued = false;
            if (!IsObserved) Detach();
        }
    }

    /// <summary>
    /// How fast <see cref="Value"/> is changing, in units per engine-time unit, sampled once per frame as <see cref="VelocityTracker"/> does.
    /// Measuring starts on the first read, so that read is 0.
    /// </summary>
    public double Velocity
    {
        get
        {
            if (_tracker == null) Track();
            return _tracker!.Velocity / Globals.TimeScale;
        }
    }

    /// <summary>True while the engine steps this value every frame, as a moving spring or lag, or a velocity sample.</summary>
    public bool IsAnimating => _active;

    /// <summary>The spring a value made by <see cref="Spring(Monaco.Presto.Spring)"/> follows its source through, or null.</summary>
    public Spring? FollowSpring => _spring;

    /// <summary>The value a <see cref="FollowSpring"/> follower is settling towards, or null when this is not one.</summary>
    public double? SpringTarget => _spring != null ? _sources[0].Value : null;

    // Time-driven values stay linked to their sources, so they keep moving whether or not anything observes them.
    private bool IsObserved => _changed != null || _observedDependents > 0 || _tracker != null || IsTimeDriven;

    private bool IsTimeDriven => _spring != null || _lagTimes != null;

    private bool IsAttached => _compute == null || IsObserved;

    /// <summary>Sets a source value, invalidating everything derived from it.</summary>
    /// <exception cref="InvalidOperationException">This value is derived from others.</exception>
    public void Set(double value)
    {
        if (_derived) throw new InvalidOperationException("A derived MotionValue follows its sources and cannot be set.");
        Assign(value);
    }

    internal void Assign(double value)
    {
        if (value == _value) return;
        _value = value;
        _version++;
        if (_changed != null) Enqueue();
        if (_tracker != null) Activate();
        Invalidate();
    }

    /// <summary>Maps this value piecewise from <paramref name="input"/> onto <paramref name="output"/>, easing each segment.</summary>
    /// <param name="input">Ascending breakpoints, one per output.</param>
    /// <param name="output">The value at each breakpoint.</param>
    /// <param name="ease">Applied to the progress within each segment; linear when omitted.</param>
    /// <param name="clamp">Holds the end outputs outside the input range instead of extrapolating the end segments.</param>
    /// <exception cref="ArgumentException">The ranges differ in length, have fewer than two points, or the input does not ascend.</exception>
    public MotionValue Map(double[] input, double[] output, EaseValue? ease = null, bool clamp = true)
    {
        ValidateRange(input, output.Length, nameof(output));
        var easing = ease?.Resolve(this, 0, 1, out _) ?? Eases.None;
        return Map(v =>
        {
            var i = Locate(v, input, easing, clamp, out var progress);
            return Helpers.Lerp(output[i - 1], output[i], progress);
        });
    }

    /// <summary>
    /// Maps this value piecewise from <paramref name="input"/> onto <paramref name="outputs"/> that are not plain numbers,
    /// interpolating their channels separately within each eased segment.
    /// </summary>
    /// <param name="input">Ascending breakpoints, one per output.</param>
    /// <param name="outputs">
    /// Values of a type registered in <see cref="Presto.Codecs"/>, such as colors, brushes or thicknesses, or strings the core
    /// parses, such as <c>"#ff0000"</c> or <c>"10px"</c>.
    /// </param>
    /// <param name="ease">Applied to the progress within each segment; linear when omitted.</param>
    /// <param name="clamp">Holds the end outputs outside the input range instead of extrapolating the end segments.</param>
    /// <exception cref="ArgumentException">
    /// The ranges are invalid as for <see cref="Map(double[], double[], EaseValue?, bool)"/>, or the outputs are not all the
    /// same kind of value with the same channels.
    /// </exception>
    public MotionObjectValue Map(double[] input, object[] outputs, EaseValue? ease = null, bool clamp = true)
    {
        ValidateRange(input, outputs.Length, nameof(outputs));
        return new(this, input, outputs, ease?.Resolve(this, 0, 1, out _) ?? Eases.None, clamp);
    }

    /// <summary>A value computed from this one by <paramref name="transform"/>.</summary>
    public MotionValue Map(Func<double, double> transform) => new([this], () => transform(Value));

    /// <summary>This value snapped to the nearest multiple of <paramref name="increment"/>.</summary>
    public MotionValue Snap(double increment) => Map(v => Utils.Snap(v, increment));

    /// <summary>This value snapped to the closest of <paramref name="points"/>.</summary>
    public MotionValue Snap(double[] points) => Map(v => Utils.Snap(v, points));

    /// <summary>A value computed from <paramref name="a"/> and <paramref name="b"/> by <paramref name="combine"/>.</summary>
    public static MotionValue Combine(MotionValue a, MotionValue b, Func<double, double, double> combine) =>
        new([a, b], () => combine(a.Value, b.Value));

    /// <summary>A value computed from every one of <paramref name="values"/>; the array <paramref name="combine"/> receives is reused between calls.</summary>
    public static MotionValue Combine(MotionValue[] values, Func<double[], double> combine)
    {
        var sources = (MotionValue[])values.Clone();
        var buffer = new double[sources.Length];
        return new(sources, () =>
        {
            for (var i = 0; i < sources.Length; i++) buffer[i] = sources[i].Value;
            return combine(buffer);
        });
    }

    /// <summary>
    /// Follows this value through a damped spring with <paramref name="spring"/>'s mass, stiffness and damping, stepped every frame.
    /// A new target keeps the follower's position and velocity, and within the spring's rest threshold it lands on the target.
    /// </summary>
    public MotionValue Spring(Spring spring)
    {
        var follower = new MotionValue(_engine, [this], Value) { _spring = spring };
        follower.Attach();
        // As in Changed: reading cleans the chain Attach marked dirty, so the next Set activates the follower.
        _ = Value;
        return follower;
    }

    /// <summary>Replays this value <paramref name="delay"/> engine-time units late, interpolating linearly between the values it recorded on each frame.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is not a finite number greater than 0.</exception>
    public MotionValue Lag(double delay)
    {
        if (!double.IsFinite(delay) || delay <= 0) throw new ArgumentOutOfRangeException(nameof(delay), delay, "A lag needs a finite delay greater than 0.");
        const int capacity = 16;
        var lag = new MotionValue(_engine, [this], Value)
        {
            _lagDelay = delay / Globals.TimeScale,
            _lagTimes = new double[capacity],
            _lagValues = new double[capacity],
        };
        lag.Attach();
        _ = Value;
        lag._lagSeen = _version;
        return lag;
    }

    /// <summary><paramref name="count"/> chained spring followers: the first follows this value, and each next one follows the previous.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1.</exception>
    public MotionValue[] Trail(int count, Spring spring) => Chain(count, v => v.Spring(spring));

    /// <summary><paramref name="count"/> chained lags, so element <c>i</c> replays this value (i + 1) × <paramref name="delay"/> late.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1, or <paramref name="delay"/> is invalid as for <see cref="Lag"/>.</exception>
    public MotionValue[] Trail(int count, double delay) => Chain(count, v => v.Lag(delay));

    private MotionValue[] Chain(int count, Func<MotionValue, MotionValue> next)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var trail = new MotionValue[count];
        var previous = this;
        for (var i = 0; i < count; i++) previous = trail[i] = next(previous);
        return trail;
    }

    /// <summary>
    /// This value inside [<paramref name="min"/>, <paramref name="max"/>], and past a bound the bound plus the excess scaled by
    /// 1 − <paramref name="friction"/> clamped to [0, 1], the rubber band of <c>Draggable</c>'s container friction.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    public MotionValue Resist(double min, double max, double friction = 0.8)
    {
        if (min > max) throw new ArgumentException("min must not be greater than max.", nameof(min));
        var scale = 1 - Utils.Clamp(friction, 0, 1);
        return Map(v => v > max ? max + (v - max) * scale : v < min ? min + (v - min) * scale : v);
    }

    /// <summary>A follower released at <paramref name="from"/> with <paramref name="velocity"/> per second towards a fixed <paramref name="target"/>, stepping from the next frame.</summary>
    internal static MotionValue Release(Engine engine, Spring spring, double from, double velocity, double target)
    {
        var follower = new MotionValue(engine, [new MotionValue(engine, [], target)], from) { _spring = spring, _springVelocity = velocity };
        follower.Activate();
        return follower;
    }

    /// <summary>Takes a stepping value out of the engine's active list; nothing steps it again until it is activated.</summary>
    internal void Stop()
    {
        if (!_active) return;
        _active = false;
        _engine._activeValues.Remove(this);
    }

    /// <summary>A value carrying this one's <see cref="Velocity"/>, updated every frame while it is non-zero.</summary>
    public MotionValue VelocityOf() => _velocityOf ??= new(_engine, [], Velocity);

    /// <summary>Advances a spring, lag or velocity sample to <paramref name="now"/>, and says whether it still needs frames.</summary>
    internal bool Step(double now)
    {
        var active = (_spring != null && StepSpring(now)) || (_lagTimes != null && StepLag(now));
        if (_tracker != null && Sample(now)) active = true;
        return active;
    }

    /// <summary>Moves a stepping value's clock, and a lag's recorded samples, <paramref name="paused"/> later so a paused interval does not count.</summary>
    internal void Rebase(double paused)
    {
        _time += paused;
        for (var i = 0; i < _lagCount; i++) _lagTimes![Slot(i)] += paused;
    }

    internal void Flush()
    {
        var value = Value;
        if (_changed == null || value == _flushedValue) return;
        _flushedValue = value;
        _changed(this);
    }

    /// <summary>The index of the breakpoint ending <paramref name="v"/>'s segment, with the eased progress through that segment.</summary>
    internal static int Locate(double v, double[] input, Ease ease, bool clamp, out double progress)
    {
        var last = input.Length - 1;
        if (clamp && v <= input[0])
        {
            progress = 0;
            return 1;
        }
        if (clamp && v >= input[last])
        {
            progress = 1;
            return last;
        }
        var i = 1;
        while (i < last && v > input[i]) i++;
        progress = ease((v - input[i - 1]) / (input[i] - input[i - 1]));
        return i;
    }

    private static void ValidateRange(double[] input, int outputLength, string outputName)
    {
        if (input.Length != outputLength) throw new ArgumentException("Input and output ranges must be the same length.", outputName);
        if (input.Length < 2) throw new ArgumentException("A range needs at least two points.", nameof(input));
        for (var i = 1; i < input.Length; i++)
        {
            if (input[i] <= input[i - 1]) throw new ArgumentException("Input breakpoints must ascend.", nameof(input));
        }
    }

    private void Refresh()
    {
        _dirty = false;
        var moved = false;
        for (var i = 0; i < _sources.Length; i++)
        {
            var source = _sources[i];
            _ = source.Value;
            if (source._version == _seenVersions[i]) continue;
            _seenVersions[i] = source._version;
            moved = true;
        }
        if (!moved) return;
        var value = _compute!();
        if (value == _value) return;
        _value = value;
        _version++;
    }

    private bool StepSpring(double now)
    {
        var target = _sources[0].Value;
        var (displacement, velocity) = Oscillate(_spring!, _value - target, _springVelocity, (now - _time) / Constants.K);
        _time = now;
        // The solver's threshold is on displacement; velocity uses the same figure per millisecond.
        const double rest = Monaco.Presto.Spring.RestThreshold;
        if (Math.Abs(displacement) < rest && Math.Abs(velocity) < rest * Constants.K)
        {
            _springVelocity = 0;
            Assign(target);
            return false;
        }
        _springVelocity = velocity;
        Assign(target + displacement);
        return true;
    }

    private bool StepLag(double now)
    {
        var source = _sources[0];
        var value = source.Value;
        if (source._version != _lagSeen)
        {
            _lagSeen = source._version;
            Record(now, value);
        }
        var target = now - _lagDelay;
        while (_lagCount > 1 && _lagTimes![Slot(1)] <= target)
        {
            _lagStart = Slot(1);
            _lagCount--;
        }
        var (t0, v0) = (_lagTimes![_lagStart], _lagValues![_lagStart]);
        if (_lagCount == 1 || target <= t0)
        {
            Assign(v0);
            return _lagCount > 1;
        }
        var next = Slot(1);
        Assign(Helpers.Lerp(v0, _lagValues[next], (target - t0) / (_lagTimes[next] - t0)));
        return true;
    }

    private void Record(double time, double value)
    {
        if (_lagCount == _lagTimes!.Length)
        {
            var times = new double[_lagCount * 2];
            var values = new double[_lagCount * 2];
            for (var i = 0; i < _lagCount; i++)
            {
                times[i] = _lagTimes[Slot(i)];
                values[i] = _lagValues![Slot(i)];
            }
            (_lagTimes, _lagValues, _lagStart) = (times, values, 0);
        }
        var slot = Slot(_lagCount++);
        _lagTimes[slot] = time;
        _lagValues![slot] = value;
    }

    private int Slot(int index) => (_lagStart + index) % _lagTimes!.Length;

    /// <summary>The closed-form displacement and velocity, per second, of a damped oscillator released from <paramref name="x"/> at <paramref name="v"/> after <paramref name="t"/> seconds.</summary>
    private static (double X, double V) Oscillate(Spring spring, double x, double v, double t)
    {
        var w0 = Math.Sqrt(spring.Stiffness / spring.Mass);
        var zeta = spring.Damping / (2 * Math.Sqrt(spring.Stiffness * spring.Mass));
        if (zeta < 1)
        {
            var wd = w0 * Math.Sqrt(1 - zeta * zeta);
            var decay = Math.Exp(-zeta * w0 * t);
            var (sin, cos) = Math.SinCos(wd * t);
            return (
                decay * (x * cos + (v + zeta * w0 * x) / wd * sin),
                decay * (v * cos - (w0 * w0 * x + zeta * w0 * v) / wd * sin));
        }
        if (zeta == 1)
        {
            var decay = Math.Exp(-w0 * t);
            var b = v + w0 * x;
            return ((x + b * t) * decay, (v - b * w0 * t) * decay);
        }
        var root = w0 * Math.Sqrt(zeta * zeta - 1);
        var (r1, r2) = (-zeta * w0 + root, -zeta * w0 - root);
        var c2 = (v - r1 * x) / (r2 - r1);
        var c1 = x - c2;
        var (e1, e2) = (Math.Exp(r1 * t), Math.Exp(r2 * t));
        return (c1 * e1 + c2 * e2, r1 * c1 * e1 + r2 * c2 * e2);
    }

    private bool Sample(double now)
    {
        var value = Value;
        if (_tracker!.AddDisplacement(value - _sampledValue, now)) _sampledValue = value;
        _velocityOf?.Assign(Velocity);
        return _tracker.Velocity != 0 || value != _sampledValue;
    }

    private void Track()
    {
        var wasObserved = IsObserved;
        _tracker = new VelocityTracker();
        if (!wasObserved) Attach();
        _sampledValue = Value;
        _tracker.Add(_sampledValue, _engine.Now);
    }

    private void Activate()
    {
        if (_active) return;
        _active = true;
        var now = _time = _engine.Now;
        // Rebased to now: the value sat still until this change, however long the engine slept.
        _tracker?.AddDisplacement(0, now);
        if (_lagTimes != null)
        {
            (_lagStart, _lagCount) = (0, 0);
            Record(now, _value);
        }
        _engine.AddActive(this);
    }

    private void Invalidate()
    {
        if (_dependents == null) return;
        for (var i = 0; i < _dependents.Count; i++)
        {
            var dependent = _dependents[i];
            if (dependent.IsTimeDriven)
            {
                dependent.Activate();
                continue;
            }
            if (dependent._dirty) continue;
            dependent._dirty = true;
            if (dependent._changed != null) dependent.Enqueue();
            if (dependent._tracker != null) dependent.Activate();
            dependent.Invalidate();
        }
    }

    private void Enqueue()
    {
        if (_queued) return;
        _queued = true;
        _engine.QueueSink(this);
    }

    // Only observed values link into their sources, so an unobserved chain is collectable and never queued.
    private void Attach()
    {
        if (_sources.Length == 0) return;
        _dirty = true;
        foreach (var source in _sources)
        {
            var wasObserved = source.IsObserved;
            (source._dependents ??= []).Add(this);
            source._observedDependents++;
            if (!wasObserved) source.Attach();
        }
    }

    private void Detach()
    {
        foreach (var source in _sources)
        {
            source._dependents!.Remove(this);
            source._observedDependents--;
            if (!source.IsObserved) source.Detach();
        }
    }
}
