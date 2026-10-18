using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using Tempo.Motion;

namespace Tempo.Views;

public partial class FocusView : UserControl
{
    public FocusView()
    {
        InitializeComponent();
    }

    // In measure rather than on SizeChanged, so the intro's ring flight lands on the final size. The dial takes the height
    // the other rows leave; it is set once per pass, since changing it twice re-invalidates the measure every pass.
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Math.Min(300, availableSize.Width - 48);
        if (!double.IsInfinity(availableSize.Height) && Content is Grid grid)
        {
            var others = grid.Margin.Top + grid.Margin.Bottom + grid.RowSpacing * (grid.RowDefinitions.Count - 1);
            foreach (var child in grid.Children)
            {
                if (child == Dial)
                    continue;
                child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
                others += child.DesiredSize.Height;
            }
            size = Math.Min(size, availableSize.Height - others);
        }
        Dial.Width = Dial.Height = Math.Max(0, size);
        return base.MeasureOverride(availableSize);
    }

    // Code-only: Animate.OnTapped plays on the tapped element, and the glyph and its ring are not the button.
    private void SkipClicked(object? sender, RoutedEventArgs e)
    {
        SkipGlyph.Animate(TempoMotion.SkipForward);
        SkipSweep.Animate(TempoMotion.SkipSweep);
    }

    private void AbandonConfirmed(object? sender, EventArgs e)
    {
        StopGlyph.Animate(TempoMotion.AbandonCross);
        AbandonRing.Animate(TempoMotion.AbandonCollapse);
    }

    private void AbandonLetGo(object? sender, EventArgs e) => AbandonHold.Animate(TempoMotion.AbandonShake);
}
