using System.Diagnostics;

namespace Monaco.Presto.Core;

/// <summary>The <see cref="Helpers"/> linked-list operations over tweens, parameterised by the link pair they walk.</summary>
internal static class TweenList
{
    public static void ForEachChildren(Timer parent, Action<Tween> callback, bool reverse = false)
    {
        AssertIntact(parent._headTween, parent._tailTween, TweenLink.Parent);
        var next = reverse ? parent._tailTween : parent._headTween;
        while (next != null)
        {
            var currentNext = reverse ? next._prev : next._next;
            callback(next);
            next = currentNext;
        }
    }

    public static void AddChild(Timer parent, Tween child) =>
        AddChild(ref parent._headTween, ref parent._tailTween, child, TweenLink.Parent, null);

    public static void AddChild(TweenSiblings parent, Tween child, TweenLink link, Func<Tween, Tween, bool>? sortMethod = null) =>
        AddChild(ref parent._head, ref parent._tail, child, link, sortMethod);

    public static void RemoveChild(Timer parent, Tween child) =>
        RemoveChild(ref parent._headTween, ref parent._tailTween, child, TweenLink.Parent);

    public static void RemoveChild(TweenSiblings parent, Tween child, TweenLink link) =>
        RemoveChild(ref parent._head, ref parent._tail, child, link);

    private static void AddChild(ref Tween? head, ref Tween? tail, Tween child, TweenLink link, Func<Tween, Tween, bool>? sortMethod)
    {
        // Checked only before a walk: a whole-list check on every unsorted insert or removal made a 20 000-tween Debug build quadratic.
        if (sortMethod != null) AssertIntact(head, tail, link);
        var prev = tail;
        while (prev != null && sortMethod != null && sortMethod(prev, child)) prev = Prev(prev, link);
        var next = prev != null ? Next(prev, link) : head;
        if (prev != null) Next(prev, link) = child; else head = child;
        if (next != null) Prev(next, link) = child; else tail = child;
        Prev(child, link) = prev;
        Next(child, link) = next;
    }

    // Unlike upstream, removing a tween that is not in the list is a no-op instead of clearing or repointing head and tail.
    private static void RemoveChild(ref Tween? head, ref Tween? tail, Tween child, TweenLink link)
    {
        var prev = Prev(child, link);
        var next = Next(child, link);
        if ((prev == null && head != child) || (next == null && tail != child)) return;
        if (prev != null) Next(prev, link) = next; else head = next;
        if (next != null) Prev(next, link) = prev; else tail = prev;
        Prev(child, link) = null;
        Next(child, link) = null;
    }

    /// <summary>Throws when the tween list is not a well-formed, acyclic chain from head to tail.</summary>
    public static void CheckIntact(Timer parent) => CheckIntact(parent._headTween, parent._tailTween, TweenLink.Parent);

    /// <summary>Throws when the sibling list is not a well-formed, acyclic chain from head to tail.</summary>
    public static void CheckIntact(TweenSiblings siblings, TweenLink link = TweenLink.Additive) =>
        CheckIntact(siblings._head, siblings._tail, link);

    [Conditional("DEBUG")]
    private static void AssertIntact(Tween? head, Tween? tail, TweenLink link) => CheckIntact(head, tail, link);

    // Throws rather than Debug.Fail so a corrupted list fails the test instead of hanging a walk or killing the test host.
    private static void CheckIntact(Tween? head, Tween? tail, TweenLink link)
    {
        if ((head == null) != (tail == null)) throw new InvalidOperationException($"Tween list ({link}) has only one end set.");
        if (head == null) return;
        if (Prev(head, link) != null) throw new InvalidOperationException($"Tween list ({link}) head is linked past the list.");
        var slow = head;
        var fast = head;
        while (Next(fast, link) is { } step)
        {
            fast = step;
            if (Next(fast, link) is not { } secondStep) break;
            fast = secondStep;
            slow = Next(slow, link)!;
            if (slow == fast) throw new InvalidOperationException($"Tween list ({link}) has a cycle.");
        }
        if (fast != tail) throw new InvalidOperationException($"Tween list ({link}) does not end at its tail.");
    }

    private static ref Tween? Prev(Tween tween, TweenLink link)
    {
        switch (link)
        {
            case TweenLink.Replace: return ref tween._prevRep;
            case TweenLink.Additive: return ref tween._prevAdd;
            default: return ref tween._prev;
        }
    }

    private static ref Tween? Next(Tween tween, TweenLink link)
    {
        switch (link)
        {
            case TweenLink.Replace: return ref tween._nextRep;
            case TweenLink.Additive: return ref tween._nextAdd;
            default: return ref tween._next;
        }
    }
}
