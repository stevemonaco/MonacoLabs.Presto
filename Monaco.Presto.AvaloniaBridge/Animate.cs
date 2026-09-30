using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Attached properties that play an <see cref="AnimationBuilder"/> on a visual from XAML when it attaches, is tapped or its watched value changes.</summary>
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

    private sealed class Trigger
    {
        private readonly Visual _visual;
        private readonly AttachedProperty<AnimationBuilder?> _builder;
        private Animation? _animation;

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
            var animation = _animation = _visual.Animate(builder);
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
