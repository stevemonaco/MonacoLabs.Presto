namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// The names of the bridge's shorthand properties, for the string indexer of params, keyframes and
/// <see cref="Animatable"/> (<c>[Shorthands.X] = 320</c>) so a typo fails to compile rather than at run time.
/// </summary>
public static class Shorthands
{
    /// <summary>Horizontal offset in DIPs, composed into the render transform's translation.</summary>
    public const string X = "X";
    /// <summary>Vertical offset in DIPs, composed into the render transform's translation.</summary>
    public const string Y = "Y";
    /// <summary>Rotation in degrees (<c>turn</c> / <c>rad</c> accepted), composed into the render transform.</summary>
    public const string Rotate = "Rotate";
    /// <summary>Uniform scale factor, composed into the render transform as both axes.</summary>
    public const string Scale = "Scale";
    /// <summary>Horizontal scale factor, composed into the render transform.</summary>
    public const string ScaleX = "ScaleX";
    /// <summary>Vertical scale factor, composed into the render transform.</summary>
    public const string ScaleY = "ScaleY";
    /// <summary>Horizontal skew in degrees, composed into the render transform.</summary>
    public const string SkewX = "SkewX";
    /// <summary>Vertical skew in degrees, composed into the render transform.</summary>
    public const string SkewY = "SkewY";
    /// <summary>Rotation about the horizontal axis in degrees, composed into a <c>Rotate3DTransform.AngleX</c>.</summary>
    public const string RotateX = "RotateX";
    /// <summary>Rotation about the vertical axis in degrees, composed into a <c>Rotate3DTransform.AngleY</c>.</summary>
    public const string RotateY = "RotateY";
    /// <summary>Perspective depth in DIPs (0 is none), composed into a <c>Rotate3DTransform.Depth</c>.</summary>
    public const string Perspective = "Perspective";

    /// <summary>Blur radius in DIPs, composed into the visual's <c>BlurEffect</c>.</summary>
    public const string Blur = "Blur";
    /// <summary>Shadow offset X in DIPs, composed into the visual's <c>DropShadowEffect</c>.</summary>
    public const string ShadowX = "ShadowX";
    /// <summary>Shadow offset Y in DIPs, composed into the visual's <c>DropShadowEffect</c>.</summary>
    public const string ShadowY = "ShadowY";
    /// <summary>Shadow blur radius in DIPs, composed into the visual's <c>DropShadowEffect</c>.</summary>
    public const string ShadowBlur = "ShadowBlur";
    /// <summary>Shadow color, composed into the visual's <c>DropShadowEffect</c>.</summary>
    public const string ShadowColor = "ShadowColor";
    /// <summary>Shadow opacity, 0 to 1, composed into the visual's <c>DropShadowEffect</c>.</summary>
    public const string ShadowOpacity = "ShadowOpacity";

    /// <summary>Inset in <c>Thickness</c> order (DIPs or <c>%</c> of the bounds), composed into a rectangular <c>Visual.Clip</c>.</summary>
    public const string ClipInset = "ClipInset";
    /// <summary>Radius fraction (1 reaches the farthest corner), composed into an elliptical <c>Visual.Clip</c>.</summary>
    public const string ClipCircle = "ClipCircle";
    /// <summary>The <see cref="ClipCircle"/> center as a <c>RelativePoint</c>, composed into the same clip.</summary>
    public const string ClipCircleCenter = "ClipCircleCenter";

    /// <summary>Sweep progress, 0 hidden to 1 shown, composed into a gradient <c>Visual.OpacityMask</c>.</summary>
    public const string MaskSweep = "MaskSweep";
    /// <summary>Sweep direction in degrees (0 is left to right), composed into the <see cref="MaskSweep"/> mask.</summary>
    public const string MaskSweepAngle = "MaskSweepAngle";
    /// <summary>Soft edge width as a fraction of the sweep, composed into the <see cref="MaskSweep"/> mask.</summary>
    public const string MaskSweepFeather = "MaskSweepFeather";

    /// <summary>A <c>Shape</c>'s drawn stroke as <c>"start end"</c> fractions, composed into its dash array and offset.</summary>
    public const string Draw = "Draw";
    /// <summary>A <c>ScrollViewer</c>'s horizontal offset in DIPs.</summary>
    public const string ScrollX = "ScrollX";
    /// <summary>A <c>ScrollViewer</c>'s vertical offset in DIPs.</summary>
    public const string ScrollY = "ScrollY";
}
