namespace Monaco.Presto.Targets;

/// <summary>Flattens the targets parameter into the list an animation tweens, porting upstream <c>parseTargets</c>.</summary>
public static class TargetParser
{
    /// <summary>Nested collections are flattened, nulls are skipped and a target repeated in the list is kept once, at its first position.</summary>
    public static IReadOnlyList<object> Parse(object? targets)
    {
        var parsed = new List<object>();
        Flatten(targets, parsed);
        return parsed;
    }

    private static void Flatten(object? target, List<object> parsed)
    {
        if (target is null) return;
        // Strings and dictionary targets are IEnumerable over structs, so IEnumerable<object> flattens lists without unpacking them.
        if (target is IEnumerable<object> items)
        {
            foreach (var item in items) Flatten(item, parsed);
            return;
        }
        for (var i = 0; i < parsed.Count; i++)
        {
            if (ReferenceEquals(parsed[i], target)) return;
        }
        parsed.Add(target);
    }
}
