using Avalonia.Controls;
using Avalonia.Layout;

namespace PrestoSample.Showcase;

/// <summary>
/// The card the gallery and the board both file: a title, an owner and a value over an accented border, sized by
/// whichever page's styles it is shown in.
/// </summary>
public static class CardView
{
    /// <summary>Builds the card for <paramref name="record"/>, with its category above the title when <paramref name="eyebrow"/> is set.</summary>
    public static Border Build(Datasets.Card record, bool eyebrow)
    {
        var accent = Palette.AccentBrushRamp[record.Accent];
        var stack = new StackPanel
        {
            Children =
            {
                new TextBlock { Classes = { "title" }, Text = record.Title },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Classes = { "caption" }, Text = $"#{record.Id:00} · {record.Owner}" },
                        new TextBlock { Classes = { "mono" }, Text = $"{record.Value}", Foreground = accent },
                    },
                },
            },
        };

        if (eyebrow)
        {
            stack.Children.Insert(0, new TextBlock { Classes = { "eyebrow", "showcase" }, Text = record.Category.ToUpperInvariant() });
        }

        return new Border
        {
            Classes = { "card", "showcase" },
            BorderBrush = accent,
            DataContext = record,
            Child = stack,
        };
    }

    /// <summary>The record <see cref="Build"/> put behind <paramref name="card"/>.</summary>
    public static Datasets.Card Record(Control card) => (Datasets.Card)card.DataContext!;
}
