using Avalonia.Media;

namespace PrestoSample.Showcase.Pages;

/// <summary>One event in the activity feed.</summary>
public sealed record ActivityEvent(ActivityKind Kind, string Title, string Actor, DateTime Time, bool Unread)
{
    public IBrush Tint => Kind switch
    {
        ActivityKind.Comment => Palette.Accent,
        ActivityKind.Order => Palette.Amber,
        ActivityKind.Deploy => Palette.Aqua,
        _ => Palette.Violet,
    };

    public string Byline => $"{Actor} · {Kind.ToString().ToLowerInvariant()}";

    public string Clock => Time.ToString("HH:mm");
}
