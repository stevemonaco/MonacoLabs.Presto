using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Monaco.Presto;

namespace PrestoSample.Showcase.Pages;

/// <summary>The chat log: seeded history, messages received on a timer and messages sent from the compose box.</summary>
public sealed partial class ChatLogViewModel : ObservableObject
{
    private const int SeedSize = 60;

    private static readonly string[] Authors = ["Mori", "Okafor", "Lindqvist", "Navarro", "Sato"];

    private static readonly string[] Lines =
    [
        "Morning! Is the build green?",
        "Deploying web to staging now.",
        "Looks good on my side.\nThe pricing page still flickers on resize, though.",
        "Can someone review the onboarding flow?",
        "Done. Two small notes:\n- the skip button is hard to find\n- step three repeats step one",
        "Lunch?",
        "Release notes are in the doc.\nPlease add anything I missed before three.\nThanks!",
        "On it.",
    ];

    private readonly RandomGenerator _random = Utils.CreateSeededRandom(2028);

    public ChatLogViewModel()
    {
        for (var i = 0; i < SeedSize; i++) Messages.Add(Next(mine: i % 3 == 2));
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    /// <summary>Whether the reader has scrolled up from the newest message, which shows the Jump to newest pill.</summary>
    [ObservableProperty]
    public partial bool Behind { get; set; }

    public void Receive() => Messages.Add(Next(mine: false));

    public void Send(string text) => Messages.Add(new ChatMessage("You", text, Mine: true));

    private ChatMessage Next(bool mine) => new(mine ? "You" : Pick(Authors), Pick(Lines), mine);

    private string Pick(string[] from) => from[(int)_random(0, from.Length - 1)];
}
