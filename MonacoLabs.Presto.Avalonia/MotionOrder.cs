namespace MonacoLabs.Presto;

/// <summary>When an element's own state animations play relative to those of the descendants following its <see cref="Motion.StateProperty"/>.</summary>
public enum MotionOrder
{
    /// <summary>The element and its descendants start together.</summary>
    Together,

    /// <summary>The descendants start once the element's own animations end.</summary>
    BeforeChildren,

    /// <summary>The element starts once the last descendant's animation ends.</summary>
    AfterChildren,
}
