namespace MonacoLabs.Presto;

/// <summary>
/// A parsed <see cref="MotionState.When"/>: ANDed tokens on the element itself. <see cref="Classes"/> holds each token as
/// Avalonia's <c>Classes</c> stores it (<c>:pointerover</c>, <c>selected</c>); <see cref="State"/> is the leading bare name.
/// <c>:pressed</c> and <c>:pointerover</c> go to <see cref="Pressed"/> and <see cref="Hovered"/> instead, since the tracker
/// synthesizes both.
/// </summary>
internal sealed class MotionCondition
{
    private MotionCondition(string[] classes, string? state, bool pressed, bool hovered)
    {
        Classes = classes;
        State = state;
        Pressed = pressed;
        Hovered = hovered;
    }

    public string[] Classes { get; }

    public string? State { get; }

    public bool Pressed { get; }

    public bool Hovered { get; }

    /// <exception cref="ArgumentException"><paramref name="when"/> is empty or has selector parts beyond the element's own tokens.</exception>
    public static MotionCondition Parse(string when)
    {
        if (when.Length == 0) throw NotACondition(when);
        var classes = new List<string>();
        string? state = null;
        var pressed = false;
        var hovered = false;
        var start = 0;
        for (var i = 1; i <= when.Length; i++)
        {
            if (i < when.Length && when[i] is not ('.' or ':')) continue;
            var token = when[start..i];
            var name = token[0] is '.' or ':' ? token[1..] : token;
            if (name.Length == 0 || !IsName(name)) throw NotACondition(when);
            if (token == ":pressed") pressed = true;
            else if (token == ":pointerover") hovered = true;
            else if (token[0] == '.') classes.Add(name);
            else if (token[0] == ':') classes.Add(token);
            else state = token;
            start = i;
        }
        return new MotionCondition(classes.ToArray(), state, pressed, hovered);
    }

    /// <summary>Whether every token holds, given the element's <paramref name="classes"/> (pseudo-classes included), its <c>Motion.State</c> <paramref name="states"/> and its synthesized press and hover.</summary>
    public bool Matches(ICollection<string> classes, ICollection<string> states, bool pressed = false, bool hovered = false)
    {
        if (State != null && !states.Contains(State)) return false;
        if (Pressed && !pressed) return false;
        if (Hovered && !hovered) return false;
        foreach (var token in Classes)
        {
            if (!classes.Contains(token)) return false;
        }
        return true;
    }

    private static bool IsName(string name)
    {
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c is not ('-' or '_')) return false;
        }
        return true;
    }

    private static ArgumentException NotACondition(string when) => new(
        $"\"{when}\" is not a motion condition, which is ANDed :pseudo-class, .class and state-name tokens on the element itself. "
        + "For a selector, set presto:Motion.State from a style setter on it and name that state here.",
        nameof(when));
}
