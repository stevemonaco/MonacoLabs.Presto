using Avalonia.Media;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>One sample notification; <see cref="Id"/> keeps two with the same text distinct.</summary>
public sealed record Notice(int Id, string Title, string Message, IBrush Accent)
{
    private static readonly (string Title, string Message, IBrush Accent)[] Samples =
    [
        ("Saved", "Your changes to Roadmap Q3 were saved.", Palette.Aqua),
        ("New comment", "Ada replied to your review on #482.", Palette.Accent),
        ("Storage almost full", "You have used 92% of your quota.", Palette.Amber),
        ("Upload failed", "photo-0193.jpg could not be sent.", Palette.Coral),
        ("Invite accepted", "Grace joined the Design team.", Palette.Violet),
    ];

    /// <summary>The sample numbered <paramref name="id"/>, cycling through five.</summary>
    public static Notice Sample(int id)
    {
        var (title, message, accent) = Samples[id % Samples.Length];
        return new Notice(id, title, message, accent);
    }
}
