namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>How <see cref="ParticleFormation.Retarget"/> sends the particles to their new points.</summary>
public enum FormationTransition
{
    /// <summary>Each point takes the nearest particle, so the shape reshuffles with the least travel.</summary>
    Nearest,

    /// <summary>Nearest pairing, with particles setting off left to right across the formation.</summary>
    Sweep,

    /// <summary>Nearest pairing, with every particle first thrown outward from the formation's center.</summary>
    Burst,

    /// <summary>Seeded random pairing, so particles cross the formation to their points.</summary>
    Swarm,
}
