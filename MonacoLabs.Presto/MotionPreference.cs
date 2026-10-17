namespace MonacoLabs.Presto;

/// <summary>Names where an app's reduced-motion flag comes from: the operating system, or a fixed choice. The core never acts on it.</summary>
public enum MotionPreference
{
    /// <summary>Follow the operating system's setting.</summary>
    System,

    /// <summary>Play full motion.</summary>
    Full,

    /// <summary>Play reduced motion.</summary>
    Reduced,
}
