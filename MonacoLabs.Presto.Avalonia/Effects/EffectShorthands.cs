namespace MonacoLabs.Presto.Effects;

internal static class EffectShorthands
{
    public static EffectShorthand? Parse(string name) => name switch
    {
        "Blur" => EffectShorthand.Blur,
        "ShadowX" => EffectShorthand.ShadowX,
        "ShadowY" => EffectShorthand.ShadowY,
        "ShadowBlur" => EffectShorthand.ShadowBlur,
        "ShadowColor" => EffectShorthand.ShadowColor,
        "ShadowOpacity" => EffectShorthand.ShadowOpacity,
        _ => null,
    };

    public static ClipShorthand? ParseClip(string name) => name switch
    {
        "ClipInset" => ClipShorthand.ClipInset,
        "ClipCircle" => ClipShorthand.ClipCircle,
        "ClipCircleCenter" => ClipShorthand.ClipCircleCenter,
        _ => null,
    };

    public static MaskShorthand? ParseMask(string name) => name switch
    {
        "MaskSweep" => MaskShorthand.MaskSweep,
        "MaskSweepAngle" => MaskShorthand.MaskSweepAngle,
        "MaskSweepFeather" => MaskShorthand.MaskSweepFeather,
        _ => null,
    };
}
