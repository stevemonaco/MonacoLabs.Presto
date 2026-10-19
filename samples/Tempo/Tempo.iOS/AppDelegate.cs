using Avalonia;
using Avalonia.iOS;
using Foundation;
using MonacoLabs.Presto;

namespace Tempo.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<Tempo.App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .UsePresto()
            .WithInterFont();
}
