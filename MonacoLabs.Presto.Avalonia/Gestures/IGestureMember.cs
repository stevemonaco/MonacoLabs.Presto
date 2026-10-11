using Avalonia;
using Avalonia.Input;

namespace MonacoLabs.Presto.Gestures;

/// <summary>A gesture in a <see cref="GestureArena"/>, told whether it won the press, lost it, or saw it end without a claim.</summary>
internal interface IGestureMember
{
    /// <summary>The element whose depth breaks a tie: the deepest wins.</summary>
    Visual Element { get; }

    /// <summary>What the arena captures the pointer to when this member wins.</summary>
    IInputElement CaptureTarget { get; }

    void Won();

    void Lost();

    /// <summary>The pointer was released before any member claimed it: a tap.</summary>
    void EndedWithoutClaim() => Lost();
}
