namespace MonacoLabs.Presto;

/// <summary>Where an <see cref="Eases.Rough"/> ease's jaggedness fades out, after GSAP's RoughEase <c>taper</c>.</summary>
public enum RoughTaper
{
    /// <summary>Full strength throughout.</summary>
    None,

    /// <summary>Weaker towards the start.</summary>
    In,

    /// <summary>Weaker towards the end.</summary>
    Out,

    /// <summary>Weaker towards both ends.</summary>
    Both,
}
