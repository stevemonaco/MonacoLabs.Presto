using Avalonia;

namespace Monaco.Presto.AvaloniaBridge;

public static class AnimateExtensions
{
    /// <summary>Animates this object's properties, installing the bridge first when nothing else has.</summary>
    public static Animation Animate(this AvaloniaObject target, AvaloniaAnimationParams parameters)
    {
        PrestoAvalonia.EnsureInitialized();
        return Presto.Animate(target, parameters);
    }
}
