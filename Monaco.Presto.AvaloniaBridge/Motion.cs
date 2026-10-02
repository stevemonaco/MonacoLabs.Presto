using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

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
    /// matches by name. A style setter here brings any selector into a state's <see cref="MotionState.When"/>. It also
    /// names the conditions of every visual descendant with states that does not set its own, down to one that does.
    /// </summary>
    public static readonly AttachedProperty<object?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("State", typeof(Motion));

    /// <summary>
    /// Milliseconds between the state animations of consecutive descendants following this element's <see cref="StateProperty"/>,
    /// ordered top to bottom, then left to right, by where they are laid out.
    /// </summary>
    public static readonly AttachedProperty<double> StaggerChildrenProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("StaggerChildren", typeof(Motion));

    /// <summary>Where <see cref="StaggerChildrenProperty"/> measures from; the default is the first descendant.</summary>
    public static readonly AttachedProperty<StaggerFrom> StaggerFromProperty =
        AvaloniaProperty.RegisterAttached<Control, StaggerFrom>("StaggerFrom", typeof(Motion));

    /// <summary>Milliseconds added to the state animations of every descendant following this element's <see cref="StateProperty"/>.</summary>
    public static readonly AttachedProperty<double> DelayChildrenProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("DelayChildren", typeof(Motion));

    /// <summary>Whether this element's own state animations play with, before or after those of its following descendants.</summary>
    public static readonly AttachedProperty<MotionOrder> OrderProperty =
        AvaloniaProperty.RegisterAttached<Control, MotionOrder>("Order", typeof(Motion));

    private static readonly AttachedProperty<MotionTracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, MotionTracker?>("Tracker", typeof(Motion));

    static Motion()
    {
        HoverProperty.Changed.AddClassHandler<Control>(HandleChanged);
        PressProperty.Changed.AddClassHandler<Control>(HandleChanged);
        FocusProperty.Changed.AddClassHandler<Control>(HandleChanged);
        StatesProperty.Changed.AddClassHandler<Control>(HandleChanged);
        StateProperty.Changed.AddClassHandler<Control>(HandleStateChanged);
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

    /// <summary>Gets the milliseconds between the state animations of consecutive descendants following <paramref name="control"/>.</summary>
    public static double GetStaggerChildren(Control control) => control.GetValue(StaggerChildrenProperty);

    /// <summary>Sets the milliseconds between the state animations of consecutive descendants following <paramref name="control"/>.</summary>
    public static void SetStaggerChildren(Control control, double value) => control.SetValue(StaggerChildrenProperty, value);

    /// <summary>Gets where the stagger of <paramref name="control"/>'s descendants measures from.</summary>
    public static StaggerFrom GetStaggerFrom(Control control) => control.GetValue(StaggerFromProperty);

    /// <summary>Sets where the stagger of <paramref name="control"/>'s descendants measures from.</summary>
    public static void SetStaggerFrom(Control control, StaggerFrom value) => control.SetValue(StaggerFromProperty, value);

    /// <summary>Gets the milliseconds added to the state animations of the descendants following <paramref name="control"/>.</summary>
    public static double GetDelayChildren(Control control) => control.GetValue(DelayChildrenProperty);

    /// <summary>Sets the milliseconds added to the state animations of the descendants following <paramref name="control"/>.</summary>
    public static void SetDelayChildren(Control control, double value) => control.SetValue(DelayChildrenProperty, value);

    /// <summary>Gets when <paramref name="control"/>'s own state animations play relative to its descendants'.</summary>
    public static MotionOrder GetOrder(Control control) => control.GetValue(OrderProperty);

    /// <summary>Sets when <paramref name="control"/>'s own state animations play relative to its descendants'.</summary>
    public static void SetOrder(Control control, MotionOrder value) => control.SetValue(OrderProperty, value);

    /// <summary>Adds <paramref name="state"/> after every other state of <paramref name="control"/>, until the returned handle is disposed.</summary>
    internal static IDisposable Add(Control control, MotionState state)
    {
        var tracker = control.GetValue(TrackerProperty);
        if (tracker is null) control.SetValue(TrackerProperty, tracker = new MotionTracker(control));
        return new Handle(tracker, tracker.Add(state));
    }

    private static void HandleStateChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        var own = control.GetValue(TrackerProperty);
        own?.ResolveSource();
        Orchestrate(control, own);
    }

    private static void Orchestrate(Control source, MotionTracker? own)
    {
        var playsOwn = own?.Prepare() == true;
        var children = new List<(MotionTracker Tracker, Point Slot, int Index)>();
        foreach (var descendant in source.GetVisualDescendants())
        {
            if (descendant is not Control control || control.GetValue(TrackerProperty) is not { } tracker) continue;
            // Followers released by clearing the source's state return to rest on its timing too.
            var followed = tracker.Source == source;
            tracker.ResolveSource();
            if ((followed || tracker.Source == source) && tracker.Prepare())
            {
                children.Add((tracker, SlotOrigin(control, source), children.Count));
            }
        }
        children.Sort(static (a, b) =>
            a.Slot.Y != b.Slot.Y ? a.Slot.Y.CompareTo(b.Slot.Y) : a.Slot.X != b.Slot.X ? a.Slot.X.CompareTo(b.Slot.X) : a.Index.CompareTo(b.Index));

        var order = GetOrder(source);
        var delay = GetDelayChildren(source);
        if (playsOwn && order == MotionOrder.BeforeChildren) delay += own!.Play(0);
        var stagger = GetStaggerChildren(source) is var amount and not 0
            ? Presto.Stagger(amount, new StaggerParams { From = GetStaggerFrom(source), Total = children.Count })
            : null;
        var end = 0d;
        for (var i = 0; i < children.Count; i++)
        {
            var offset = stagger?.Invoke(children[i].Tracker, i, [], null).AsDouble ?? 0;
            end = Math.Max(end, children[i].Tracker.Play(delay + offset));
        }
        if (playsOwn && order != MotionOrder.BeforeChildren) own!.Play(order == MotionOrder.AfterChildren ? end : 0);
    }

    // Layout slots, ignoring render transforms.
    private static Point SlotOrigin(Visual visual, Visual ancestor)
    {
        var origin = default(Point);
        for (Visual? current = visual; current != null && current != ancestor; current = current.GetVisualParent()) origin += current.Bounds.Position;
        return origin;
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
