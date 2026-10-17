using System;
using Avalonia;
using Avalonia.Logging;
using MonacoLabs.Presto;

namespace PrestoGallery.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // A WinExe has no console of its own, so this output is seen only when the caller redirects it.
        if (!LaunchOptions.TryParse(args, out var options, out var listPages, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(LaunchOptions.Usage);
            return 2;
        }
        if (listPages)
        {
            foreach (var line in LaunchOptions.PageList()) Console.WriteLine(line);
            return 0;
        }
        App.Launch = options;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UsePresto()
            .WithInterFont()
#if DEBUG
            .LogToTrace();
#else
            .LogToTextWriter(Console.Error, LogEventLevel.Warning);
#endif
}
