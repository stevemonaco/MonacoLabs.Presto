using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// A chat log in a virtualized <see cref="ListBox"/> whose scroller sets <see cref="Scroll.StickToEndProperty"/>: at the end
/// it follows new messages, scrolling up releases it and shows a pill that eases back through
/// <see cref="PrestoAvalonia.ScrollToIndex(ItemsControl, int, ScrollAlignment, AnimationBuilder?)"/> and re-arms it.
/// </summary>
public partial class ChatLogPage : ShowcasePage
{
    private const double MessageInterval = 2500;
    private const double EndTolerance = 1;

    private Animation? _jump;
    private int _jumpIndex;

    public ChatLogPage()
    {
        InitializeComponent();
        Log.AddHandler(ScrollViewer.ScrollChangedEvent, LogScrolled);
    }

    public ChatLogViewModel ViewModel => (ChatLogViewModel)DataContext!;

    protected override void OnAttached()
    {
        AfterFirstArrange(Log, () => ((ScrollViewer)Log.Scroll!).ScrollToEnd());
        Presto.CreateTimer(new TimerParams { Duration = MessageInterval, Loop = true, OnLoop = _ => ViewModel.Receive() });
    }

    private void LogScrolled(object? sender, ScrollChangedEventArgs e)
    {
        var scroll = (ScrollViewer)Log.Scroll!;
        if (scroll.Offset.Y >= scroll.ScrollBarMaximum.Y - EndTolerance) ViewModel.Behind = false;
        else if (e.OffsetDelta.Y < 0) ViewModel.Behind = true;
        // ScrollToIndex follows the item it was given, so a message landing mid-jump needs a new target.
        if (_jump is { Completed: false, Canceled: false } && _jumpIndex != ViewModel.Messages.Count - 1) JumpToNewest();
    }

    private void JumpToNewest(object? sender, RoutedEventArgs e) => JumpToNewest();

    private void JumpToNewest()
    {
        _jumpIndex = ViewModel.Messages.Count - 1;
        _jump = PrestoAvalonia.ScrollToIndex(Log, _jumpIndex, ScrollAlignment.End);
    }

    private void SendClicked(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Draft.Text)) return;
        ViewModel.Send(Draft.Text);
        Draft.Text = "";
    }
}
