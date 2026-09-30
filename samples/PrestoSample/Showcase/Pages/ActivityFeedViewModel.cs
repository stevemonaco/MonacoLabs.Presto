using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Monaco.Presto;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// The activity feed: seeded events under day headers, newest first, with live events arriving under today's header, or
/// held back while the feed is scrolled down, and older pages appended behind skeleton rows.
/// </summary>
public sealed partial class ActivityFeedViewModel : ObservableObject
{
    public const int SeedSize = 2000;
    public const int PageSize = 50;
    private const int SkeletonRows = 3;
    private const int UnreadWindow = 30;

    private static readonly string[] Actors =
        ["A. Mori", "B. Okafor", "C. Lindqvist", "D. Navarro", "E. Sato", "F. Kaur", "G. Haddad", "H. Brennan"];

    private static readonly string[] Documents =
        ["the Q3 roadmap", "Atlas Pass", "the launch checklist", "Beacon Field", "the pricing page", "Cascade Loop", "the onboarding flow"];

    private static readonly string[] Services = ["api", "web", "billing", "search", "notifications"];

    private readonly RandomGenerator _random = Utils.CreateSeededRandom(2027);
    private readonly List<ActivityEvent> _held = [];
    private DateTime _oldest = DateTime.Now;

    public ActivityFeedViewModel()
    {
        Append(SeedSize);
        UnreadCount = Items.OfType<ActivityEvent>().Count(item => item.Unread);
    }

    public ObservableCollection<object> Items { get; } = [];

    [ObservableProperty]
    public partial int UnreadCount { get; set; }

    /// <summary>How many live events are held back while the feed is scrolled down, or null when there are none.</summary>
    [ObservableProperty]
    public partial int? Unseen { get; set; }

    [ObservableProperty]
    public partial int InitialPlayed { get; set; }

    [ObservableProperty]
    public partial int RevealPlayed { get; set; }

    [ObservableProperty]
    public partial int EnterPlayed { get; set; }

    [ObservableProperty]
    public partial int ExitPlayed { get; set; }

    [ObservableProperty]
    public partial int AttachedPlayed { get; set; }

    [ObservableProperty]
    public partial int ChangedPlayed { get; set; }

    public bool Loading => Items[^1] is SkeletonRow;

    public void BeginLoad()
    {
        for (var i = 0; i < SkeletonRows; i++) Items.Add(new SkeletonRow());
    }

    public void EndLoad()
    {
        while (Loading) Items.RemoveAt(Items.Count - 1);
        Append(PageSize);
    }

    /// <summary>
    /// Adds a live event under today's header, or with <paramref name="hold"/> keeps it back and counts it in
    /// <see cref="Unseen"/> until <see cref="Release"/>: an insert above a scrolled-down list moves what it shows.
    /// </summary>
    public void AddLive(bool hold)
    {
        var item = Next(DateTime.Now, unread: true);
        UnreadCount++;
        if (hold)
        {
            _held.Add(item);
            Unseen = _held.Count;
        }
        else
        {
            Insert(item);
        }
    }

    /// <summary>Inserts the events <see cref="AddLive"/> held back, newest on top.</summary>
    public void Release()
    {
        foreach (var item in _held) Insert(item);
        _held.Clear();
        Unseen = null;
    }

    public void Dismiss(ActivityEvent item)
    {
        Items.Remove(item);
        if (item.Unread) UnreadCount--;
    }

    private void Insert(ActivityEvent item)
    {
        if (Items[0] is not DayHeader { Day: var day } || day != item.Time.Date) Items.Insert(0, new DayHeader(item.Time.Date));
        Items.Insert(1, item);
    }

    private void Append(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var day = _oldest.Date;
            _oldest -= TimeSpan.FromMinutes(_random(5, 90));
            if (Items.Count == 0 || _oldest.Date != day) Items.Add(new DayHeader(_oldest.Date));
            Items.Add(Next(_oldest, unread: Items.Count < UnreadWindow && _random(0, 2) == 0));
        }
    }

    private ActivityEvent Next(DateTime time, bool unread)
    {
        var kind = (ActivityKind)(int)_random(0, 3);
        var title = kind switch
        {
            ActivityKind.Comment => $"Commented on {Pick(Documents)}",
            ActivityKind.Order => $"Placed order #{(int)_random(10000, 99999)}",
            ActivityKind.Deploy => $"Deployed {Pick(Services)} to production",
            _ => $"Mentioned you in {Pick(Documents)}",
        };
        return new ActivityEvent(kind, title, Pick(Actors), time, unread);
    }

    private string Pick(string[] from) => from[(int)_random(0, from.Length - 1)];
}
