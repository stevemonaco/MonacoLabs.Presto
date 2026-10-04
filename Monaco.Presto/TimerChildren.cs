namespace Monaco.Presto;

/// <summary>A timeline's children in playback order, for <c>foreach</c> without allocating; see <see cref="Timer.Children"/>.</summary>
public readonly struct TimerChildren(Timer timer)
{
    /// <summary>Starts at the first child.</summary>
    public Enumerator GetEnumerator() => new(timer._head);

    /// <summary>Walks the child list from its head.</summary>
    public struct Enumerator(Timer? head)
    {
        private Timer? _next = head;

        /// <summary>The child at the current position.</summary>
        public Timer Current { get; private set; } = null!;

        /// <summary>Advances to the next child, returning false past the last one.</summary>
        public bool MoveNext()
        {
            if (_next == null) return false;
            Current = _next;
            _next = _next._next;
            return true;
        }
    }
}
