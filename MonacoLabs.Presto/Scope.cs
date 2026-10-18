namespace MonacoLabs.Presto;

/// <summary>
/// A lifetime for everything created inside it. Timers, animations, animatables and
/// nested scopes register themselves with the scope that is current while they are constructed, so one
/// <see cref="Revert"/> undoes the lot, and constructors added with <see cref="Add(Func{Scope, Action})"/> can be re-run
/// as a group by <see cref="Refresh"/>.
/// </summary>
/// <remarks>Upstream's media queries and their <c>handleEvent</c> refresh are DOM specific and are left to the bridge.</remarks>
public class Scope : IRevertible, IReleasable
{
    private readonly List<Func<Scope, Action?>> _constructors = [];
    private readonly List<Action> _revertConstructors = [];
    private readonly List<IRevertible> _revertibles = [];
    private readonly List<Delegate> _constructorsOnce = [];
    private readonly List<Action> _revertConstructorsOnce = [];
    private readonly List<IRevertible> _revertiblesOnce = [];
    private readonly Engine _engine;
    private bool _once;
    private int _onceIndex;
    private bool _released;
    // A prune ticket names the list and its generation (once-list negative); clearing a list bumps its generation, retiring the tickets.
    private int _generation = 1;
    private int _onceGeneration = 1;
    internal PruneMark _pruned;

    /// <summary>
    /// Creates a scope on the current engine, sharing that engine's defaults (or a configured copy of them) and registering with
    /// whichever scope is current.
    /// </summary>
    public Scope(ScopeParams? parameters = null)
    {
        _engine = Engine.Current;
        _engine.CurrentScope?.Register(this);
        Root = parameters?.Root;
        var defaults = _engine.Defaults;
        if (parameters?.Defaults is { } configure)
        {
            defaults = defaults with { };
            configure(defaults);
        }
        Defaults = defaults;
    }

    /// <inheritdoc cref="ScopeParams.Root"/>
    public object? Root { get; }

    /// <summary>The defaults in force while the scope executes: its engine's own instance, or a copy configured by <see cref="ScopeParams.Defaults"/> when one is given.</summary>
    public PrestoDefaults Defaults { get; }

    /// <summary>Scratch storage keyed by name, cleared by <see cref="Revert"/>.</summary>
    public IDictionary<string, object?> Data { get; } = new Dictionary<string, object?>();

    /// <summary>The methods registered with <see cref="Add(string, Action)"/>; calling one runs it inside the scope.</summary>
    public IDictionary<string, Action> Methods { get; } = new Dictionary<string, Action>();

    /// <summary>Records <paramref name="revertible"/> so <see cref="Revert"/> undoes it.</summary>
    /// <remarks>
    /// As the list grows, entries that reverting again would restore nothing are dropped, and so are canceled animations whose original values
    /// a later animation of the same targets and properties takes over. A dropped timer that is revived registers again.
    /// </remarks>
    public void Register(IRevertible revertible)
    {
        Reuse();
        if (revertible is Timer { _diagnostics: { } diagnostics }) diagnostics.Scope = this;
        Append(revertible, _once);
    }

    internal void Return(IRevertible revertible, int ticket)
    {
        if (ticket == _generation) Append(revertible, false);
        else if (ticket == -_onceGeneration) Append(revertible, true);
    }

    private void Append(IRevertible revertible, bool once)
    {
        var revertibles = once ? _revertiblesOnce : _revertibles;
        revertibles.Add(revertible);
        var count = revertibles.Count;
        // Pruning only at powers of two keeps registration amortized constant time.
        if (count >= 32 && (count & (count - 1)) == 0) Prune(revertibles, once ? -_onceGeneration : _generation);
    }

    private void Reuse()
    {
        _released = false;
        _pruned.Rejoin(this);
    }

    internal int RevertibleCount => _revertibles.Count + _revertiblesOnce.Count;

    /// <summary>Runs <paramref name="callback"/> with this scope current, its defaults in force and its engine ambient.</summary>
    /// <remarks>All three are restored even when the callback throws.</remarks>
    public T Execute<T>(Func<Scope, T> callback)
    {
        var activeScope = _engine.CurrentScope;
        var activeDefaults = _engine.Defaults;
        _engine.CurrentScope = this;
        _engine.Defaults = Defaults;
        try
        {
            using (_engine.MakeCurrent()) return callback(this);
        }
        finally
        {
            _engine.CurrentScope = activeScope;
            _engine.Defaults = activeDefaults;
        }
    }

    /// <inheritdoc cref="Execute{T}(Func{Scope, T})"/>
    public void Execute(Action<Scope> callback) => Execute(AsConstructor(callback));

    /// <summary>Runs <paramref name="constructor"/> inside the scope and keeps it to be re-run by <see cref="Refresh"/>.</summary>
    public Scope Add(Action<Scope> constructor) => Add(AsConstructor(constructor));

    /// <inheritdoc cref="Add(Action{Scope})"/>
    /// <param name="constructor">Returns the cleanup to run before the next refresh, or null when there is nothing to clean up.</param>
    public Scope Add(Func<Scope, Action?> constructor)
    {
        Reuse();
        _once = false;
        _constructors.Add(constructor);
        Execute(scope =>
        {
            if (constructor(scope) is { } cleanup) _revertConstructors.Add(cleanup);
        });
        return this;
    }

    /// <summary>Exposes <paramref name="method"/> through <see cref="Methods"/>, where calling it runs it inside the scope.</summary>
    public Scope Add(string name, Action method)
    {
        Reuse();
        _once = false;
        Methods[name] = () => Execute(_ => method());
        return this;
    }

    /// <summary>Runs <paramref name="constructor"/> inside the scope the first time only; <see cref="Refresh"/> skips it and leaves what it created alone.</summary>
    /// <remarks>Calls are tracked by their order, so a refresh must reach them in the same order.</remarks>
    public Scope AddOnce(Action<Scope> constructor) => AddOnce(AsConstructor(constructor));

    /// <inheritdoc cref="AddOnce(Action{Scope})"/>
    /// <param name="constructor">Returns the cleanup to run on <see cref="Revert"/>, or null when there is nothing to clean up.</param>
    public Scope AddOnce(Func<Scope, Action?> constructor)
    {
        Reuse();
        _once = true;
        if (_onceIndex++ < _constructorsOnce.Count) return this;
        _constructorsOnce.Add(constructor);
        Execute(scope =>
        {
            if (constructor(scope) is { } cleanup) _revertConstructorsOnce.Add(cleanup);
        });
        return this;
    }

    /// <summary>
    /// Runs <paramref name="constructor"/> inside the scope, and on every refresh reverts the timer it returned and hands
    /// its iteration and progress to the replacement.
    /// </summary>
    public Timer KeepTime(Func<Scope, Timer> constructor)
    {
        _once = true;
        var index = _onceIndex++;
        if (index < _constructorsOnce.Count) return ((Func<Scope, Timer>)_constructorsOnce[index])(this);
        var timekeeper = Utils.KeepTime(constructor);
        _constructorsOnce.Add(timekeeper);
        return Execute(timekeeper);
    }

    /// <summary>Reverts everything the constructors created, then runs them again.</summary>
    public Scope Refresh()
    {
        _onceIndex = 0;
        Execute(_ =>
        {
            RevertAll(_revertibles);
            for (var i = _revertConstructors.Count - 1; i >= 0; i--) _revertConstructors[i]();
            _revertibles.Clear();
            _generation++;
            _revertConstructors.Clear();
            for (var i = 0; i < _constructors.Count; i++)
            {
                if (_constructors[i](this) is { } cleanup) _revertConstructors.Add(cleanup);
            }
        });
        return this;
    }

    /// <summary>Reverts everything created inside the scope and empties it.</summary>
    public virtual Scope Revert()
    {
        RevertAll(_revertibles);
        for (var i = _revertConstructors.Count - 1; i >= 0; i--) _revertConstructors[i]();
        RevertAll(_revertiblesOnce);
        for (var i = _revertConstructorsOnce.Count - 1; i >= 0; i--) _revertConstructorsOnce[i]();
        _revertibles.Clear();
        _revertConstructors.Clear();
        _constructors.Clear();
        _revertiblesOnce.Clear();
        _revertConstructorsOnce.Clear();
        _constructorsOnce.Clear();
        _generation++;
        _onceGeneration++;
        _onceIndex = 0;
        Methods.Clear();
        Data.Clear();
        _released = true;
        return this;
    }

    void IRevertible.Revert() => Revert();

    bool IReleasable.IsReleased => _released;

    private static void RevertAll(List<IRevertible> revertibles)
    {
        for (var i = revertibles.Count - 1; i >= 0; i--)
        {
            if (!IsReleased(revertibles[i])) revertibles[i].Revert();
        }
    }

    private static bool IsReleased(IRevertible revertible) => revertible is IReleasable { IsReleased: true };

    private void Prune(List<IRevertible> revertibles, int ticket)
    {
        var kept = 0;
        for (var i = 0; i < revertibles.Count; i++)
        {
            var revertible = revertibles[i];
            if (IsReleased(revertible) || DroppedWithParent(revertible) || HandsOver(revertible, revertibles, i + 1))
            {
                MarkPruned(revertible, ticket);
            }
            else
            {
                revertibles[kept++] = revertible;
            }
        }
        revertibles.RemoveRange(kept, revertibles.Count - kept);
    }

    private void MarkPruned(IRevertible revertible, int ticket)
    {
        if (revertible is Timer timer) timer._pruned = new(this, ticket);
        else if (revertible is Scope scope) scope._pruned = new(this, ticket);
    }

    private bool DroppedWithParent(IRevertible revertible) => revertible is Timer { IsDetached: true, Parent._pruned.By: { } scope } && scope == this;

    // A canceled timeline is dropped with its children, which the prune drops on reaching them, so each must be released or hand over.
    private bool HandsOver(IRevertible revertible, List<IRevertible> revertibles, int start)
    {
        if (revertible is Animation { IsDetached: true, Parent: null } animation)
        {
            if (!CanHandOver(animation, revertibles, start)) return false;
            HandOver(animation, revertibles, start);
            return true;
        }
        if (revertible is not Timeline { IsDetached: true } timeline) return false;
        foreach (var child in timeline.Children)
        {
            var index = revertibles.IndexOf(child, start);
            if (index < 0) return false;
            if (!IsReleased(child) && (child is not Animation { IsDetached: true } childAnimation || !CanHandOver(childAnimation, revertibles, index + 1))) return false;
        }
        for (var i = start; i < revertibles.Count; i++)
        {
            if (revertibles[i] is Animation child && child.Parent == timeline && !IsReleased(child)) HandOver(child, revertibles, i + 1);
        }
        return true;
    }

    private bool CanHandOver(Animation animation, List<IRevertible> revertibles, int start)
    {
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            if (tween._hasInlineValue && FindSuccessor(tween, revertibles, start) == null) return false;
        }
        return true;
    }

    // Revert runs newest first, so the oldest original of a property is the one left written; it moves to the next animation of that property.
    private void HandOver(Animation animation, List<IRevertible> revertibles, int start)
    {
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            if (!tween._hasInlineValue) continue;
            var successor = FindSuccessor(tween, revertibles, start)!;
            successor._inlineValue = tween._inlineValue;
            successor._hasInlineValue = true;
        }
    }

    private Tween? FindSuccessor(Tween tween, List<IRevertible> revertibles, int start)
    {
        for (var i = start; i < revertibles.Count; i++)
        {
            if (revertibles[i] is not Animation later || IsReleased(later) || DroppedWithParent(later)) continue;
            for (var candidate = later._headTween; candidate != null; candidate = candidate._next)
            {
                if (ReferenceEquals(candidate.Target, tween.Target) && candidate.Property == tween.Property) return candidate;
            }
        }
        return null;
    }

    private static Func<Scope, Action?> AsConstructor(Action<Scope> constructor) => scope =>
    {
        constructor(scope);
        return null;
    };
}

internal struct PruneMark(Scope by, int ticket)
{
    public Scope? By = by;
    public int Ticket = ticket;

    public void Rejoin(IRevertible self)
    {
        if (By is not { } scope) return;
        By = null;
        scope.Return(self, Ticket);
    }
}
