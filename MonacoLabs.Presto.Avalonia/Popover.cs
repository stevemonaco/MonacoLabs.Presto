using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto;

/// <summary>
/// Open and close motion for popups: <see cref="OpenProperty"/> plays on the popup's presenter as it opens, and
/// <see cref="CloseProperty"/> plays on it before it closes.
/// </summary>
/// <remarks>
/// <para>
/// Set them on a <see cref="PopupFlyoutBase"/>, such as a <see cref="Flyout"/> or a <see cref="MenuFlyout"/>, whose
/// presenter is its <see cref="FlyoutPresenter"/> or <see cref="MenuFlyoutPresenter"/>, or on a <see cref="ContextMenu"/>,
/// which is its own presenter. A flyout cannot be styled, so a style reaches it through its presenter, narrowed by a class
/// from the flyout's <c>FlyoutPresenterClasses</c> where needed; a value on the flyout wins.
/// </para>
/// <para>
/// Open's start values are applied in the turn the popup opens, so it never draws at rest. Light dismiss, Escape,
/// <see cref="FlyoutBase.Hide"/> and <see cref="ContextMenu.Close"/> all play Close and then close; asking again while it
/// plays changes nothing, and a context menu requested again plays Close back from where it stands. A placement target
/// that detaches closes the popup at once. The presenter's values revert once it has closed. While a popup with either
/// builder is open, its presenter's <c>Opacity</c> transitions are lifted. Under <see cref="PrestoDefaults.ReducedMotion"/>,
/// read as the popup closes, it closes at once.
/// </para>
/// <para>
/// On a <see cref="Popup"/>, set directly or through <c>/template/</c> (such as <c>ComboBox /template/ Popup#PART_Popup</c>),
/// they play on its child, and a Close set as it opens holds <c>IsOpen</c> true at <see cref="Avalonia.Data.BindingPriority.Animation"/>:
/// writing <c>IsOpen</c> false underneath, through a local value or a binding such as the <c>ComboBox</c>'s, plays Close
/// and then closes, and writing it true again plays Close back. A <c>ClearValue</c> under the hold is lost, so close it by
/// writing false. Light dismiss closes the popup at once and plays Close on a ghost; the popup detaching closes it at once.
/// </para>
/// <para>
/// On a <see cref="ToolTip"/>, set on it or through a style, they play on the tooltip, whose Fluent <c>Opacity</c> fade is
/// lifted. Nothing can hold a tooltip open, so it closes at once and Close plays on a ghost in its anchor's window
/// overlay, which clips it to that window even where the tooltip was a native popup; a window without an overlay layer,
/// or an anchor leaving the tree, closes it with no ghost.
/// </para>
/// </remarks>
public static class Popover
{
    /// <summary>Plays the builder on the popup's presenter as it opens, stating the values it opens from.</summary>
    public static readonly AttachedProperty<AnimationBuilder?> OpenProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, AnimationBuilder?>("Open", typeof(Popover));

    /// <summary>Plays the builder on the popup's presenter before it closes, stating the values it closes to.</summary>
    public static readonly AttachedProperty<AnimationBuilder?> CloseProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, AnimationBuilder?>("Close", typeof(Popover));

    private static readonly AttachedProperty<PopoverRun?> RunProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, PopoverRun?>("Run", typeof(Popover));

    static Popover()
    {
        // The current value, not the notification's, since a canceled hide re-opens inside the owner's own handler.
        FlyoutBase.IsOpenProperty.Changed.AddClassHandler<PopupFlyoutBase>((flyout, _) =>
        {
            if (!flyout.IsOpen) End(flyout);
            else if (flyout.Popup.Child is { } presenter) Start(flyout, presenter, flyout.Popup);
        });
        MenuBase.IsOpenProperty.Changed.AddClassHandler<ContextMenu>((menu, _) =>
        {
            if (!menu.IsOpen) End(menu);
            else if (menu.Parent is Popup popup) Start(menu, menu, popup);
        });
        Popup.IsOpenProperty.Changed.AddClassHandler<Popup>((popup, _) =>
        {
            AvaloniaObject owner = popup.Child as ToolTip ?? (AvaloniaObject)popup;
            if (!popup.IsOpen) End(owner);
            // A flyout's or context menu's presenter can carry the builders too, but only that owner plays them.
            else if (popup.Child is { } child && child.IsAttachedToVisualTree() &&
                     (owner is ToolTip || popup.GetValue(OpenProperty) is not null || popup.GetValue(CloseProperty) is not null))
                Start(owner, child, popup);
        });
    }

    /// <summary>Gets the builder played on <paramref name="element"/>'s presenter as its popup opens.</summary>
    public static AnimationBuilder? GetOpen(AvaloniaObject element) => element.GetValue(OpenProperty);

    /// <summary>Sets the builder played on <paramref name="element"/>'s presenter as its popup opens.</summary>
    public static void SetOpen(AvaloniaObject element, AnimationBuilder? value) => element.SetValue(OpenProperty, value);

    /// <summary>Gets the builder played on <paramref name="element"/>'s presenter before its popup closes.</summary>
    public static AnimationBuilder? GetClose(AvaloniaObject element) => element.GetValue(CloseProperty);

    /// <summary>Sets the builder played on <paramref name="element"/>'s presenter before its popup closes.</summary>
    public static void SetClose(AvaloniaObject element, AnimationBuilder? value) => element.SetValue(CloseProperty, value);

    private static void Start(AvaloniaObject owner, Control presenter, Popup popup)
    {
        if (Design.IsDesignMode || owner.GetValue(RunProperty) is not null) return;
        if (Builder(OpenProperty, owner, presenter) is null && Builder(CloseProperty, owner, presenter) is null) return;
        var run = new PopoverRun(owner, presenter, popup);
        owner.SetValue(RunProperty, run);
        run.Open();
    }

    internal static AnimationBuilder? Builder(AttachedProperty<AnimationBuilder?> property, AvaloniaObject owner, Control presenter) =>
        owner.GetValue(property) ?? presenter.GetValue(property);

    private static void End(AvaloniaObject owner)
    {
        if (owner.GetValue(RunProperty) is not { } run) return;
        owner.ClearValue(RunProperty);
        run.End();
    }
}
