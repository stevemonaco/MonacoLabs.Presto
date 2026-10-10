using System.ComponentModel;
using Avalonia.Controls;
using MonacoLabs.Presto;
using Tempo.Motion;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class SettingsFlyout : UserControl
{
    private MainViewModel? _viewModel;

    public SettingsFlyout()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ReplayPreview;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += ReplayPreview;
    }

    // Code-only: the reduced form is resolved when the preview starts, and nothing replays OnAttached on a change of the flag (BACKLOG, BRIDGE-REDUCED).
    private void ReplayPreview(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Motion))
            return;
        Animate.SetOnAttached(MotionPreview, null);
        Animate.SetOnAttached(MotionPreview, TempoMotion.MotionPreview);
    }
}
