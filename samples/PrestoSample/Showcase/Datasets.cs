using Monaco.Presto;

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
