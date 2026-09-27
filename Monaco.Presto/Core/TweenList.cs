namespace Monaco.Presto.Core;

/// <summary>The <see cref="Helpers"/> linked-list operations over tweens, parameterised by the link pair they walk.</summary>
internal static class TweenList
{
    public static void ForEachChildren(Timer parent, Action<Tween> callback, bool reverse = false)
    {
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
        var prev = tail;
        while (prev != null && sortMethod != null && sortMethod(prev, child)) prev = Prev(prev, link);
        var next = prev != null ? Next(prev, link) : head;
        if (prev != null) Next(prev, link) = child; else head = child;
        if (next != null) Prev(next, link) = child; else tail = child;
        Prev(child, link) = prev;
        Next(child, link) = next;
    }

    private static void RemoveChild(ref Tween? head, ref Tween? tail, Tween child, TweenLink link)
    {
        var prev = Prev(child, link);
        var next = Next(child, link);
        if (prev != null) Next(prev, link) = next; else head = next;
        if (next != null) Prev(next, link) = prev; else tail = prev;
        Prev(child, link) = null;
        Next(child, link) = null;
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
