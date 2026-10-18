namespace MonacoLabs.Presto.Controls;

/// <summary>When a <see cref="Marquee"/> scrolls.</summary>
public enum MarqueeMode
{
    /// <summary>Always, whether or not the content fits.</summary>
    Always,

    /// <summary>Only while the content is longer than the marquee; content that fits is shown once, aligned.</summary>
    WhenOverflowing,
}
