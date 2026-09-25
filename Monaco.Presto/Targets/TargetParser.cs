namespace Monaco.Presto.Targets;

/// <summary>Flattens the targets parameter into the list an animation tweens, porting upstream <c>parseTargets</c>.</summary>
public static class TargetParser
{
    // Below this many targets the upstream linear scan beats hashing.
    private const int HashThreshold = 16;

    /// <summary>Nested collections are flattened, nulls are skipped and a target repeated in the list is kept once, at its first position.</summary>
    public static IReadOnlyList<object> Parse(object? targets)
    {
        var parsed = new List<object>();
        HashSet<object>? seen = null;
        Flatten(targets, parsed, ref seen);
        return parsed;
    }

    private static void Flatten(object? target, List<object> parsed, ref HashSet<object>? seen)
    {
        if (target is null) return;
        // Strings and dictionary targets are IEnumerable over structs, so IEnumerable<object> flattens lists without unpacking them.
        if (target is IEnumerable<object> items)
        {
            foreach (var item in items) Flatten(item, parsed, ref seen);
            return;
        }
        if (seen != null)
        {
            if (!seen.Add(target)) return;
        }
        else
        {
            for (var i = 0; i < parsed.Count; i++)
            {
                if (ReferenceEquals(parsed[i], target)) return;
            }
        }
        parsed.Add(target);
        if (seen == null && parsed.Count == HashThreshold) seen = new HashSet<object>(parsed, ReferenceEqualityComparer.Instance);
    }
}
