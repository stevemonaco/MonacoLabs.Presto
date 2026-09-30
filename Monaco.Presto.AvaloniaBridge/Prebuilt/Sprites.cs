namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>Frame-by-frame recipes for <see cref="SpriteSheet"/> and <see cref="ImageSequence"/>.</summary>
public static class Sprites
{
    /// <summary>
    /// Plays each target's frames at <paramref name="fps"/>, as <see cref="SpriteSheet.Play"/> does, reading the frame count
    /// per target; any other target counts as one frame. Reduced: holds frame 0.
    /// </summary>
    public static AnimationBuilder PlayFrames(double fps = 24, bool loop = true, bool reverse = false, double? delay = null)
    {
        var builder = Presto.Build()
            .Property("Frame", to: (FunctionValue)((target, _, _) => FrameCount(target)), from: 0)
            .Duration((FunctionValue)((target, _, _) => FrameCount(target) * 1000 / fps))
            .Ease(Eases.None)
            .Loop(loop)
            .Reversed(reverse)
            .WhenReduced(reduced => reduced.Loop(false).Reversed(false).Property("Frame", 0).Duration(0));
        return delay is { } ms ? builder.Delay(ms) : builder;
    }

    private static int FrameCount(object target) => target switch
    {
        SpriteSheet sheet => sheet.EffectiveFrameCount,
        ImageSequence sequence => sequence.FrameCount,
        _ => 1,
    };
}
