using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Builder;

/// <summary>
/// A toast stack whose item template attaches its entrance and countdown from XAML; the page only plays the exit, then
/// removes the item. The template snippet is a constant pinned to the markup by a test.
/// </summary>
public partial class ToastsPage : ShowcasePage
{
    private const int MaxToasts = 4;

    public const string TemplateXaml = """
        <DataTemplate x:DataType="local:Toast">
            <Border Classes="toast" presto:Animate.OnAttached="{x:Static local:ToastsPage.SlideIn}">
                <Grid ColumnDefinitions="16,Auto,*,Auto" RowDefinitions="Auto,Auto,Auto">
                    <Border Grid.Column="1" Grid.RowSpan="2" Classes="accent" Background="{Binding Accent}" />
                    <TextBlock Grid.Column="2" Classes="title" Text="{Binding Title}" />
                    <TextBlock Grid.Column="2" Grid.Row="1" Classes="message" Text="{Binding Message}" />
                    <Button Grid.Column="3" Classes="close" Content="✕" Click="Close" />
                    <Border Grid.Row="2" Grid.ColumnSpan="4" Classes="countdown" Background="{Binding Accent}"
                            presto:Animate.OnAttached="{x:Static local:ToastsPage.Countdown}" />
                </Grid>
            </Border>
        </DataTemplate>
        """;

    public static readonly AnimationBuilder SlideIn;
    public static readonly AnimationBuilder Countdown;
    private static readonly Func<Control, AnimationBuilder> Leave;
    private static readonly string Recipes;

    private readonly HashSet<Toast> _leaving = [];

    static ToastsPage()
    {
        (SlideIn, var slideIn) = Snippet.Of(Presto.Build()
            .X(from: 120, to: 0)
            .Opacity(from: 0, to: 1)
            .Duration(650)
            .Ease(Eases.OutBack(1.2)));
        (Countdown, var countdown) = Snippet.Of(Presto.Build()
            .ScaleX(from: 1, to: 0)
            .Duration(5000)
            .Ease(Eases.None)
            .OnComplete(Expire));
        (Leave, var leave) = Snippet.Of<Func<Control, AnimationBuilder>>(container => Presto.Build()
            .Height(from: container.Bounds.Height, to: 0)
            .Opacity(0)
            .X(80)
            .Duration(360)
            .Ease(Eases.InOutQuad));
        const string field = "public static readonly AnimationBuilder";
        Recipes = string.Join("\n\n",
            Snippet.Declare($"{field} SlideIn", slideIn),
            Snippet.Declare($"{field} Countdown", countdown),
            Snippet.Declare("static readonly Func<Control, AnimationBuilder> Leave", leave));
    }

    public ToastsPage()
    {
        InitializeComponent();
        RecipesCode.Text = Recipes;
        ViewModel.Toasts.CollectionChanged += TrimStack;
    }

    public ToastsViewModel ViewModel => (ToastsViewModel)DataContext!;

    protected override void OnAttached()
    {
        if (ViewModel.Toasts.Count == 0) ViewModel.NotifyCommand.Execute(null);
    }

    /// <summary>Plays <see cref="Leave"/> on the toast's container and removes the toast once it has collapsed.</summary>
    public Task Dismiss(Toast toast) => _leaving.Add(toast) ? CollapseAndRemove(toast) : Task.CompletedTask;

    private async Task CollapseAndRemove(Toast toast)
    {
        // A toast added and trimmed before any layout pass has no container yet, so it simply goes.
        if (ToastList.ContainerFromItem(toast) is Control container)
        {
            container.ClipToBounds = true;
            await container.Animate(Leave(container));
        }
        _leaving.Remove(toast);
        ViewModel.Toasts.Remove(toast);
    }

    private static void Expire(Timer timer)
    {
        if (((Animation)timer).Targets[0] is Control { DataContext: Toast toast } bar && bar.FindAncestorOfType<ToastsPage>() is { } page)
            _ = page.Dismiss(toast);
    }

    private void TrimStack(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        var staying = ViewModel.Toasts.Where(toast => !_leaving.Contains(toast)).ToList();
        if (staying.Count <= MaxToasts || !_leaving.Add(staying[0])) return;
        // Posted because the collection may not change inside its own CollectionChanged.
        Dispatcher.UIThread.Post(() => _ = CollapseAndRemove(staying[0]));
    }

    private void Close(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Toast toast }) _ = Dismiss(toast);
    }
}
