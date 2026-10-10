using MonacoLabs.Presto.Diagnostics;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Core;

/// <summary>Port of anime.js <c>additive.js</c>: one engine's blended tweens, summed into their lookup tween and rendered.</summary>
internal sealed class Additive(Engine engine)
{
    /// <summary>The blended tween siblings of the targets animated on this engine, keyed by target then property.</summary>
    public Dictionary<object, Dictionary<string, TweenSiblings>> Lookups { get; } = new(ReferenceEqualityComparer.Instance);

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
