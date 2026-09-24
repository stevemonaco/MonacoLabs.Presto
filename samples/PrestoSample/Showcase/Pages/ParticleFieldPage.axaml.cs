using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PrestoSample.Showcase.Pages;

public partial class ParticleFieldPage : ShowcasePage
{
    private const int BrowserMaximum = 4000;

    public ParticleFieldPage()
    {
        InitializeComponent();
        if (OperatingSystem.IsBrowser()) CountSlider.Maximum = BrowserMaximum;
        CountSlider.Value = ParticleFieldScene.DefaultCount;
    }

    protected override void OnAttached() => Field.Scene = new ParticleFieldScene { FpsChanged = fps => Meter.SceneFps = fps };

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(Field);
        Field.Post(new ParticleFieldScene.MovePointer(position, Inside(position)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Field.Post(new ParticleFieldScene.MovePointer(default, false));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var position = e.GetPosition(Field);
        if (Inside(position)) Field.Post(new ParticleFieldScene.BurstAt(position));
    }

    private bool Inside(Point position) => new Rect(Field.Bounds.Size).Contains(position);

    private void CountChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        Field.Post(new ParticleFieldScene.SetCount((int)e.NewValue));

    // Posted below Render priority so the button paints its pressed state before the dispatcher stops answering.
    private void Freeze(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() => Thread.Sleep(2000), DispatcherPriority.Background);
}
