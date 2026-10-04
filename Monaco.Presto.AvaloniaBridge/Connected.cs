using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.Diagnostics;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Attached properties that give navigation connected animations from XAML: the Toolkit's <c>Connected.Key</c> and <c>Connected.AnchorElement</c>.</summary>
public static class Connected
{
    /// <summary>
    /// Marks elements in two views as the same thing: when one attaches while another with the key is attached, or one that
    /// left prepared it on detach, it flies from where that one is drawn.
    /// </summary>
    /// <remarks>
    /// A keyed element measures itself on every layout pass, one parent walk each; fine for a page or a list of dozens, but a
    /// virtualising list of thousands should key only the items it needs.
    /// </remarks>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Key", typeof(Connected));

    /// <summary>
    /// How an element arrives: a keyed one's flight, under the rules of <see cref="PrestoAvalonia.AnimateSharedElement(Control, Control, AnimationBuilder, LayoutAnimateMode, bool)"/>,
    /// or an anchored one's entrance, played with <see cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> MotionProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Motion", typeof(Connected));

    /// <summary>
    /// Makes an element enter alongside the flight of this key without flying itself: when the flight starts, or when the
    /// element attaches while it is in progress. Detaching reverts the entrance.
    /// </summary>
    public static readonly AttachedProperty<string?> AnchorProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Anchor", typeof(Connected));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, Tracker?>("Tracker", typeof(Connected));

    private static readonly AttachedProperty<Entrance?> EntranceProperty =
        AvaloniaProperty.RegisterAttached<Control, Entrance?>("Entrance", typeof(Connected));

    private static readonly Dictionary<string, Registration> s_keys = new();
    private static AnimationBuilder s_defaultMotion = Presto.Build().Duration(500).Ease(Eases.OutQuint);
    private static AnimationBuilder s_defaultAnchorMotion =
        Presto.Build().Y(from: 24).Opacity(from: 0).Duration(400).Delay(150).Ease(Eases.OutQuart);

    static Connected()
    {
        KeyProperty.Changed.AddClassHandler<Control>(HandleKeyChanged);
        AnchorProperty.Changed.AddClassHandler<Control>(HandleAnchorChanged);
    }

    /// <summary>The flight for an arriving keyed element with no <see cref="MotionProperty"/> of its own; the getter hands out a copy.</summary>
    public static AnimationBuilder DefaultMotion
    {
        get => s_defaultMotion.Clone();
        set => s_defaultMotion = value;
    }

    /// <summary>The entrance for an anchored element with no <see cref="MotionProperty"/> of its own; the getter hands out a copy.</summary>
    public static AnimationBuilder DefaultAnchorMotion
    {
        get => s_defaultAnchorMotion.Clone();
        set => s_defaultAnchorMotion = value;
    }

    /// <summary>Gets the key that connects <paramref name="control"/> to its counterpart in another view.</summary>
    public static string? GetKey(Control control) => control.GetValue(KeyProperty);

    /// <summary>Sets the key that connects <paramref name="control"/> to its counterpart in another view.</summary>
    public static void SetKey(Control control, string? value) => control.SetValue(KeyProperty, value);

    /// <summary>Gets how <paramref name="control"/> flies in when it arrives.</summary>
    public static AnimationBuilder? GetMotion(Control control) => control.GetValue(MotionProperty);

    /// <summary>Sets how <paramref name="control"/> flies in when it arrives.</summary>
    public static void SetMotion(Control control, AnimationBuilder? value) => control.SetValue(MotionProperty, value);

    /// <summary>Gets the key whose flight <paramref name="control"/> enters alongside.</summary>
    public static string? GetAnchor(Control control) => control.GetValue(AnchorProperty);

    /// <summary>Sets the key whose flight <paramref name="control"/> enters alongside.</summary>
    public static void SetAnchor(Control control, string? value) => control.SetValue(AnchorProperty, value);

    private static void HandleKeyChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var attached = control.IsAttachedToVisualTree();
        if (attached && e.OldValue is string old) Unregister(old, control);
        var tracker = control.GetValue(TrackerProperty);
        if (e.NewValue is not string key)
        {
            tracker?.Release();
            control.ClearValue(TrackerProperty);
            return;
        }
        if (tracker is null) control.SetValue(TrackerProperty, new Tracker(control));
        if (attached) Register(key, control);
    }

    private static void HandleAnchorChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var attached = control.IsAttachedToVisualTree();
        if (attached && e.OldValue is string old) Unregister(old, control, anchor: true);
        var entrance = control.GetValue(EntranceProperty);
        if (e.NewValue is not string key)
        {
            entrance?.Release();
            control.ClearValue(EntranceProperty);
            return;
        }
        if (entrance is null) control.SetValue(EntranceProperty, new Entrance(control));
        if (attached) Register(key, control, anchor: true);
    }

    private static Registration Register(string key, Control control, bool anchor = false)
    {
        if (!s_keys.TryGetValue(key, out var registration)) s_keys[key] = registration = new();
        (anchor ? registration.Anchors : registration.Elements).Add(new(control));
        return registration;
    }

    private static void Unregister(string key, Control control, bool anchor = false)
    {
        if (!s_keys.TryGetValue(key, out var registration)) return;
        (anchor ? registration.Anchors : registration.Elements).RemoveAll(element => !element.TryGetTarget(out var other) || other == control);
        if (registration.Elements.Count == 0 && registration.Anchors.Count == 0) s_keys.Remove(key);
    }

    private static Control? Other(string key, Control control)
    {
        if (!s_keys.TryGetValue(key, out var registration)) return null;
        foreach (var element in registration.Elements)
        {
            if (element.TryGetTarget(out var other) && other != control) return other;
        }
        return null;
    }

    private static void Arrive(string key, Control control)
    {
        var registration = Register(key, control);
        var motion = GetMotion(control) ?? s_defaultMotion;
        using var origin = PrestoDiagnostics.Origin("Connected");
        var flight = Other(key, control) is { } source
            ? PrestoAvalonia.AnimateSharedElement(source, control, motion)
            : PrestoAvalonia.AnimateSharedElement(key, control, motion);
        if (flight is null) return;
        registration.Flight = flight;
        foreach (var anchor in registration.Anchors)
        {
            if (anchor.TryGetTarget(out var anchored)) anchored.GetValue(EntranceProperty)?.Play();
        }
    }

    private sealed class Registration
    {
        public List<WeakReference<Control>> Elements { get; } = [];

        public List<WeakReference<Control>> Anchors { get; } = [];

        public LayoutTransition? Flight { get; set; }
    }

    private sealed class Tracker
    {
        private readonly Control _control;
        private Rect? _slot;

        public Tracker(Control control)
        {
            _control = control;
            control.AttachedToVisualTree += HandleAttached;
            control.DetachedFromVisualTree += HandleDetached;
            control.LayoutUpdated += HandleLayoutUpdated;
        }

        public void Release()
        {
            _control.AttachedToVisualTree -= HandleAttached;
            _control.DetachedFromVisualTree -= HandleDetached;
            _control.LayoutUpdated -= HandleLayoutUpdated;
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Arrive(GetKey(_control)!, _control);

        // The leaving view is already cut from the window here, so the slot recorded on its last layout pass is all there is.
        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            var key = GetKey(_control)!;
            Unregister(key, _control);
            if (_slot is { } slot && Other(key, _control) is null) LayoutTransition.Prepare(key, LayoutTransition.Drawn(_control, slot));
            _slot = null;
        }

        private void HandleLayoutUpdated(object? sender, EventArgs e) => _slot = LayoutTransition.MeasureOnScreen(_control);
    }

    private sealed class Entrance
    {
        private readonly Control _control;
        private CompositorAnimation? _animation;

        public Entrance(Control control)
        {
            _control = control;
            control.AttachedToVisualTree += HandleAttached;
            control.DetachedFromVisualTree += HandleDetached;
        }

        public void Play()
        {
            Stop();
            using var origin = PrestoDiagnostics.Origin("Connected");
            _animation = PrestoAvalonia.AnimateOnCompositor(_control, GetMotion(_control) ?? s_defaultAnchorMotion);
        }

        public void Release()
        {
            _control.AttachedToVisualTree -= HandleAttached;
            _control.DetachedFromVisualTree -= HandleDetached;
            Stop();
        }

        private void Stop()
        {
            _animation?.Revert();
            _animation = null;
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (Register(GetAnchor(_control)!, _control, anchor: true).Flight is { InFlight: true }) Play();
        }

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            Unregister(GetAnchor(_control)!, _control, anchor: true);
            Stop();
        }
    }
}
