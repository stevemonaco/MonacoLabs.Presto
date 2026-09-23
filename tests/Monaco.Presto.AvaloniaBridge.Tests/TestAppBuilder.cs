using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public static class TestAppBuilder
{
    /// <summary>Skia replaces the headless drawing stub, whose geometries report a <c>ContourLength</c> of 0.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Carries the Fluent theme, without which templated controls such as <c>ScrollViewer</c> never measure their parts.</summary>
internal sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
