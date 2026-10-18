namespace MonacoLabs.Presto;

/// <summary>Lets the bridge subscribe to any <see cref="Cue"/> or <see cref="Cue{T}"/> with one object-valued sink.</summary>
internal interface ICueSource
{
    IDisposable Subscribe(Action<object?> sink);
}
