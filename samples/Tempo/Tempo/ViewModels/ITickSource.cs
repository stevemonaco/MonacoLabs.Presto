namespace Tempo.ViewModels;

/// <summary>The periodic refresh that re-reads the wall clock; a <see cref="DispatcherTickSource"/> in the app, driven by hand in tests.</summary>
public interface ITickSource
{
    event EventHandler? Tick;
}
