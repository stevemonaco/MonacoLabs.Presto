namespace MonacoLabs.Presto;

/// <summary>
/// A raise-only signal carrying a <typeparamref name="T"/> that a view model exposes as a property. It holds no value:
/// <see cref="Raise"/> reaches only the subscribers present when it starts.
/// </summary>
public sealed class Cue<T> : IObservable<T>, ICueSource
{
    private Subscription[] _subscriptions = [];

    /// <summary>Notifies the current subscribers, in subscription order, with <paramref name="value"/>.</summary>
    public void Raise(T value)
    {
        foreach (var subscription in _subscriptions)
            subscription.Observer.OnNext(value);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return Add(new Subscription(this, observer));
    }

    IDisposable ICueSource.Subscribe(Action<object?> sink) => Add(new Subscription(this, new SinkObserver(sink)));

    private Subscription Add(Subscription subscription)
    {
        _subscriptions = [.. _subscriptions, subscription];
        return subscription;
    }

    private void Remove(Subscription subscription)
    {
        var index = Array.IndexOf(_subscriptions, subscription);
        if (index < 0)
            return;
        var next = new Subscription[_subscriptions.Length - 1];
        Array.Copy(_subscriptions, next, index);
        Array.Copy(_subscriptions, index + 1, next, index, next.Length - index);
        _subscriptions = next;
    }

    private sealed class Subscription(Cue<T> cue, IObserver<T> observer) : IDisposable
    {
        public readonly IObserver<T> Observer = observer;

        public void Dispose() => cue.Remove(this);
    }

    private sealed class SinkObserver(Action<object?> sink) : IObserver<T>
    {
        public void OnNext(T value) => sink(value);
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
