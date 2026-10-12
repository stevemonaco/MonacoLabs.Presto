namespace PrestoGallery.Showcase.Pages;

/// <summary>One message in the chat log; <paramref name="Mine"/> puts it on the right.</summary>
public sealed record ChatMessage(string Author, string Text, bool Mine);
