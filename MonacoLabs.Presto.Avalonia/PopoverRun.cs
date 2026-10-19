using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>
/// One open of a popup that uses <see cref="Popover"/>: plays Open on the presenter, then turns the first cancellable
/// close into Close, closing the popup for real through <see cref="Popup.Close"/> once Close ends. A <see cref="Popup"/>
/// owner is held open instead, its base <c>IsOpen</c> read by <see cref="StartPolling"/>. A <see cref="ToolTip"/>, and a held popup closed
/// past <c>IsOpen</c>, close at once while Close plays on a ghost.
/// </summary>
internal sealed class PopoverRun
{
    private readonly AvaloniaObject _owner;
    private readonly Control _presenter;
    private readonly Popup _popup;
    private readonly IDisposable? _transitions;
    private readonly AnimatedValue<bool>? _hold;
    private readonly Action? _stopPolling;
    private Point? _drawnAt;
    private Animation? _open;
    private Animation? _close;
    private bool _closing;
    private bool _releasing;

    /// <summary>
    /// Starts reading a held popup's base <c>IsOpen</c>, which no notification reports, and returns how to stop. A
    /// dispatcher timer rather than an engine timer, so an open popup lets the engine idle.
    /// </summary>
    internal static Func<Action, Action> StartPolling = poll =>
        new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, Dispatcher.UIThread, (_, _) => poll()).Stop;

    public PopoverRun(AvaloniaObject owner, Control presenter, Popup popup)
    {
        _owner = owner;
        _presenter = presenter;
        _popup = popup;
        if (owner is PopupFlyoutBase flyout) flyout.Closing += HandleClosing;
        if (owner is ContextMenu menu)
        {
            menu.Closing += HandleClosing;
            menu.Opening += HandleOpening;
        }
        if (owner == popup && Popover.Builder(Popover.CloseProperty, owner, presenter) is not null)
        {
            // Pushed at open, since a hold taken in IsOpen's change notification comes after the popup has closed.
            _hold = new AnimatedValue<bool>(popup, Popup.IsOpenProperty);
            _hold.Push(true);
            _stopPolling = StartPolling(Poll);
        }
        if (presenter is ToolTip || _hold is not null) presenter.LayoutUpdated += RecordPosition;
        _transitions = ThemeTransitions.Lift(presenter, presenter.Transitions, Visual.OpacityProperty);
    }

    public void Open()
    {
        if (Popover.Builder(Popover.OpenProperty, _owner, _presenter) is { } open) _open = Play(open.Resolve());
    }

    public void End()
    {
        _closing = false;
        if (_owner is PopupFlyoutBase flyout) flyout.Closing -= HandleClosing;
        if (_owner is ContextMenu menu)
        {
            menu.Closing -= HandleClosing;
            menu.Opening -= HandleOpening;
        }
        _presenter.LayoutUpdated -= RecordPosition;
        // Light dismiss and a detach close past IsOpen, overriding the hold's value and leaving the base true underneath.
        var bypassed = _hold is not null && !_popup.IsOpen && _popup.GetBaseValue(Popup.IsOpenProperty).GetValueOrDefault();
        if (_presenter is ToolTip || bypassed) Ghost();
        _close?.Revert();
        _open?.Revert();
        _transitions?.Dispose();
        _stopPolling?.Invoke();
        _hold?.Dispose();
        if (bypassed) _popup.ClearValue(Popup.IsOpenProperty);
    }

    private void HandleClosing(object? sender, CancelEventArgs e)
    {
        // A detaching target still reads as attached inside its detach handlers, so the cut is found through its parents.
        // A presenter already detached, as a native popup is when its window closes first, has nothing left to animate.
        if (_releasing || e.Cancel || _popup.PlacementTarget?.FindAncestorOfType<TopLevel>(includeSelf: true) is null ||
            !_presenter.IsAttachedToVisualTree()) return;
        if (_closing)
        {
            e.Cancel = true;
            return;
        }
        e.Cancel = PlayClose();
    }

    private void Poll()
    {
        var open = _popup.GetBaseValue(Popup.IsOpenProperty).GetValueOrDefault();
        if (open && _closing)
        {
            _closing = false;
            _close!.Reverse();
        }
        else if (!open && !_closing && !PlayClose())
        {
            CloseNow();
        }
    }

    private bool PlayClose()
    {
        if (Popover.Builder(Popover.CloseProperty, _owner, _presenter) is not { } close || Presto.Defaults.ReducedMotion) return false;
        if (_close is { Completed: false } reopened)
        {
            reopened.Play();
        }
        else
        {
            _close?.Revert();
            _close = Play(Closing(close.Resolve()));
            // An instant close, as PrestoDefaults.ReduceAll makes every one, has nothing to wait for.
            if (_close.Completed) return false;
        }
        _closing = true;
        return true;
    }

    private void HandleOpening(object? sender, CancelEventArgs e)
    {
        if (!_closing) return;
        _closing = false;
        _close!.Reverse();
        // Escape and ContextMenu.Close leave IsOpen false under the canceled close, where a later close would write nothing.
        _popup.SetCurrentValue(Popup.IsOpenProperty, true);
    }

    private AnimationParams Closing(AnimationParams parameters)
    {
        var completed = parameters.OnComplete;
        var paused = parameters.OnPause;
        return parameters with
        {
            OnComplete = timer =>
            {
                completed?.Invoke(timer);
                if (_closing && timer == _close) CloseNow();
            },
            OnPause = timer =>
            {
                paused?.Invoke(timer);
                if (timer.Canceled && _closing && timer == _close) CloseNow();
            },
        };
    }

    private void CloseNow()
    {
        _closing = false;
        if (_hold is not null)
        {
            _hold.Dispose();
            return;
        }
        _releasing = true;
        _popup.Close();
        _releasing = false;
    }

    private void RecordPosition(object? sender, EventArgs e)
    {
        // Through the screen, since a native popup is not in the anchor's window, and untransformed, as the ghost is placed.
        if (TopLevel.GetTopLevel(_popup.PlacementTarget ?? _popup) is { } top && _presenter.GetVisualParent() is { } parent)
            _drawnAt = top.PointToClient(parent.PointToScreen(_presenter.Bounds.Position));
    }

    private void Ghost()
    {
        // A popup leaving the logical tree, as it does with its anchor, closes at once.
        if (!((ILogical)_popup).IsAttachedToLogicalTree || Popover.Builder(Popover.CloseProperty, _owner, _presenter) is not { } close ||
            Presto.Defaults.ReducedMotion || _drawnAt is not { } position || TopLevel.GetTopLevel(_popup.PlacementTarget ?? _popup) is not { } top) return;
        // Losing :open has dropped the Fluent tooltip's opacity to 0.
        using var opaque = _presenter is ToolTip ? _presenter.SetValue(Visual.OpacityProperty, 1d, BindingPriority.Animation) : null;
        Presence.Ghost(_presenter, top, position, close);
    }

    private Animation Play(AnimationParams parameters)
    {
        PrestoAvalonia.EnsureInitialized();
        using var origin = PrestoDiagnostics.Origin("Popover");
        return Presto.Animate(_presenter, parameters with { HostLifetime = WhenDetached.Continue });
    }
}
