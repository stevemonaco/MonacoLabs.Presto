using Avalonia;

namespace Monaco.Presto.AvaloniaBridge;

public static class AppBuilderExtensions
{
    /// <summary>Installs the bridge once Avalonia has been set up, instead of leaving it to the first use of Presto.</summary>
    public static AppBuilder UsePresto(this AppBuilder builder) => builder.AfterSetup(static _ => PrestoAvalonia.EnsureInitialized());
}
