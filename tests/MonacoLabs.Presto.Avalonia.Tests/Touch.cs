using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Synthetic touch for a headless window, which has mouse helpers only: raises pointer events for touch pointers on the
/// element under the point, or on the element a pointer is captured to.
/// </summary>
internal sealed class Touch(Window window)
{
    private readonly Dictionary<int, Pointer> _pointers = [];
    private ulong _timestamp;

    public PointerPressedEventArgs Down(int id, Point point, PointerType type = PointerType.Touch)
    {
        var pointer = _pointers[id] = new Pointer(id, type, _pointers.Count == 0);
        var args = new PointerPressedEventArgs(Target(pointer, point), pointer, window, point, ++_timestamp,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None);
        return Raise(args);
    }

    public PointerEventArgs Move(int id, Point point)
    {
        var pointer = _pointers[id];
        var args = new PointerEventArgs(InputElement.PointerMovedEvent, Target(pointer, point), pointer, window, point, ++_timestamp,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None);
        return Raise(args);
    }

    public PointerReleasedEventArgs Up(int id, Point point)
    {
        _pointers.Remove(id, out var pointer);
        var args = new PointerReleasedEventArgs(Target(pointer!, point), pointer!, window, point, ++_timestamp,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left);
        Raise(args);
        pointer!.Capture(null);
        return args;
    }

    /// <summary>
    /// Drags pointer 1 by <paramref name="by"/> in 20 ms steps of <paramref name="frames"/>, held still before letting go
    /// unless <paramref name="hold"/> is false, so the release carries no throw.
    /// </summary>
    public void Drag(ManualFrameSource frames, Point from, Vector by, int steps = 5, bool hold = true)
    {
        var pressed = Down(1, from);
        // Implicitly, as the platform's touch device captures each touch to what it pressed.
        pressed.Pointer.Capture(pressed.Pointer.Captured ?? (IInputElement)pressed.Source!);
        for (var i = 1; i <= steps; i++)
        {
            frames.Advance(20);
            Move(1, from + (by * i / steps));
        }
        for (var i = 0; hold && i < 4; i++)
        {
            frames.Advance(20);
            Move(1, from + by);
        }
        Up(1, from + by);
    }

    // Hit testing reads the last rendered frame, so one is rendered first, as the headless mouse helpers do.
    private Interactive Target(Pointer pointer, Point point)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (Interactive)(pointer.Captured ?? window.InputHitTest(point) ?? window);
    }

    private static T Raise<T>(T args) where T : RoutedEventArgs
    {
        ((Interactive)args.Source!).RaiseEvent(args);
        return args;
    }
}
