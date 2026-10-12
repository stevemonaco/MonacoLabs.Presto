using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using Tempo.Motion;

namespace Tempo.Views;

/// <summary>
/// The startup intro: <see cref="TempoIntro"/> plays on attach, and <see cref="HandedOff"/> completes once it and the load
/// are both done. Its ring and wordmark carry the <c>Connected.Key</c>s of the Focus ring and the title-bar wordmark.
/// </summary>
public partial class IntroView : UserControl
{
    private readonly Task _load;
    private readonly TaskCompletionSource _handedOff = new();
    private Timeline? _timeline;
    private ParticleEffect? _particles;

    public IntroView() : this(Task.CompletedTask)
    {
    }

    /// <param name="load">The startup work the handoff waits for; while it runs past the intro, the ring breathes.</param>
    public IntroView(Task load)
    {
        _load = load;
        InitializeComponent();
    }

    /// <summary>Completes when the window should swap to the shell.</summary>
    public Task HandedOff => _handedOff.Task;

    /// <summary>Ends the intro at once with the wordmark fully drawn; the handoff still waits for the load.</summary>
    public void Skip()
    {
        _timeline?.Complete();
        _particles?.Cancel();
        TempoMotion.IntroWordmarkLand.Set(Wordmark);
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        PrestoAvalonia.CreateDrawable(Track);
        PrestoAvalonia.CreateDrawable(Fill);
        _timeline = TempoIntro.Play(this, effect => _particles = effect);
        await _timeline.Then();
        if (!_load.IsCompleted)
        {
            _ = TempoMotion.RingBreathe.Animate(Ring);
            await _load;
        }
        _handedOff.TrySetResult();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter or Key.Escape)
        {
            e.Handled = true;
            Skip();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Skip();
    }
}
