using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto;

/// <summary>
/// Physics-based spring ease whose <see cref="SettlingDuration"/> is computed from its parameters rather than
/// supplied. Solver adapted from https://webkit.org/demos/spring/spring.js (c) 2016 Webkit - Apple Inc.
/// </summary>
public sealed class Spring
{
    private const double MaxSpringParamValue = Constants.K * 10;
    private const double TimeStep = .02;
    internal const double RestThreshold = .0005;

    /// <summary>Time the solver watches the spring sit still before calling it settled, included in <see cref="SettlingDuration"/>.</summary>
    internal const double RestDuration = 200;

    private const double MaxDuration = 60000;
    private const double MaxRestSteps = RestDuration / TimeStep / Constants.K;
    private const double MaxIterations = MaxDuration / TimeStep / Constants.K;

    private double _bounce;
    private double _perceivedDuration;
    private double _mass;
    private double _stiffness;
    private double _damping;
    private double _velocity;
    private double _w0;
    private double _zeta;
    private double _wd;
    private double _b;

    /// <summary>
    /// Creates a spring, each parameter falling back to its upstream default when null. Supplying
    /// <paramref name="bounce"/> or <paramref name="duration"/> derives <see cref="Stiffness"/> and
    /// <see cref="Damping"/> rather than taking them from the arguments.
    /// </summary>
    public Spring(
        double? mass = null,
        double? stiffness = null,
        double? damping = null,
        double? velocity = null,
        double? bounce = null,
        double? duration = null,
        Action<Timer?>? onComplete = null)
    {
        _bounce = Helpers.Clamp(bounce ?? .5, -1, 1);
        _perceivedDuration = Helpers.Clamp(duration ?? 628 * Globals.TimeScale, 10 * Globals.TimeScale, MaxSpringParamValue * Globals.TimeScale);
        _mass = Helpers.Clamp(mass ?? 1, 1, MaxSpringParamValue);
        _stiffness = Helpers.Clamp(stiffness ?? 100, Constants.MinValue, MaxSpringParamValue);
        _damping = Helpers.Clamp(damping ?? 10, Constants.MinValue, MaxSpringParamValue);
        _velocity = Helpers.Clamp(velocity ?? 0, -MaxSpringParamValue, MaxSpringParamValue);
        OnComplete = onComplete;
        if (bounce != null || duration != null) CalculateSDFromBD();
        Compute();
        PureEase = t => t == 0 || t == 1 ? t : Solve(t * SolverDuration);
        Ease = t =>
        {
            var currentTime = t * SettlingDuration;
            var completed = Completed;
            var perceivedTime = _perceivedDuration;
            if (currentTime >= perceivedTime && !completed)
            {
                Completed = true;
                OnComplete?.Invoke(Parent);
            }
            if (currentTime < perceivedTime && completed)
            {
                Completed = false;
            }
            return PureEase(t);
        };
    }

    /// <summary>The easing function; sampling it past <see cref="Duration"/> raises <see cref="OnComplete"/> once.</summary>
    public Ease Ease { get; }

    /// <summary>The same curve without the <see cref="Completed"/> bookkeeping, for samplers that must not raise <see cref="OnComplete"/>.</summary>
    public Ease PureEase { get; }

    /// <summary>Time in the engine's unit for the spring to come to rest; animations use it as their duration.</summary>
    public double SettlingDuration { get; private set; }

    /// <summary>Unscaled solver time at which the spring is at rest.</summary>
    public double SolverDuration { get; private set; }

    /// <summary>Whether the ease has been sampled past <see cref="Duration"/>; sampling it before that again clears the flag.</summary>
    public bool Completed { get; private set; }

    /// <summary>The animation driving this spring, passed to <see cref="OnComplete"/>.</summary>
    public Timer? Parent { get; internal set; }

    /// <summary>Raised when the spring reaches <see cref="Duration"/>, before it has finished settling.</summary>
    public Action<Timer?>? OnComplete { get; set; }

    /// <summary>Mass of the weight on the spring, clamped to [1, 10000]; a heavier weight takes longer to settle. Defaults to 1.</summary>
    public double Mass
    {
        get => _mass;
        set
        {
            _mass = Helpers.Clamp(value, 1, MaxSpringParamValue);
            Compute();
        }
    }

    /// <summary>Stiffness of the spring, clamped to (0, 10000]; a stiffer spring snaps back harder. Defaults to 100.</summary>
    public double Stiffness
    {
        get => _stiffness;
        set
        {
            _stiffness = Helpers.Clamp(value, Constants.MinValue, MaxSpringParamValue);
            CalculateBDFromSD();
            Compute();
        }
    }

    /// <summary>Friction opposing the motion, clamped to (0, 10000]; more damping means fewer oscillations. Defaults to 10.</summary>
    public double Damping
    {
        get => _damping;
        set
        {
            _damping = Helpers.Clamp(value, Constants.MinValue, MaxSpringParamValue);
            CalculateBDFromSD();
            Compute();
        }
    }

    /// <summary>Speed the weight starts with, clamped to [-10000, 10000]; a negative one launches it backwards. Defaults to 0.</summary>
    public double Velocity
    {
        get => _velocity;
        set
        {
            _velocity = Helpers.Clamp(value, -MaxSpringParamValue, MaxSpringParamValue);
            Compute();
        }
    }

    /// <summary>Bounciness in [-1, 1]; setting it derives <see cref="Stiffness"/> and <see cref="Damping"/>.</summary>
    public double Bounce
    {
        get => _bounce;
        set
        {
            _bounce = Helpers.Clamp(value, -1, 1);
            CalculateSDFromBD();
            Compute();
        }
    }

    /// <summary>Perceived duration in the engine's unit; setting it derives <see cref="Stiffness"/> and <see cref="Damping"/>.</summary>
    public double Duration
    {
        get => _perceivedDuration;
        set
        {
            _perceivedDuration = Helpers.Clamp(value, 10 * Globals.TimeScale, MaxSpringParamValue * Globals.TimeScale);
            CalculateSDFromBD();
            Compute();
        }
    }

    /// <summary>Damping over critical damping: below 1 the spring oscillates, exactly 1 is critically damped, above 1 is overdamped.</summary>
    public double DampingRatio => _zeta;

    /// <summary>Passes the spring where a bare <see cref="Ease"/> is expected, which drops its <see cref="SettlingDuration"/>.</summary>
    public static implicit operator Ease(Spring spring) => spring.Ease;

    internal double Solve(double time)
    {
        var t = time;
        if (_zeta < 1)
        {
            t = Math.Exp(-t * _zeta * _w0) * (1 * Math.Cos(_wd * t) + _b * Math.Sin(_wd * t));
        }
        else if (_zeta == 1)
        {
            t = (1 + _b * t) * Math.Exp(-t * _w0);
        }
        else
        {
            // Two exponentials instead of cosh/sinh so overdamped springs cannot overflow to infinity.
            t = ((1 + _b) * Math.Exp((-_zeta * _w0 + _wd) * t) + (1 - _b) * Math.Exp((-_zeta * _w0 - _wd) * t)) / 2;
        }
        return 1 - t;
    }

    // Apple's SwiftUI perceived spring duration, via https://www.kvin.me/posts/effortless-ui-spring-animations
    private void CalculateSDFromBD()
    {
        var pds = Globals.TimeScale == 1 ? _perceivedDuration / Constants.K : _perceivedDuration;
        _mass = 1;
        _velocity = 0;
        _stiffness = Math.Pow((2 * Math.PI) / pds, 2);
        _damping = _bounce >= 0
            ? ((1 - _bounce) * 4 * Math.PI) / pds
            : (4 * Math.PI) / (pds * (1 + _bounce));
        _stiffness = Helpers.Round(Helpers.Clamp(_stiffness, Constants.MinValue, MaxSpringParamValue), 3);
        // Damping above 300 produces insane values in the solver.
        _damping = Helpers.Round(Helpers.Clamp(_damping, Constants.MinValue, 300), 3);
    }

    private void CalculateBDFromSD()
    {
        var pds = (2 * Math.PI) / Math.Sqrt(_stiffness);
        _perceivedDuration = pds * (Globals.TimeScale == 1 ? Constants.K : 1);
        var zeta = _damping / (2 * Math.Sqrt(_stiffness));
        _bounce = zeta <= 1
            ? 1 - (_damping * pds) / (4 * Math.PI)
            : (4 * Math.PI) / (_damping * pds) - 1;
        _bounce = Helpers.Round(Helpers.Clamp(_bounce, -1, 1), 3);
        _perceivedDuration = Helpers.Round(Helpers.Clamp(_perceivedDuration, 10 * Globals.TimeScale, MaxSpringParamValue * Globals.TimeScale), 3);
    }

    private void Compute()
    {
        var w0 = _w0 = Helpers.Clamp(Math.Sqrt(_stiffness / _mass), Constants.MinValue, Constants.K);
        var zeta = _zeta = _damping / (2 * Math.Sqrt(_stiffness * _mass));
        if (zeta < 1)
        {
            _wd = w0 * Math.Sqrt(1 - zeta * zeta);
            _b = (zeta * w0 + -_velocity) / _wd;
        }
        else if (zeta == 1)
        {
            _wd = 0;
            _b = -_velocity + w0;
        }
        else
        {
            _wd = w0 * Math.Sqrt(zeta * zeta - 1);
            _b = (zeta * w0 + -_velocity) / _wd;
        }

        var solverTime = 0.0;
        var restSteps = 0;
        var iterations = 0;
        while (restSteps <= MaxRestSteps && iterations <= MaxIterations)
        {
            if (Math.Abs(1 - Solve(solverTime)) < RestThreshold)
            {
                restSteps++;
            }
            else
            {
                restSteps = 0;
            }
            SolverDuration = solverTime;
            solverTime += TimeStep;
            iterations++;
        }
        SettlingDuration = Helpers.Round(SolverDuration * Constants.K, 0) * Globals.TimeScale;
    }
}
