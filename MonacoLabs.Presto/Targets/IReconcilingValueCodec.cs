using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Targets;

/// <summary>
/// A codec for values whose shape can differ between a tween's two ends, such as gradients with different stop counts or
/// shadow lists of different lengths, which zero padding would interpolate wrongly.
/// </summary>
public interface IReconcilingValueCodec : IValueCodec
{
    /// <summary>
    /// Claims a string end value before the core parses it, returning false to leave it to the core. Relative values
    /// (<c>"+=10"</c>) never reach it.
    /// </summary>
    bool TryParse(string text, DecomposedValue into);

    /// <summary>
    /// Makes both ends the same kind with the same number of channels, once both are decomposed and relative values
    /// resolved. Either end may still be the number 0 the core decomposes a missing value into.
    /// </summary>
    void Reconcile(DecomposedValue from, DecomposedValue to);
}
