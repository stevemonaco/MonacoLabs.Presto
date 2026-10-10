namespace MonacoLabs.Presto.Controls;

/// <summary>How <see cref="FilteredImage"/> swaps one image for the next.</summary>
public enum ImageTransitionKind
{
    /// <summary>The old image fades out as the new one fades in.</summary>
    Crossfade,

    /// <summary>The new image appears through seeded noise over the old one.</summary>
    Dissolve,

    /// <summary>The old image breaks into blocks that grow to a peak mid-way, then shrink away on the new image.</summary>
    Pixelate,

    /// <summary>A crossfade under a noise displacement that peaks mid-way.</summary>
    Ripple,

    /// <summary>The new image is revealed behind a soft edge sweeping across the old one.</summary>
    Wipe,
}
