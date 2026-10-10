using System.ComponentModel;
using Avalonia;
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
    private readonly Dictionary<TextBlock, Timeline> _digitMorphs = new();
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

    // In measure rather than on SizeChanged, so the intro's ring flight lands on the final size.
    protected override Size MeasureOverride(Size availableSize)
    {
        Dial.Width = Dial.Height = Math.Min(300, availableSize.Width - 48);
        return base.MeasureOverride(availableSize);
    }

    // Not in XAML: attaching mid-session would apply IconToPause's MorphTo at once, which throws (BACKLOG, BRIDGE-MOTION).
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        PrestoMotion.SetStates(PlayPause, (MotionStates)Resources["IconStates"]!);
        if (_viewModel is not null)
            ShowRemaining(_viewModel.RemainingText, morph: false);
        StartLabel.Text = _viewModel?.StartPauseLabel;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // A morph whose block detaches pauses for good, leaving its overlay host behind (BACKLOG, BRIDGE-RECIPES).
        CompleteDigitMorphs();
        _labelMorph?.Complete();
    }

    // Code-only: MorphGlyphs has no XAML trigger on a binding change (BACKLOG, BRIDGE-RECIPES).
    private void Morph(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsLoaded)
            return;
        if (e.PropertyName == nameof(FocusViewModel.RemainingText))
            ShowRemaining(_viewModel!.RemainingText, morph: true);
        else if (e.PropertyName == nameof(FocusViewModel.StartPauseLabel))
            MorphLabel(_viewModel!.StartPauseLabel);
    }

    // One block per character, so only the digits that change morph; reduced, a ticking crossfade would only flicker.
    private void ShowRemaining(string text, bool morph)
    {
        if (Digits.Children.Count != text.Length)
        {
            CompleteDigitMorphs();
            Digits.Children.Clear();
            foreach (var c in text)
            {
                var block = new TextBlock { Text = c.ToString() };
                if (c != ':')
                    block.Classes.Add("digit");
                Digits.Children.Add(block);
            }
            return;
        }
        for (var i = 0; i < text.Length; i++)
        {
            var block = (TextBlock)Digits.Children[i];
            var c = text[i].ToString();
            if (block.Text == c)
                continue;
            if (morph && !Presto.Defaults.ReducedMotion)
            {
                _digitMorphs[block] = Recipes.MorphGlyphs(block, c);
                continue;
            }
            if (_digitMorphs.Remove(block, out var running))
                running.Complete();
            block.Text = c;
        }
    }

    private void CompleteDigitMorphs()
    {
        foreach (var morph in _digitMorphs.Values)
            morph.Complete();
        _digitMorphs.Clear();
    }

    // The label hides while running, so only a word replacing a word morphs, and never under reduced motion.
    private void MorphLabel(string label)
    {
        if (label.Length > 0 && StartLabel.Text is { Length: > 0 } && !Presto.Defaults.ReducedMotion)
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
