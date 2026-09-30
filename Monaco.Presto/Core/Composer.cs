using System.Runtime.CompilerServices;

namespace Monaco.Presto.Core;

/// <summary>
/// Port of anime.js <c>composition.js</c>: the per-(target, property) sibling registries and the composition of a new tween
/// against the tweens already animating the same property.
/// </summary>
internal static class Composer
{
    // Keyed by target and never iterated, so unlike the additive lookups it can stay process-wide (COMPOSITOR-PLAN §4.1).
    private static readonly ConditionalWeakTable<object, Dictionary<string, TweenSiblings>> s_replaceLookups = new();

    public static TweenSiblings GetTweenSiblings(object target, string property, Additive? additive = null)
    {
        Dictionary<string, TweenSiblings>? targetLookup;
        if (additive != null)
        {
            var lookups = additive.Lookups;
            if (!lookups.TryGetValue(target, out targetLookup)) lookups[target] = targetLookup = [];
        }
        else
        {
            targetLookup = s_replaceLookups.GetValue(target, static _ => []);
        }
        if (!targetLookup.TryGetValue(property, out var siblings)) targetLookup[property] = siblings = new();
        return siblings;
    }

    public static void OverrideTween(Tween tween)
    {
        tween._isOverlapped = true;
        tween._isOverridden = true;
        tween._changeDuration = Constants.MinValue;
        tween._currentTime = Constants.MinValue;
    }

    public static void ComposeTween(Tween tween, TweenSiblings siblings)
    {
        if (tween._composition == Composition.Replace) ComposeReplacedTween(tween, siblings);
        else if (tween._composition == Composition.Blend) ComposeBlendedTween(tween);
    }

    public static void RemoveTweenSiblings(Tween tween)
    {
        var composition = tween._composition;
        if (composition == Composition.None) return;

        if (s_replaceLookups.TryGetValue(tween.Target, out var replaceProperties) &&
            replaceProperties.TryGetValue(tween.Property, out var replaceSiblings))
        {
            TweenList.RemoveChild(replaceSiblings, tween, TweenLink.Replace);
        }

        if (composition != Composition.Blend) return;

        var additive = tween.Parent.OwnerEngine.Additive;
        if (!additive.Lookups.TryGetValue(tween.Target, out var additiveProperties) ||
            !additiveProperties.TryGetValue(tween.Property, out var additiveSiblings)) return;

        TweenList.RemoveChild(additiveSiblings, tween, TweenLink.Additive);

        // A single remaining tween in an additive list is the lookup tween itself, so the whole entry goes with it.
        var lookupTween = additiveSiblings._head;
        if (lookupTween == null || lookupTween != additiveSiblings._tail) return;

        TweenList.RemoveChild(additiveSiblings, lookupTween, TweenLink.Additive);
        if (additive.Animation is { } additiveAnimation) TweenList.RemoveChild(additiveAnimation, lookupTween);

        foreach (var properties in additiveProperties.Values)
        {
            if (properties._head != null) return;
        }
        additive.Lookups.Remove(tween.Target);
    }

    /// <summary>
    /// Strips <paramref name="targets"/> from every animation under <paramref name="parent"/>, cancelling the animations left
    /// with no tweens and the parent itself when nothing is left.
    /// </summary>
    public static void RemoveTargetsFromRenderable(IReadOnlyList<object> targets, Clock parent, string? property)
    {
        var parentTimer = parent as Timer;
        var removeMatches = false;
        if (parent._hasChildren)
        {
            var iterationDuration = 0d;
            Helpers.ForEachChildren(parent, child =>
            {
                if (!child._hasChildren)
                {
                    removeMatches = RemoveTargetsFromAnimation(targets, child, property);
                    if (removeMatches && child._headTween == null)
                    {
                        child.Cancel();
                        Helpers.RemoveChild(parent, child);
                    }
                    else
                    {
                        var childEnd = child._offset + child._delay + child.Duration;
                        if (childEnd > iterationDuration) iterationDuration = childEnd;
                    }
                }
                if (child._head != null) RemoveTargetsFromRenderable(targets, child, property);
                else child._hasChildren = false;
            }, true);
            // Keeps the surviving last child completing the parent at the right time in render().
            if (parentTimer != null) parentTimer.IterationDuration = iterationDuration;
        }
        else if (parentTimer != null)
        {
            removeMatches = RemoveTargetsFromAnimation(targets, parentTimer, property);
        }
        if (!removeMatches || parent._head != null || parentTimer?._headTween != null) return;
        parent._hasChildren = false;
        // The engine is a valid parent and has nothing to cancel.
        parentTimer?.Cancel();
    }

    // Upstream also re-flags the previous transform tween so it keeps rendering the transform string; that belongs to the bridge.
    private static bool RemoveTargetsFromAnimation(IReadOnlyList<object> targets, Timer animation, string? property)
    {
        var matched = false;
        TweenList.ForEachChildren(animation, tween =>
        {
            if (!Contains(targets, tween.Target)) return;
            if (!string.IsNullOrEmpty(property) && Presto.Canonicalise(tween.Target, property) != tween.Property) return;
            TweenList.RemoveChild(animation, tween);
            RemoveTweenSiblings(tween);
            matched = true;
        }, true);
        return matched;
    }

    private static bool Contains(IReadOnlyList<object> targets, object target)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            if (ReferenceEquals(targets[i], target)) return true;
        }
        return false;
    }

    private static void ComposeReplacedTween(Tween tween, TweenSiblings siblings)
    {
        var tweenAbsStartTime = tween._absoluteStartTime;
        TweenList.AddChild(siblings, tween, TweenLink.Replace, AddTweenSortMethod);

        var prevSibling = tween._prevRep;
        if (prevSibling == null) return;

        var prevParent = prevSibling.Parent;
        var prevAbsEndTime = prevSibling._absoluteEndTime;

        // A looped previous animation renders its remaining iterations over the new tween, so its whole keyframe chain goes.
        if (tween.Parent.Id != prevParent.Id &&
            prevParent.IterationCount > 1 &&
            prevAbsEndTime + (prevParent.Duration - prevParent.IterationDuration) > tweenAbsStartTime)
        {
            OverrideTween(prevSibling);
            var prevPrevSibling = prevSibling._prevRep;
            while (prevPrevSibling != null && prevPrevSibling.Parent.Id == prevParent.Id)
            {
                OverrideTween(prevPrevSibling);
                prevPrevSibling = prevPrevSibling._prevRep;
            }
        }

        // The precision-matched update start, not _absoluteStartTime - _delay, so keyframes touching at a boundary don't read as overlapping.
        var absoluteUpdateStartTime = tween._absoluteUpdateStartTime;
        if (prevAbsEndTime > absoluteUpdateStartTime)
        {
            var prevChangeStartTime = prevSibling._startTime;
            var prevTimelineOffset = prevAbsEndTime - (prevChangeStartTime + prevSibling._updateDuration);
            // Rounding minimises the floating point errors of working in seconds.
            var updatedPrevChangeDuration = Helpers.Round(absoluteUpdateStartTime - prevTimelineOffset - prevChangeStartTime, 12);
            prevSibling._changeDuration = updatedPrevChangeDuration;
            prevSibling._currentTime = updatedPrevChangeDuration;
            prevSibling._isOverlapped = true;
            if (updatedPrevChangeDuration < Constants.MinValue) OverrideTween(prevSibling);
        }

        // A timeline cannot replace itself, so tweens sharing a parent timeline never cancel each other.
        var tweenParentTimeline = tween.Parent.Parent;
        if (tweenParentTimeline != null && tweenParentTimeline == prevParent.Parent) return;
        if (!AllTweensOverlapped(prevParent)) return;

        var prevParentTimeline = prevParent.Parent;
        if (prevParentTimeline == null)
        {
            prevParent.Cancel();
            return;
        }

        // A loop rather than ForEachChildren: its closure would be allocated on every call, since the capture is method-wide.
        for (var child = prevParentTimeline._head; child != null; child = child._next)
        {
            if (child != prevParent && !AllTweensOverlapped(child)) return;
        }
        prevParentTimeline.Cancel();
    }

    private static void ComposeBlendedTween(Tween tween)
    {
        var additive = tween.Parent.OwnerEngine.Additive;
        var additiveSiblings = GetTweenSiblings(tween.Target, tween.Property, additive);
        var additiveAnimation = additive.GetOrCreate();
        var lookupTween = additiveSiblings._head;

        if (lookupTween == null)
        {
            lookupTween = tween.Clone();
            lookupTween._composition = Composition.Replace;
            lookupTween._updateDuration = Constants.MinValue;
            lookupTween._startTime = 0;
            lookupTween._numbers = Helpers.CloneNumbers(tween._fromNumbers);
            lookupTween._number = 0;
            lookupTween._prev = null;
            lookupTween._next = null;
            TweenList.AddChild(additiveSiblings, lookupTween, TweenLink.Parent);
            TweenList.AddChild(additiveAnimation, lookupTween);
        }

        // The lookup tween holds the composed value the blended tweens add their deltas to.
        var toNumber = tween._toNumber;
        tween._fromNumber = lookupTween._fromNumber - toNumber;
        tween._toNumber = 0;
        tween._numbers = Helpers.CloneNumbers(tween._fromNumbers);
        tween._number = 0;
        lookupTween._fromNumber = toNumber;

        if (tween._toNumbers.Length > 0)
        {
            var toNumbers = Helpers.CloneNumbers(tween._toNumbers);
            for (var i = 0; i < toNumbers.Length; i++)
            {
                tween._fromNumbers[i] = lookupTween._fromNumbers[i] - toNumbers[i];
                tween._toNumbers[i] = 0;
            }
            lookupTween._fromNumbers = toNumbers;
        }

        TweenList.AddChild(additiveSiblings, tween, TweenLink.Additive);
    }

    private static bool AddTweenSortMethod(Tween prev, Tween child) =>
        prev._isOverridden || prev._absoluteStartTime > child._absoluteStartTime;

    private static bool AllTweensOverlapped(Timer animation)
    {
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            if (!tween._isOverlapped) return false;
        }
        return true;
    }
}
