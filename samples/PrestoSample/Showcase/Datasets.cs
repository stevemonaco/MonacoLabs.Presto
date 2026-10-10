using MonacoLabs.Presto;

namespace PrestoSample.Showcase;

/// <summary>
/// The sample records the showcase demos share, drawn once from a seeded generator so every run and every test sees the
/// same data (SAMPLES-PLAN §3).
/// </summary>
public static class Datasets
{
    // Rounds of dashboard data, cycled by the live feed so a long run repeats rather than drifting away.
    private const int Rounds = 8;

    private static readonly string[] Marks =
        ["Atlas", "Beacon", "Cascade", "Drift", "Ember", "Ferry", "Glimmer", "Harbor", "Ion", "Jetty", "Kelvin", "Lantern"];

    private static readonly string[] Kinds = ["Pass", "Field", "Loop", "Rig", "Stack", "Track"];

    private static readonly string[] Owners =
        ["A. Mori", "B. Okafor", "C. Lindqvist", "D. Navarro", "E. Sato", "F. Kaur"];

    private static readonly string[] Moods = ["Neon", "Quiet", "Paper", "Velvet", "Hollow", "Golden", "Static", "Salt", "Midnight", "Glass"];

    private static readonly string[] Nouns = ["Harbor", "Orchard", "Signal", "Coast", "Hours", "Rooms", "Weather", "Engines", "Gardens", "Tides"];

    private static readonly string[] Artists =
        ["Lumen Parade", "The Cartographers", "Ada Voss", "Northbound", "Mira Kell", "Low Tide Club",
         "Pale Arcade", "Juno Hart", "Sister Static", "The Long Weekend", "Oren Blake", "Fieldnotes"];

    private static readonly (string City, string Country)[] Cities =
    [
        ("Lisbon", "Portugal"), ("Kyoto", "Japan"), ("Reykjavík", "Iceland"), ("Oaxaca", "Mexico"),
        ("Marrakesh", "Morocco"), ("Tbilisi", "Georgia"), ("Hobart", "Australia"), ("Valparaíso", "Chile"),
        ("Ljubljana", "Slovenia"), ("Hoi An", "Vietnam"), ("Tromsø", "Norway"), ("Cartagena", "Colombia"),
        ("Zanzibar", "Tanzania"), ("Queenstown", "New Zealand"), ("Bruges", "Belgium"), ("Luang Prabang", "Laos"),
        ("Dubrovnik", "Croatia"), ("Cusco", "Peru"), ("Seville", "Spain"), ("Quebec City", "Canada"),
    ];

    private static readonly string[] Openers = ["Slow mornings,", "Come for", "Three days of", "Stay for", "Long evenings of"];

    private static readonly string[] Pleasures =
        ["harbor light", "night markets", "tiled stairways", "mountain air", "street food", "old bookshops",
         "hot springs", "cliff walks", "late dinners", "river ferries", "quiet gardens", "rooftop bars"];

    private static readonly string[] Channels = ["Search", "Social", "Email", "Direct", "Video", "Partner", "Events", "Print"];

    /// <summary>Twelve albums, each with its own hue and a track list of eight to ten songs.</summary>
    public static readonly IReadOnlyList<Album> Albums = BuildAlbums();

    /// <summary>Twenty destinations, each with its own hue and a one-line blurb.</summary>
    public static readonly IReadOnlyList<Place> Places = BuildPlaces();

    /// <summary>Rounds of the same eight chart points: the labels stay put from round to round and the numbers change.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<ChartPoint>> ChartPoints = BuildChartPoints();

    /// <summary>The categories every card belongs to, in the order the demos list them.</summary>
    public static readonly IReadOnlyList<string> Categories = ["Motion", "Layout", "Shapes", "Text"];

    /// <summary>Twenty-four cards, the same ones on every run.</summary>
    public static readonly IReadOnlyList<Card> Cards = Build();

    /// <summary>The lanes the board deals <see cref="Cards"/> into, in the order it shows them.</summary>
    public static readonly IReadOnlyList<string> Lanes = ["Backlog", "In flight", "Shipped"];

    /// <summary>The same cards dealt across <see cref="Lanes"/>, so every run opens on the same board.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<Card>> Board = Deal();

    /// <summary>The dashboard's KPI tiles, each with one reading per round the live feed cycles through.</summary>
    public static readonly IReadOnlyList<Metric> Metrics = BuildMetrics();

    /// <summary>Rounds of bar heights, as percentages of the chart.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<double>> Bars = BuildRounds(7734, 12, 8, 96);

    /// <summary>Rounds of donut weights, one weight per <see cref="Categories"/> entry.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<double>> Slices = BuildRounds(2281, Categories.Count, 12, 60);

    /// <summary>Rounds of sparkline readings, as percentages of the plot, walking rather than jumping so the stroke reads as a series.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<double>> Sparklines = BuildWalks(6042, 28, 22, 92);

    /// <summary>One record. <paramref name="Accent"/> indexes <see cref="Palette.AccentRamp"/>.</summary>
    public sealed record Card(int Id, string Title, string Category, int Value, int Accent, string Owner);

    /// <summary>One KPI tile: its label, the unit under the number, the decimals its counter shows and a reading per round.</summary>
    public sealed record Metric(string Label, string Unit, int Decimals, IReadOnlyList<double> Readings);

    /// <summary>One album. <paramref name="Hue"/> is in degrees, for the art tile's gradient and the detail view's tint.</summary>
    public sealed record Album(string Title, string Artist, int Year, double Hue, IReadOnlyList<Track> Tracks);

    public sealed record Track(string Title, TimeSpan Length);

    /// <summary>One destination. <paramref name="Hue"/> is in degrees, for the card's gradient art.</summary>
    public sealed record Place(string City, string Country, double Hue, string Blurb);

    /// <summary>One chart point: <paramref name="Value"/> in thousands, <paramref name="Secondary"/> a percentage and <paramref name="Weight"/> 1 to 5.</summary>
    public sealed record ChartPoint(string Label, double Value, double Secondary, double Weight);

    private static ChartPoint[][] BuildChartPoints()
    {
        var random = Utils.CreateSeededRandom(3141);
        var rounds = new ChartPoint[Rounds][];
        for (var i = 0; i < rounds.Length; i++)
        {
            rounds[i] = [.. Channels.Select(label => new ChartPoint(label, random(12, 140), random(4, 56), random(1, 5)))];
        }
        return rounds;
    }

    private static Place[] BuildPlaces()
    {
        var random = Utils.CreateSeededRandom(2718);
        var places = new Place[Cities.Length];
        for (var i = 0; i < places.Length; i++)
        {
            var first = (int)random(0, Pleasures.Length - 1);
            var second = (first + (int)random(1, Pleasures.Length - 1)) % Pleasures.Length;
            places[i] = new Place(
                Cities[i].City,
                Cities[i].Country,
                ((i * 137) + random(0, 20)) % 360,
                $"{Openers[(int)random(0, Openers.Length - 1)]} {Pleasures[first]} and {Pleasures[second]}.");
        }
        return places;
    }

    private static Album[] BuildAlbums()
    {
        var random = Utils.CreateSeededRandom(5150);
        var titles = new HashSet<string>();
        var albums = new Album[Artists.Length];
        for (var i = 0; i < albums.Length; i++)
        {
            string title;
            do
            {
                title = $"{Moods[(int)random(0, Moods.Length - 1)]} {Nouns[(int)random(0, Nouns.Length - 1)]}";
            }
            while (!titles.Add(title));

            var tracks = new Track[(int)random(8, 10)];
            for (var j = 0; j < tracks.Length; j++)
            {
                tracks[j] = new Track(
                    $"{Nouns[(int)random(0, Nouns.Length - 1)]} of {Moods[(int)random(0, Moods.Length - 1)]}",
                    TimeSpan.FromSeconds(random(130, 400)));
            }

            albums[i] = new Album(title, Artists[i], (int)random(1971, 2024), (i * 30) + random(0, 20), tracks);
        }
        return albums;
    }

    private static Card[] Build()
    {
        var random = Utils.CreateSeededRandom(4407);
        var titles = new HashSet<string>();
        var cards = new Card[24];
        for (var i = 0; i < cards.Length; i++)
        {
            string title;
            do
            {
                title = $"{Marks[(int)random(0, Marks.Length - 1)]} {Kinds[(int)random(0, Kinds.Length - 1)]}";
            }
            while (!titles.Add(title));

            cards[i] = new Card(
                i + 1,
                title,
                Categories[(int)random(0, Categories.Count - 1)],
                (int)random(40, 980),
                (int)random(1, Palette.AccentRamp.Count - 1),
                Owners[(int)random(0, Owners.Length - 1)]);
        }
        return cards;
    }

    private static Metric[] BuildMetrics()
    {
        var random = Utils.CreateSeededRandom(8821);
        (string Label, string Unit, int Decimals, double Min, double Max)[] shapes =
        [
            ("Sessions", "live now", 0, 2400, 9600),
            ("Throughput", "requests / s", 0, 240, 1450),
            ("Latency", "ms, 95th", 1, 18, 140),
            ("Uptime", "% of the window", 2, 99, 100),
        ];
        return
        [
            .. shapes.Select(shape => new Metric(
                shape.Label,
                shape.Unit,
                shape.Decimals,
                [.. Enumerable.Range(0, Rounds).Select(_ => random(shape.Min, shape.Max, shape.Decimals))])),
        ];
    }

    private static double[][] BuildRounds(int seed, int length, double min, double max)
    {
        var random = Utils.CreateSeededRandom(seed);
        var rounds = new double[Rounds][];
        for (var i = 0; i < rounds.Length; i++)
        {
            rounds[i] = new double[length];
            for (var j = 0; j < length; j++) rounds[i][j] = random(min, max);
        }
        return rounds;
    }

    private static double[][] BuildWalks(int seed, int length, double min, double max)
    {
        var random = Utils.CreateSeededRandom(seed);
        var rounds = new double[Rounds][];
        for (var i = 0; i < rounds.Length; i++)
        {
            rounds[i] = new double[length];
            var value = random(min, max);
            for (var j = 0; j < length; j++)
            {
                value = Math.Clamp(value + random(-11, 11), min, max);
                rounds[i][j] = value;
            }
        }
        return rounds;
    }

    private static Card[][] Deal()
    {
        var random = Utils.CreateSeededRandom(1189);
        var lanes = new List<Card>[Lanes.Count];
        for (var i = 0; i < lanes.Length; i++) lanes[i] = [];
        foreach (var card in Cards) lanes[(int)random(0, lanes.Length - 1)].Add(card);
        return [.. lanes.Select(lane => lane.ToArray())];
    }
}
