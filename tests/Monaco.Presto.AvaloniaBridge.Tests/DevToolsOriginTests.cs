using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Monaco.Presto.Diagnostics;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class DevToolsOriginTests : BridgeTestBase, IDisposable
{
    private readonly List<Timer> _created = [];

    public DevToolsOriginTests()
    {
        Presto.Engine.Diagnostics.Enabled = true;
        Presto.Engine.Diagnostics.TimerCreated += _created.Add;
    }

    public void Dispose() => Presto.Engine.Diagnostics.TimerCreated -= _created.Add;

    [AvaloniaFact]
    public void ADraggableReleaseAnimationCarriesDraggable()
    {
        var target = Box();
        var window = Show(new Canvas { Children = { target } });
        var draggable = PrestoAvalonia.CreateDraggable(target);

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var i = 1; i <= 3; i++)
        {
            window.MouseMove(new Point(25 + (i * 20), 25));
            Frames.Advance(20);
        }
        window.MouseUp(new Point(85, 25), MouseButton.Left);
        Frames.Advance(16);

        var release = draggable.Animate.Animations["X"];
        Assert.False(release.Paused);
        Assert.Contains(release, _created);
        Assert.All(_created, timer => Assert.Equal("Draggable", timer.Origin));
    }

    [AvaloniaFact]
    public void APageTransitionCarriesPageTransition()
    {
        var (from, to) = (Box(), Box());
        Show(new Panel { Children = { from, to } });
        var transition = new PrestoPageTransition
        {
            Enter = Presto.Build().Opacity(from: 0).Duration(100),
            Exit = Presto.Build().Opacity(0).Duration(100),
            UseCompositor = false,
        };

        _ = transition.Start(from, to, true, CancellationToken.None);

        Assert.NotEmpty(_created);
        Assert.All(_created, timer => Assert.Equal("PageTransition", timer.Origin));
    }
}
