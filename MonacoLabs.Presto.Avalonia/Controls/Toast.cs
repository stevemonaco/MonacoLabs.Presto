using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Gestures;

namespace MonacoLabs.Presto.Controls;

/// <summary>A toast <see cref="ToastHost.Show"/> added, whose countdown starts once it loads.</summary>
public sealed class Toast
{
    private readonly ToastHost _host;
    private readonly double _duration;
    private Control? _container;
    private Timer? _countdown;

    internal Toast(ToastHost host, double duration)
    {
        _host = host;
        _duration = duration;
    }

    /// <summary>Removes the toast from its host, which plays its exit.</summary>
    public void Dismiss()
    {
        Detach();
        _countdown?.Cancel();
        _host.Remove(this);
    }

    internal void Attach(Control container)
    {
        Detach();
        _container = container;
        container.SetValue(ToastHost.ToastProperty, this);
        Swipe.SetDismiss(container, true);
        Swipe.SetCommand(container, new Swiped(this));
        container.Loaded += HandleLoaded;
        container.PropertyChanged += HandlePropertyChanged;
    }

    internal void Detach()
    {
        if (_container is not { } container) return;
        container.Loaded -= HandleLoaded;
        container.PropertyChanged -= HandlePropertyChanged;
    }

    private void HandleLoaded(object? sender, RoutedEventArgs e)
    {
        if (_countdown is not null) return;
        var bar = _container!.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => ToastHost.GetCountdown(control) is not null);
        if (bar is null)
        {
            _countdown = Presto.CreateTimer(new TimerParams { Duration = _duration, OnComplete = _ => Dismiss() });
        }
        else
        {
            var reduced = Engine.Current.Defaults.ReducedMotion;
            _countdown = bar.Animate(new AvaloniaAnimationParams
            {
                [reduced ? "Opacity" : ToastHost.GetCountdown(bar)!] = TweenValue.FromTo(1, reduced ? .35 : 0),
                Duration = _duration,
                Ease = Eases.None,
                OnComplete = _ => Dismiss(),
            });
        }
        Hold();
    }

    private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == InputElement.IsPointerOverProperty || e.Property == InputElement.IsKeyboardFocusWithinProperty) Hold();
    }

    private void Hold()
    {
        if (_container is { IsPointerOver: true } or { IsKeyboardFocusWithin: true }) _countdown?.Pause();
        else _countdown?.Resume();
    }

    private sealed class Swiped(Toast toast) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            // It has slid out already; at opacity 0 it leaves without playing its exit from its slot.
            toast._container!.Opacity = 0;
            toast.Dismiss();
        }
    }
}
