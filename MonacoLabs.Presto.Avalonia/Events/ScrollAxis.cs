namespace MonacoLabs.Presto.Events;

/// <summary>The axis a <see cref="ScrollObserver"/> watches, upstream's <c>axis: 'x' | 'y'</c>.</summary>
public enum ScrollAxis
{
    /// <summary>The scroller's vertical offset, the default (upstream <c>'y'</c>).</summary>
    Vertical,

    /// <summary>The scroller's horizontal offset (upstream <c>'x'</c>).</summary>
    Horizontal,
}
