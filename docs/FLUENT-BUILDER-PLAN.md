# Monaco.Presto — Fluent builder plan

A fluent, typed way to write the same animations the `AnimationParams` indexer writes. The idea and the survey of the
Windows Community Toolkit `AnimationBuilder` are in [DESIGN-NOTES.md](DESIGN-NOTES.md) §2; this plan decides the open
question there and specifies the work. It is PLAN.md Phase 14.

```csharp
// today
square.Animate(new()
{
    ["X"] = new TweenParams { From = -20, To = 0 },
    ["Opacity"] = 1,
    ["Rotate"] = new TweenParams { To = 90, Ease = Eases.OutBack() },
    Duration = 1250,
    Ease = Eases.OutExpo,
});

// with the builder
Presto.Build()
    .X(from: -20, to: 0)
    .Opacity(1)
    .Rotate(90, ease: Eases.OutBack())
    .Duration(1250)
    .Ease(Eases.OutExpo)
    .Animate(square);                // -> Animation, full playback control
```

## 1. Decisions

1. **The builder sits beside the indexer; it does not replace it.** The indexer is what makes a call read like the
   anime.js object literal, which is the project's stated goal, and every ported upstream test is written against it.
   The builder is an alternative spelling for C# users who want discoverability and compile-time names.
2. **The builder only builds params.** It produces an `AnimationParams` and hands it to `Presto.Animate`, as PLAN §7
   decision 3 requires ("can be layered on later without changing the model"). No new runtime type, no second code
   path, no change to `Animation`, `Tween` or the tick. Anything the builder can express, the indexer can express too.
3. **It ends in a live object, not a fire-and-forget.** Terminals return `Animation` (or `CompositorAnimation`), so
   `Seek`/`Reverse`/`Stretch`/`Then()` work exactly as they do today. This is the one place we deliberately diverge
   from the Toolkit's `AnimationBuilder`.
4. **A builder is a reusable recipe.** It is mutable while being configured, and every terminal snapshots it into a
   fresh `AnimationParams`. Reusing one builder across targets, or changing it after a call, never affects an
   animation already created. (This matters because `AnimationParams`' property dictionary is shared by `with` copies.)
5. **Core names only what core knows.** Transform shorthands (`X`, `Rotate`, …) and Avalonia properties are bridge
   concepts, so their typed methods are bridge extension methods. Core offers the generic `Property(name, …)` that every
   typed method is written in terms of, which is also how plain-object targets (view models) use the builder.
6. **Last write wins, first position stays.** Setting the same property or setting twice replaces the earlier value
   and keeps its original position, exactly as a duplicate key does in a JS object literal and as the ordered
   dictionary behind the indexer already does. Callbacks replace; they do not chain.

## 2. API shape

### 2.1 Core (`Monaco.Presto`)

```csharp
public static class Presto
{
    public static AnimationBuilder Build();
}

// Shared by the animation builder and the keyframe builder so the bridge's typed extensions apply to both.
public abstract class PropertyBuilder<TSelf> where TSelf : PropertyBuilder<TSelf>
{
    private protected PropertyBuilder();

    public TSelf Property(string name, TweenValue to);
    public TSelf Property(string name, TweenValue? to = null, TweenValue? from = null,
        TweenValue? duration = null, TweenValue? delay = null, EaseValue? ease = null,
        Composition? composition = null, Func<double, double>? modifier = null,
        Func<double, string>? textModifier = null);
    public TSelf Property(string name, FunctionValue to);                    // lambdas without a cast
    public TSelf Property(string name, Action<TweenKeyframesBuilder> keyframes);
}

public sealed class AnimationBuilder : PropertyBuilder<AnimationBuilder>
{
    // AnimationParams tween defaults
    public AnimationBuilder Duration(TweenValue duration);
    public AnimationBuilder Delay(TweenValue delay);                         // numbers, FunctionValue, Stagger
    public AnimationBuilder Ease(EaseValue ease);
    public AnimationBuilder PlaybackEase(EaseValue ease);
    public AnimationBuilder Composition(Composition composition);
    public AnimationBuilder Modifier(Func<double, double> modifier);

    // TimerParams
    public AnimationBuilder Id(string id);
    public AnimationBuilder Loop();                                          // infinite
    public AnimationBuilder Loop(LoopCount loop);                            // Loop(3), Loop(false)
    public AnimationBuilder LoopDelay(double ms);
    public AnimationBuilder Alternate(bool alternate = true);
    public AnimationBuilder Reversed(bool reversed = true);
    public AnimationBuilder Autoplay(bool autoplay);
    public AnimationBuilder FrameRate(double fps);
    public AnimationBuilder PlaybackRate(double rate);
    public AnimationBuilder Priority(int priority);

    // Callbacks (replace, see decision 6)
    public AnimationBuilder OnBegin(Action<Timer> callback);                 // ...OnBeforeUpdate, OnUpdate, OnLoop,
    public AnimationBuilder OnComplete(Action<Timer> callback);              // OnPause, OnRender likewise

    // Animation-level keyframes (AnimationParams.Keyframes); duration-based and percentage-based cannot be mixed
    public AnimationBuilder Keyframes(Action<KeyframesBuilder> keyframes);

    // Terminals
    public AnimationParams ToParams();
    public Animation Animate(object? targets);
    public Animation Set(object? targets);
}

public sealed class TweenKeyframesBuilder          // per-property keyframes: ["X"] = TweenValue.Keyframes(...)
{
    public TweenKeyframesBuilder To(TweenValue to, TweenValue? duration = null, TweenValue? delay = null,
        EaseValue? ease = null);
}

public sealed class KeyframesBuilder               // AnimationParams.Keyframes
{
    public KeyframesBuilder Frame(Action<KeyframeBuilder> frame);                     // Keyframe[]
    public KeyframesBuilder At(double percent, Action<KeyframeBuilder> frame);        // "30%" keys, 0-100
}

public sealed class KeyframeBuilder : PropertyBuilder<KeyframeBuilder>
{
    public KeyframeBuilder Duration(TweenValue duration);    // plus Delay, Ease, Composition, Modifier (Keyframe's own)
}
```

`Property(name, to)` with nothing but `to` writes the bare value; any other argument writes a `TweenParams`. The two
therefore produce exactly what the equivalent indexer call produces, which is what the equivalence tests pin.

`Timeline` gets `Add(object? targets, AnimationBuilder builder, TimelinePosition position = default)`, the `Stagger`
position overload and `Set(...)` beside the existing `AnimationParams` ones, each forwarding `builder.ToParams()`.

### 2.2 Bridge (`Monaco.Presto.AvaloniaBridge`)

Extension methods on `PropertyBuilder<T>`, generic in `T` so they chain on both `AnimationBuilder` and
`KeyframeBuilder`:

```csharp
public static class PropertyBuilderExtensions
{
    // Transform shorthands: one method per TransformShorthands name.
    public static T X<T>(this T b, TweenValue to) where T : PropertyBuilder<T>;
    public static T X<T>(this T b, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T>;
    public static T X<T>(this T b, FunctionValue to) where T : PropertyBuilder<T>;
    public static T X<T>(this T b, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T>;
    // ... Y, Rotate, Scale, ScaleX, ScaleY, SkewX, SkewY

    // The properties the samples animate most (see §5): same four overloads each.
    // Opacity, Width, Height, Background, Foreground, CornerRadius, BorderThickness, Margin, Padding
    // Background/Foreground additionally take Color and IBrush; the thickness/radius ones take Thickness/CornerRadius.

    // Pseudo-properties: Draw(start, end) and Text(...) (ScrambleText) and Data (morph).

    // Any AvaloniaProperty, keyed exactly as the typed indexer keys it.
    public static T Property<T>(this T b, AvaloniaProperty property, TweenValue to) where T : PropertyBuilder<T>;
    public static T Property<T>(this T b, AvaloniaProperty property, TweenValue? to = null, /* ...as core */)
        where T : PropertyBuilder<T>;
}

public static class AnimateExtensions
{
    public static Animation Animate(this AvaloniaObject target, AnimationBuilder builder);
    public static CompositorAnimation AnimateOnCompositor(this Visual target, AnimationBuilder builder);
}

public static partial class PrestoAvalonia
{
    public static CompositorAnimation AnimateOnCompositor(object? targets, AnimationBuilder builder);
}
```

- Every bridge extension calls `PrestoAvalonia.EnsureInitialized()`, as `AvaloniaAnimationParams`' constructor does,
  so a builder that names a shorthand works on a control without `UsePresto()`. `control.Animate(builder)` does too,
  which covers a builder that only used the core `Property(string, …)`.
- Typed values (`Color`, `IBrush`, `Thickness`, `CornerRadius`) go through `TweenValue.Of`, the path the samples
  already use for `Palette` colors.
- The compositor terminal copies the subset `CompositorAnimationParams` has. A builder that set something it lacks
  (`Keyframes`, `FrameRate`, `PlaybackEase`, `Composition`, `Modifier`, `OnUpdate`, …) throws `NotSupportedException`
  naming the setting, rather than dropping it; unsupported *properties* already throw inside `CompositorAnimation`.

## 3. Implementation steps

1. **Core builder.** `PropertyBuilder<TSelf>`, `AnimationBuilder`, `TweenKeyframesBuilder`, `KeyframesBuilder`,
   `KeyframeBuilder` at the core root beside `AnimationParams` (namespace `Monaco.Presto`, so the one `using` still
   suffices and folder still equals namespace). `Presto.Build()`. `ToParams` builds a new
   `AnimationParams` per call and fills the internal `Properties` directly. Tests: `AnimationBuilderTests`.
2. **Timeline overloads.** `Add`/`Set` taking `AnimationBuilder`. Tests in `AnimationBuilderTests` (a timeline built
   both ways lays out identically: same child offsets and total duration).
3. **Bridge extensions.** `PropertyBuilderExtensions` (shorthands, common properties, pseudo-properties, typed
   `AvaloniaProperty`) and the `Animate(builder)` extension. Keys come from the same place the indexers get them:
   `TransformShorthands` names and `AvaloniaObjectTargetAdapter.Encode`. Tests: `AnimationBuilderBridgeTests`.
4. **Compositor terminal.** `AnimateOnCompositor(builder)` on `Visual` and `PrestoAvalonia`, with the
   `NotSupportedException` guard. Tests in `AnimationBuilderBridgeTests`.
5. **Docs and sample.**
   - README: a "Fluent builder" section after the params section, and a builder column (or a second line per row)
     in the anime.js → Presto cheat sheet for the rows it shortens.
   - XML docs: 1–2 sentence summaries per the repo convention; the `Property` overloads document the
     bare-value-vs-`TweenParams` rule once, on the class.
   - Samples are a follow-up after this phase, not part of it.
   - PLAN.md: tick Phase 14; reword §7 decision 3 to "string indexer plus an optional fluent builder over it";
     DESIGN-NOTES §2 gets a one-line "Built, see FLUENT-BUILDER-PLAN.md" banner like §1's.

## 4. Tests

**Core — `AnimationBuilderTests`** (no Avalonia, `ManualFrameSource`):

- Equivalence: for a table of cases (bare values, from/to, per-property duration/delay/ease, function values,
  staggers, per-property keyframes, duration- and percentage-based animation keyframes, every timer setting), the
  builder's `ToParams()` has the same property order, the same raw values and the same settings as the indexer form.
- Behavioral equivalence: the same two animations on two plain objects, one per spelling, read the same values at
  several `Advance` points, including across a loop and an alternate.
- `Property(name, to)` alone writes a bare value; adding any other argument writes a `TweenParams`.
- Duplicate property: last value wins, first position kept. Duplicate setting and callback: last wins.
- Reuse: two `ToParams()` calls return independent params; mutating the builder afterwards changes neither.
- `Keyframes` mixing `Frame` and `At` throws `InvalidOperationException`; `At` outside 0–100 throws.
- `Loop()` is infinite, `Loop(3)` is three, `Loop(false)` is none, matching `LoopCount`'s conversions.
- Timeline: `Add` with a builder and with the equivalent params produce the same child offsets and duration.

**Bridge — `AnimationBuilderBridgeTests`** (`Avalonia.Headless`):

- Each shorthand and common-property method writes the same key the string indexer would, so a builder animation and an
  indexer animation on the same property compose (the later one replaces the earlier — the DESIGN-NOTES §1 regression,
  from the builder side).
- `Property(AvaloniaProperty, …)` composes with a string-keyed animation on the same property.
- Shorthand methods chain on `KeyframeBuilder` inside `Keyframes(...)`.
- `Background(Color)` and `Background(IBrush)` animate to the right color.
- `control.Animate(builder)` works on a fresh app with only core `Property("Opacity", …)` calls (initialization).
- `AnimateOnCompositor(builder)` runs a translate/opacity animation; a builder with `Keyframes` or `OnUpdate` throws
  `NotSupportedException` naming it.
- `AllocationTests` unchanged: the builder is configuration-time only and never runs on the tick.

## 5. Which properties get typed methods

Frequency of string keys across `samples/` (as of this plan): `Opacity` 70, `Y` 59, `X` 54, `Scale` 43, `Rotate` 34,
`Background` 18, `Text` 13, `ScaleY` 12, `Draw` 11, `Data` 8, `Width` 7, `ScaleX` 7, `Foreground` 5, `Height` 5,
`CornerRadius` 4, then single uses. That, plus all eight transform shorthands and the layout-ish thickness properties,
is the list in §2.2. Everything else is one `Property(SomeControl.SomeProperty, …)` call away, typed and
refactor-safe, so the list does not need to grow to cover every control.

## 6. Out of scope

- Builders for `Timer`, `Timeline`, `Animatable`, `Draggable`, `ScrollObserver` params. Their object initializers have
  no string keys, so a builder buys little. Revisit if users ask.
- Sample pages for the builder (a follow-up phase), and migrating existing samples and README examples to it. The indexer stays the primary, documented spelling.
- A typed `PrestoProperty` handle in the params surface (DESIGN-NOTES §1 option 3). The builder gets typed names
  without it; the indexer stays string/`AvaloniaProperty`-keyed.
- XAML surface. Still deferred (PLAN §7); the builder does not change that decision, but a XAML layer could later be
  written on top of `AnimationBuilder` rather than raw params.

## 7. Open questions

- **Entry-point name.** Decided: `Presto.Build()`, plus a public `AnimationBuilder` constructor.
- **Per-property overload count.** Four overloads per typed method (bare, full, `FunctionValue`, keyframes) × ~20
  properties is ~80 small methods in the bridge. Hand-written is fine at this size; a source generator is not worth it
  unless the list grows.

## 8. Deviations as built

- Adding the builder overloads made every existing `new() { … }` call to `Timeline.Add`/`Set`, `control.Animate` and
  `AnimateOnCompositor` ambiguous, since a target-typed `new()` fits both parameter types. The existing params overloads
  are marked `[OverloadResolutionPriority(1)]`, so those calls bind as before.
- The compositor guard also rejects `Id`, a non-default `Priority`, `OnBeforeUpdate`, `OnLoop`, `OnPause` and `OnRender`.
- Only `Text` and the typed `Property(AvaloniaProperty, …)` extensions take `textModifier`; the other typed methods stop
  at `modifier`.
