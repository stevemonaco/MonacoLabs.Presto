using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Monaco.Presto.Core;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Material's press ripple: ink grows from the press point while the press is held until it covers the element, and
/// fades on release. Enter or Space on the focused element ripples from its centre.
/// </summary>
/// <remarks>
/// The ink is drawn over the element's content by a composition visual of its own on the render thread, so it never
/// lays the element out or takes its input. Its timing comes from <see cref="Recipes.Tokens"/>: it grows over
/// <see cref="MotionTokens.Medium"/> and fades over <see cref="MotionTokens.Short"/>, still growing as it fades.
/// Dragging out of the element, losing the pointer capture or scrolling an ancestor <see cref="ScrollViewer"/> cancels
/// the press. Inside a scroller a touch press waits 100 ms before it shows, so a fling never ripples, and a tap released
/// sooner still shows a short ripple. Under <see cref="PrestoDefaults.ReducedMotion"/>, read at the press, there is no
/// ink: a press shows the pressed tint, whatever <see cref="StateLayerProperty"/> says.
/// </remarks>
public static class Ripple
{
    private const double InkOpacity = .24;
    private const double HoverOpacity = .08;
    private const double FocusOpacity = .10;
    private const double PressedOpacity = .10;

    // Android's ViewConfiguration.getTapTimeout(), which Compose also waits inside a scroller.
    private const double TapTimeout = 100;

    /// <summary>Whether the element ripples when pressed.</summary>
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Enabled", typeof(Ripple));

    /// <summary>The ink colour, alpha included; null (the default) uses the theme accent at 24 % alpha, read at each press.</summary>
    public static readonly AttachedProperty<Color?> ColorProperty =
        AvaloniaProperty.RegisterAttached<Control, Color?>("Color", typeof(Ripple));

    /// <summary>
    /// Whether the ink grows from the press point and is clipped to the element's rounded rectangle (the default), or
    /// grows from the centre to an unclipped circle around the element.
    /// </summary>
    public static readonly AttachedProperty<bool> BoundedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Bounded", typeof(Ripple), true);

    /// <summary>
    /// Whether the element also shows Material's state layer: <see cref="ColorProperty"/>'s hue at 8 % while hovered, and at
    /// 10 % while focused from the keyboard or pressed, fading over <see cref="MotionTokens.Short"/>.
    /// </summary>
    public static readonly AttachedProperty<bool> StateLayerProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("StateLayer", typeof(Ripple));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, Tracker?>("Tracker", typeof(Ripple));

    static Ripple() => EnabledProperty.Changed.AddClassHandler<Control>(HandleEnabledChanged);

    /// <summary>Gets whether <paramref name="control"/> ripples when pressed.</summary>
    public static bool GetEnabled(Control control) => control.GetValue(EnabledProperty);

    /// <summary>Sets whether <paramref name="control"/> ripples when pressed.</summary>
    public static void SetEnabled(Control control, bool value) => control.SetValue(EnabledProperty, value);

    /// <summary>Gets the ink colour of <paramref name="control"/>, or null for the theme accent.</summary>
    public static Color? GetColor(Control control) => control.GetValue(ColorProperty);

    /// <summary>Sets the ink colour of <paramref name="control"/>, or null for the theme accent.</summary>
    public static void SetColor(Control control, Color? value) => control.SetValue(ColorProperty, value);

    /// <summary>Gets whether the ink of <paramref name="control"/> is clipped to it.</summary>
    public static bool GetBounded(Control control) => control.GetValue(BoundedProperty);

    /// <summary>Sets whether the ink of <paramref name="control"/> is clipped to it.</summary>
    public static void SetBounded(Control control, bool value) => control.SetValue(BoundedProperty, value);

    /// <summary>Gets whether <paramref name="control"/> shows the state-layer tint.</summary>
    public static bool GetStateLayer(Control control) => control.GetValue(StateLayerProperty);

    /// <summary>Sets whether <paramref name="control"/> shows the state-layer tint.</summary>
    public static void SetStateLayer(Control control, bool value) => control.SetValue(StateLayerProperty, value);

    internal static RippleHandler? HandlerOf(Control control) => control.GetValue(TrackerProperty)?.Handler;

    private static void HandleEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        control.GetValue(TrackerProperty)?.Release();
        if (e.NewValue is true) control.SetValue(TrackerProperty, new Tracker(control));
        else control.ClearValue(TrackerProperty);
    }

    private static Color InkColor(Control host)
    {
        if (GetColor(host) is { } color) return color;
        var theme = host.ActualThemeVariant;
        var accent = host.TryFindResource(theme == ThemeVariant.Dark ? "SystemAccentColorLight2" : "SystemAccentColor", theme, out var value) && value is Color found
            ? found
            : (host.GetValue(TextElement.ForegroundProperty) as ISolidColorBrush)?.Color ?? Colors.Black;
        return Color.FromArgb((byte)Math.Round(InkOpacity * 255), accent.R, accent.G, accent.B);
    }

    private static CornerRadius Corners(Control host) =>
        host switch { Border border => border.CornerRadius, TemplatedControl templated => templated.CornerRadius, _ => default };

    private static double Farthest(Point point, Rect box)
    {
        var x = Math.Max(point.X - box.Left, box.Right - point.X);
        var y = Math.Max(point.Y - box.Top, box.Bottom - point.Y);
        return Math.Sqrt((x * x) + (y * y));
    }

    private sealed class Tracker
    {
        private readonly Control _host;
        private CompositionCustomVisual? _visual;
        private IPointer? _pointer;
        private ScrollViewer? _scroller;
        private Timer? _tapTimer;
        private Point _pendingAt;
        private bool _pending;
        private bool _pressed;
        private bool _reduced;
        private bool _keyHeld;
        private double _tint;

        public Tracker(Control host)
        {
            _host = host;
            host.AttachedToVisualTree += HandleAttached;
            host.DetachedFromVisualTree += HandleDetached;
            host.SizeChanged += HandleSizeChanged;
            host.PropertyChanged += HandlePropertyChanged;
            host.AddHandler(InputElement.PointerPressedEvent, HandlePressed, handledEventsToo: true);
            host.AddHandler(InputElement.PointerMovedEvent, HandleMoved, handledEventsToo: true);
            host.AddHandler(InputElement.PointerReleasedEvent, HandleReleased, handledEventsToo: true);
            host.AddHandler(InputElement.PointerCaptureLostEvent, HandleCaptureLost, handledEventsToo: true);
            host.AddHandler(InputElement.KeyDownEvent, HandleKeyDown, handledEventsToo: true);
            host.AddHandler(InputElement.KeyUpEvent, HandleKeyUp, handledEventsToo: true);
            if (host.IsAttachedToVisualTree()) HandleAttached(null, null);
        }

        public RippleHandler? Handler { get; private set; }

        public void Release()
        {
            _host.AttachedToVisualTree -= HandleAttached;
            _host.DetachedFromVisualTree -= HandleDetached;
            _host.SizeChanged -= HandleSizeChanged;
            _host.PropertyChanged -= HandlePropertyChanged;
            _host.RemoveHandler(InputElement.PointerPressedEvent, HandlePressed);
            _host.RemoveHandler(InputElement.PointerMovedEvent, HandleMoved);
            _host.RemoveHandler(InputElement.PointerReleasedEvent, HandleReleased);
            _host.RemoveHandler(InputElement.PointerCaptureLostEvent, HandleCaptureLost);
            _host.RemoveHandler(InputElement.KeyDownEvent, HandleKeyDown);
            _host.RemoveHandler(InputElement.KeyUpEvent, HandleKeyUp);
            HandleDetached(null, null);
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs? e)
        {
            if (ElementComposition.GetElementVisual(_host) is not { } element) return;
            Handler = new RippleHandler();
            _visual = element.Compositor.CreateCustomVisual(Handler);
            _visual.Size = new Vector(_host.Bounds.Width, _host.Bounds.Height);
            ElementComposition.SetElementChildVisual(_host, _visual);
        }

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs? e)
        {
            End(released: false);
            _keyHeld = false;
            _tint = 0;
            if (_visual is null) return;
            ElementComposition.SetElementChildVisual(_host, null);
            _visual = null;
            Handler = null;
        }

        private void HandleSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_visual is not null) _visual.Size = new Vector(e.NewSize.Width, e.NewSize.Height);
        }

        private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == InputElement.IsPointerOverProperty || e.Property == InputElement.IsFocusedProperty || e.Property == StateLayerProperty) UpdateTint();
        }

        private void HandlePressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(_host).Properties.IsLeftButtonPressed) return;
            End(released: false);
            _pointer = e.Pointer;
            _reduced = Engine.Current.Defaults.ReducedMotion;
            _scroller = _host.FindAncestorOfType<ScrollViewer>();
            if (_scroller is not null) _scroller.ScrollChanged += HandleScrolled;
            if (e.Pointer.Type != PointerType.Touch || _scroller is null)
            {
                Show(e.GetPosition(_host));
                return;
            }
            _pendingAt = e.GetPosition(_host);
            _pending = true;
            _tapTimer ??= Presto.CreateTimer(new TimerParams { Autoplay = false, Duration = TapTimeout * Globals.TimeScale, OnComplete = _ => { _pending = false; Show(_pendingAt); } });
            _tapTimer.Restart();
        }

        private void HandleMoved(object? sender, PointerEventArgs e)
        {
            if (ReferenceEquals(e.Pointer, _pointer) && !new Rect(_host.Bounds.Size).Contains(e.GetPosition(_host))) End(released: false);
        }

        private void HandleReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (ReferenceEquals(e.Pointer, _pointer)) End(released: true);
        }

        private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (ReferenceEquals(e.Pointer, _pointer)) End(released: false);
        }

        private void HandleScrolled(object? sender, ScrollChangedEventArgs e)
        {
            if (e.OffsetDelta != default) End(released: false);
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (_keyHeld || e.Key is not (Key.Enter or Key.Space) || !ReferenceEquals(e.Source, _host)) return;
            _keyHeld = true;
            _reduced = Engine.Current.Defaults.ReducedMotion;
            Show(new Rect(_host.Bounds.Size).Center);
        }

        private void HandleKeyUp(object? sender, KeyEventArgs e)
        {
            if (!_keyHeld || e.Key is not (Key.Enter or Key.Space)) return;
            _keyHeld = false;
            End(released: true);
        }

        private void End(bool released)
        {
            // A pending touch press shows (then fades at once) only when released.
            if (_pending)
            {
                _tapTimer!.Cancel();
                _pending = false;
                if (released) Show(_pendingAt);
            }
            if (_pressed) _visual?.SendHandlerMessage(RippleHandler.ReleaseMessage);
            if (_scroller is not null) _scroller.ScrollChanged -= HandleScrolled;
            _scroller = null;
            _pointer = null;
            _pressed = false;
            UpdateTint();
        }

        private void Show(Point at)
        {
            _pressed = true;
            UpdateTint();
            if (_visual is null || _reduced) return;
            var box = new Rect(_host.Bounds.Size);
            var bounded = GetBounded(_host);
            var centre = bounded ? at : box.Center;
            var tokens = Recipes.Tokens;
            _visual.ClipToBounds = bounded;
            _visual.SendHandlerMessage(new RippleHandler.Press(
                centre,
                Farthest(centre, box),
                bounded ? new RoundedRect(box, Corners(_host)) : null,
                InkColor(_host),
                tokens.Medium,
                tokens.Ease.Resolve(_host, 0, 1, out _),
                tokens.Short));
        }

        private void UpdateTint()
        {
            var layer = GetStateLayer(_host);
            var opacity = _pressed && (layer || _reduced) ? PressedOpacity
                : !layer ? 0
                : _host.IsFocused && _host.Classes.Contains(":focus-visible") ? FocusOpacity
                : _host.IsPointerOver ? HoverOpacity
                : 0;
            if (opacity == _tint || _visual is null) return;
            _tint = opacity;
            var color = InkColor(_host);
            _visual.SendHandlerMessage(new RippleHandler.Tint(
                Color.FromRgb(color.R, color.G, color.B),
                opacity,
                Corners(_host),
                Engine.Current.Defaults.ReducedMotion ? 0 : Recipes.Tokens.Short));
        }
    }
}
