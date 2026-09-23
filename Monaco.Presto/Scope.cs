using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>
/// Port of anime.js <c>createScope</c>: a lifetime for everything created inside it. Timers, animations, animatables and
/// nested scopes register themselves with the scope that is current while they are constructed, so one
/// <see cref="Revert"/> undoes the lot, and constructors added with <see cref="Add(Func{Scope, Action})"/> can be re-run
/// as a group by <see cref="Refresh"/>.
/// </summary>
/// <remarks>Upstream's media queries and their <c>handleEvent</c> refresh are DOM specific and are left to the bridge.</remarks>
public class Scope : IRevertible
{
    private readonly List<Func<Scope, Action?>> _constructors = [];
    private readonly List<Action> _revertConstructors = [];
    private readonly List<IRevertible> _revertibles = [];
    private readonly List<Delegate> _constructorsOnce = [];
    private readonly List<Action> _revertConstructorsOnce = [];
    private readonly List<IRevertible> _revertiblesOnce = [];
    private bool _once;
    private int _onceIndex;

    /// <summary>
    /// Creates a scope, taking a copy of the global defaults as its own and registering with whichever scope is current.
    /// </summary>
    public Scope(ScopeParams? parameters = null)
    {
        Globals.CurrentScope?.Register(this);
        Root = parameters?.Root;
        var defaults = Globals.Defaults;
        if (parameters?.Defaults is { } configure)
        {
            defaults = defaults with { };
            configure(defaults);
        }
        Defaults = defaults;
    }

    /// <inheritdoc cref="ScopeParams.Root"/>
    public object? Root { get; }

    /// <summary>The defaults in force while the scope executes; a copy of the globals as they stood when it was created.</summary>
    public PrestoDefaults Defaults { get; }

    /// <summary>Scratch storage keyed by name, cleared by <see cref="Revert"/>.</summary>
    public IDictionary<string, object?> Data { get; } = new Dictionary<string, object?>();

    /// <summary>The methods registered with <see cref="Add(string, Action)"/>; calling one runs it inside the scope.</summary>
    public IDictionary<string, Action> Methods { get; } = new Dictionary<string, Action>();

    /// <summary>Records <paramref name="revertible"/> so <see cref="Revert"/> undoes it.</summary>
    public void Register(IRevertible revertible) => (_once ? _revertiblesOnce : _revertibles).Add(revertible);

    /// <summary>Runs <paramref name="callback"/> with this scope current and its defaults in force.</summary>
    /// <remarks>Both are restored in a <c>finally</c>, where upstream restores them only when the callback returns.</remarks>
    public T Execute<T>(Func<Scope, T> callback)
    {
        var activeScope = Globals.CurrentScope;
        var activeDefaults = Globals.Defaults;
        Globals.CurrentScope = this;
        Globals.Defaults = Defaults;
        try
        {
            return callback(this);
        }
        finally
        {
            Globals.CurrentScope = activeScope;
            Globals.Defaults = activeDefaults;
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
            for (var i = _revertibles.Count - 1; i >= 0; i--) _revertibles[i].Revert();
            for (var i = _revertConstructors.Count - 1; i >= 0; i--) _revertConstructors[i]();
            _revertibles.Clear();
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
        for (var i = _revertibles.Count - 1; i >= 0; i--) _revertibles[i].Revert();
        for (var i = _revertConstructors.Count - 1; i >= 0; i--) _revertConstructors[i]();
        for (var i = _revertiblesOnce.Count - 1; i >= 0; i--) _revertiblesOnce[i].Revert();
        for (var i = _revertConstructorsOnce.Count - 1; i >= 0; i--) _revertConstructorsOnce[i]();
        _revertibles.Clear();
        _revertConstructors.Clear();
        _constructors.Clear();
        _revertiblesOnce.Clear();
        _revertConstructorsOnce.Clear();
        _constructorsOnce.Clear();
        _onceIndex = 0;
        Methods.Clear();
        Data.Clear();
        return this;
    }

    void IRevertible.Revert() => Revert();

    private static Func<Scope, Action?> AsConstructor(Action<Scope> constructor) => scope =>
    {
        constructor(scope);
        return null;
    };
}
