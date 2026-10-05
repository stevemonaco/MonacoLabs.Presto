using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Monaco.Presto.AvaloniaBridge.Text;

/// <summary>
/// A trigger's animation played on the split pieces <see cref="TextReveal.PiecesProperty"/> asks for: the block is held
/// transparent until its first split, the animation is rebuilt over every re-split and the split is reverted when it completes.
/// </summary>
internal sealed class PieceTargets : IRevertible
{
    private IDisposable? _hold;
    private TextSplitter? _split;

    private PieceTargets(TextBlock block, TextPieces pieces, AnimationParams parameters, bool exit)
    {
        if (!exit)
        {
            _hold = block.SetValue(Visual.OpacityProperty, 0d, BindingPriority.Animation);
            var completed = parameters.OnComplete;
            parameters = parameters with
            {
                OnComplete = timer =>
                {
                    completed?.Invoke(timer);
                    Revert();
                },
            };
        }
        var mask = TextReveal.GetMask(block);
        _split = PrestoAvalonia.SplitText(block, pieces switch
        {
            TextPieces.Lines => new() { Lines = new() { Wrap = mask }, Words = null },
            TextPieces.Words => new() { Words = new() { Wrap = mask } },
            _ => new() { Words = null, Chars = new() { Wrap = mask } },
        });
        _split.AddEffect(split =>
        {
            var animation = Presto.Animate(pieces switch
            {
                TextPieces.Lines => split.Lines,
                TextPieces.Words => split.Words,
                _ => split.Chars,
            }, parameters);
            // A re-split reverts the animation it replaces before this one exists.
            animation._onRevert += _ =>
            {
                if (Animation == animation) Animation = null;
            };
            Animation = animation;
            _hold?.Dispose();
            _hold = null;
            return animation;
        });
    }

    /// <summary>The animation over the current pieces, rebuilt on every re-split; null once reverted.</summary>
    public Animation? Animation { get; private set; }

    /// <summary>
    /// Plays <paramref name="parameters"/> on <paramref name="target"/>'s pieces; null when it is not a <see cref="TextBlock"/> asking
    /// for pieces, or the parameters are a reduced form. An exit splits at once with no hold and keeps the split until <see cref="Revert"/>.
    /// </summary>
    public static PieceTargets? Play(Visual target, AnimationParams parameters, bool exit = false) =>
        target is TextBlock block && TextReveal.GetPieces(block) is var pieces and not TextPieces.None && !parameters.IsReducedMotion
            ? new PieceTargets(block, pieces, parameters, exit)
            : null;

    public void Revert()
    {
        _hold?.Dispose();
        _hold = null;
        _split?.Revert();
        _split = null;
    }
}
