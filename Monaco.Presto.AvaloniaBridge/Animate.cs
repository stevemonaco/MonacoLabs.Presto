using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Attached properties that play an <see cref="AnimationBuilder"/> on a visual from XAML.</summary>
public static class Animate
{
    /// <summary>
    /// Plays the builder on the visual each time it attaches to the visual tree and reverts it on detach; changing the
    /// value reverts the previous animation and plays the new one, and <see langword="null"/> stops listening.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> OnAttachedProperty =
        AvaloniaProperty.RegisterAttached<Visual, AnimationBuilder?>("OnAttached", typeof(Animate));

    private static readonly AttachedProperty<Attachment?> AttachmentProperty =
        AvaloniaProperty.RegisterAttached<Visual, Attachment?>("Attachment", typeof(Animate));

    static Animate() => OnAttachedProperty.Changed.AddClassHandler<Visual>(HandleOnAttachedChanged);

    /// <summary>Gets the builder played when <paramref name="visual"/> attaches to the visual tree.</summary>
    public static AnimationBuilder? GetOnAttached(Visual visual) => visual.GetValue(OnAttachedProperty);

    /// <summary>Sets the builder played when <paramref name="visual"/> attaches to the visual tree.</summary>
    public static void SetOnAttached(Visual visual, AnimationBuilder? value) => visual.SetValue(OnAttachedProperty, value);

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
        if (attachment is null) visual.SetValue(AttachmentProperty, attachment = new Attachment(visual));
        attachment.Play();
    }

    private sealed class Attachment
    {
        private readonly Visual _visual;
        private Animation? _animation;

        public Attachment(Visual visual)
        {
            _visual = visual;
            visual.AttachedToVisualTree += HandleAttached;
            visual.DetachedFromVisualTree += HandleDetached;
        }

        public void Play()
        {
            Stop();
            if (_visual.IsAttachedToVisualTree() && GetOnAttached(_visual) is { } builder) _animation = _visual.Animate(builder);
        }

        public void Release()
        {
            _visual.AttachedToVisualTree -= HandleAttached;
            _visual.DetachedFromVisualTree -= HandleDetached;
            Stop();
        }

        private void Stop()
        {
            _animation?.Revert();
            _animation = null;
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Play();

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Stop();
    }
}
