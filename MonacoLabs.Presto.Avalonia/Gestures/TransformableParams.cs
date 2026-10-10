using Avalonia.Controls;

namespace MonacoLabs.Presto.Gestures;

/// <summary>Parameters for <see cref="PrestoAvalonia.CreateTransformable"/>.</summary>
public sealed record TransformableParams
{
    /// <summary>The control the pointers grab, when it is not the transformed control itself.</summary>
    public Control? Trigger { get; init; }

    /// <summary>The control whose box the pan is bounded by, the target's parent by default.</summary>
    public Control? Container { get; init; }

    /// <summary>Whether the pointers pan the target, true by default.</summary>
    public bool Pan { get; init; } = true;

    /// <summary>Whether two fingers zoom the target, true by default.</summary>
    public bool Zoom { get; init; } = true;

    /// <summary>Whether two fingers rotate the target.</summary>
    public bool Rotate { get; init; }

    /// <summary>Whether one pointer pans at <see cref="MinScale"/>; off by default, so an enclosing scroller or carousel still gets the swipe.</summary>
    public bool PanAtRest { get; init; }

    /// <summary>How far the scaled, rotated content may pan within <see cref="Container"/>, <see cref="Gestures.PanBounds.Contain"/> by default.</summary>
    public PanBounds PanBounds { get; init; } = PanBounds.Contain;

    /// <summary>The smallest scale the target rests at, 1 by default.</summary>
    public double MinScale { get; init; } = 1;

    /// <summary>The largest scale the target rests at, 4 by default.</summary>
    public double MaxScale { get; init; } = 4;

    /// <summary>How much of a gesture past the scale limits and pan bounds is kept, .35 by default; 0 pins the target to the limit.</summary>
    public double ScaleFriction { get; init; } = 0.35;

    /// <summary>The angle, in degrees, a released rotation lands on a multiple of; 0 for none.</summary>
    public double RotationSnap { get; init; }

    /// <summary>The scale a double-tap zooms to about the tap point, 2.5 by default; null disables double-tap.</summary>
    public double? DoubleTapScale { get; init; } = 2.5;

    /// <summary>When the mouse wheel zooms about the cursor, <see cref="Gestures.WheelZoom.WithControl"/> by default.</summary>
    public WheelZoom WheelZoom { get; init; } = WheelZoom.WithControl;

    /// <summary>The scale factor per wheel notch, 1.15 by default.</summary>
    public double WheelZoomStep { get; init; } = 1.15;

    /// <summary>The template the release coasts pan and rotation with; null for the default inertia.</summary>
    public Inertia? ReleaseInertia { get; init; }

    /// <summary>The animation zooms, resets and the spring-back to the scale limits play through.</summary>
    public AnimationBuilder? Settle { get; init; }

    /// <summary>Runs when the pointers take hold of the target.</summary>
    public Action<Transformable>? OnGrab { get; init; }

    /// <summary>Runs on every write of the target's transform.</summary>
    public Action<Transformable>? OnUpdate { get; init; }

    /// <summary>Runs when the last pointer lets go.</summary>
    public Action<Transformable>? OnRelease { get; init; }

    /// <summary>Runs once the transform has come to rest after a release or anything played through <see cref="Settle"/>.</summary>
    public Action<Transformable>? OnSettle { get; init; }
}
