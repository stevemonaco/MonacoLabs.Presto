using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;
using MonacoLabs.Presto;

namespace Tempo.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App.Storage = new BrowserTempoStorage();
        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePresto();

    [JSExport]
    internal static Task Flush() => App.FlushAsync();
}
