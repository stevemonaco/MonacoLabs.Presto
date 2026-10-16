using Avalonia;
using Avalonia.Controls;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// A stack of toasts: <see cref="Show"/> adds one, which counts down and then dismisses itself. Hovering a toast or moving
/// focus into it holds its countdown, a horizontal swipe dismisses it, and past <see cref="MaxCount"/> the oldest leaves.
/// </summary>
/// <remarks>
/// <para>
/// The host owns its <see cref="ItemsControl.Items"/>: changing them other than through <see cref="Show"/> and
/// <see cref="Toast.Dismiss"/>, or setting <see cref="ItemsControl.ItemsSource"/>, throws
/// <see cref="InvalidOperationException"/>. Each toast's content is an item, built by
/// <see cref="ItemsControl.ItemTemplate"/>. The motion is the template's: put
/// <see cref="Presence.ArriveProperty"/> and <see cref="Presence.ExitProperty"/> on its root, and
/// <see cref="Animate.ItemLayoutProperty"/> on the host to restack the rest. Mark an element of the template with
/// <see cref="CountdownProperty"/> to make it the countdown bar. A swiped toast has slid out already, so it leaves
/// without its exit.
/// </para>
/// <para>
/// Under <see cref="PrestoDefaults.ReducedMotion"/>, read when each countdown starts, the bar holds full and fades to 35 %
/// opacity instead, so it still counts without moving; the toast still dismisses itself at the end.
/// </para>
/// </remarks>
public sealed class ToastHost : ItemsControl
{
    /// <summary>How many toasts show at once; showing another dismisses the oldest. Defaults to 3.</summary>
    public static readonly StyledProperty<int> MaxCountProperty =
        AvaloniaProperty.Register<ToastHost, int>(nameof(MaxCount), 3);

    /// <summary>
    /// Makes an element of a toast its countdown bar: the named property or shorthand, such as <c>ScaleX</c> or a
    /// <see cref="Avalonia.Controls.Primitives.RangeBase"/>'s <c>Value</c>, runs linearly from 1 to 0 over the toast's duration.
    /// </summary>
    public static readonly AttachedProperty<string?> CountdownProperty =
        AvaloniaProperty.RegisterAttached<ToastHost, Control, string?>("Countdown");

    /// <summary>The toast an element belongs to, inherited from the toast's item container.</summary>
    public static readonly AttachedProperty<Toast?> ToastProperty =
        AvaloniaProperty.RegisterAttached<ToastHost, Control, Toast?>("Toast", inherits: true);

    private readonly List<Toast> _toasts = [];
    private bool _changing;

    /// <summary>Creates an empty host.</summary>
    public ToastHost() => Items.CollectionChanged += (_, _) =>
    {
        if (!_changing)
        {
            throw new InvalidOperationException("A ToastHost owns its Items: add toasts with Show, remove them with Toast.Dismiss, and leave ItemsSource unset.");
        }
    };

    /// <inheritdoc cref="MaxCountProperty"/>
    public int MaxCount
    {
        get => GetValue(MaxCountProperty);
        set => SetValue(MaxCountProperty, value);
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    /// <summary>Gets the property <paramref name="control"/> counts down.</summary>
    public static string? GetCountdown(Control control) => control.GetValue(CountdownProperty);

    /// <summary>Sets the property <paramref name="control"/> counts down.</summary>
    public static void SetCountdown(Control control, string? value) => control.SetValue(CountdownProperty, value);

    /// <summary>Gets the toast <paramref name="control"/> belongs to, or null outside one.</summary>
    public static Toast? GetToast(Control control) => control.GetValue(ToastProperty);

    /// <summary>Adds a toast showing <paramref name="content"/> that dismisses itself <paramref name="duration"/> ms after it loads.</summary>
    public Toast Show(object content, double duration = 4000)
    {
        while (_toasts.Count >= Math.Max(MaxCount, 1)) _toasts[0].Dismiss();
        var toast = new Toast(this, duration);
        _toasts.Add(toast);
        _changing = true;
        Items.Add(content);
        _changing = false;
        return toast;
    }

    internal void Remove(Toast toast)
    {
        var index = _toasts.IndexOf(toast);
        if (index < 0) return;
        _toasts.RemoveAt(index);
        _changing = true;
        Items.RemoveAt(index);
        _changing = false;
    }

    /// <inheritdoc/>
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        _toasts[index].Attach(container);
    }

    /// <inheritdoc/>
    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        GetToast(container)?.Detach();
    }
}
