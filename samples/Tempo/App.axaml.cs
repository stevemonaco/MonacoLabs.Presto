using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MonacoLabs.Presto;
using Tempo.Model;
using Tempo.ViewModels;
using Tempo.Views;
#if DEBUG
using MonacoLabs.Presto.DevTools;
#endif

namespace Tempo;

public partial class App : Application
{
    /// <summary>Set by <see cref="Program"/> before the app starts.</summary>
    public static LaunchOptions Launch { get; set; } = new();

    private static IDisposable? s_following;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
        Control.LoadedEvent.AddClassHandler<TopLevel>((top, _) => PrestoDevTools.Attach(top));
#endif
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var viewModel = await MainViewModel.LoadAsync(new TempoStore(), TimeProvider.System, new DispatcherTickSource());
        ApplyMotionSetting(viewModel);
        var window = new MainWindow { DataContext = viewModel };
        desktop.MainWindow = window;
        // Already loaded: the placement, the motion setting and the intro setting all come from it.
        window.Start(Task.CompletedTask, Launch.SkipIntro);
        window.Show();
    }

    /// <summary>Applies <see cref="MainViewModel.Motion"/> to <see cref="PrestoDefaults.ReducedMotion"/> now and on every change.</summary>
    /// <remarks>Code-only: the flag has no XAML spelling (BACKLOG, BRIDGE-REDUCED).</remarks>
    public static void ApplyMotionSetting(MainViewModel viewModel)
    {
        Apply();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Motion))
                Apply();
        };

        void Apply()
        {
            s_following?.Dispose();
            s_following = null;
            if (viewModel.Motion == MotionPreference.System)
                s_following = PrestoAvalonia.FollowSystemReducedMotion();
            else
                Presto.Defaults.ReducedMotion = viewModel.Motion == MotionPreference.Reduced;
        }
    }
}
