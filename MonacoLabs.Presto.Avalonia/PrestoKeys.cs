using Avalonia;
using MonacoLabs.Presto.Controls;
using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto;

/// <summary>
/// Typed keys for the bridge's shorthands and pseudo-properties, the names no <see cref="AvaloniaProperty"/> field spells:
/// <c>[PrestoKeys.X] = 100</c>, or <c>[X] = 100</c> under <c>using static</c>. A real property is keyed by its field, or
/// <see cref="Of"/> where a key value is needed.
/// </summary>
public static class PrestoKeys
{
    /// <summary>The horizontal translation.</summary>
    public static readonly PropertyKey X = "X";

    /// <summary>The vertical translation.</summary>
    public static readonly PropertyKey Y = "Y";

    /// <summary>The rotation, in degrees.</summary>
    public static readonly PropertyKey Rotate = "Rotate";

    /// <summary>The uniform scale.</summary>
    public static readonly PropertyKey Scale = "Scale";

    /// <summary>The horizontal scale.</summary>
    public static readonly PropertyKey ScaleX = "ScaleX";

    /// <summary>The vertical scale.</summary>
    public static readonly PropertyKey ScaleY = "ScaleY";

    /// <summary>The horizontal skew, in degrees.</summary>
    public static readonly PropertyKey SkewX = "SkewX";

    /// <summary>The vertical skew, in degrees.</summary>
    public static readonly PropertyKey SkewY = "SkewY";

    /// <summary>The 3D rotation about the horizontal axis, in degrees.</summary>
    public static readonly PropertyKey RotateX = "RotateX";

    /// <summary>The 3D rotation about the vertical axis, in degrees.</summary>
    public static readonly PropertyKey RotateY = "RotateY";

    /// <summary>The 3D perspective depth.</summary>
    public static readonly PropertyKey Perspective = "Perspective";

    /// <summary>The blur radius.</summary>
    public static readonly PropertyKey Blur = "Blur";

    /// <summary>The drop-shadow horizontal offset.</summary>
    public static readonly PropertyKey ShadowX = "ShadowX";

    /// <summary>The drop-shadow vertical offset.</summary>
    public static readonly PropertyKey ShadowY = "ShadowY";

    /// <summary>The drop-shadow blur radius.</summary>
    public static readonly PropertyKey ShadowBlur = "ShadowBlur";

    /// <summary>The drop-shadow color.</summary>
    public static readonly PropertyKey ShadowColor = "ShadowColor";

    /// <summary>The drop-shadow opacity.</summary>
    public static readonly PropertyKey ShadowOpacity = "ShadowOpacity";

    /// <summary>The inset clip reveal.</summary>
    public static readonly PropertyKey ClipInset = "ClipInset";

    /// <summary>The circular clip reveal radius.</summary>
    public static readonly PropertyKey ClipCircle = "ClipCircle";

    /// <summary>The circular clip reveal center.</summary>
    public static readonly PropertyKey ClipCircleCenter = "ClipCircleCenter";

    /// <summary>The opacity-mask sweep reveal.</summary>
    public static readonly PropertyKey MaskSweep = "MaskSweep";

    /// <summary>The opacity-mask sweep direction.</summary>
    public static readonly PropertyKey MaskSweepAngle = "MaskSweepAngle";

    /// <summary>The opacity-mask sweep soft edge.</summary>
    public static readonly PropertyKey MaskSweepFeather = "MaskSweepFeather";

    /// <summary>The warp of a split <see cref="Avalonia.Controls.TextBlock"/>'s outline pieces.</summary>
    public static readonly PropertyKey Warp = "Warp";

    /// <summary>The warp phase of a split <see cref="Avalonia.Controls.TextBlock"/>.</summary>
    public static readonly PropertyKey WarpPhase = "WarpPhase";

    /// <summary>The warp origin of a split <see cref="Avalonia.Controls.TextBlock"/>.</summary>
    public static readonly PropertyKey WarpOrigin = "WarpOrigin";

    /// <summary>The warp radius of a split <see cref="Avalonia.Controls.TextBlock"/>.</summary>
    public static readonly PropertyKey WarpRadius = "WarpRadius";

    /// <summary>The drawn slice of a <see cref="Avalonia.Controls.Shapes.Shape"/>'s stroke.</summary>
    public static readonly PropertyKey Draw = "Draw";

    /// <summary>A <see cref="Avalonia.Controls.ScrollViewer"/>'s horizontal offset.</summary>
    public static readonly PropertyKey ScrollX = "ScrollX";

    /// <summary>A <see cref="Avalonia.Controls.ScrollViewer"/>'s vertical offset.</summary>
    public static readonly PropertyKey ScrollY = "ScrollY";

    /// <summary>The text, as the builder's <c>Text</c> methods write it.</summary>
    public static readonly PropertyKey Text = "Text";

    /// <summary>A path's geometry, as the builder's <c>Data</c> methods write it.</summary>
    public static readonly PropertyKey Data = "Data";

    /// <summary>The key the <see cref="AvaloniaProperty"/> indexers and <c>Property(AvaloniaProperty, …)</c> write for <paramref name="property"/>.</summary>
    public static PropertyKey Of(AvaloniaProperty property) => AvaloniaObjectTargetAdapter.Encode(property);
}
