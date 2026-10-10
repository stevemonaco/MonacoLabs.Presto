namespace MonacoLabs.Presto;

/// <summary>
/// The name of an animated property, as every API that names one takes it. A string converts to a key implicitly, and two
/// keys are equal when their names are ordinally equal; the engine sees only the name.
/// </summary>
public readonly record struct PropertyKey
{
    /// <summary>A key naming <paramref name="name"/>.</summary>
    public PropertyKey(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>The property name, or null for <c>default(PropertyKey)</c>.</summary>
    public string Name { get; }

    /// <summary>A property name, such as <c>"X"</c> or <c>"Opacity"</c>.</summary>
    public static implicit operator PropertyKey(string name) => new(name);

    internal string Unwrap() => Name ?? throw new ArgumentException("A default PropertyKey names no property.");

    /// <summary>The name.</summary>
    public override string ToString() => Name;
}
