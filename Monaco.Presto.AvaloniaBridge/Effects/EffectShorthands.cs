namespace Monaco.Presto.AvaloniaBridge.Effects;

internal static class EffectShorthands
{
    public static EffectShorthand? Parse(string name) => name switch
    {
        Shorthands.Blur => EffectShorthand.Blur,
        Shorthands.ShadowX => EffectShorthand.ShadowX,
        Shorthands.ShadowY => EffectShorthand.ShadowY,
        Shorthands.ShadowBlur => EffectShorthand.ShadowBlur,
        Shorthands.ShadowColor => EffectShorthand.ShadowColor,
        Shorthands.ShadowOpacity => EffectShorthand.ShadowOpacity,
        _ => null,
    };

    public static ClipShorthand? ParseClip(string name) => name switch
    {
        Shorthands.ClipInset => ClipShorthand.ClipInset,
        Shorthands.ClipCircle => ClipShorthand.ClipCircle,
        Shorthands.ClipCircleCenter => ClipShorthand.ClipCircleCenter,
        _ => null,
    };

    public static MaskShorthand? ParseMask(string name) => name switch
    {
        Shorthands.MaskSweep => MaskShorthand.MaskSweep,
        Shorthands.MaskSweepAngle => MaskShorthand.MaskSweepAngle,
        Shorthands.MaskSweepFeather => MaskShorthand.MaskSweepFeather,
        _ => null,
    };
}
