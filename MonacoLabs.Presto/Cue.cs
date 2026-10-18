namespace MonacoLabs.Presto;

/// <summary>
/// A raise-only signal that a view model exposes as a property. It holds no value: <see cref="Raise"/> reaches only the
/// subscribers present when it starts, passing <see langword="null"/>.
/// </summary>
public sealed class Cue : IObservable<object?>, ICueSource
{
    private readonly Cue<object?> _cue = new();

    /// <summary>Notifies the current subscribers, in subscription order.</summary>
    public void Raise() => _cue.Raise(null);

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<object?> observer) => _cue.Subscribe(observer);

    IDisposable ICueSource.Subscribe(Action<object?> sink) => ((ICueSource)_cue).Subscribe(sink);
}
