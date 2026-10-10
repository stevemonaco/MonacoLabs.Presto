using MonacoLabs.Presto;

namespace PrestoSample.Tests;

/// <summary>Sets <see cref="PrestoDefaults.ReducedMotion"/> until disposed, then restores the previous value.</summary>
internal sealed class ReducedMotion : IDisposable
{
    private readonly bool _was = Presto.Defaults.ReducedMotion;

    private ReducedMotion() => Presto.Defaults.ReducedMotion = true;

    public static IDisposable On() => new ReducedMotion();

    public void Dispose() => Presto.Defaults.ReducedMotion = _was;
}
