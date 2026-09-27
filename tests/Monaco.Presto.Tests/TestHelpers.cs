using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

internal static class TestHelpers
{
    public static Timer? GetChildAtIndex(Clock parent, int index)
    {
        var next = parent._head;
        var i = 0;
        while (next != null)
        {
            var currentNext = next._next;
            if (i == index) break;
            next = currentNext;
            i++;
        }
        return next;
    }

    public static double OffsetOf(Timeline timeline, int index) => GetChildAtIndex(timeline, index)!._offset;

    public static int GetChildLength(Clock parent)
    {
        var next = parent._head;
        var i = 0;
        while (next != null)
        {
            next = next._next;
            i++;
        }
        return i;
    }

    public static Tween? GetTweenAtIndex(Timer parent, int index)
    {
        var next = parent._headTween;
        var i = 0;
        while (next != null)
        {
            var currentNext = next._next;
            if (i == index) break;
            next = currentNext;
            i++;
        }
        return next;
    }

    public static int GetTweenCount(Timer parent)
    {
        var next = parent._headTween;
        var i = 0;
        while (next != null)
        {
            next = next._next;
            i++;
        }
        return i;
    }
}
