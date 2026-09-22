using Avalonia;
using Avalonia.Headless;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

internal sealed class TestApp : Application;
