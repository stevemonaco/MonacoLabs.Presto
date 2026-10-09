using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Monaco.Presto.AvaloniaBridge.Platform;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The headless platform has no touch, so the gesture events are raised directly, as the scroll recognizer raises them.</summary>
public sealed class InertiaWakeTests : BridgeTestBase
{
    private static void RaiseInertiaStarting(InputElement target, int id) =>
        target.RaiseEvent((ScrollGestureInertiaStartingEventArgs)Activator.CreateInstance(
            typeof(ScrollGestureInertiaStartingEventArgs), BindingFlags.Instance | BindingFlags.NonPublic, null, [id, new Vector(0, 2000)], null)!);

    [AvaloniaFact]
    public void ACoastingScrollWakesTheUIThreadUntilItsGestureEnds()
    {
        var box = Box();
        Show(box);
        var id = ScrollGestureEventArgs.GetNextFreeId();

        RaiseInertiaStarting(box, id);
        Assert.True(InertiaWake.Waking);

        box.RaiseEvent(new ScrollGestureEndedEventArgs(id));
        Assert.False(InertiaWake.Waking);
    }

    [AvaloniaFact]
    public void OverlappingGesturesWakeUntilTheLastEnds()
    {
        var box = Box();
        Show(box);
        var first = ScrollGestureEventArgs.GetNextFreeId();
        var second = ScrollGestureEventArgs.GetNextFreeId();

        RaiseInertiaStarting(box, first);
        RaiseInertiaStarting(box, second);
        box.RaiseEvent(new ScrollGestureEndedEventArgs(first));
        Assert.True(InertiaWake.Waking);

        box.RaiseEvent(new ScrollGestureEndedEventArgs(second));
        Assert.False(InertiaWake.Waking);
    }
}
