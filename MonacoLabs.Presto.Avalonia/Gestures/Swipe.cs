using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace MonacoLabs.Presto.Gestures;

/// <summary>
/// Swipe actions on a row, as SwiftUI's <c>swipeActions</c> and Flutter's <c>Dismissible</c>: a horizontal drag slides the
/// element aside to reveal its <see cref="LeadingProperty"/> or <see cref="TrailingProperty"/> actions behind it, a partial
/// reveal coasts open or closed, and a swipe released past half the element's width commits through its side's command
/// (<see cref="LeadingCommandProperty"/>, <see cref="TrailingCommandProperty"/> or <see cref="CommandProperty"/>) and, with <see cref="DismissProperty"/>, slides the element away.
/// </summary>
/// <remarks>
/// <para>
/// Set it on an item container or on any control that does not clip to its bounds; the actions are drawn in the strip the
/// element uncovers, as wide as it, and open as wide as they measure. A drag locks to one axis once it passes the drag
/// threshold, so a vertical one, such as a touch scroll, passes through to the ancestor <see cref="ScrollViewer"/>. Past
/// the ends it rubber-bands. One element per list is open at a time; pressing anywhere outside it, tapping it, or Escape
/// closes it.
/// </para>
/// <para>
/// The actions are reachable without swiping: a context request on the element, the context-menu key or Shift+F10 with it
/// focused, or a right-click, opens its trailing actions, then its leading ones, then closes it, and a keyboard request
/// focuses the first action. An element with its own <see cref="Control.ContextMenu"/> or <see cref="Control.ContextFlyout"/>
/// keeps it instead. A full swipe is a shortcut, so offer what it commits as an action too.
/// </para>
/// <para>Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each release, the element lands at once and a dismiss commits at once.</para>
/// </remarks>
public static class Swipe
{
    /// <summary>
    /// The actions revealed by a drag towards the right: a <see cref="Control"/> for one element, or an
    /// <see cref="IDataTemplate"/> built with its data context, which a style can share.
    /// </summary>
    public static readonly AttachedProperty<object?> LeadingProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("Leading", typeof(Swipe));

    /// <summary>The actions revealed by a drag towards the left; see <see cref="LeadingProperty"/>.</summary>
    public static readonly AttachedProperty<object?> TrailingProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("Trailing", typeof(Swipe));

    /// <summary>
    /// Runs when a swipe is released past half the element's width on a side with no command of its own, with the
    /// <see cref="Edge"/> the element moved towards as its parameter: <see cref="Edge.Right"/> for the leading side,
    /// <see cref="Edge.Left"/> for the trailing one. Setting it does not make an element swipeable.
    /// </summary>
    /// <remarks>
    /// A side commits only while the command it would run can execute, or <see cref="DismissProperty"/> is set; otherwise its
    /// drag stops at its actions' width. Both are read at each drag move and release.
    /// </remarks>
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(Swipe));

    /// <summary>Runs with <see cref="CommandParameterProperty"/> when a swipe towards the right commits, in place of <see cref="CommandProperty"/>.</summary>
    public static readonly AttachedProperty<ICommand?> LeadingCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("LeadingCommand", typeof(Swipe));

    /// <summary>Runs with <see cref="CommandParameterProperty"/> when a swipe towards the left commits, in place of <see cref="CommandProperty"/>.</summary>
    public static readonly AttachedProperty<ICommand?> TrailingCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("TrailingCommand", typeof(Swipe));

    /// <summary>The parameter of <see cref="LeadingCommandProperty"/> and <see cref="TrailingCommandProperty"/>; <see cref="CommandProperty"/> gets the edge instead.</summary>
    public static readonly AttachedProperty<object?> CommandParameterProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("CommandParameter", typeof(Swipe));

    /// <summary>
    /// Whether a committed swipe slides the element out before its command runs. A command that removes its
    /// item lets <see cref="Presence.ExitProperty"/> play; an element still shown afterwards is hidden, which plays its exit
    /// as <see cref="Presence.HideModeProperty"/> says. Either way <see cref="Animate.ItemLayoutProperty"/> closes the gap.
    /// An element with no actions swipes away either way.
    /// </summary>
    public static readonly AttachedProperty<bool> DismissProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Dismiss", typeof(Swipe));

    /// <summary>
    /// Whether a drag released short of committing can rest open on its side's actions; true by default. False springs every
    /// such release closed, so the actions show only during the drag and a full swipe commits; a context request still opens them.
    /// </summary>
    public static readonly AttachedProperty<bool> RevealProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Reveal", typeof(Swipe), true);

    private static readonly AttachedProperty<SwipeTracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, SwipeTracker?>("Tracker", typeof(Swipe));

    static Swipe()
    {
        LeadingProperty.Changed.AddClassHandler<Control>(HandleActionsChanged);
        TrailingProperty.Changed.AddClassHandler<Control>(HandleActionsChanged);
        DismissProperty.Changed.AddClassHandler<Control>(HandleActionsChanged);
    }

    /// <summary>Gets whether a released drag of <paramref name="control"/> can rest open on its actions.</summary>
    public static bool GetReveal(Control control) => control.GetValue(RevealProperty);

    /// <summary>Sets whether a released drag of <paramref name="control"/> can rest open on its actions.</summary>
    public static void SetReveal(Control control, bool value) => control.SetValue(RevealProperty, value);

    /// <summary>Gets the actions revealed by a drag of <paramref name="control"/> towards the right.</summary>
    public static object? GetLeading(Control control) => control.GetValue(LeadingProperty);

    /// <summary>Sets the actions revealed by a drag of <paramref name="control"/> towards the right.</summary>
    public static void SetLeading(Control control, object? value) => control.SetValue(LeadingProperty, value);

    /// <summary>Gets the actions revealed by a drag of <paramref name="control"/> towards the left.</summary>
    public static object? GetTrailing(Control control) => control.GetValue(TrailingProperty);

    /// <summary>Sets the actions revealed by a drag of <paramref name="control"/> towards the left.</summary>
    public static void SetTrailing(Control control, object? value) => control.SetValue(TrailingProperty, value);

    /// <summary>Gets the command a full swipe of <paramref name="control"/> runs.</summary>
    public static ICommand? GetCommand(Control control) => control.GetValue(CommandProperty);

    /// <summary>Sets the command a full swipe of <paramref name="control"/> runs.</summary>
    public static void SetCommand(Control control, ICommand? value) => control.SetValue(CommandProperty, value);

    /// <summary>Gets the command a full swipe of <paramref name="control"/> towards the right runs.</summary>
    public static ICommand? GetLeadingCommand(Control control) => control.GetValue(LeadingCommandProperty);

    /// <summary>Sets the command a full swipe of <paramref name="control"/> towards the right runs.</summary>
    public static void SetLeadingCommand(Control control, ICommand? value) => control.SetValue(LeadingCommandProperty, value);

    /// <summary>Gets the command a full swipe of <paramref name="control"/> towards the left runs.</summary>
    public static ICommand? GetTrailingCommand(Control control) => control.GetValue(TrailingCommandProperty);

    /// <summary>Sets the command a full swipe of <paramref name="control"/> towards the left runs.</summary>
    public static void SetTrailingCommand(Control control, ICommand? value) => control.SetValue(TrailingCommandProperty, value);

    /// <summary>Gets the parameter of the side commands of <paramref name="control"/>.</summary>
    public static object? GetCommandParameter(Control control) => control.GetValue(CommandParameterProperty);

    /// <summary>Sets the parameter of the side commands of <paramref name="control"/>.</summary>
    public static void SetCommandParameter(Control control, object? value) => control.SetValue(CommandParameterProperty, value);

    /// <summary>Gets whether a full swipe slides <paramref name="control"/> out.</summary>
    public static bool GetDismiss(Control control) => control.GetValue(DismissProperty);

    /// <summary>Sets whether a full swipe slides <paramref name="control"/> out.</summary>
    public static void SetDismiss(Control control, bool value) => control.SetValue(DismissProperty, value);

    private static void HandleActionsChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var tracker = control.GetValue(TrackerProperty);
        if (GetLeading(control) is null && GetTrailing(control) is null && !GetDismiss(control))
        {
            tracker?.Release();
            control.ClearValue(TrackerProperty);
        }
        else if (tracker is null) control.SetValue(TrackerProperty, new SwipeTracker(control));
        else tracker.Rebuild();
    }
}
