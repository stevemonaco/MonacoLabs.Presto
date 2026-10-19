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
    /// <summary>Set by a head before the app starts.</summary>
    public static LaunchOptions Launch { get; set; } = new();

    /// <summary>Set by a head before the app starts; null keeps the data in a <see cref="FileTempoStorage"/>.</summary>
    public static ITempoStorage? Storage { get; set; }

    private static Task<MainViewModel>? _load;

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
        var load = _load = MainViewModel.LoadAsync(new TempoStore(Storage ?? new FileTempoStorage()),TimeProvider.System, new DispatcherTickSource());
        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
            activatable.Deactivated += async (_, e) =>
            {
                if (e.Kind == ActivationKind.Background)
                    await FlushAsync();
            };
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                var window = new MainWindow { DataContext = await load };
                if (Launch.Size is { } size)
                    window.UseLaunchSize(size);
                desktop.MainWindow = window;
                // Already loaded: the placement, the motion setting and the intro setting all come from it.
                window.View.Start(Task.CompletedTask, Launch.SkipIntro);
                window.Show();
                break;
            // Set before any await: Android reads the factory as soon as this returns.
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => CreateView(load);
                break;
            case ISingleViewApplicationLifetime single:
                single.MainView = CreateView(load);
                break;
        }
    }

    /// <summary>Lands any pending save of the running app's view model; for a head whose own signal that it is going away the lifetime does not report.</summary>
    public static async Task FlushAsync()
    {
        if (_load is { } load)
            await (await load).FlushAsync();
    }

    private static MainView CreateView(Task<MainViewModel> load)
    {
        var view = new MainView { IsVisible = false };
        ShowWhenLoaded(view, load);
        return view;
    }

    private static async void ShowWhenLoaded(MainView view, Task<MainViewModel> load)
    {
        view.DataContext = await load;
        view.IsVisible = true;
        view.Start(Task.CompletedTask, skipIntro: false);
    }
}
