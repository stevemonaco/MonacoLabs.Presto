using Avalonia;
using MonacoLabs.Presto;

namespace Tempo;

sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // A WinExe has no console of its own, so this output is seen only when the caller redirects it.
        if (!LaunchOptions.TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(LaunchOptions.Usage);
            return 2;
        }
        App.Launch = options;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UsePresto()
            .WithInterFont()
            .LogToTrace();
}
