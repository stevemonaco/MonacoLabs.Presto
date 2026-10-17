using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
        var window = new MainWindow { DataContext = viewModel };
        if (Launch.Size is { } size)
            window.UseLaunchSize(size);
        desktop.MainWindow = window;
        // Already loaded: the placement, the motion setting and the intro setting all come from it.
        window.Start(Task.CompletedTask, Launch.SkipIntro);
        window.Show();
    }
}
