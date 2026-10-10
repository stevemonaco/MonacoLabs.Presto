namespace MonacoLabs.Presto;

/// <summary>
/// Default headless frame source. A background thread paces frames about every millisecond and, when the thread calling
/// <see cref="Start"/> has a <see cref="SynchronizationContext"/>, posts each frame through it so the engine ticks on that thread.
/// Without a context the callback runs on the pump thread itself, so the engine and its timers may then only be touched from within frame callbacks.
/// </summary>
public sealed class StopwatchFrameSource : IFrameSource
{
    private readonly TimeProvider _timeProvider;
    private readonly long _origin;
    private volatile Pump? _pump;

    /// <summary>Creates a frame source that reads <see cref="Now"/> from <see cref="TimeProvider.System"/>.</summary>
    public StopwatchFrameSource() : this(TimeProvider.System)
    {
    }

    /// <summary>Creates a frame source that reads <see cref="Now"/> from <paramref name="timeProvider"/>; frames are still paced in real time.</summary>
    public StopwatchFrameSource(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _origin = timeProvider.GetTimestamp();
    }

    /// <inheritdoc/>
    public double Now => _timeProvider.GetElapsedTime(_origin).TotalMilliseconds;

    /// <inheritdoc/>
    public void Start(Action onFrame)
    {
        if (_pump is not null) return;
        var pump = new Pump(this, onFrame, SynchronizationContext.Current);
        _pump = pump;
        pump.Start();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        var pump = _pump;
        if (pump is null) return;
        _pump = null;
        pump.Join();
    }

    private sealed class Pump
    {
        private readonly StopwatchFrameSource _owner;
        private readonly Action _onFrame;
        private readonly SynchronizationContext? _context;
        private readonly SendOrPostCallback _postedFrame;
        private readonly Thread _thread;
        private int _posted;

        public Pump(StopwatchFrameSource owner, Action onFrame, SynchronizationContext? context)
        {
            _owner = owner;
            _onFrame = onFrame;
            _context = context;
            _postedFrame = PostedFrame;
            _thread = new Thread(Run) { IsBackground = true, Name = "Presto frame pump" };
        }

        public void Start() => _thread.Start();

        public void Join()
        {
            if (_thread != Thread.CurrentThread) _thread.Join();
        }

        private void Run()
        {
            // Comparing against the owner's field lets a Stop from inside the callback followed by a Start retire this loop instead of doubling the pump.
            while (_owner._pump == this)
            {
                if (_context is null) _onFrame();
                // One frame in flight at a time, so a busy context thread never accumulates a backlog of stale frames.
                else if (Interlocked.Exchange(ref _posted, 1) == 0) _context.Post(_postedFrame, null);
                Thread.Sleep(1);
            }
        }

        private void PostedFrame(object? _)
        {
            if (_owner._pump == this) _onFrame();
            Volatile.Write(ref _posted, 0);
        }
    }
}
