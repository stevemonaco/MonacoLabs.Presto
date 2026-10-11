using System.Reflection;
using System.Text.RegularExpressions;

namespace MonacoLabs.Presto.Tests;

/// <summary>Parses the feature specs in <c>docs/specs</c> (format in <c>docs/specs/README.md</c>).</summary>
internal static partial class SpecCatalog
{
    public static readonly string[] Sections =
    [
        "Purpose", "Requirements", "Invariants", "Edge cases", "Threading and lifetime",
        "Reduced motion", "Allocation", "Provenance", "Decisions", "Non-goals", "Open items",
    ];

    public static readonly string[] ListKeys = ["namespaces", "sources", "types", "tests", "upstream", "depends"];
    public static readonly string[] ScalarKeys = ["id", "title", "package"];

    public static readonly (string Folder, string Prefix, string Package)[] Folders =
    [
        ("core", "CORE", "MonacoLabs.Presto"),
        ("avalonia-bridge", "BRIDGE", "MonacoLabs.Presto.Avalonia"),
        ("lottie", "LOTTIE", "MonacoLabs.Presto.Avalonia.Lottie"),
        ("devtools", "DEVTOOLS", "MonacoLabs.Presto.Avalonia.DevTools"),
    ];

    private static readonly Lazy<string> LazyRoot = new(FindRoot);
    private static readonly Lazy<IReadOnlyList<SpecDocument>> LazySpecs = new(LoadAll);

    public static string Root => LazyRoot.Value;
    public static IReadOnlyList<SpecDocument> All => LazySpecs.Value;

    public static IEnumerable<string> OwnershipErrors(Assembly assembly, string folder)
    {
        var exported = assembly.GetExportedTypes()
            .Where(t => !t.IsNested && t.Namespace?.StartsWith("MonacoLabs.Presto", StringComparison.Ordinal) == true)
            .Select(t => TypeName(t.Name))
            .ToHashSet(StringComparer.Ordinal);
        var owners = All.Where(s => s.Folder == folder)
            .SelectMany(s => s.List("types").Select(type => (Type: TypeName(type), s.RelativePath)))
            .GroupBy(o => o.Type, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(o => o.RelativePath).ToList(), StringComparer.Ordinal);

        foreach (var type in exported.Order(StringComparer.Ordinal))
        {
            if (!owners.TryGetValue(type, out var specs))
                yield return $"{assembly.GetName().Name}: public type {type} is owned by no spec in docs/specs/{folder}/";
            else if (specs.Count > 1)
                yield return $"{assembly.GetName().Name}: public type {type} is owned by {string.Join(", ", specs)}";
        }
        foreach (var (type, specs) in owners)
        {
            if (!exported.Contains(type))
                yield return $"{string.Join(", ", specs)}: owns {type}, which is not a public type of {assembly.GetName().Name}";
        }
    }

    public static void AssertNone(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        if (list.Count > 0)
            Assert.Fail($"{list.Count} spec problem(s):\n" + string.Join("\n", list));
    }

    private static string TypeName(string name)
    {
        var cut = name.IndexOfAny(['`', '<']);
        return (cut < 0 ? name : name[..cut]).Trim();
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MonacoLabs.Presto.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root (MonacoLabs.Presto.slnx) not found above " + AppContext.BaseDirectory);
    }

    private static IReadOnlyList<SpecDocument> LoadAll()
    {
        var specsRoot = Path.Combine(Root, "docs", "specs");
        return Directory.EnumerateFiles(specsRoot, "*.md", SearchOption.AllDirectories)
            .Where(path => Path.GetDirectoryName(path) != specsRoot)
            .Where(path => !Path.GetFileName(path).StartsWith('_') && Path.GetFileName(path) != "index.md")
            .Order(StringComparer.Ordinal)
            .Select(Parse)
            .ToList();
    }

    private static SpecDocument Parse(string path)
    {
        var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
        var folder = relative.Split('/')[2];
        var lines = File.ReadAllLines(path);
        var errors = new List<string>();
        var scalars = new Dictionary<string, string>(StringComparer.Ordinal);
        var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var end = lines.Length > 0 && lines[0] == "---" ? Array.IndexOf(lines, "---", 1) : -1;
        if (end < 0)
            errors.Add("front matter must open and close with a '---' line");

        List<string>? currentList = null;
        for (var i = 1; i < end; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (ListItem().Match(line) is { Success: true } item && currentList != null)
            {
                currentList.Add(item.Groups[1].Value.Trim());
                continue;
            }
            var pair = KeyValue().Match(line);
            if (!pair.Success)
            {
                errors.Add($"line {i + 1}: front matter line is neither 'key: value' nor a '  - item' under a list key");
                continue;
            }
            var (key, value) = (pair.Groups[1].Value, pair.Groups[2].Value.Trim());
            if (scalars.ContainsKey(key) || lists.ContainsKey(key))
                errors.Add($"line {i + 1}: duplicate key '{key}'");
            if (value.Length == 0 || value == "[]")
            {
                currentList = [];
                lists[key] = currentList;
            }
            else
            {
                currentList = null;
                scalars[key] = value;
            }
        }

        var sections = new List<(string Title, int Line)>();
        var requirements = new List<SpecRequirement>();
        var decisions = new List<SpecDecision>();
        var body = new List<string>();
        var inFence = false;
        var inRequirements = false;
        var inDecisions = false;
        for (var i = Math.Max(end + 1, 0); i < lines.Length; i++)
        {
            var line = lines[i];
            body.Add(line);
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence) continue;
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var title = line[3..].Trim();
                sections.Add((title, i + 1));
                inRequirements = title == "Requirements";
                inDecisions = title == "Decisions";
                continue;
            }
            if (inDecisions && DecisionLine().Match(line) is { Success: true } decision)
            {
                var pointer = PointerText().Match(decision.Groups[2].Value);
                decisions.Add(new SpecDecision(decision.Groups[1].Value.TrimEnd('.'), pointer.Success ? pointer.Groups[1].Value : null, i + 1));
                continue;
            }
            if (!inRequirements) continue;
            var requirement = RequirementLine().Match(line);
            if (!requirement.Success)
            {
                if (line.StartsWith("- ", StringComparison.Ordinal))
                    errors.Add($"line {i + 1}: a requirement bullet must read '- **ID-NNN** — text' or '- **ID-NNN** (inherited) — text'");
                continue;
            }
            var removed = requirement.Groups[1].Success;
            string? tests = null;
            if (i + 1 < lines.Length && TestsLine().Match(lines[i + 1]) is { Success: true } testsLine)
                tests = testsLine.Groups[1].Value.Trim();
            requirements.Add(new SpecRequirement(requirement.Groups[2].Value, removed, tests, i + 1));
        }

        return new SpecDocument
        {
            Path = path,
            RelativePath = relative,
            Folder = folder,
            Scalars = scalars,
            Lists = lists.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.Ordinal),
            Sections = sections,
            Requirements = requirements,
            Decisions = decisions,
            Body = string.Join('\n', body),
            ParseErrors = errors,
        };
    }

    [GeneratedRegex(@"^\s+-\s+(.+)$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^([a-z]+):(.*)$")]
    private static partial Regex KeyValue();

    [GeneratedRegex(@"^- (~~)?\*\*([A-Z0-9]+(?:-[A-Z0-9]+)*)\*\*(?:~~)?(?: \(inherited\))? — \S")]
    private static partial Regex RequirementLine();

    [GeneratedRegex(@"^\s+- Tests: (.+)$")]
    private static partial Regex TestsLine();

    [GeneratedRegex(@"^- \*\*(.+?)\*\*(.*)$")]
    private static partial Regex DecisionLine();

    [GeneratedRegex(@"^\s*See ([A-Z]+(?:-[A-Z0-9]+)+)\.?\s*$")]
    private static partial Regex PointerText();
}

internal sealed record SpecRequirement(string Id, bool Removed, string? Tests, int Line);

internal sealed record SpecDecision(string Title, string? PointsTo, int Line);

internal sealed class SpecDocument
{
    public required string Path { get; init; }
    public required string RelativePath { get; init; }
    public required string Folder { get; init; }
    public required IReadOnlyDictionary<string, string> Scalars { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Lists { get; init; }
    public required IReadOnlyList<(string Title, int Line)> Sections { get; init; }
    public required IReadOnlyList<SpecRequirement> Requirements { get; init; }
    public required IReadOnlyList<SpecDecision> Decisions { get; init; }
    public required string Body { get; init; }
    public required IReadOnlyList<string> ParseErrors { get; init; }

    public string Id => Scalars.GetValueOrDefault("id") ?? "";

    public IReadOnlyList<string> List(string key) => Lists.GetValueOrDefault(key) ?? [];

    public override string ToString() => RelativePath;
}
