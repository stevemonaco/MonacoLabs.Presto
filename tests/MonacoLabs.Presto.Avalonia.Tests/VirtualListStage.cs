using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// A shown 200 by 200 <see cref="ListBox"/> on a <see cref="VirtualizingStackPanel"/> over <see cref="Count"/> observable items, each item its
/// own index, whose containers are exactly as high as <c>height(index)</c>.
/// </summary>
internal sealed record VirtualListStage(Window Window, ListBox List)
{
    public const double ViewportSize = 200;
    public const int Count = 5000;

    public ScrollViewer Scroller => (ScrollViewer)List.Scroll!;

    public ObservableCollection<int> Items => (ObservableCollection<int>)List.ItemsSource!;

    public static VirtualListStage Show(Func<int, double> height)
    {
        var list = new ListBox
        {
            Width = ViewportSize,
            Height = ViewportSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ItemsSource = new ObservableCollection<int>(Enumerable.Range(0, Count)),
            ItemTemplate = new FuncDataTemplate<int>((index, _) => new Border { Height = height(index), Background = Brushes.Blue }),
            Styles =
            {
                new Style(x => x.OfType<ListBoxItem>())
                {
                    Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(0)), new Setter(Layoutable.MinHeightProperty, 0d) },
                },
            },
        };
        var window = new Window { Content = list, Width = 300, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new VirtualListStage(window, list);
    }

    /// <summary>The top of the container for <paramref name="index"/> in the viewport, or null while it is not realized.</summary>
    public double? Top(int index) =>
        List.ContainerFromIndex(index) is { } container ? container.TranslatePoint(default, Scroller)!.Value.Y : null;
}
