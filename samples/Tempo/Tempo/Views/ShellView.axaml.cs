using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using Tempo.Motion;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class ShellView : UserControl
{
    private const double CompactBreakpoint = 720;

    public static readonly StyledProperty<Thickness> SafeAreaProperty = AvaloniaProperty.Register<ShellView, Thickness>(nameof(SafeArea));

    private MainViewModel? _viewModel;
    private CompositorAnimation? _drift;
    private ControlScope? _swell;
    private bool? _compact;

    public ShellView()
    {
        InitializeComponent();
        // Code-only: the Tasks page adopts the one TasksPane, so the column and the page never hold two lists' state.
        // Not on null: the template reaches nested presenters, and a typed FuncDataTemplate matches null content.
        PageHost.DataTemplates.Add(new FuncDataTemplate<MainViewModel>(vm => vm is not null, (_, _) => TasksPage()));
    }

    /// <summary>The system bars' insets, which pad the bars, the panels, the toasts and the bottom sheets while the backdrop runs edge to edge.</summary>
    public Thickness SafeArea
    {
        get => GetValue(SafeAreaProperty);
        set => SetValue(SafeAreaProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SafeAreaProperty)
            PadForSafeArea();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
            _viewModel.ConfirmRequested -= ShowConfirm;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
            _viewModel.ConfirmRequested += ShowConfirm;
        _compact = null;
        InvalidateMeasure();
    }

    // In measure rather than on SizeChanged, so the first frame already has the right layout.
    protected override Size MeasureOverride(Size availableSize)
    {
        var compact = availableSize.Width < CompactBreakpoint;
        if (_viewModel is not null && !double.IsInfinity(availableSize.Width) && compact != _compact)
        {
            _compact = compact;
            _viewModel.IsCompact = compact;
            Classes.Set("compact", compact);
            if (!compact)
                Move(TasksPane, TasksColumn);
            if (SafeArea != default)
                PadForSafeArea();
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Drift();
        Swell();
        Presto.Engine.ReducedMotionChanged += Redrift;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Presto.Engine.ReducedMotionChanged -= Redrift;
        _drift?.Revert();
        _drift = null;
    }

    // The base spacing is the styles' and the XAML's. Compact's bottom bar takes the bottom inset; wide, the panels and toasts do.
    private void PadForSafeArea()
    {
        var (left, top, right, bottom) = (SafeArea.Left, SafeArea.Top, SafeArea.Right, SafeArea.Bottom);
        var compact = _compact == true;
        var below = compact ? 0 : bottom;
        TitleBar.Height = 44 + top;
        TitleBar.Padding = new Thickness(left, top, right, 0);
        BottomBar.Padding = new Thickness(8 + left, 6, 8 + right, 6 + bottom);
        Panels.Margin = new Thickness(16 + left, 16, 16 + right, 16 + below);
        Toasts.Margin = new Thickness(16 + left, 0, 16 + right, 24 + below);
        DetailBody.Margin = SettingsBody.Margin = new Thickness(0, 0, 0, compact ? bottom : 0);
    }

    // Back in its column once the page leaves, after the detach, so a hidden column holds it between visits.
    private Decorator TasksPage()
    {
        var page = new Decorator();
        Move(TasksPane, page);
        page.DetachedFromVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (TasksPane.Parent == page)
                Move(TasksPane, TasksColumn);
        });
        return page;
    }

    private static void Move(Control child, Decorator to)
    {
        if (child.Parent == to)
            return;
        ((Decorator)child.Parent!).Child = null;
        to.Child = child;
    }

    // Code-only: XAML entry points stay on the UI thread (BRIDGE-THREAD), and the drift must not cost UI-thread work.
    private void Drift()
    {
        _drift?.Revert();
        _drift = PrestoAvalonia.AnimateOnCompositor(Blobs.Children, TempoMotion.BlobDrift);
    }

    // Code-only: a proximity value has no XAML spelling. The scope reverts with the blobs, stopping the values.
    private void Swell()
    {
        _swell?.Revert();
        _swell = PrestoAvalonia.CreateScope(Blobs);
        _swell.Execute(_ =>
        {
            foreach (var blob in Blobs.Children.Select(slot => ((Panel)slot).Children[0]))
            {
                var near = PrestoAvalonia.PointerProximityValue(blob, TempoMotion.BlobReach, fromCenter: true)
                    .Map(p => Presto.Defaults.ReducedMotion ? 0 : p)
                    .Spring(TempoMotion.BlobSpring);
                Follow.SetScale(blob, near.Map(TempoMotion.BlobSwell));
                Follow.Bind(blob, OpacityProperty, near.Map(TempoMotion.BlobGlow));
            }
        });
    }

    // A caller-owned compositor loop keeps the form it started with (BRIDGE-REDUCED).
    private void Redrift(Engine engine) => Drift();

    private void ShowConfirm(object? sender, EventArgs e) => Dialog.Reset();
}
