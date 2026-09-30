using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Effects;

/// <summary>
/// Tracks which shorthands animate one visual's shared property and keeps its two kinds apart: the first member of
/// <typeparamref name="TShorthand"/> is kind A, <paramref name="firstB"/> through <paramref name="last"/> kind B.
/// <paramref name="conflict"/> ends the message thrown when both kinds would animate one visual at once.
/// </summary>
internal sealed class ExclusiveKinds<TShorthand>(TShorthand firstB, TShorthand last, string conflict)
    where TShorthand : unmanaged, Enum
{
    private TShorthand _claimed;
    // Set from a claim until the first frame, so both kinds in one animation conflict before either is registered.
    private bool _pending;
    private int _animated;

    public bool IsKindB { get; private set; }

    /// <summary>Whether <paramref name="shorthand"/>'s kind is the one currently animated.</summary>
    public bool Shows(TShorthand shorthand) => _animated != 0 && IsKindB == IsB(shorthand);

    public bool Animates(TShorthand shorthand) => (_animated & Bit(shorthand)) != 0;

    /// <summary>Records that an animation is being built for <paramref name="shorthand"/>.</summary>
    /// <exception cref="InvalidOperationException">An animation of the other kind is running on, or being built for, <paramref name="visual"/>.</exception>
    public void Claim(Visual visual, TShorthand shorthand)
    {
        var isB = IsB(shorthand);
        var other = _pending && IsB(_claimed) != isB ? _claimed : Running(visual, !isB);
        if (other is { } running) throw new InvalidOperationException($"{running} and {shorthand} cannot animate one visual at once: {conflict}");

        _claimed = shorthand;
        if (_pending) return;
        _pending = true;
        // A claim no animation renders, such as a Presto.Get, lapses with the current job.
        Dispatcher.UIThread.Post(() => _pending = false);
    }

    /// <summary>Marks <paramref name="shorthand"/> animated; true means its kind starts afresh, dropping the other, so the caller hides and resets.</summary>
    public bool Begin(TShorthand shorthand)
    {
        _pending = false;
        if (Animates(shorthand)) return false;

        var isB = IsB(shorthand);
        var fresh = _animated == 0 || IsKindB != isB;
        if (fresh) IsKindB = isB;
        _animated = (fresh ? 0 : _animated) | Bit(shorthand);
        return fresh;
    }

    /// <summary>Releases <paramref name="shorthand"/>; true when others are still animated.</summary>
    public bool Release(TShorthand shorthand)
    {
        _animated &= ~Bit(shorthand);
        return _animated != 0;
    }

    private static int Index(TShorthand shorthand) => Unsafe.As<TShorthand, int>(ref shorthand);

    private static int Bit(TShorthand shorthand) => 1 << Index(shorthand);

    private bool IsB(TShorthand shorthand) => Index(shorthand) >= Index(firstB);

    private TShorthand? Running(Visual visual, bool kindB)
    {
        var end = kindB ? Index(last) : Index(firstB) - 1;
        for (var index = kindB ? Index(firstB) : 0; index <= end; index++)
        {
            var shorthand = Unsafe.As<int, TShorthand>(ref index);
            if (Presto.GetAnimations(visual, shorthand.ToString()).Count != 0) return shorthand;
        }
        return null;
    }
}
