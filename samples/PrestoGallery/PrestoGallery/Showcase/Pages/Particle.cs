using Avalonia.Media;

namespace PrestoGallery.Showcase.Pages;

/// <summary>
/// One dot of the <see cref="ParticleFieldScene"/>. Nothing below the canvas is an Avalonia object: this is a plain
/// class the core's reflection adapter drives, <see cref="Tint"/> included, through the bridge's <see cref="Color"/> codec.
/// </summary>
public sealed class Particle
{
    /// <summary>The center of the grid cell this particle belongs to, in canvas pixels, as the scene laid the field out.</summary>
    public double HomeX { get; set; }

    /// <inheritdoc cref="HomeX"/>
    public double HomeY { get; set; }

    /// <summary>The offset from <see cref="HomeX"/> the drift and the bursts tween.</summary>
    public double X { get; set; }

    /// <inheritdoc cref="X"/>
    public double Y { get; set; }

    /// <summary>The drawn radius, in canvas pixels.</summary>
    public double Size { get; set; }

    public double Opacity { get; set; }

    public Color Tint { get; set; }

    /// <summary>The pointer displacement, damped each frame by the scene rather than tweened, and added in when drawing.</summary>
    public double PushX { get; set; }

    /// <inheritdoc cref="PushX"/>
    public double PushY { get; set; }
}
