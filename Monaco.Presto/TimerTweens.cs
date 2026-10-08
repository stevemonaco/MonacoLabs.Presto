namespace Monaco.Presto;

/// <summary>A timer's tweens in order, for <c>foreach</c> without allocating; see <see cref="Timer.Tweens"/>.</summary>
public readonly struct TimerTweens(Timer timer)
{
    /// <summary>Starts at the first tween.</summary>
    public Enumerator GetEnumerator() => new(timer._headTween);

    /// <summary>Walks the tween list from its head.</summary>
    public struct Enumerator(Tween? head)
    {
        private Tween? _next = head;

        /// <summary>The tween at the current position.</summary>
        public Tween Current { get; private set; } = null!;

        /// <summary>Advances to the next tween, returning false past the last one.</summary>
        public bool MoveNext()
        {
            if (_next == null) return false;
            Current = _next;
            _next = _next._next;
            return true;
        }
    }
}
