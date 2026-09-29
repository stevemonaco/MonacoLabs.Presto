using Avalonia;
using Avalonia.Controls;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// State motion: while a condition holds on an element, the properties its builder names go to its values, and when it stops
/// holding they return to the next state's values or to rest. Later states win the properties they share, in the order
/// <see cref="HoverProperty"/>, <see cref="FocusProperty"/>, <see cref="PressProperty"/>, then <see cref="StatesProperty"/>.
/// </summary>
/// <remarks>
/// A state that already holds when the element attaches is applied instantly rather than played, and detaching reverts
/// every state animation. State animations compose by replacement, so each takes its properties over from where they stand.
/// </remarks>
public static class Motion
{
    /// <summary>
    /// The builder played while the pointer is over the element (<c>:pointerover</c>). Once entered, hover lasts until the
    /// pointer leaves the element's layout slot, so a state that moves the element does not flicker at its edge.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> HoverProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Hover", typeof(Motion));

    /// <summary>
    /// The builder played while the left button, a touch or a pen presses the element, until release or capture loss; it wins
    /// over hover and focus. Presto synthesises this <c>:pressed</c> for every element, not only buttons.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> PressProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Press", typeof(Motion));

    /// <summary>The builder played while the element has keyboard focus (<c>:focus-visible</c>); a state <c>When=":focus"</c> also plays on a click.</summary>
    public static readonly AttachedProperty<AnimationBuilder?> FocusProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Focus", typeof(Motion));

    /// <summary>The element's conditional states, after the hover, focus and press sugar and in declaration order.</summary>
    public static readonly AttachedProperty<MotionStates?> StatesProperty =
        AvaloniaProperty.RegisterAttached<Control, MotionStates?>("States", typeof(Motion));

    /// <summary>
    /// Named conditions: the whitespace- or comma-separated tokens of the value's <see cref="object.ToString"/>, so an enum
    /// matches by name. A style setter here brings any selector into a state's <see cref="MotionState.When"/>.
    /// </summary>
    public static readonly AttachedProperty<object?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("State", typeof(Motion));

    private static readonly AttachedProperty<MotionTracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, MotionTracker?>("Tracker", typeof(Motion));

    static Motion()
    {
        HoverProperty.Changed.AddClassHandler<Control>(HandleChanged);
        PressProperty.Changed.AddClassHandler<Control>(HandleChanged);
        FocusProperty.Changed.AddClassHandler<Control>(HandleChanged);
        StatesProperty.Changed.AddClassHandler<Control>(HandleChanged);
        StateProperty.Changed.AddClassHandler<Control>((control, _) => control.GetValue(TrackerProperty)?.ReadState());
    }

    /// <summary>Gets the builder played while the pointer is over <paramref name="control"/>.</summary>
    public static AnimationBuilder? GetHover(Control control) => control.GetValue(HoverProperty);

    /// <summary>Sets the builder played while the pointer is over <paramref name="control"/>.</summary>
    public static void SetHover(Control control, AnimationBuilder? value) => control.SetValue(HoverProperty, value);

    /// <summary>Gets the builder played while <paramref name="control"/> is pressed.</summary>
    public static AnimationBuilder? GetPress(Control control) => control.GetValue(PressProperty);

    /// <summary>Sets the builder played while <paramref name="control"/> is pressed.</summary>
    public static void SetPress(Control control, AnimationBuilder? value) => control.SetValue(PressProperty, value);

    /// <summary>Gets the builder played while <paramref name="control"/> has keyboard focus.</summary>
    public static AnimationBuilder? GetFocus(Control control) => control.GetValue(FocusProperty);

    /// <summary>Sets the builder played while <paramref name="control"/> has keyboard focus.</summary>
    public static void SetFocus(Control control, AnimationBuilder? value) => control.SetValue(FocusProperty, value);

    /// <summary>Gets the conditional states of <paramref name="control"/>.</summary>
    public static MotionStates? GetStates(Control control) => control.GetValue(StatesProperty);

    /// <summary>Sets the conditional states of <paramref name="control"/>.</summary>
    public static void SetStates(Control control, MotionStates? value) => control.SetValue(StatesProperty, value);

    /// <summary>Gets the value whose tokens name the conditions that hold on <paramref name="control"/>.</summary>
    public static object? GetState(Control control) => control.GetValue(StateProperty);

    /// <summary>Sets the value whose tokens name the conditions that hold on <paramref name="control"/>.</summary>
    public static void SetState(Control control, object? value) => control.SetValue(StateProperty, value);

    /// <summary>Adds <paramref name="state"/> after every other state of <paramref name="control"/>, until the returned handle is disposed.</summary>
    internal static IDisposable Add(Control control, MotionState state)
    {
        var tracker = control.GetValue(TrackerProperty);
        if (tracker is null) control.SetValue(TrackerProperty, tracker = new MotionTracker(control));
        return new Handle(tracker, tracker.Add(state));
    }

    private static void HandleChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var tracker = control.GetValue(TrackerProperty);
        if (GetHover(control) is null && GetPress(control) is null && GetFocus(control) is null && GetStates(control) is null
            && tracker?.HasAdded != true)
        {
            tracker?.Release();
            control.ClearValue(TrackerProperty);
        }
        else if (tracker is null)
        {
            control.SetValue(TrackerProperty, new MotionTracker(control));
        }
        else
        {
            tracker.Rebuild();
        }
    }

    /// <summary>Removes a code-added state; the tracker ignores an entry it no longer holds, so a second dispose does nothing.</summary>
    private sealed class Handle(MotionTracker tracker, MotionTracker.Entry entry) : IDisposable
    {
        public void Dispose() => tracker.Remove(entry);
    }
}
