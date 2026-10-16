using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace MonacoLabs.Presto;

/// <summary>
/// Drag-to-reorder for an <see cref="ItemsControl"/>: a container dragged along <see cref="AxisProperty"/> lifts, follows the
/// pointer, moves its item one step each time its center crosses a neighbor's midpoint, and springs into its slot on release.
/// Set <see cref="Animate.ItemLayoutProperty"/> on the list too and the neighbors ease out of the way.
/// </summary>
public static class Reorder
{
    /// <summary>
    /// Whether the list's containers can be dragged to reorder their items. Without <see cref="CommandProperty"/> the items
    /// source must be an <c>ObservableCollection&lt;T&gt;</c>, which is moved in place.
    /// </summary>
    /// <remarks>
    /// Escape cancels a drag back to its start index, and Alt+Up/Down (Alt+Left/Right horizontally) moves the focused item.
    /// The dragged container is focused to keep it realized, so a non-focusable one can be recycled when a virtualized drag
    /// scrolls it far past the viewport.
    /// </remarks>
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, bool>("Enabled", typeof(Reorder));

    /// <summary>The axis the containers are dragged along, vertical by default.</summary>
    public static readonly AttachedProperty<Orientation> AxisProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, Orientation>("Axis", typeof(Reorder), Orientation.Vertical);

    /// <summary>Marks an element of an item template as a grab handle; a container with a handle is grabbed only by its handles.</summary>
    public static readonly AttachedProperty<bool> HandleProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Handle", typeof(Reorder));

    /// <summary>
    /// Milliseconds a touch or pen press must hold still before it lifts its container, so one that moves first scrolls or swipes
    /// instead; 0, the default, drags on touch and pen as on a mouse. A mouse always drags at once.
    /// </summary>
    public static readonly AttachedProperty<double> TouchHoldProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, double>("TouchHold", typeof(Reorder));

    /// <summary>
    /// Runs with the item when a <see cref="TouchHoldProperty"/> hold lifts its container and is let go without moving it, so a
    /// long press can open the item while a long press and drag reorders it.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> HoldCommandProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, ICommand?>("HoldCommand", typeof(Reorder));

    /// <summary>The builder played on a container while it is dragged and reversed on release; null plays <see cref="Recipes.ReorderLift"/>.</summary>
    public static readonly AttachedProperty<AnimationBuilder?> LiftProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, AnimationBuilder?>("Lift", typeof(Reorder));

    /// <summary>
    /// Runs each step instead of the move, with the <see cref="ReorderMovingEventArgs"/> as its parameter, so the view model
    /// owns the change.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, ICommand?>("Command", typeof(Reorder));

    /// <summary>Raised on the list before each one-step move; setting <see cref="ReorderMovingEventArgs.Cancel"/> stops it, and a stopped step is not raised again until the drag crosses back.</summary>
    public static readonly RoutedEvent<ReorderMovingEventArgs> MovingEvent =
        RoutedEvent.Register<ReorderMovingEventArgs>("Moving", RoutingStrategies.Bubble, typeof(Reorder));

    private static readonly AttachedProperty<ReorderList?> ListProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, ReorderList?>("List", typeof(Reorder));

    static Reorder() => EnabledProperty.Changed.AddClassHandler<ItemsControl>(HandleEnabledChanged);

    /// <summary>Gets whether the containers of <paramref name="list"/> can be dragged to reorder.</summary>
    public static bool GetEnabled(ItemsControl list) => list.GetValue(EnabledProperty);

    /// <summary>Sets whether the containers of <paramref name="list"/> can be dragged to reorder.</summary>
    public static void SetEnabled(ItemsControl list, bool value) => list.SetValue(EnabledProperty, value);

    /// <summary>Gets the axis the containers of <paramref name="list"/> are dragged along.</summary>
    public static Orientation GetAxis(ItemsControl list) => list.GetValue(AxisProperty);

    /// <summary>Sets the axis the containers of <paramref name="list"/> are dragged along.</summary>
    public static void SetAxis(ItemsControl list, Orientation value) => list.SetValue(AxisProperty, value);

    /// <summary>Gets whether <paramref name="control"/> is a grab handle.</summary>
    public static bool GetHandle(Control control) => control.GetValue(HandleProperty);

    /// <summary>Sets whether <paramref name="control"/> is a grab handle.</summary>
    public static void SetHandle(Control control, bool value) => control.SetValue(HandleProperty, value);

    /// <summary>Gets how long a touch or pen press on <paramref name="list"/> must hold still before it lifts.</summary>
    public static double GetTouchHold(ItemsControl list) => list.GetValue(TouchHoldProperty);

    /// <summary>Sets how long a touch or pen press on <paramref name="list"/> must hold still before it lifts.</summary>
    public static void SetTouchHold(ItemsControl list, double value) => list.SetValue(TouchHoldProperty, value);

    /// <summary>Gets the command a hold released in place on <paramref name="list"/> runs.</summary>
    public static ICommand? GetHoldCommand(ItemsControl list) => list.GetValue(HoldCommandProperty);

    /// <summary>Sets the command a hold released in place on <paramref name="list"/> runs.</summary>
    public static void SetHoldCommand(ItemsControl list, ICommand? value) => list.SetValue(HoldCommandProperty, value);

    /// <summary>Gets the builder played on a dragged container of <paramref name="list"/>.</summary>
    public static AnimationBuilder? GetLift(ItemsControl list) => list.GetValue(LiftProperty);

    /// <summary>Sets the builder played on a dragged container of <paramref name="list"/>.</summary>
    public static void SetLift(ItemsControl list, AnimationBuilder? value) => list.SetValue(LiftProperty, value);

    /// <summary>Gets the command that runs each step for <paramref name="list"/>.</summary>
    public static ICommand? GetCommand(ItemsControl list) => list.GetValue(CommandProperty);

    /// <summary>Sets the command that runs each step for <paramref name="list"/>.</summary>
    public static void SetCommand(ItemsControl list, ICommand? value) => list.SetValue(CommandProperty, value);

    /// <summary>Adds a handler for <see cref="MovingEvent"/> on <paramref name="element"/>.</summary>
    public static void AddMovingHandler(Interactive element, EventHandler<ReorderMovingEventArgs> handler) =>
        element.AddHandler(MovingEvent, handler);

    /// <summary>Removes a handler for <see cref="MovingEvent"/> from <paramref name="element"/>.</summary>
    public static void RemoveMovingHandler(Interactive element, EventHandler<ReorderMovingEventArgs> handler) =>
        element.RemoveHandler(MovingEvent, handler);

    private static void HandleEnabledChanged(ItemsControl list, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        list.GetValue(ListProperty)?.Release();
        if (e.NewValue is true) list.SetValue(ListProperty, new ReorderList(list));
        else list.ClearValue(ListProperty);
    }
}
