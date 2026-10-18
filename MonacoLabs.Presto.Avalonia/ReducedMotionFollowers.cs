using System.Runtime.CompilerServices;

namespace MonacoLabs.Presto;

/// <summary>
/// The bridge triggers and states whose live animation follows a change of the reduced-motion flags: one
/// <see cref="Engine.ReducedMotionChanged"/> subscription per engine, holding only the followers live on it, so the
/// engine never roots a detached control.
/// </summary>
internal static class ReducedMotionFollowers
{
    private static readonly ConditionalWeakTable<Engine, List<Action>> s_followers = new();

    /// <summary>Registers <paramref name="follow"/> on the current engine, which it returns for <see cref="Remove"/>.</summary>
    public static Engine Add(Action follow)
    {
        var engine = Engine.Current;
        if (!s_followers.TryGetValue(engine, out var followers))
        {
            s_followers.Add(engine, followers = []);
            engine.ReducedMotionChanged += HandleChanged;
        }
        followers.Add(follow);
        return engine;
    }

    public static void Remove(Engine engine, Action follow)
    {
        if (s_followers.TryGetValue(engine, out var followers)) followers.Remove(follow);
    }

    internal static List<Action> Of(Engine engine) => s_followers.TryGetValue(engine, out var followers) ? followers : [];

    private static void HandleChanged(Engine engine)
    {
        if (!s_followers.TryGetValue(engine, out var followers) || followers.Count == 0) return;
        using var current = engine.MakeCurrent();
        // A replay re-registers its follower, so walk a copy.
        foreach (var follow in followers.ToArray()) follow();
    }

    /// <summary>The form resolved params play on the current engine: the reduced alternative, collapsed by <see cref="PrestoDefaults.ReduceAll"/>, looping forever.</summary>
    internal readonly record struct Form(bool Reduced, bool Collapsed, bool Loops)
    {
        public static Form Of(AnimationParams resolved)
        {
            var defaults = Engine.Current.Defaults;
            var collapsed = !resolved.IsReducedMotion && defaults.ReduceAll;
            return new(resolved.IsReducedMotion, collapsed, !collapsed && (resolved.Loop ?? defaults.Loop).IsInfinite);
        }

        /// <summary>Whether an animation playing this form must replay as <paramref name="now"/>: the form changed and one of the two loops forever.</summary>
        public bool Replays(Form now) => (now.Reduced != Reduced || now.Collapsed != Collapsed) && (Loops || now.Loops);
    }
}
