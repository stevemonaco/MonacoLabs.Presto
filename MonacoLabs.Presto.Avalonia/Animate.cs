using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Text;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>
/// Attached properties that play an <see cref="AnimationBuilder"/> on a visual from XAML when it attaches, is tapped or its
/// watched value changes, or that ease a control's layout moves or size while it is attached.
/// </summary>
public static class Animate
{
    /// <summary>
    /// Plays the builder on the visual each time it attaches to the visual tree and reverts it on detach; changing the
    /// value reverts the previous animation and plays the new one, and <see langword="null"/> stops listening.
    /// </summary>
    /// <remarks>
    /// Every attach includes a virtualizing panel's recycling, which suits loops and timers tied to being on screen, such as
    /// a skeleton shimmer or a countdown. Entrances belong on <see cref="Presence"/>, which classifies each arrival.
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> OnAttachedProperty =
        AvaloniaProperty.RegisterAttached<Visual, AnimationBuilder?>("OnAttached", typeof(Animate));

    private static readonly AttachedProperty<Trigger?> AttachmentProperty =
        AvaloniaProperty.RegisterAttached<Visual, Trigger?>("Attachment", typeof(Animate));

    /// <summary>
    /// Plays the builder on the element each time it is tapped, restarting it from the start on every tap, and reverts it
    /// when it completes or the element detaches; <see langword="null"/> stops listening.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> OnTappedProperty =
        AvaloniaProperty.RegisterAttached<InputElement, AnimationBuilder?>("OnTapped", typeof(Animate));

    /// <summary>
    /// Plays the builder each time <see cref="WatchProperty"/> changes after its first value, restarting it on every change,
    /// and reverts it when it completes or the visual detaches; <see langword="null"/> stops listening.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> OnChangedProperty =
        AvaloniaProperty.RegisterAttached<Visual, AnimationBuilder?>("OnChanged", typeof(Animate));

    /// <summary>The value whose changes play <see cref="OnChangedProperty"/>, usually bound to a view-model property.</summary>
    public static readonly AttachedProperty<object?> WatchProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("Watch", typeof(Animate));

    /// <summary>
    /// Eases every move layout gives the control while it is attached, as <see cref="PrestoAvalonia.AnimateLayout(Control, AnimationBuilder, LayoutAnimateMode)"/>
    /// does, taking only the builder's timing and eases. Attaching records where the control is without easing its arrival,
    /// detaching reverts, changing the value re-arms, and <see langword="null"/> stops listening. A keyed flight wins its move
    /// and the property takes over again once it has landed.
    /// </summary>
    /// <remarks>Applying a builder that sets anything a layout transition cannot honor throws <see cref="NotSupportedException"/>.</remarks>
    public static readonly AttachedProperty<AnimationBuilder?> LayoutProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Layout", typeof(Animate));

    /// <summary>How much of a layout change <see cref="LayoutProperty"/> eases; changing it re-arms.</summary>
    public static readonly AttachedProperty<LayoutAnimateMode> LayoutModeProperty =
        AvaloniaProperty.RegisterAttached<Control, LayoutAnimateMode>("LayoutMode", typeof(Animate));

    private static readonly AttachedProperty<LayoutArm?> LayoutArmProperty =
        AvaloniaProperty.RegisterAttached<Control, LayoutArm?>("LayoutArm", typeof(Animate));

    /// <summary>
    /// Eases the layout moves of every item container of the list as <see cref="LayoutProperty"/> does, with the list's
    /// <see cref="LayoutModeProperty"/>. A container realized or recycled records where it is rather than flying there, a pass
    /// that only scrolls the list moves its containers in one frame, and <see langword="null"/> reverts every container.
    /// </summary>
    /// <remarks>Applying a builder that sets anything a layout transition cannot honor throws <see cref="NotSupportedException"/>.</remarks>
    public static readonly AttachedProperty<AnimationBuilder?> ItemLayoutProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, AnimationBuilder?>("ItemLayout", typeof(Animate));

    private static readonly AttachedProperty<ItemArms?> ItemArmsProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, ItemArms?>("ItemArms", typeof(Animate));

    private static readonly AttachedProperty<LayoutArm?> ItemArmProperty =
        AvaloniaProperty.RegisterAttached<Control, LayoutArm?>("ItemArm", typeof(Animate));

    /// <summary>
    /// Eases the real layout size of the host whenever its content changes size, as
    /// <see cref="PrestoAvalonia.AnimateContentSize(Control, AnimationBuilder)"/> does, taking only the builder's timing and
    /// ease. Attaching records the size without easing it, detaching reverts, changing the value re-arms, and
    /// <see langword="null"/> stops listening. Meant for a <see cref="Decorator"/>, <see cref="ContentControl"/> or <see cref="Border"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Swapping the host's content resizes it from the size drawn to the new content's, so with <see cref="Presence.ExitProperty"/>
    /// and <see cref="Presence.EnterProperty"/> on the content the old one ghosts out, clipped to the box the host had when
    /// it was swapped, while the new one enters. The host owns its size: a <see cref="LayoutProperty"/> on it eases only its
    /// moves, whatever its <see cref="LayoutModeProperty"/>, and a sibling's layout move under the same parent lands in one
    /// frame while the host resizes, since the resize already eases it.
    /// </para>
    /// <para>Applying a builder that animates a property or sets anything a layout transition cannot honor throws <see cref="NotSupportedException"/>.</para>
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> ContentSizeProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("ContentSize", typeof(Animate));

    private static readonly AttachedProperty<ContentSizeArm?> ContentSizeArmProperty =
        AvaloniaProperty.RegisterAttached<Control, ContentSizeArm?>("ContentSizeArm", typeof(Animate));

    private static readonly AttachedProperty<Trigger?> TappedTriggerProperty =
        AvaloniaProperty.RegisterAttached<Visual, Trigger?>("TappedTrigger", typeof(Animate));

    private static readonly AttachedProperty<Trigger?> ChangedTriggerProperty =
        AvaloniaProperty.RegisterAttached<Visual, Trigger?>("ChangedTrigger", typeof(Animate));

    static Animate()
    {
        OnAttachedProperty.Changed.AddClassHandler<Visual>(HandleOnAttachedChanged);
        OnTappedProperty.Changed.AddClassHandler<Visual>(HandleTriggerChanged);
        OnChangedProperty.Changed.AddClassHandler<Visual>(HandleTriggerChanged);
        WatchProperty.Changed.AddClassHandler<Visual>(HandleWatchChanged);
        LayoutProperty.Changed.AddClassHandler<Control>(HandleLayoutChanged);
        LayoutModeProperty.Changed.AddClassHandler<Control>(HandleLayoutChanged);
        ContentSizeProperty.Changed.AddClassHandler<Control>(HandleContentSizeChanged);
        ItemLayoutProperty.Changed.AddClassHandler<ItemsControl>(HandleItemLayoutChanged);
        LayoutModeProperty.Changed.AddClassHandler<ItemsControl>(HandleItemLayoutChanged);
    }

    /// <summary>Gets the builder played when <paramref name="visual"/> attaches to the visual tree.</summary>
    public static AnimationBuilder? GetOnAttached(Visual visual) => visual.GetValue(OnAttachedProperty);

    /// <summary>Sets the builder played when <paramref name="visual"/> attaches to the visual tree.</summary>
    public static void SetOnAttached(Visual visual, AnimationBuilder? value) => visual.SetValue(OnAttachedProperty, value);

    /// <summary>Gets the builder played when <paramref name="element"/> is tapped.</summary>
    public static AnimationBuilder? GetOnTapped(InputElement element) => element.GetValue(OnTappedProperty);

    /// <summary>Sets the builder played when <paramref name="element"/> is tapped.</summary>
    public static void SetOnTapped(InputElement element, AnimationBuilder? value) => element.SetValue(OnTappedProperty, value);

    /// <summary>Gets the builder played when the watched value of <paramref name="visual"/> changes.</summary>
    public static AnimationBuilder? GetOnChanged(Visual visual) => visual.GetValue(OnChangedProperty);

    /// <summary>Sets the builder played when the watched value of <paramref name="visual"/> changes.</summary>
    public static void SetOnChanged(Visual visual, AnimationBuilder? value) => visual.SetValue(OnChangedProperty, value);

    /// <summary>Gets the value whose changes play the <see cref="OnChangedProperty"/> builder.</summary>
    public static object? GetWatch(Visual visual) => visual.GetValue(WatchProperty);

    /// <summary>Sets the value whose changes play the <see cref="OnChangedProperty"/> builder.</summary>
    public static void SetWatch(Visual visual, object? value) => visual.SetValue(WatchProperty, value);

    /// <summary>Gets the builder that eases the layout moves of <paramref name="control"/>.</summary>
    public static AnimationBuilder? GetLayout(Control control) => control.GetValue(LayoutProperty);

    /// <summary>Sets the builder that eases the layout moves of <paramref name="control"/>.</summary>
    public static void SetLayout(Control control, AnimationBuilder? value) => control.SetValue(LayoutProperty, value);

    /// <summary>Gets how much of a layout change of <paramref name="control"/> is eased.</summary>
    public static LayoutAnimateMode GetLayoutMode(Control control) => control.GetValue(LayoutModeProperty);

    /// <summary>Sets how much of a layout change of <paramref name="control"/> is eased.</summary>
    public static void SetLayoutMode(Control control, LayoutAnimateMode value) => control.SetValue(LayoutModeProperty, value);

    /// <summary>Gets the builder that eases the layout size of <paramref name="control"/>.</summary>
    public static AnimationBuilder? GetContentSize(Control control) => control.GetValue(ContentSizeProperty);

    /// <summary>Sets the builder that eases the layout size of <paramref name="control"/>.</summary>
    public static void SetContentSize(Control control, AnimationBuilder? value) => control.SetValue(ContentSizeProperty, value);

    /// <summary>Gets the builder that eases the layout moves of the item containers of <paramref name="list"/>.</summary>
    public static AnimationBuilder? GetItemLayout(ItemsControl list) => list.GetValue(ItemLayoutProperty);

    /// <summary>Sets the builder that eases the layout moves of the item containers of <paramref name="list"/>.</summary>
    public static void SetItemLayout(ItemsControl list, AnimationBuilder? value) => list.SetValue(ItemLayoutProperty, value);

    /// <summary>The arm <see cref="ItemLayoutProperty"/> keeps on an item container, or null when its list sets none.</summary>
    internal static LayoutArm? ItemArmOf(Control container) => container.GetValue(ItemArmProperty);

    private static void HandleOnAttachedChanged(Visual visual, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var attachment = visual.GetValue(AttachmentProperty);
        if (e.NewValue is null)
        {
            attachment?.Release();
            visual.ClearValue(AttachmentProperty);
            return;
        }
        if (attachment is null) visual.SetValue(AttachmentProperty, attachment = new Trigger(visual, OnAttachedProperty));
        attachment.Play();
    }

    private static void HandleLayoutChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var arm = control.GetValue(LayoutArmProperty);
        if (GetLayout(control) is null)
        {
            arm?.Release();
            control.ClearValue(LayoutArmProperty);
            return;
        }
        if (arm is null) control.SetValue(LayoutArmProperty, arm = new LayoutArm(control));
        arm.Arm(attaching: false);
    }

    private static void HandleContentSizeChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var arm = control.GetValue(ContentSizeArmProperty);
        if (GetContentSize(control) is null)
        {
            arm?.Release();
            control.ClearValue(ContentSizeArmProperty);
            return;
        }
        if (arm is null) control.SetValue(ContentSizeArmProperty, arm = new ContentSizeArm(control));
        arm.Arm(attaching: false);
    }

    private static void HandleItemLayoutChanged(ItemsControl list, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var arms = list.GetValue(ItemArmsProperty);
        if (GetItemLayout(list) is not { } builder)
        {
            arms?.Release();
            list.ClearValue(ItemArmsProperty);
            return;
        }
        // Read before any container is realized, so an invalid builder throws where it is set.
        LayoutMotion.From(builder);
        if (arms is null) list.SetValue(ItemArmsProperty, arms = new ItemArms(list));
        arms.Arm();
    }

    private static void HandleTriggerChanged(Visual visual, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var holder = e.Property == OnTappedProperty ? TappedTriggerProperty : ChangedTriggerProperty;
        var trigger = visual.GetValue(holder);
        if (e.NewValue is null)
        {
            trigger?.Release();
            visual.ClearValue(holder);
        }
        else if (trigger is null)
        {
            visual.SetValue(holder, new Trigger(visual, (AttachedProperty<AnimationBuilder?>)e.Property) { Seen = visual.IsSet(WatchProperty) });
        }
    }

    private static void HandleWatchChanged(Visual visual, AvaloniaPropertyChangedEventArgs e)
    {
        if (visual.GetValue(ChangedTriggerProperty) is not { } trigger) return;
        if (trigger.Seen) trigger.Play();
        trigger.Seen = true;
    }

    private sealed class ItemArms
    {
        private readonly ItemsControl _list;

        public ItemArms(ItemsControl list)
        {
            _list = list;
            list.ContainerPrepared += HandlePrepared;
            list.ContainerClearing += HandleClearing;
        }

        public void Arm()
        {
            foreach (var container in _list.GetRealizedContainers()) Arm(container, attaching: false);
        }

        public void Release()
        {
            _list.ContainerPrepared -= HandlePrepared;
            _list.ContainerClearing -= HandleClearing;
            foreach (var container in _list.GetRealizedContainers()) Disarm(container);
        }

        private void Arm(Control container, bool attaching)
        {
            var arm = container.GetValue(ItemArmProperty);
            if (arm is null) container.SetValue(ItemArmProperty, arm = new LayoutArm(container, _list));
            arm.Arm(attaching);
        }

        private static void Disarm(Control container)
        {
            container.GetValue(ItemArmProperty)?.Release();
            container.ClearValue(ItemArmProperty);
        }

        // Arming as if attaching makes a recycled container record its new slot instead of flying there from its old one.
        private void HandlePrepared(object? sender, ContainerPreparedEventArgs e) => Arm(e.Container, attaching: true);

        private void HandleClearing(object? sender, ContainerClearingEventArgs e) => Disarm(e.Container);
    }

    private sealed class Trigger
    {
        private readonly Visual _visual;
        private readonly AttachedProperty<AnimationBuilder?> _builder;
        private IRevertible? _animation;

        public Trigger(Visual visual, AttachedProperty<AnimationBuilder?> builder)
        {
            _visual = visual;
            _builder = builder;
            visual.DetachedFromVisualTree += HandleDetached;
            if (builder == OnAttachedProperty) visual.AttachedToVisualTree += HandleAttached;
            else if (builder == OnTappedProperty && visual is InputElement element) element.Tapped += HandleTapped;
        }

        public bool Seen { get; set; }

        public void Play()
        {
            Stop();
            if (!_visual.IsAttachedToVisualTree() || _visual.GetValue(_builder) is not { } builder) return;
            PrestoAvalonia.EnsureInitialized();
            var parameters = builder.Resolve();
            using var origin = PrestoDiagnostics.Origin("Animate");
            // A text reveal reverts itself when it completes.
            if (_builder != OnChangedProperty && PieceTargets.Play(_visual, parameters) is { } reveal)
            {
                _animation = reveal;
                return;
            }
            var animation = Presto.Animate(_visual, parameters);
            _animation = animation;
            if (_builder == OnAttachedProperty) return;
            var completed = animation.OnComplete;
            animation.OnComplete = timer =>
            {
                completed?.Invoke(timer);
                Stop();
            };
        }

        public void Release()
        {
            _visual.AttachedToVisualTree -= HandleAttached;
            _visual.DetachedFromVisualTree -= HandleDetached;
            if (_visual is InputElement element) element.Tapped -= HandleTapped;
            Stop();
        }

        private void Stop()
        {
            _animation?.Revert();
            _animation = null;
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Play();

        private void HandleTapped(object? sender, TappedEventArgs e) => Play();

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Stop();
    }
}
