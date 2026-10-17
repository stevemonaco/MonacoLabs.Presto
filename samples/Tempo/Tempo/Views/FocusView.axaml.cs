using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using Tempo.Model;
using Tempo.Motion;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class FocusView : UserControl
{
    private FocusViewModel? _viewModel;
    private Timeline? _labelMorph;

    public FocusView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.SessionCompleted -= Celebrate;
            _viewModel.PropertyChanged -= Morph;
        }
        _viewModel = DataContext as FocusViewModel;
        if (_viewModel is not null)
        {
            _viewModel.SessionCompleted += Celebrate;
            _viewModel.PropertyChanged += Morph;
        }
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

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Remaining.Text = _viewModel?.RemainingText;
        StartLabel.Text = _viewModel?.StartPauseLabel;
    }

    // Code-only: MorphGlyphs has no XAML trigger on a binding change (BACKLOG, BRIDGE-RECIPES).
    private void Morph(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        if (e.PropertyName == nameof(FocusViewModel.RemainingText))
            Recipes.MorphGlyphs(Remaining, _viewModel!.RemainingText, reduced: ReducedMorph.Instant);
        else if (e.PropertyName == nameof(FocusViewModel.StartPauseLabel))
            MorphLabel(_viewModel!.StartPauseLabel);
    }

    // The label hides while running, so only a word replacing a word morphs.
    private void MorphLabel(string label)
    {
        if (label.Length > 0 && StartLabel.Text is { Length: > 0 })
        {
            _labelMorph = Recipes.MorphGlyphs(StartLabel, label, reduced: ReducedMorph.Instant);
            return;
        }
        _labelMorph?.Complete();
        StartLabel.Text = label;
    }

    private void Celebrate(object? sender, SessionCompletedEventArgs e)
    {
        if (e.Record.Mode != FocusMode.Focus)
        {
            Summary.Content = null;
            return;
        }

        // Code-only: a particle burst has no XAML trigger (BACKLOG, BRIDGE-PARTICLES); under reduced motion it spawns nothing.
        Recipes.Burst(Ring, color: TempoMotion.FocusAccent);
        var minutes = (int)Math.Round(e.Record.Focused.TotalMinutes);
        var length = minutes == 1 ? "1 minute" : $"{minutes} minutes";
        Summary.Content = e.TaskTitle is null ? $"{length} of focus" : $"{length} on {e.TaskTitle}";
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
