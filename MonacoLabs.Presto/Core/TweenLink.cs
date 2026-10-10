namespace MonacoLabs.Presto.Core;

/// <summary>Which of a tween's three link pairs a <see cref="TweenList"/> operation walks.</summary>
internal enum TweenLink
{
    /// <summary><c>_prev</c>/<c>_next</c>: the tweens of one animation.</summary>
    Parent,

    /// <summary><c>_prevRep</c>/<c>_nextRep</c>: the tweens replacing each other on one target property.</summary>
    Replace,

    /// <summary><c>_prevAdd</c>/<c>_nextAdd</c>: the tweens blending into one target property.</summary>
    Additive,
}
