using Avalonia.Controls;
using Avalonia.Input;
using MonacoLabs.Presto;
using Tempo.Motion;

namespace Tempo.Views;

public partial class ConfirmDialog : UserControl
{
    private bool _pressed;

    public ConfirmDialog()
    {
        InitializeComponent();
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

    private void HoldPressed(object? sender, EventArgs e) => _pressed = true;
}
