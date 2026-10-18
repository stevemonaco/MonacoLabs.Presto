using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Text;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>
/// Attached properties that play an <see cref="AnimationBuilder"/> or invoke a <see cref="CueAction"/> on a visual from XAML
/// when it attaches, is tapped, its watched value changes or a view-model cue is raised, or that ease a control's layout
/// moves or size while it is attached.
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
    /// when it completes or the element detaches; changing the value while it plays reverts it and plays the new builder,
    /// and <see langword="null"/> stops listening.
    /// </summary>
    /// <remarks>Setting it on a visual that is not an <see cref="InputElement"/> throws <see cref="InvalidOperationException"/>.</remarks>
    public static readonly AttachedProperty<AnimationBuilder?> OnTappedProperty =
        AvaloniaProperty.RegisterAttached<InputElement, AnimationBuilder?>("OnTapped", typeof(Animate));

    /// <summary>
    /// Plays the builder each time <see cref="WatchProperty"/> changes after its first value, restarting it on every change,
    /// and reverts it when it completes or the visual detaches; changing the value while it plays reverts it and plays the
    /// new builder, and <see langword="null"/> stops listening.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> OnChangedProperty =
        AvaloniaProperty.RegisterAttached<Visual, AnimationBuilder?>("OnChanged", typeof(Animate));

    /// <summary>The value whose changes play <see cref="OnChangedProperty"/>, usually bound to a view-model property.</summary>
    public static readonly AttachedProperty<object?> WatchProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("Watch", typeof(Animate));

    /// <summary>
    /// Invokes the action with every <see cref="WatchProperty"/> value while the visual is attached, telling it which value
    /// is the first, and on attach with a value it has not seen. A change made while the <see cref="StyledElement.DataContext"/>
    /// is dropped, and the one that restores it, count as first again.
    /// </summary>
    public static readonly AttachedProperty<CueAction?> DoOnChangedProperty =
        AvaloniaProperty.RegisterAttached<Visual, CueAction?>("DoOnChanged", typeof(Animate));

    private static readonly AttachedProperty<ChangedAction?> ChangedActionProperty =
        AvaloniaProperty.RegisterAttached<Visual, ChangedAction?>("ChangedAction", typeof(Animate));

    /// <summary>
    /// The cue whose raises play <see cref="OnCueProperty"/> and invoke <see cref="DoOnCueProperty"/>, usually bound to a
    /// view-model <see cref="Cue"/>. It is subscribed only while the visual is attached, so raises while detached do nothing.
    /// </summary>
    /// <remarks>Takes a <see cref="Cue"/>, a <see cref="Cue{T}"/> or an <see cref="IObservable{T}"/> of <see cref="object"/>; any other value throws <see cref="ArgumentException"/>.</remarks>
    public static readonly AttachedProperty<object?> CueProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("Cue", typeof(Animate));

    private static readonly AttachedProperty<CueListener?> CueListenerProperty =
        AvaloniaProperty.RegisterAttached<Visual, CueListener?>("CueListener", typeof(Animate));

    /// <summary>
    /// Plays the builder each time <see cref="CueProperty"/> is raised, restarting it on every raise, and reverts it when it
    /// completes or the visual detaches; changing the value while it plays reverts it and plays the new builder, and
    /// <see langword="null"/> stops playing.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> OnCueProperty =
        AvaloniaProperty.RegisterAttached<Visual, AnimationBuilder?>("OnCue", typeof(Animate));

    /// <summary>Invokes the action on the visual with the raised value each time <see cref="CueProperty"/> is raised.</summary>
    public static readonly AttachedProperty<CueAction?> DoOnCueProperty =
        AvaloniaProperty.RegisterAttached<Visual, CueAction?>("DoOnCue", typeof(Animate));

    /// <summary>
    /// Eases every move layout gives the control while it is attached, as <see cref="PrestoAvalonia.AnimateLayout(Control, AnimationBuilder, LayoutAnimateMode)"/>
    /// does, taking only the builder's timing and eases. Attaching records where the control is without easing its arrival,
    /// detaching reverts, changing the value re-arms, and <see langword="null"/> stops listening. A keyed flight wins its move
    /// and the property takes over again once it has landed.
    /// </summary>
    /// <remarks>
    /// Applying a builder that sets anything a layout transition cannot honor throws <see cref="NotSupportedException"/>, and
    /// arming an item container whose list already arms it through <see cref="ItemLayoutProperty"/> throws <see cref="InvalidOperationException"/>.
    /// </remarks>
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
    /// <remarks>
    /// Applying a builder that sets anything a layout transition cannot honor throws <see cref="NotSupportedException"/>, and
    /// arming a container that already has its own <see cref="LayoutProperty"/> throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> ItemLayoutProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, AnimationBuilder?>("ItemLayout", typeof(Animate));

    /// <summary>Names the lists with <see cref="ItemLayoutProperty"/> whose items fly from one to another when moved between them.</summary>
    public static readonly AttachedProperty<string?> ItemGroupProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, string?>("ItemGroup", typeof(Animate));

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

    private static readonly AttachedProperty<Trigger?> CueTriggerProperty =
        AvaloniaProperty.RegisterAttached<Visual, Trigger?>("CueTrigger", typeof(Animate));

    static Animate()
    {
        OnAttachedProperty.Changed.AddClassHandler<Visual>(HandleOnAttachedChanged);
        OnTappedProperty.Changed.AddClassHandler<Visual>(HandleTriggerChanged);
        OnChangedProperty.Changed.AddClassHandler<Visual>(HandleTriggerChanged);
        OnCueProperty.Changed.AddClassHandler<Visual>(HandleTriggerChanged);
        WatchProperty.Changed.AddClassHandler<Visual>(HandleWatchChanged);
        DoOnChangedProperty.Changed.AddClassHandler<Visual>(HandleDoOnChangedChanged);
        CueProperty.Changed.AddClassHandler<Visual>(HandleCueChanged);
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

    /// <summary>Gets the action invoked with each watched value of <paramref name="visual"/>.</summary>
    public static CueAction? GetDoOnChanged(Visual visual) => visual.GetValue(DoOnChangedProperty);

    /// <summary>Sets the action invoked with each watched value of <paramref name="visual"/>.</summary>
    public static void SetDoOnChanged(Visual visual, CueAction? value) => visual.SetValue(DoOnChangedProperty, value);

    /// <summary>Gets the cue whose raises play <see cref="OnCueProperty"/> and invoke <see cref="DoOnCueProperty"/>.</summary>
    public static object? GetCue(Visual visual) => visual.GetValue(CueProperty);

    /// <summary>Sets the cue whose raises play <see cref="OnCueProperty"/> and invoke <see cref="DoOnCueProperty"/>.</summary>
    public static void SetCue(Visual visual, object? value) => visual.SetValue(CueProperty, value);

    /// <summary>Gets the builder played when the cue of <paramref name="visual"/> is raised.</summary>
    public static AnimationBuilder? GetOnCue(Visual visual) => visual.GetValue(OnCueProperty);

    /// <summary>Sets the builder played when the cue of <paramref name="visual"/> is raised.</summary>
    public static void SetOnCue(Visual visual, AnimationBuilder? value) => visual.SetValue(OnCueProperty, value);

    /// <summary>Gets the action invoked when the cue of <paramref name="visual"/> is raised.</summary>
    public static CueAction? GetDoOnCue(Visual visual) => visual.GetValue(DoOnCueProperty);

    /// <summary>Sets the action invoked when the cue of <paramref name="visual"/> is raised.</summary>
    public static void SetDoOnCue(Visual visual, CueAction? value) => visual.SetValue(DoOnCueProperty, value);

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

    /// <summary>Gets the group of lists whose items fly between them.</summary>
    public static string? GetItemGroup(ItemsControl list) => list.GetValue(ItemGroupProperty);

    /// <summary>Sets the group of lists whose items fly between them.</summary>
    public static void SetItemGroup(ItemsControl list, string? value) => list.SetValue(ItemGroupProperty, value);

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
        if (arm is null)
        {
            if (ItemArmOf(control) is not null) throw TwoArms();
            control.SetValue(LayoutArmProperty, arm = new LayoutArm(control));
        }
        arm.Arm(attaching: false);
    }

    private static InvalidOperationException TwoArms() =>
        new("Animate.Layout and its list's Animate.ItemLayout would both arm this container; set one of them.");

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
        if (e.Property == OnTappedProperty && e.NewValue is not null && visual is not InputElement)
            throw new InvalidOperationException($"Animate.OnTapped listens for Tapped, which a {visual.GetType().Name} does not raise; set it on an InputElement.");
        var holder = e.Property == OnTappedProperty ? TappedTriggerProperty
            : e.Property == OnChangedProperty ? ChangedTriggerProperty
            : CueTriggerProperty;
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
        else if (trigger.Running)
        {
            trigger.Play();
        }
    }

    private static void HandleWatchChanged(Visual visual, AvaloniaPropertyChangedEventArgs e)
    {
        if (visual.GetValue(ChangedTriggerProperty) is { } trigger)
        {
            if (trigger.Seen) trigger.Play();
            trigger.Seen = true;
        }
        visual.GetValue(ChangedActionProperty)?.Changed(e.NewValue);
    }

    private static void HandleDoOnChangedChanged(Visual visual, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        visual.GetValue(ChangedActionProperty)?.Release();
        if (e.NewValue is null)
        {
            visual.ClearValue(ChangedActionProperty);
            return;
        }
        var action = new ChangedAction(visual);
        visual.SetValue(ChangedActionProperty, action);
        if (visual.IsAttachedToVisualTree()) action.CatchUp();
    }

    private static void HandleCueChanged(Visual visual, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        visual.GetValue(CueListenerProperty)?.Release();
        visual.ClearValue(CueListenerProperty);
        if (e.NewValue is null) return;
        if (e.NewValue is not (ICueSource or IObservable<object?>))
            throw new ArgumentException($"Animate.Cue takes a Cue, a Cue<T> or an IObservable<object?>, not a {e.NewValue.GetType().Name}.", "value");
        visual.SetValue(CueListenerProperty, new CueListener(visual, e.NewValue));
    }

    private sealed class CueListener : IObserver<object?>
    {
        private readonly Visual _visual;
        private readonly object _source;
        private IDisposable? _subscription;

        public CueListener(Visual visual, object source)
        {
            _visual = visual;
            _source = source;
            visual.AttachedToVisualTree += HandleAttached;
            visual.DetachedFromVisualTree += HandleDetached;
            if (visual.IsAttachedToVisualTree()) Subscribe();
        }

        public void Release()
        {
            _visual.AttachedToVisualTree -= HandleAttached;
            _visual.DetachedFromVisualTree -= HandleDetached;
            Unsubscribe();
        }

        public void OnNext(object? value)
        {
            _visual.GetValue(CueTriggerProperty)?.Play();
            GetDoOnCue(_visual)?.Invoke(_visual, value, first: false);
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        private void Subscribe() =>
            _subscription = _source is ICueSource cue ? cue.Subscribe(OnNext) : ((IObservable<object?>)_source).Subscribe(this);

        private void Unsubscribe()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Subscribe();

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Unsubscribe();
    }

    private sealed class ChangedAction
    {
        private readonly Visual _visual;
        private object? _last;
        private bool _invoked;
        private bool _hadContext;
        private bool _dropped;

        public ChangedAction(Visual visual)
        {
            _visual = visual;
            _hadContext = visual.DataContext is not null;
            visual.AttachedToVisualTree += HandleAttached;
        }

        public void Release() => _visual.AttachedToVisualTree -= HandleAttached;

        // A drop (null after a context) and the change that follows it are rebinds, so a surface move sets rather than morphs.
        public void Changed(object? value)
        {
            var context = _visual.DataContext is not null;
            var rebind = context ? _dropped : _hadContext;
            _dropped = !context && _hadContext;
            _hadContext |= context;
            if (_visual.IsAttachedToVisualTree()) Invoke(value, !_invoked || rebind);
        }

        public void CatchUp()
        {
            var value = GetWatch(_visual);
            if (!_invoked || !Equals(value, _last)) Invoke(value, !_invoked);
        }

        private void Invoke(object? value, bool first)
        {
            _invoked = true;
            _last = value;
            GetDoOnChanged(_visual)?.Invoke(_visual, value, first);
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => CatchUp();
    }

    private sealed class ItemArms
    {
        private readonly ItemsControl _list;

        public ItemArms(ItemsControl list)
        {
            _list = list;
            list.ContainerPrepared += HandlePrepared;
            list.ContainerClearing += HandleClearing;
            list.Items.CollectionChanged += HandleItemsChanged;
        }

        public void Arm()
        {
            foreach (var container in _list.GetRealizedContainers()) Arm(container, attaching: false);
        }

        public void Release()
        {
            _list.ContainerPrepared -= HandlePrepared;
            _list.ContainerClearing -= HandleClearing;
            _list.Items.CollectionChanged -= HandleItemsChanged;
            foreach (var container in _list.GetRealizedContainers()) Disarm(container);
        }

        private void Arm(Control container, bool attaching)
        {
            var arm = container.GetValue(ItemArmProperty);
            if (arm is null)
            {
                if (container.GetValue(LayoutArmProperty) is not null) throw TwoArms();
                container.SetValue(ItemArmProperty, arm = new LayoutArm(container, _list));
            }
            arm.Arm(attaching);
        }

        private static void Disarm(Control container)
        {
            container.GetValue(ItemArmProperty)?.Release();
            container.ClearValue(ItemArmProperty);
        }

        // Arming as if attaching makes a recycled container record its new slot instead of flying there from its old one.
        private void HandlePrepared(object? sender, ContainerPreparedEventArgs e)
        {
            ItemTransfers.Arrive(_list, e.Container, _list.Items[e.Index]);
            Arm(e.Container, attaching: true);
        }

        private void HandleClearing(object? sender, ContainerClearingEventArgs e) => Disarm(e.Container);

        private void HandleItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ItemTransfers.OnItemsChanged(_list, e);
    }

    // Avalonia raises IsEffectivelyVisibleChanged from the visibility walk it already does, but keeps the event internal.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "add_IsEffectivelyVisibleChanged")]
    internal static extern void AddVisibilityHandler(Visual visual, EventHandler handler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "remove_IsEffectivelyVisibleChanged")]
    internal static extern void RemoveVisibilityHandler(Visual visual, EventHandler handler);

    private sealed class Trigger
    {
        private readonly Visual _visual;
        private readonly AttachedProperty<AnimationBuilder?> _builder;
        private readonly Action _follow;
        private IRevertible? _animation;
        private bool _pausedHidden;
        private Engine? _engine;
        private ReducedMotionFollowers.Form _form;

        public Trigger(Visual visual, AttachedProperty<AnimationBuilder?> builder)
        {
            _visual = visual;
            _builder = builder;
            _follow = Follow;
            visual.DetachedFromVisualTree += HandleDetached;
            if (builder == OnAttachedProperty)
            {
                visual.AttachedToVisualTree += HandleAttached;
                AddVisibilityHandler(visual, HandleVisibilityChanged);
            }
            else if (builder == OnTappedProperty && visual is InputElement element) element.Tapped += HandleTapped;
        }

        public bool Seen { get; set; }

        public bool Running => _animation != null;

        public void Play()
        {
            Stop();
            if (!_visual.IsAttachedToVisualTree() || _visual.GetValue(_builder) is not { } builder) return;
            PrestoAvalonia.EnsureInitialized();
            var parameters = builder.Resolve();
            _form = ReducedMotionFollowers.Form.Of(parameters);
            if (_builder != OnAttachedProperty)
            {
                var completed = parameters.OnComplete;
                parameters = parameters with
                {
                    OnComplete = timer =>
                    {
                        completed?.Invoke(timer);
                        Stop();
                    },
                };
            }
            using var origin = PrestoDiagnostics.Origin("Animate");
            _animation = (_builder == OnAttachedProperty || _builder == OnTappedProperty) && PieceTargets.Play(_visual, parameters) is { } reveal
                ? reveal
                : Presto.Animate(_visual, parameters);
            _engine = ReducedMotionFollowers.Add(_follow);
            if (_builder == OnAttachedProperty) FollowVisibility();
        }

        public void Release()
        {
            _visual.AttachedToVisualTree -= HandleAttached;
            RemoveVisibilityHandler(_visual, HandleVisibilityChanged);
            _visual.DetachedFromVisualTree -= HandleDetached;
            if (_visual is InputElement element) element.Tapped -= HandleTapped;
            Stop();
        }

        private void Stop()
        {
            _animation?.Revert();
            _animation = null;
            _pausedHidden = false;
            if (_engine == null) return;
            ReducedMotionFollowers.Remove(_engine, _follow);
            _engine = null;
        }

        private void Follow()
        {
            if (_engine != null && _visual.GetValue(_builder) is { } builder && _form.Replays(ReducedMotionFollowers.Form.Of(builder.Resolve()))) Play();
        }

        // Resumes only a pause made here, so a pause from elsewhere survives a show.
        private void FollowVisibility()
        {
            if (_visual.IsEffectivelyVisible)
            {
                if (!_pausedHidden) return;
                _pausedHidden = false;
                if (_animation is PieceTargets pieces) pieces.Paused = false;
                else (_animation as Timer)?.Resume();
            }
            else if (_animation is PieceTargets { Paused: false, Animation: not { Paused: true } } pieces)
            {
                pieces.Paused = _pausedHidden = true;
            }
            else if (_animation is Timer { Paused: false } timer)
            {
                timer.Pause();
                _pausedHidden = true;
            }
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Play();

        private void HandleTapped(object? sender, TappedEventArgs e) => Play();

        private void HandleVisibilityChanged(object? sender, EventArgs e) => FollowVisibility();

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Stop();
    }
}
