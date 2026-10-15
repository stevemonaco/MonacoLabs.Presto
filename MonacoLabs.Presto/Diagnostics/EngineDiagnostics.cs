using System.Diagnostics;

namespace MonacoLabs.Presto.Diagnostics;

/// <summary>Opt-in registry of what an engine runs, for inspectors; costs nothing while <see cref="Enabled"/> is off.</summary>
public sealed class EngineDiagnostics
{
    private const int RecentlyEndedCapacity = 32;
    private readonly Engine _engine;
    private readonly List<WeakReference<Timer>> _roots = [];
    private readonly List<WeakReference<Animatable>> _animatables = [];
    private readonly List<Timer> _recentlyEnded = [];
    private FrameStats _lastFrame;
    private bool _enabled;

    internal EngineDiagnostics(Engine engine) => _engine = engine;

    /// <summary>Stops counting a collected engine that was still enabled toward <see cref="PrestoDiagnostics.Active"/>.</summary>
    ~EngineDiagnostics()
    {
        if (_enabled) PrestoDiagnostics.EngineToggled(false);
    }

    /// <summary>Whether the engine registers its roots and records <see cref="LastFrame"/>; enabling picks up the roots already running, disabling forgets everything.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _engine.AssertThreadAffinity();
            if (_enabled == value) return;
            _enabled = value;
            PrestoDiagnostics.EngineToggled(value);
            if (value)
            {
                for (var root = _engine._head; root != null; root = root._next) Register(root).Announced = true;
                return;
            }
            _roots.Clear();
            _animatables.Clear();
            _recentlyEnded.Clear();
            _lastFrame = default;
        }
    }

    /// <summary>Live root timers in any state, held weakly so the registry never extends a lifetime; each read returns a new list.</summary>
    public IReadOnlyList<Timer> Roots => Live(_roots);

    /// <summary>Live <see cref="Animatable"/>s created while enabled, held weakly; each read returns a new list.</summary>
    public IReadOnlyList<Animatable> Animatables => Live(_animatables);

    /// <summary>The engine's moving <see cref="MotionValue"/>s.</summary>
    public IReadOnlyList<MotionValue> ActiveValues => _engine._activeValues;

    /// <summary>The latest roots that completed, were canceled or were reverted, oldest first and capped at 32.</summary>
    public IReadOnlyList<Timer> RecentlyEnded => _recentlyEnded;

    /// <summary>Statistics of the latest engine update.</summary>
    public ref readonly FrameStats LastFrame => ref _lastFrame;

    /// <summary>Raised once per root timer created while enabled, after a factory has initialized it.</summary>
    public event Action<Timer>? TimerCreated;

    internal TimerDiagnostics Register(Timer root)
    {
        _roots.Add(new(root));
        return root._diagnostics ??= new();
    }

    internal void Register(Animatable animatable) => _animatables.Add(new(animatable));

    internal void Announce(Timer root)
    {
        if (_enabled) TimerCreated?.Invoke(root);
    }

    internal void Ended(Timer root)
    {
        if (!_enabled) return;
        // A revived root, such as an Animatable's property animation on every Set, ends again.
        if (!_recentlyEnded.Remove(root) && _recentlyEnded.Count == RecentlyEndedCapacity) _recentlyEnded.RemoveAt(0);
        _recentlyEnded.Add(root);
    }

    internal void Record(double time, long started, int valuesStepped, int sinksFlushed)
    {
        var duration = Stopwatch.GetElapsedTime(started);
        var roots = 0;
        var tweens = 0;
        for (var root = _engine._head; root != null; root = root._next)
        {
            roots++;
            tweens += CountTweens(root);
        }
        _lastFrame = new(time, _engine.DeltaTime, duration, roots, tweens, valuesStepped, sinksFlushed);
    }

    private static int CountTweens(Timer timer)
    {
        var count = 0;
        for (var tween = timer._headTween; tween != null; tween = tween._next) count++;
        for (var child = timer._head; child != null; child = child._next) count += CountTweens(child);
        return count;
    }

    private static List<T> Live<T>(List<WeakReference<T>> references) where T : class
    {
        var live = new List<T>(references.Count);
        var kept = 0;
        for (var i = 0; i < references.Count; i++)
        {
            if (!references[i].TryGetTarget(out var target)) continue;
            live.Add(target);
            references[kept++] = references[i];
        }
        references.RemoveRange(kept, references.Count - kept);
        return live;
    }
}
