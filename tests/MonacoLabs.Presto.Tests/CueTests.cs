namespace MonacoLabs.Presto.Tests;

public sealed class CueTests
{
    [Fact]
    public void RaiseNotifiesSubscribersInSubscriptionOrder()
    {
        var cue = new Cue<string>();
        var log = new List<string>();
        cue.Subscribe(new Observer<string>(v => log.Add($"a:{v}")));
        cue.Subscribe(new Observer<string>(v => log.Add($"b:{v}")));
        cue.Subscribe(new Observer<string>(v => log.Add($"c:{v}")));

        cue.Raise("x");

        Assert.Equal(["a:x", "b:x", "c:x"], log);
    }

    [Fact]
    public void DisposingInsideARaiseEndsTheSubscriptionForLaterRaisesOnly()
    {
        var cue = new Cue();
        var log = new List<string>();
        IDisposable? self = null, next = null;
        self = cue.Subscribe(new Observer<object?>(_ => { log.Add("self"); self!.Dispose(); next!.Dispose(); }));
        next = cue.Subscribe(new Observer<object?>(_ => log.Add("next")));
        cue.Subscribe(new Observer<object?>(_ => log.Add("last")));

        cue.Raise();
        cue.Raise();

        Assert.Equal(["self", "next", "last", "last"], log);
    }

    [Fact]
    public void ASubscriberAddedAfterARaiseReceivesNothing()
    {
        var cue = new Cue<int>();
        cue.Raise(1);
        var received = new List<int>();

        cue.Subscribe(new Observer<int>(received.Add));

        Assert.Empty(received);
        cue.Raise(2);
        Assert.Equal([2], received);
    }

    [Fact]
    public void ValueTypeCueDeliversTypedValuesAndAnObjectSinkGetsTheSameValue()
    {
        var cue = new Cue<int>();
        var typed = new List<int>();
        var boxed = new List<object?>();
        cue.Subscribe(new Observer<int>(typed.Add));
        var plain = new Cue();
        ((ICueSource)cue).Subscribe(boxed.Add);
        ((ICueSource)plain).Subscribe(boxed.Add);

        cue.Raise(7);
        plain.Raise();

        Assert.Equal([7], typed);
        Assert.Equal([7, null], boxed);
    }

    [Fact]
    public void CuesWorkAsObservablesAndNeverCompleteOrFail()
    {
        var cue = new Cue();
        var typedCue = new Cue<string>();
        var observer = new Observer<object?>(_ => { });
        IObservable<object?> observable = cue;
        IObservable<object?> covariant = typedCue;
        observable.Subscribe(observer);
        var subscription = covariant.Subscribe(observer);

        cue.Raise();
        typedCue.Raise("a");
        subscription.Dispose();
        subscription.Dispose();
        typedCue.Raise("b");

        Assert.Equal([null, "a"], observer.Values);
        Assert.Equal(0, observer.Completed);
        Assert.Equal(0, observer.Errors);
    }

    private sealed class Observer<T>(Action<T> onNext) : IObserver<T>
    {
        public List<T> Values { get; } = [];
        public int Completed { get; private set; }
        public int Errors { get; private set; }

        public void OnNext(T value)
        {
            Values.Add(value);
            onNext(value);
        }

        public void OnCompleted() => Completed++;
        public void OnError(Exception error) => Errors++;
    }
}
