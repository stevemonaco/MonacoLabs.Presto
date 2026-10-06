using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>
/// A dialog over a scrim that covers its host: <see cref="Open(Visual?)"/> reveals its content in a circle grown from an
/// element's center, and Escape, a tap on the scrim or <see cref="Close"/> closes it and gives focus back to the opener.
/// </summary>
/// <remarks>
/// <para>
/// It fills its host, such as a page's panel over the view it covers, and draws its content over the scrim; the content's
/// own alignment places it. While it is open, the scrim stops input to the host, and focus moves into it. The scrim fades
/// in with <see cref="Recipes.Fade"/> and out with <see cref="Recipes.FadeOut"/>, and <see cref="Backdrop"/>, when set,
/// blurs under it. Opened from an origin, the content grows from there with <c>ClipCircle</c> and shrinks back into it on
/// close. Opened without one, it shows at once, for content that animates in by itself, such as a shared element or the
/// entrances <see cref="Presence.EnterOnShowProperty"/> replays inside it, and fades out on close.
/// </para>
/// <para>
/// Closing hides the scrim and the content through <see cref="Presence"/>'s held hide
/// (<see cref="PresenceHideMode.Hold"/>), so they take no input while their exits play, and opening again before those
/// end plays the exits back from where they stand.
/// </para>
/// <para>
/// Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each open and close, the content shows and hides at once
/// while the scrim still fades.
/// </para>
/// </remarks>
public sealed class Modal : Control
{
    /// <summary>The dialog; it gives the modal its look and places itself through its alignment.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<Modal, Control?>(nameof(Content));

    /// <summary>Whether the modal is open. Setting it true opens it without an origin.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Modal, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The scrim over the host; null shows none and leaves a tap there to the host.</summary>
    public static readonly StyledProperty<IBrush?> ScrimProperty =
        AvaloniaProperty.Register<Modal, IBrush?>(nameof(Scrim), Dismissal.Scrim);

    /// <summary>The element under the modal that blurs while it is open, such as the view it covers.</summary>
    public static readonly StyledProperty<Control?> BackdropProperty =
        AvaloniaProperty.Register<Modal, Control?>(nameof(Backdrop));

    private const double BackdropRadius = 6;

    private readonly Border _scrim = new() { IsVisible = false, IsHitTestVisible = false };
    private readonly Decorator _panel = new() { IsVisible = false, IsHitTestVisible = false, Focusable = true };
    private Point? _origin;
    private IInputElement? _opener;

    /// <summary>Creates a closed modal.</summary>
    public Modal()
    {
        _scrim.Bind(Border.BackgroundProperty, this.GetObservable(ScrimProperty));
        _scrim.Tapped += (_, _) => IsOpen = false;
        Dismissal.Attach(this, () => IsOpen, Close);
        Presence.SetEnterOnShow(_panel, true);
        VisualChildren.Add(_scrim);
        VisualChildren.Add(_panel);
        LogicalChildren.Add(_scrim);
        LogicalChildren.Add(_panel);
    }

    /// <summary>Raised when the modal starts to close, however it was closed.</summary>
    public event EventHandler? Closed;

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public Control? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="ScrimProperty"/>
    public IBrush? Scrim
    {
        get => GetValue(ScrimProperty);
        set => SetValue(ScrimProperty, value);
    }

    /// <inheritdoc cref="BackdropProperty"/>
    public Control? Backdrop
    {
        get => GetValue(BackdropProperty);
        set => SetValue(BackdropProperty, value);
    }

    /// <summary>Opens the modal, revealing its content from the center of <paramref name="from"/>, or at once without it.</summary>
    public void Open(Visual? from = null) => Reveal(from?.TranslatePoint(new Point(from.Bounds.Width / 2, from.Bounds.Height / 2), this));

    /// <summary>Opens the modal, revealing its content from <paramref name="origin"/>, in the modal's coordinates.</summary>
    public void Open(Point origin) => Reveal(origin);

    /// <summary>Closes the modal, shrinking its content back into the origin it opened from.</summary>
    public void Close() => IsOpen = false;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty) Toggle(IsOpen);
        else if (change.Property == ContentProperty) _panel.Child = Content;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _scrim.Measure(availableSize);
        _panel.Measure(availableSize);
        // Measured even while the panel is hidden, so opening and closing never change the host's layout.
        Content?.Measure(availableSize);
        return Content?.DesiredSize ?? default;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _scrim.Arrange(new Rect(finalSize));
        _panel.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void Reveal(Point? origin)
    {
        if (IsOpen) return;
        _origin = origin;
        IsOpen = true;
    }

    private void Toggle(bool open)
    {
        var reduced = Engine.Current.Defaults.ReducedMotion;
        if (open)
        {
            _opener = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Presence.SetEnter(_scrim, Recipes.Fade());
            Presence.SetEnter(_panel, reduced || _origin is not { } at ? null : Recipes.CircularReveal(new RelativePoint(at, RelativeUnit.Absolute)));
        }
        else
        {
            Presence.SetExit(_scrim, Recipes.FadeOut());
            Presence.SetExit(_panel, reduced ? null : _origin is { } at ? Shrink(at) : Recipes.FadeOut());
            _origin = null;
        }
        _scrim.IsHitTestVisible = _panel.IsHitTestVisible = open;
        _scrim.IsVisible = _panel.IsVisible = open;
        Backdrop?.Animate(b => b.Blur(open ? BackdropRadius : 0).Duration(Recipes.Tokens.Medium).Ease(Recipes.Tokens.Ease));
        if (open)
        {
            _panel.Focus();
            return;
        }
        _opener?.Focus();
        _opener = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private static AnimationBuilder Shrink(Point at)
    {
        var center = TweenValue.Of(new RelativePoint(at, RelativeUnit.Absolute));
        return Presto.Build().ClipCircle(0).ClipCircleCenter(from: center, to: center).Duration(Recipes.Tokens.Medium).Ease(Recipes.Tokens.Ease);
    }
}
