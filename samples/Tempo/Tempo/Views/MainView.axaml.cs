using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using MonacoLabs.Presto.Controls;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class MainView : UserControl
{
    private readonly KeyBinding[] _keys;
    private readonly KeyBinding[] _plainKeys;
    private TopLevel? _top;

    public MainView()
    {
        InitializeComponent();
        _keys = [.. KeyBindings];
        _plainKeys = [.. _keys.Where(b => b.Gesture?.KeyModifiers == KeyModifiers.None)];
        KeyBindings.Clear();
    }

    /// <summary>
    /// Plays the intro over <paramref name="load"/> and then shows the shell, unless the intro setting is off or
    /// <paramref name="skipIntro"/> is set, in which case the shell shows with its normal entrance.
    /// </summary>
    public async void Start(Task load, bool skipIntro)
    {
        if (skipIntro || DataContext is not MainViewModel { PlayIntro: true })
            return;
        var shell = Content;
        var intro = new IntroView(load);
        Content = intro;
        EnablePlainKeys(false);
        await intro.HandedOff;
        Content = shell;
        EnablePlainKeys(true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top is null)
            return;
        _top.KeyBindings.AddRange(_keys);
        EnablePlainKeys(true);
        // Avalonia runs key bindings before the focused control sees the key, so N and Del would fire while typing.
        _top.AddHandler(GotFocusEvent, HandleGotFocus);
        _top.AddHandler(LostFocusEvent, HandleLostFocus);
        // Tunneling, so it wins over the focused control: as a key binding, Space also clicked a focused button.
        _top.AddHandler(KeyDownEvent, HandleSpace, RoutingStrategies.Tunnel);
        _top.AddHandler(KeyUpEvent, HandleSpace, RoutingStrategies.Tunnel);
        _top.AddHandler(TopLevel.BackRequestedEvent, HandleBackRequest);
        if (Classes.Contains("windowed") || _top.InsetsManager is not { } insets)
            return;
        insets.DisplayEdgeToEdgePreference = true;
        insets.SafeAreaChanged += HandleSafeAreaChanged;
        Shell.SafeArea = insets.SafeAreaPadding;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_top is null)
            return;
        foreach (var binding in _keys)
            _top.KeyBindings.Remove(binding);
        _top.RemoveHandler(GotFocusEvent, HandleGotFocus);
        _top.RemoveHandler(LostFocusEvent, HandleLostFocus);
        _top.RemoveHandler(KeyDownEvent, HandleSpace);
        _top.RemoveHandler(KeyUpEvent, HandleSpace);
        _top.RemoveHandler(TopLevel.BackRequestedEvent, HandleBackRequest);
        if (_top.InsetsManager is { } insets)
            insets.SafeAreaChanged -= HandleSafeAreaChanged;
        _top = null;
    }

    private void HandleSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => Shell.SafeArea = e.SafeAreaPadding;

    // Not called once Dismissal's class handler has closed a sheet or modal; left unhandled on Focus, so the platform backgrounds the app.
    private void HandleBackRequest(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            e.Handled = viewModel.GoBack();
    }

    private void HandleGotFocus(object? sender, FocusChangedEventArgs e) => EnablePlainKeys(e.Source is not TextBox);

    private void HandleLostFocus(object? sender, FocusChangedEventArgs e) => EnablePlainKeys(true);

    // Typing keeps its spaces and a hold ring keeps its Space hold.
    private void HandleSpace(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.None || Content is not ShellView || e.Source is TextBox or ProgressRing { HoldDuration: > 0 })
            return;
        e.Handled = true;
        if (e.RoutedEvent == KeyDownEvent)
            (DataContext as MainViewModel)?.Focus.StartPauseCommand.Execute(null);
    }

    private void EnablePlainKeys(bool enabled)
    {
        if (_top is null)
            return;
        foreach (var binding in _plainKeys)
            _top.KeyBindings.Remove(binding);
        // The intro takes Space as a skip.
        if (enabled && Content is not IntroView)
            _top.KeyBindings.AddRange(_plainKeys);
    }
}
