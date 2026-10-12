using Avalonia.Controls;
using Avalonia.Input;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using Tempo.Motion;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class ConfirmDialog : UserControl
{
    private bool _pressed;

    public ConfirmDialog()
    {
        InitializeComponent();
        Hold.PropertyChanged += (_, e) => _pressed |= e.Property == ProgressRing.ValueProperty && Hold.Value > 0;
    }

    /// <summary>Forgets the last opening's press, so Escape cancels until the hold is pressed again.</summary>
    public void Reset() => _pressed = false;

    // Once the hold has been pressed, Escape shakes the card and is handled before Dismissal's top-level handler closes the modal.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape || !_pressed)
            return;
        e.Handled = true;
        Card.Animate(TempoMotion.DialogShake);
    }

    private void HoldConfirmed(object? sender, EventArgs e) => (DataContext as MainViewModel)?.ConfirmCommand.Execute(null);
}
