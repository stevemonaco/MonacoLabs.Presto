using System.Text.Json;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Generated parity against anime.js 4.5.0: every ease and spring is checked against 101 samples dumped from upstream by
/// tools/anime-fixtures (see docs/ARCHITECTURE.md §3).
/// </summary>
public sealed class EaseFixtureTests : EngineTestBase
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, EaseFixture> _eases = LoadFixtures<EaseFixture>("eases.json").ToDictionary(f => f.Name);
    private static readonly Dictionary<string, SpringFixture> _springs = LoadFixtures<SpringFixture>("springs.json").ToDictionary(f => f.Name);

    public static TheoryData<string> EaseNames => new(_eases.Keys);
    public static TheoryData<string> SpringNames => new(_springs.Keys);

    private static T[] LoadFixtures<T>(string file) =>
        JsonSerializer.Deserialize<T[]>(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", file)), _json)!;

    [Theory]
    [MemberData(nameof(EaseNames))]
    public void EaseMatchesUpstream(string name)
    {
        var fixture = _eases[name];
        var args = fixture.Args ?? [];
        Ease ease = fixture.Kind switch
        {
            "string" => EaseParser.Parse(name),
            "cubicBezier" => Eases.CubicBezier(args[0].GetDouble(), args[1].GetDouble(), args[2].GetDouble(), args[3].GetDouble()),
            "steps" => Eases.Steps(args[0].GetInt32(), args.Length > 1 && args[1].ValueKind == JsonValueKind.True),
            "linear" => Eases.Linear(Array.ConvertAll(args, a => a.ValueKind == JsonValueKind.String ? (LinearStop)a.GetString()! : a.GetDouble())),
            _ => throw new InvalidOperationException($"Unknown fixture kind \"{fixture.Kind}\"."),
        };
        AssertSamples(fixture.Samples, ease);
    }

    [Theory]
    [MemberData(nameof(SpringNames))]
    public void SpringMatchesUpstream(string name)
    {
        var fixture = _springs[name];
        var p = fixture.Params;
        var spring = Eases.Spring(mass: p.Mass, stiffness: p.Stiffness, damping: p.Damping, velocity: p.Velocity, bounce: p.Bounce, duration: p.Duration);
        Assert.Equal(fixture.Mass, spring.Mass);
        Assert.Equal(fixture.Stiffness, spring.Stiffness);
        Assert.Equal(fixture.Damping, spring.Damping);
        Assert.Equal(fixture.Velocity, spring.Velocity);
        Assert.Equal(fixture.Bounce, spring.Bounce);
        Assert.Equal(fixture.Duration, spring.Duration);
        Assert.Equal(fixture.SettlingDuration, spring.SettlingDuration);
        Assert.Equal(fixture.SolverDuration, spring.SolverDuration, 1e-9);
        AssertSamples(fixture.Samples, spring.Ease);
    }

    private static void AssertSamples(double[] expected, Ease ease)
    {
        Assert.Equal(101, expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], ease(i / 100.0), 1e-6);
        }
    }

    private sealed record EaseFixture(string Kind, string Name, JsonElement[]? Args, double[] Samples);

    private sealed record SpringFixture(
        string Name,
        SpringFixtureParams Params,
        double Mass,
        double Stiffness,
        double Damping,
        double Velocity,
        double Bounce,
        double Duration,
        double SettlingDuration,
        double SolverDuration,
        double[] Samples);

    private sealed record SpringFixtureParams(double? Mass, double? Stiffness, double? Damping, double? Velocity, double? Bounce, double? Duration);
}
