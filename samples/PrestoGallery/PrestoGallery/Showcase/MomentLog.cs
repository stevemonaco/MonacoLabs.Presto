using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace PrestoGallery.Showcase;

/// <summary>A newest-first log of tinted moments, such as the ones a page's builders record from <c>OnBegin</c>.</summary>
public sealed class MomentLog : ItemsControl
{
    private const int MaxMoments = 12;

    private readonly ObservableCollection<(string Moment, IBrush Tint, string Detail)> _moments = [];

    public MomentLog()
    {
        ItemsSource = _moments;
        ItemTemplate = new FuncDataTemplate<(string Moment, IBrush Tint, string Detail)>((entry, _) => Line(entry));
    }

    protected override Type StyleKeyOverride => typeof(ItemsControl);

    public void Record(string moment, IBrush tint, string detail)
    {
        _moments.Insert(0, (moment, tint, detail));
        while (_moments.Count > MaxMoments) _moments.RemoveAt(_moments.Count - 1);
    }

    private static Control Line((string Moment, IBrush Tint, string Detail) entry) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 10,
        Margin = new Thickness(0, 0, 0, 4),
        Children =
        {
            new Border { Classes = { "moment" }, Background = entry.Tint, Child = new TextBlock { Text = entry.Moment } },
            new TextBlock { Text = entry.Detail, VerticalAlignment = VerticalAlignment.Center, Opacity = .8 },
        },
    };
}
