using System.Runtime.CompilerServices;
using MonacoLabs.Presto.Diagnostics;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Core;

/// <summary>One engine's additive tweens, summed into their lookup tween and rendered.</summary>
internal sealed class Additive(Engine engine)
{
    /// <summary>The additive tween siblings of the targets animated on this engine, keyed by target then property.</summary>
    public Dictionary<object, Dictionary<string, TweenSiblings>> Lookups { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>The <see cref="Lookups"/> entries of targets whose additive tweens all belong to paused roots, held weakly and not rendered.</summary>
    public ConditionalWeakTable<object, Dictionary<string, TweenSiblings>> Parked { get; } = new();

    /// <summary>Owns the additive lookup tweens; never added to the engine and only rendered by <see cref="Update"/>.</summary>
    public Timer? Animation { get; private set; }

    public Timer GetOrCreate()
    {
        if (Animation != null) return Animation;
        using (PrestoDiagnostics.Origin("Additive"))
        {
            return Animation = new Timer(new TimerParams { Duration = Constants.MinValue, Delay = 0, Loop = 0, Autoplay = false }, null, 0, engine) { _offset = 0 };
        }
    }

    /// <summary>Parks the entry of each target <paramref name="root"/> animates additively once every additive tween of that entry belongs to a paused root.</summary>
    public void Park(Timer root)
    {
        for (var tween = root._headTween; tween != null; tween = tween._next)
        {
            var target = tween.Target;
            if (tween._composition != Composition.Additive || !Lookups.TryGetValue(target, out var properties) || !AllPaused(properties)) continue;
            Lookups.Remove(target);
            Parked.AddOrUpdate(target, properties);
            foreach (var siblings in properties.Values)
            {
                if (siblings._head is { } lookupTween) TweenList.RemoveChild(Animation!, lookupTween);
            }
        }
        for (var child = root._head; child != null; child = child._next) Park(child);
    }

    /// <summary>Restores the parked entries of the targets <paramref name="root"/> animates additively.</summary>
    /// <returns>Whether any entry was parked.</returns>
    public bool Unpark(Timer root)
    {
        var unparked = false;
        for (var tween = root._headTween; tween != null; tween = tween._next)
        {
            if (tween._composition == Composition.Additive) unparked |= UnparkTarget(tween.Target);
        }
        for (var child = root._head; child != null; child = child._next) unparked |= Unpark(child);
        return unparked;
    }

    public bool UnparkTarget(object target)
    {
        if (!Parked.TryGetValue(target, out var properties)) return false;
        Parked.Remove(target);
        Lookups[target] = properties;
        var animation = GetOrCreate();
        foreach (var siblings in properties.Values)
        {
            if (siblings._head is { } lookupTween) TweenList.AddChild(animation, lookupTween);
        }
        return true;
    }

    private static bool AllPaused(Dictionary<string, TweenSiblings> properties)
    {
        foreach (var siblings in properties.Values)
        {
            for (var tween = siblings._tail; tween != null && tween != siblings._head; tween = tween._prevAdd)
            {
                var root = tween.Parent;
                while (root.Parent != null) root = root.Parent;
                if (!root.Paused) return false;
            }
        }
        return true;
    }

    public void Update()
    {
        var animation = Animation;
        if (animation == null) return;

        foreach (var properties in Lookups.Values)
        {
            foreach (var siblings in properties.Values)
            {
                var lookupTween = siblings._head;
                if (lookupTween == null) continue;

                var valueType = lookupTween._valueType;
                var additiveNumbers = valueType is ValueKind.Complex or ValueKind.Color ? Helpers.CloneNumbers(lookupTween._fromNumbers) : null;
                var additiveNumber = lookupTween._fromNumber;

                var tween = siblings._tail;
                while (tween != null && tween != lookupTween)
                {
                    if (additiveNumbers != null)
                    {
                        for (var i = 0; i < tween._numbers.Length; i++) additiveNumbers[i] += tween._numbers[i];
                    }
                    else
                    {
                        additiveNumber += tween._number;
                    }
                    tween = tween._prevAdd;
                }

                lookupTween._toNumber = additiveNumber;
                lookupTween._toNumbers = additiveNumbers ?? [];
            }
        }

        Renderer.Render(animation, 1, true, false, TickMode.Force);
    }
}
