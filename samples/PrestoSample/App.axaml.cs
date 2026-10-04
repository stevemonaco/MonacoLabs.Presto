using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Monaco.Presto.AvaloniaBridge.DevTools;
using PrestoSample.ViewModels;
using PrestoSample.Views;

namespace PrestoSample;

public partial class App : Application
{
    /// <summary>Set by a head before the app starts; applied to the first view model and the main window.</summary>
    public static LaunchOptions Launch { get; set; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
        #region snippet DevToolsAttach
        Control.LoadedEvent.AddClassHandler<TopLevel>((top, _) => PrestoDevTools.Attach(top));
        #endregion
#endif
#if PRESTO_HOTRELOAD
        AvaloniaUI.DiagnosticsSupport.HotReload.HotReloadDiagnostics.EntryLogged +=
            entry => Console.WriteLine($"[HotReload:{entry.Level}] {entry.Message}");
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow { DataContext = CreateViewModel() };
            if (Launch is { Width: { } width, Height: { } height })
            {
                window.Width = width;
                window.Height = height;
            }
            desktop.MainWindow = window;
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = CreateViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = CreateViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainViewModel CreateViewModel()
    {
        var viewModel = new MainViewModel(Launch.Page);
        if (Launch.Motion is { } motion) viewModel.Motion = motion;
        return viewModel;
    }
}
