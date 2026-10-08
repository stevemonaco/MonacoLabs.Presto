namespace Monaco.Presto.AvaloniaBridge;

/// <summary>The bridge's own settings on an <see cref="AnimationBuilder"/>.</summary>
public static class AnimationBuilderExtensions
{
    /// <inheritdoc cref="AvaloniaAnimationParams.WhenDetached"/>
    public static AnimationBuilder WhenDetached(this AnimationBuilder builder, WhenDetached whenDetached) => builder.HostLifetime(whenDetached);
}
