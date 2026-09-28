using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>The toast stack: <see cref="NotifyCommand"/> appends the next sample toast, and the view removes one once it has animated it away.</summary>
public sealed partial class ToastsViewModel : ObservableObject
{
    private static readonly (string Title, string Message, IBrush Accent)[] Samples =
    [
        ("Saved", "Your changes to Roadmap Q3 were saved.", Palette.Aqua),
        ("New comment", "Ada replied to your review on #482.", Palette.Accent),
        ("Storage almost full", "You have used 92% of your quota.", Palette.Amber),
        ("Upload failed", "photo-0193.jpg could not be sent.", Palette.Coral),
        ("Invite accepted", "Grace joined the Design team.", Palette.Violet),
    ];

    private int _next;

    public ObservableCollection<Toast> Toasts { get; } = [];

    [RelayCommand]
    private void Notify()
    {
        var (title, message, accent) = Samples[_next % Samples.Length];
        Toasts.Add(new Toast(_next++, title, message, accent));
    }
}

/// <summary>One notification; <see cref="Id"/> keeps two toasts with the same text distinct.</summary>
public sealed record Toast(int Id, string Title, string Message, IBrush Accent);
