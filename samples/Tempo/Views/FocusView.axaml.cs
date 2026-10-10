using Avalonia.Controls;
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

    public FocusView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
            _viewModel.SessionCompleted -= Celebrate;
        _viewModel = DataContext as FocusViewModel;
        if (_viewModel is not null)
            _viewModel.SessionCompleted += Celebrate;
    }

    // Not in XAML: attaching mid-session would apply IconToPause's MorphTo at once, which throws (BACKLOG, BRIDGE-MOTION).
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        PrestoMotion.SetStates(PlayPause, (MotionStates)Resources["IconStates"]!);
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

    private void AbandonConfirmed(object? sender, EventArgs e) => _viewModel?.AbandonCommand.Execute(null);
}
