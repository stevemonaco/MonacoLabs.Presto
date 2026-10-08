using System.Text.RegularExpressions;

namespace Monaco.Presto.Tests;

public sealed partial class SpecTests
{
    private static readonly Lazy<Dictionary<string, List<string>>> TestSourcesByClass = new(IndexTestSources);

    [Fact]
    public void SpecsExistForEveryPackageFolder()
    {
        SpecCatalog.AssertNone(SpecCatalog.Folders
            .Where(f => SpecCatalog.All.All(s => s.Folder != f.Folder))
            .Select(f => $"docs/specs/{f.Folder}/ has no specs"));
    }

    [Fact]
    public void EverySpecHasTheTemplateFrontMatter()
    {
        SpecCatalog.AssertNone(SpecCatalog.All.SelectMany(FrontMatterErrors));
    }

    [Fact]
    public void EverySpecHasTheTemplateSectionsInOrder()
    {
        SpecCatalog.AssertNone(SpecCatalog.All
            .Where(s => !s.Sections.Select(x => x.Title).SequenceEqual(SpecCatalog.Sections))
            .Select(s => $"{s}: sections are [{string.Join(", ", s.Sections.Select(x => x.Title))}], expected [{string.Join(", ", SpecCatalog.Sections)}]"));
    }

    [Fact]
    public void SpecAndRequirementIdsAreUnique()
    {
        var errors = SpecCatalog.All.GroupBy(s => s.Id).Where(g => g.Count() > 1)
            .Select(g => $"spec id {g.Key} is used by {string.Join(", ", g)}")
            .Concat(SpecCatalog.All.SelectMany(s => s.Requirements.Select(r => (Spec: s, r.Id)))
                .GroupBy(x => x.Id).Where(g => g.Count() > 1)
                .Select(g => $"requirement id {g.Key} appears {g.Count()} times ({string.Join(", ", g.Select(x => x.Spec).Distinct())})"))
            .Concat(SpecCatalog.All.SelectMany(s => s.Requirements
                .Where(r => !Regex.IsMatch(r.Id, $"^{Regex.Escape(s.Id)}-\\d{{3}}$"))
                .Select(r => $"{s}:{r.Line}: requirement id {r.Id} is not {s.Id}-NNN")));
        SpecCatalog.AssertNone(errors);
    }

    [Fact]
    public void EveryRequirementCitesTestsOrSaysWhyNot()
    {
        SpecCatalog.AssertNone(SpecCatalog.All.SelectMany(s =>
        {
            var errors = s.Requirements.Where(r => !r.Removed && !IsValidTestsLine(r.Tests))
                .Select(r => $"{s}:{r.Line}: {r.Id} needs a '  - Tests:' line of `Class.Method` citations, 'untested' or 'manual — why'");
            return s.Requirements.Count == 0 ? errors.Append($"{s}: has no requirements") : errors;
        }));
    }

    [Fact]
    public void EveryCitedTestExists()
    {
        SpecCatalog.AssertNone(SpecCatalog.All.SelectMany(s => s.Requirements
            .SelectMany(r => Citations(r.Tests).Where(c => !TestExists(c)).Select(c => $"{s}:{r.Line}: {r.Id} cites {c}, which is not a test method"))));
    }

    [Fact]
    public void EveryCitedTestClassExists()
    {
        SpecCatalog.AssertNone(SpecCatalog.All.SelectMany(s => s.List("tests")
            .Where(c => !TestSourcesByClass.Value.ContainsKey(c.Split('.')[^1]))
            .Select(c => $"{s}: lists test class {c}, which does not exist under tests/")));
    }

    [Fact]
    public void EverySourcePathExists()
    {
        SpecCatalog.AssertNone(SpecCatalog.All.SelectMany(s =>
            s.List("sources").Where(p => !PathExists(p)).Select(p => $"{s}: source {p} does not exist")));
    }

    [Fact]
    public void EveryDependencyIsASpecAndCoreDependsOnlyOnCore()
    {
        var ids = SpecCatalog.All.Select(s => s.Id).ToHashSet();
        SpecCatalog.AssertNone(SpecCatalog.All.SelectMany(s => s.List("depends")
            .Where(d => !ids.Contains(d) || d == s.Id || (s.Folder == "core" && !d.StartsWith("CORE-", StringComparison.Ordinal)))
            .Select(d => $"{s}: depends on {d}, which is {(ids.Contains(d) ? "not allowed" : "not a spec id")}")));
    }

    [Fact]
    public void EveryDecisionPointerNamesADecisionOfItsTarget()
    {
        var owned = SpecCatalog.All.ToDictionary(s => s.Id, s => s.Decisions.Where(d => d.PointsTo == null).Select(d => d.Title).ToHashSet());
        SpecCatalog.AssertNone(SpecCatalog.All.SelectMany(s => s.Decisions
            .Where(d => d.PointsTo != null && !(owned.TryGetValue(d.PointsTo, out var titles) && titles.Contains(d.Title)))
            .Select(d => $"{s}:{d.Line}: '{d.Title}' points to {d.PointsTo}, which has no decision with that title")));
    }

    [Fact]
    public void SpecsCiteNoDecisionNumbersOrPlanDocuments()
    {
        SpecCatalog.AssertNone(SpecCatalog.All.SelectMany(s => s.Body.Split('\n')
            .Select((line, i) => (line, i))
            .Where(x => RetiredReference().IsMatch(x.line))
            .Select(x => $"{s}: body line {x.i + 1} cites a decision number or plan document: {x.line.Trim()}")));
    }

    [Fact]
    public void CoreSpecsNeverMentionAvalonia()
    {
        SpecCatalog.AssertNone(SpecCatalog.All.Where(s => s.Folder == "core")
            .Where(s => LinkTarget().Replace(s.Body, "()").Contains("Avalonia", StringComparison.OrdinalIgnoreCase))
            .Select(s => $"{s}: mentions Avalonia; refer to bridge behavior by spec id only"));
    }

    [Fact]
    public void EveryPublicCoreTypeIsOwnedByExactlyOneSpec()
    {
        SpecCatalog.AssertNone(SpecCatalog.OwnershipErrors(typeof(Presto).Assembly, "core"));
    }

    private static IEnumerable<string> FrontMatterErrors(SpecDocument spec)
    {
        foreach (var error in spec.ParseErrors)
            yield return $"{spec}: {error}";
        foreach (var key in SpecCatalog.ScalarKeys.Where(k => !spec.Scalars.ContainsKey(k)))
            yield return $"{spec}: missing '{key}: value'";
        foreach (var key in SpecCatalog.ListKeys.Where(k => !spec.Lists.ContainsKey(k)))
            yield return $"{spec}: missing list '{key}:' (use '{key}: []' when empty)";
        foreach (var key in spec.Scalars.Keys.Concat(spec.Lists.Keys).Except(SpecCatalog.ScalarKeys).Except(SpecCatalog.ListKeys).Except(["status"]))
            yield return $"{spec}: unknown key '{key}'";
        if (spec.Scalars.TryGetValue("status", out var status) && status != "draft")
            yield return $"{spec}: status must be 'draft' or omitted, was '{status}'";

        var folder = SpecCatalog.Folders.FirstOrDefault(f => f.Folder == spec.Folder);
        if (folder.Folder == null)
        {
            yield return $"{spec}: is not in a package folder ({string.Join(", ", SpecCatalog.Folders.Select(f => f.Folder))})";
            yield break;
        }
        if (!Regex.IsMatch(spec.Id, $"^{folder.Prefix}-[A-Z0-9]+(?:-[A-Z0-9]+)*$"))
            yield return $"{spec}: id '{spec.Id}' must be {folder.Prefix}-NAME";
        if (spec.Scalars.GetValueOrDefault("package") != folder.Package)
            yield return $"{spec}: package must be {folder.Package}";
    }

    private static bool IsValidTestsLine(string? tests) =>
        tests != null && (tests == "untested" || tests.StartsWith("manual — ", StringComparison.Ordinal) || CitationList().IsMatch(tests));

    private static IEnumerable<string> Citations(string? tests) =>
        tests != null && CitationList().IsMatch(tests) ? Citation().Matches(tests).Select(m => m.Groups[1].Value) : [];

    private static bool TestExists(string citation)
    {
        var parts = citation.Split('.');
        if (parts.Length < 2 || !TestSourcesByClass.Value.TryGetValue(parts[^2], out var sources))
            return false;
        var method = new Regex($@"\b(?:void|Task|ValueTask)\s+{Regex.Escape(parts[^1])}\s*[<(]");
        return sources.Any(method.IsMatch);
    }

    private static bool PathExists(string relative)
    {
        var full = Path.Combine(SpecCatalog.Root, relative.TrimEnd('/'));
        return File.Exists(full) || Directory.Exists(full);
    }

    private static Dictionary<string, List<string>> IndexTestSources()
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(Path.Combine(SpecCatalog.Root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match match in ClassDeclaration().Matches(text))
            {
                if (!index.TryGetValue(match.Groups[1].Value, out var list))
                    index[match.Groups[1].Value] = list = [];
                list.Add(text);
            }
        }
        return index;
    }

    [GeneratedRegex(@"^`[\w.]+`(?:, `[\w.]+`)*$")]
    private static partial Regex CitationList();

    [GeneratedRegex(@"`([\w.]+)`")]
    private static partial Regex Citation();

    [GeneratedRegex(@"\bclass\s+(\w+)")]
    private static partial Regex ClassDeclaration();

    [GeneratedRegex(@"\]\([^)]*\)")]
    private static partial Regex LinkTarget();

    [GeneratedRegex(@"\b[Dd]ecision \d+|\b[A-Z0-9-]*PLAN\.md|DESIGN-NOTES|\b[A-Z][A-Z0-9]+(?:-[A-Z0-9]+)*-PLAN\b")]
    private static partial Regex RetiredReference();
}
