using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Events;
using MonacoLabs.Presto.Transforms;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>
/// GSAP ScrollSmoother-style scrolling: <see cref="SpeedProperty"/> parallax on an element inside a <see cref="ScrollViewer"/>,
/// <see cref="SmoothProperty"/> wheel easing on the scroller itself, and <see cref="CueForProperty"/> and <see cref="FadeEdgesProperty"/> edge cues.
/// </summary>
/// <remarks>
/// The parallax offset, <c>(1 − speed) × max(0, scroll − entry)</c>, drives the <c>"Y"</c> shorthand, or <c>"X"</c> when the scroller
/// only scrolls horizontally, and is written in the scroller's offset notification so it never trails the content by a frame.
/// Detaching the element or setting the speed back to 1 reverts it. While <see cref="PrestoDefaults.ReducedMotion"/> is set
/// the element stays at its layout position; the flag is read at each scroll, so a change takes effect at the next one.
/// </remarks>
public static class Scroll
{
    private const double WheelStep = 50;
    private const double SmoothDuration = 300;
    private const double EndTolerance = 1;

    /// <summary>How fast the element moves relative to the scrolled content: below 1 it lags behind, above 1 it leads.</summary>
    public static readonly AttachedProperty<double> SpeedProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("Speed", typeof(Scroll), 1);

    private static readonly AttachedProperty<Parallax?> ParallaxProperty =
        AvaloniaProperty.RegisterAttached<Control, Parallax?>("Parallax", typeof(Scroll));

    /// <summary>Whether mouse-wheel notches ease the scroller's offset to where they add up to instead of jumping 50 units each.</summary>
    /// <remarks>
    /// Only Win32 tells a notch (a whole multiple of 120) apart from precision-touchpad deltas, so elsewhere this does nothing.
    /// Touchpad, touch, scrollbar and keyboard input pass through, and any offset change the ease did not write stops it.
    /// Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each notch, the wheel takes its native steps.
    /// </remarks>
    public static readonly AttachedProperty<bool> SmoothProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("Smooth", typeof(Scroll));

    private static readonly AttachedProperty<Smoother?> SmootherProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, Smoother?>("Smoother", typeof(Scroll));

    /// <summary>Whether the scroller follows its content's growth while it is scrolled to the end, as a chat log does.</summary>
    /// <remarks>
    /// It scrolls vertically, or horizontally when only that axis scrolls. Within 1 DIP of the end, content that does not
    /// reach the viewport included, it is armed: growth of the extent, or a shrink of the viewport, eases to the new end with
    /// the motion of <see cref="PrestoAvalonia.ScrollTo(ScrollViewer, Vector, AnimationBuilder?)"/>, retargeting while
    /// more arrives, and jumps under <see cref="PrestoDefaults.ReducedMotion"/>. Any scroll it did not make that leaves the
    /// view short of the end releases it, and coming back to the end by any means re-arms it. While it is armed,
    /// <see cref="Presence.ScrollAnchorProperty"/> leaves the list to it.
    /// </remarks>
    public static readonly AttachedProperty<bool> StickToEndProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("StickToEnd", typeof(Scroll));

    private static readonly AttachedProperty<EndFollower?> EndFollowerProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, EndFollower?>("EndFollower", typeof(Scroll));

    /// <summary>The scroller, or the control templating one, whose <see cref="CueEdgeProperty"/> this element cues by its opacity.</summary>
    /// <remarks>
    /// The opacity is written with <c>SetCurrentValue</c> in the scroller's own notifications as the distance past the edge
    /// over <see cref="CueRampProperty"/>, clamped to 0..1, and cleared when this is cleared or the element detaches.
    /// </remarks>
    public static readonly AttachedProperty<Control?> CueForProperty =
        AvaloniaProperty.RegisterAttached<Visual, Control?>("CueFor", typeof(Scroll));

    /// <summary>The edge of the <see cref="CueForProperty"/> scroller that this element cues.</summary>
    public static readonly AttachedProperty<ScrollEdge> CueEdgeProperty =
        AvaloniaProperty.RegisterAttached<Visual, ScrollEdge>("CueEdge", typeof(Scroll), ScrollEdge.Top);

    /// <summary>How far, in DIPs, content must go past an edge before its cue is at full strength; set on the scroller or the control templating it.</summary>
    public static readonly AttachedProperty<double> CueRampProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("CueRamp", typeof(Scroll), 24);

    private static readonly AttachedProperty<ScrollCue?> CueProperty =
        AvaloniaProperty.RegisterAttached<Visual, ScrollCue?>("Cue", typeof(Scroll));

    /// <summary>
    /// How far, in DIPs, each end of the scroller, or of the control templating one, fades out while content lies past it; 0 turns the fade off.
    /// </summary>
    /// <remarks>
    /// The fade is an <see cref="Visual.OpacityMask"/> along the axis <see cref="StickToEndProperty"/> follows, replacing any mask on the
    /// control. Each fade is capped at half the control's length, and its strength follows <see cref="CueRampProperty"/> as a cue's does.
    /// </remarks>
    public static readonly AttachedProperty<double> FadeEdgesProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("FadeEdges", typeof(Scroll));

    private static readonly AttachedProperty<ScrollCue?> FadeProperty =
        AvaloniaProperty.RegisterAttached<Control, ScrollCue?>("Fade", typeof(Scroll));

    static Scroll()
    {
        SpeedProperty.Changed.AddClassHandler<Control>(HandleSpeedChanged);
        SmoothProperty.Changed.AddClassHandler<ScrollViewer>(HandleSmoothChanged);
        StickToEndProperty.Changed.AddClassHandler<ScrollViewer>(HandleStickToEndChanged);
        CueForProperty.Changed.AddClassHandler<Visual>(HandleCueForChanged);
        CueEdgeProperty.Changed.AddClassHandler<Visual>(static (element, _) => element.GetValue(CueProperty)?.Update());
        CueRampProperty.Changed.AddClassHandler<Control>(HandleCueRampChanged);
        FadeEdgesProperty.Changed.AddClassHandler<Control>(HandleFadeEdgesChanged);
    }

    /// <summary>Gets how fast <paramref name="control"/> moves relative to the scrolled content.</summary>
    public static double GetSpeed(Control control) => control.GetValue(SpeedProperty);

    /// <summary>Sets how fast <paramref name="control"/> moves relative to the scrolled content.</summary>
    public static void SetSpeed(Control control, double value) => control.SetValue(SpeedProperty, value);

    /// <summary>Gets whether <paramref name="scroller"/> eases mouse-wheel notches.</summary>
    public static bool GetSmooth(ScrollViewer scroller) => scroller.GetValue(SmoothProperty);

    /// <summary>Sets whether <paramref name="scroller"/> eases mouse-wheel notches.</summary>
    public static void SetSmooth(ScrollViewer scroller, bool value) => scroller.SetValue(SmoothProperty, value);

    private static void HandleSmoothChanged(ScrollViewer scroller, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode || !OperatingSystem.IsWindows()) return;
        if (e.NewValue is true) scroller.SetValue(SmootherProperty, new Smoother(scroller));
        else
        {
            scroller.GetValue(SmootherProperty)?.Release();
            scroller.ClearValue(SmootherProperty);
        }
    }

    /// <summary>Gets whether <paramref name="scroller"/> follows its content's growth at the end.</summary>
    public static bool GetStickToEnd(ScrollViewer scroller) => scroller.GetValue(StickToEndProperty);

    /// <summary>Sets whether <paramref name="scroller"/> follows its content's growth at the end.</summary>
    public static void SetStickToEnd(ScrollViewer scroller, bool value) => scroller.SetValue(StickToEndProperty, value);

    internal static bool StuckToEnd(ScrollViewer scroller) => scroller.GetValue(EndFollowerProperty)?.Armed == true;

    private static void HandleStickToEndChanged(ScrollViewer scroller, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        if (e.NewValue is true) scroller.SetValue(EndFollowerProperty, new EndFollower(scroller));
        else
        {
            scroller.GetValue(EndFollowerProperty)?.Release();
            scroller.ClearValue(EndFollowerProperty);
        }
    }

    /// <summary>Gets the scroller, or the control templating one, that <paramref name="element"/> cues.</summary>
    public static Control? GetCueFor(Visual element) => element.GetValue(CueForProperty);

    /// <summary>Sets the scroller, or the control templating one, that <paramref name="element"/> cues.</summary>
    public static void SetCueFor(Visual element, Control? value) => element.SetValue(CueForProperty, value);

    /// <summary>Gets the edge <paramref name="element"/> cues.</summary>
    public static ScrollEdge GetCueEdge(Visual element) => element.GetValue(CueEdgeProperty);

    /// <summary>Sets the edge <paramref name="element"/> cues.</summary>
    public static void SetCueEdge(Visual element, ScrollEdge value) => element.SetValue(CueEdgeProperty, value);

    /// <summary>Gets how far content must go past an edge of <paramref name="control"/>'s scroller before its cue is at full strength.</summary>
    public static double GetCueRamp(Control control) => control.GetValue(CueRampProperty);

    /// <summary>Sets how far content must go past an edge of <paramref name="control"/>'s scroller before its cue is at full strength.</summary>
    public static void SetCueRamp(Control control, double value) => control.SetValue(CueRampProperty, value);

    private static void HandleCueForChanged(Visual element, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        element.GetValue(CueProperty)?.Release();
        if (e.NewValue is Control target) element.SetValue(CueProperty, new ScrollCue(element, target));
        else element.ClearValue(CueProperty);
    }

    private static void HandleCueRampChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (ScrollCue.ScrollerOf(control) is { } scroller && ScrollContainer.Containers.TryGetValue(scroller, out var container)) container.UpdateCues();
    }

    /// <summary>Gets how far each end of <paramref name="control"/>'s scroller fades out while content lies past it.</summary>
    public static double GetFadeEdges(Control control) => control.GetValue(FadeEdgesProperty);

    /// <summary>Sets how far each end of <paramref name="control"/>'s scroller fades out while content lies past it.</summary>
    public static void SetFadeEdges(Control control, double value) => control.SetValue(FadeEdgesProperty, value);

    private static void HandleFadeEdgesChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var fade = control.GetValue(FadeProperty);
        if (e.NewValue is > 0d)
        {
            if (fade != null) fade.Update();
            else control.SetValue(FadeProperty, new ScrollCue(control, control, fade: true));
        }
        else
        {
            fade?.Release();
            control.ClearValue(FadeProperty);
        }
    }

    private static void HandleSpeedChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var parallax = control.GetValue(ParallaxProperty);
        if (e.NewValue is 1d)
        {
            parallax?.Release();
            control.ClearValue(ParallaxProperty);
        }
        else if (parallax == null)
        {
            control.SetValue(ParallaxProperty, new Parallax(control));
        }
    }

    internal sealed class Parallax
    {
        private readonly Control _control;
        private ScrollContainer? _container;
        private TransformShorthand _shorthand = TransformShorthand.Y;
        private double _entry;

        public Parallax(Control control)
        {
            _control = control;
            control.AttachedToVisualTree += HandleAttached;
            control.DetachedFromVisualTree += HandleDetached;
            if (control.IsAttachedToVisualTree()) HandleAttached(null, null);
        }

        public void Release()
        {
            _control.AttachedToVisualTree -= HandleAttached;
            _control.DetachedFromVisualTree -= HandleDetached;
            HandleDetached(null, null);
        }

        public void Measure()
        {
            var scroller = _container!.Scroller;
            var horizontal = scroller.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled
                && scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
            var shorthand = horizontal ? TransformShorthand.X : TransformShorthand.Y;
            if (shorthand != _shorthand) RenderTransformComposer.Revert(_control, _shorthand);
            _shorthand = shorthand;
            // TranslatePoint includes the render transform, so the parallax is lifted while measuring.
            RenderTransformComposer.Set(_control, shorthand, 0);
            var origin = _control.TranslatePoint(default, scroller) ?? default;
            var offset = scroller.Offset;
            // An element already in view at the top enters at a scroll of 0, so it starts at its layout position.
            _entry = Math.Max(0, horizontal
                ? origin.X + offset.X - scroller.Viewport.Width
                : origin.Y + offset.Y - scroller.Viewport.Height);
            Update();
        }

        public void Update()
        {
            var offset = _container!.Scroller.Offset;
            var scroll = _shorthand == TransformShorthand.X ? offset.X : offset.Y;
            var speed = Engine.Current.Defaults.ReducedMotion ? 1 : GetSpeed(_control);
            RenderTransformComposer.Set(_control, _shorthand, (1 - speed) * Math.Max(0, scroll - _entry));
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs? e)
        {
            if (_control.FindAncestorOfType<ScrollViewer>() is not { } scroller) return;
            _container = ScrollContainer.Register(scroller, this);
            _control.PropertyChanged += HandlePropertyChanged;
            Measure();
        }

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs? e)
        {
            if (_container == null) return;
            _control.PropertyChanged -= HandlePropertyChanged;
            RenderTransformComposer.Revert(_control, _shorthand);
            _container.Remove(this);
            _container = null;
        }

        private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.BoundsProperty) Measure();
        }
    }

    private sealed class Smoother
    {
        private readonly ScrollViewer _scroller;
        private readonly ScrollWriter _writer;
        private readonly Timer _ease;
        private Vector _from;
        private Vector? _to;

        public Smoother(ScrollViewer scroller)
        {
            _scroller = scroller;
            _writer = ScrollWriter.Of(scroller);
            using var origin = PrestoDiagnostics.Origin("ScrollObserver");
            _ease = Presto.CreateTimer(new TimerParams
            {
                Autoplay = false,
                Duration = SmoothDuration * Globals.TimeScale,
                OnUpdate = self => _writer.Write(this, self.Progress == 1 ? _to!.Value : _from + (_to!.Value - _from) * Eases.OutQuad(self.Progress)),
                OnComplete = _ => _to = null,
            });
            scroller.AddHandler(InputElement.PointerWheelChangedEvent, HandleWheel, RoutingStrategies.Tunnel);
            scroller.PropertyChanged += HandlePropertyChanged;
        }

        public void Release()
        {
            _scroller.RemoveHandler(InputElement.PointerWheelChangedEvent, HandleWheel);
            _scroller.PropertyChanged -= HandlePropertyChanged;
            _ease.Cancel();
        }

        private void HandleWheel(object? sender, PointerWheelEventArgs e)
        {
            if (Engine.Current.Defaults.ReducedMotion) return;
            // Ctrl/Cmd+wheel is the zoom gesture; on the tunnel pass this would take it from zoomable content inside.
            if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0) return;
            var delta = e.Delta;
            if (delta.X != Math.Round(delta.X) || delta.Y != Math.Round(delta.Y)) return;
            // Mirrors ScrollContentPresenter: Shift turns a vertical wheel horizontal.
            if (e.KeyModifiers == KeyModifiers.Shift && delta.X == 0) delta = new Vector(delta.Y, 0);
            var max = _scroller.ScrollBarMaximum;
            var from = _to ?? _scroller.Offset;
            var to = new Vector(
                Math.Clamp(from.X - delta.X * WheelStep, 0, max.X),
                Math.Clamp(from.Y - delta.Y * WheelStep, 0, max.Y));
            // At rest at the end the presenter gets the notch, so scroll chaining still works; mid-ease it would jump the rest.
            if (to == from)
            {
                e.Handled = _to != null;
                return;
            }
            e.Handled = true;
            var easing = _to != null;
            _from = _scroller.Offset;
            _to = to;
            // Rebasing drops Resume's head start; mid-ease, _from is the last frame's offset, so it times from that frame.
            _ease.Restart().ResetTime(easing ? _ease.Engine._currentTime : _ease.Engine.Now);
        }

        private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != ScrollViewer.OffsetProperty || _to == null || _writer.Wrote(this, _scroller.Offset)) return;
            _ease.Cancel();
            _to = null;
        }
    }

    private sealed class EndFollower
    {
        private readonly ScrollViewer _scroller;
        private Animation? _ease;

        public EndFollower(ScrollViewer scroller)
        {
            PrestoAvalonia.EnsureInitialized();
            _scroller = scroller;
            Armed = AtEnd();
            scroller.PropertyChanged += HandlePropertyChanged;
        }

        public bool Armed { get; private set; }

        private bool Horizontal => _scroller.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled
            && _scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;

        private bool Easing => _ease is { Completed: false, Canceled: false };

        public void Release()
        {
            _scroller.PropertyChanged -= HandlePropertyChanged;
            if (Easing) _ease!.Revert();
        }

        private bool AtEnd() => ScrollWriter.Along(_scroller.Offset, Horizontal) >= ScrollWriter.Max(_scroller, Horizontal) - EndTolerance;


        private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty)
            {
                if (AtEnd()) Armed = true;
                else if (!Easing || !ScrollWriter.Of(_scroller).TweenWrote(Horizontal, ScrollWriter.Along(_scroller.Offset, Horizontal))) Armed = false;
            }
            // A running ease aims at the end every frame, so further growth only retargets it.
            else if (e.Property == ScrollViewer.ScrollBarMaximumProperty && Armed && !Easing && !AtEnd())
            {
                var horizontal = Horizontal;
                var from = ScrollWriter.Along(_scroller.Offset, horizontal);
                var viewport = ScrollWriter.Along(_scroller.Viewport, horizontal);
                _ease = Presto.Animate(_scroller, ScrollMotion.Apply(
                    new AnimationParams { [horizontal ? "ScrollX" : "ScrollY"] = ScrollMotion.Tween(_scroller, horizontal, from, double.PositiveInfinity) },
                    (ScrollWriter.Max(_scroller, horizontal) - from) / viewport));
            }
        }
    }
}
