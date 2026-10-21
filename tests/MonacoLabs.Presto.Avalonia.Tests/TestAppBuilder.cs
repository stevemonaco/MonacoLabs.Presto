using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace MonacoLabs.Presto.Tests;

public static class TestAppBuilder
{
    /// <summary>
    /// Skia replaces the headless drawing stub, whose geometries report a <c>ContourLength</c> of 0. <c>Fps = 1</c> all but stops the
    /// headless render timer, which otherwise renders again inside <c>RunJobs</c> whenever a frame outlasts 16 ms of wall time.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { Fps = 1, UseHeadlessDrawing = false });
}

/// <summary>Carries the Fluent theme, without which templated controls such as <c>ScrollViewer</c> never measure their parts.</summary>
internal sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
