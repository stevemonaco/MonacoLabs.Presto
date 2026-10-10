using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using Tempo.Model;
using Tempo.Motion;
using Tempo.ViewModels;
using PrestoMotion = MonacoLabs.Presto.Motion;

namespace Tempo.Views;

public partial class FocusView : UserControl
{
    private FocusViewModel? _viewModel;
    private Timeline? _morph;
    private Timeline? _labelMorph;
    private bool _abandoned;

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

    // Not in XAML: attaching mid-session would apply IconToPause's MorphTo at once, which throws (BACKLOG, BRIDGE-MOTION).
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        PrestoMotion.SetStates(PlayPause, (MotionStates)Resources["IconStates"]!);
        Digits.Text = _viewModel?.RemainingText;
        StartLabel.Text = _viewModel?.StartPauseLabel;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // A morph whose block detaches pauses for good, leaving its overlay host behind (BACKLOG, BRIDGE-RECIPES).
        _morph?.Complete();
        _labelMorph?.Complete();
    }

    // Code-only: MorphGlyphs has no XAML trigger on a binding change (BACKLOG, BRIDGE-RECIPES).
    private void Morph(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        if (e.PropertyName == nameof(FocusViewModel.RemainingText))
            _morph = Recipes.MorphGlyphs(Digits, _viewModel!.RemainingText);
        else if (e.PropertyName == nameof(FocusViewModel.StartPauseLabel))
            MorphLabel(_viewModel!.StartPauseLabel);
    }

    // The label hides while running, so only a word replacing a word morphs.
    private void MorphLabel(string label)
    {
        if (label.Length > 0 && StartLabel.Text is { Length: > 0 })
        {
            _labelMorph = Recipes.MorphGlyphs(StartLabel, label);
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

    private void AbandonConfirmed(object? sender, EventArgs e)
    {
        _abandoned = true;
        _viewModel?.AbandonCommand.Execute(null);
    }

    // Code-only: the ring raises nothing when a hold ends short of confirming (BACKLOG, BRIDGE-SUBMIT).
    private void AbandonReleased(object? sender, RoutedEventArgs e)
    {
        if (e is KeyEventArgs { Key: not (Key.Space or Key.Enter) })
            return;
        if (!_abandoned)
            AbandonHold.Animate(TempoMotion.AbandonShake);
        _abandoned = false;
    }
}
