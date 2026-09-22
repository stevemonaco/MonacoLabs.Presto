namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Initializes the bridge, then resets the engine exactly as <see cref="Tests.EngineTestBase"/> does so tests tick deterministically.</summary>
public abstract class BridgeTestBase : Monaco.Presto.Tests.EngineTestBase
{
    protected BridgeTestBase()
    {
        PrestoAvalonia.EnsureInitialized();
        Presto.Engine.FrameSource = Frames; // EnsureInitialized installs the Avalonia source over the base's manual one
    }
}
