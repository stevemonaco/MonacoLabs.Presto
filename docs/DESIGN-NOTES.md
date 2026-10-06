# Design notes

Findings from the investigation after Phase 8, ahead of the API quality pass. Each section states what was measured, not what is assumed.

## 1. String-keyed properties

> **Fixed.** `ITargetAdapter.Canonicalise` now resolves every spelling of a property to one key, applied where a caller-supplied
> name meets a target: tween construction (`Animation`), the `Remove` filter (`Composer`) and `Animatable`'s lookups. Covered by
> `PropertyNamingTests`, each case confirmed to fail with the fix disabled. The history below is kept for the rationale.

### The defect

`Composer.GetTweenSiblings(target, property)` keys the per-(target, property) sibling registry on the **property string**. The bridge has two spellings for the same property:

| Written as | Key |
|---|---|
| `["Opacity"] = 0.5` | `Opacity` |
| `[Visual.OpacityProperty] = 0.5` | `Visual.Opacity` (`AvaloniaObjectTargetAdapter.Encode`) |

Both resolve to the same `AvaloniaProperty`, but they land in different sibling registries, so **composition never fires between them**. Two animations on the same property both stay live, both write every frame, and the later one wins by write order.

Reproduced on the current tree with a throwaway bridge test (since deleted):

```csharp
var a = border.Animate(new() { ["Opacity"] = 0.0, Duration = 100, Ease = Eases.None });
var b = border.Animate(new AvaloniaAnimationParams { [Visual.OpacityProperty] = 1.0, Duration = 100, Ease = Eases.None });
Frames.Advance(50);
// a.Cancelled == false  (expected true: b should have overridden it)
// border.Opacity == 1    (a alone would give 0.5)
```

The same-spelling control case overrides correctly, so the mechanism is specifically the spelling mismatch.

Two consequences already recorded elsewhere are the same root cause:

- `Presto.Remove(target, AvaloniaProperty)` only cancels tweens declared through the typed indexer (Phase 6 note).
- Transform shorthands (`"X"`, `"Rotate"`) are magic strings parsed by `TransformShorthands.Parse`; an unknown name throws `ArgumentException` at run time rather than failing to compile.

### Options

1. **Canonicalise the key (smallest fix).** Have the bridge encode both spellings to one key — resolve the name to its `AvaloniaProperty` first, then encode. Fixes composition, `Remove`, and the `Animatable` accessors together, and needs no change to the core. The core stays string-keyed, which keeps the upstream port intact and keeps plain-object targets working.
2. **Key on the resolved accessor instead of the string.** `Composer` would key on `IPropertyAccessor` (or a `PropertyKey` struct) rather than `string`. Structurally cleaner, but it moves the port away from upstream's `composition.js` and touches `Tween`, `Animation`, `Composer` and `Animatable`.
3. **Typed property handles in the params surface.** A `PrestoProperty` abstraction over "string name | AvaloniaProperty | transform shorthand" that the indexer and the builder both take. Gets compile-time safety for shorthands. Largest change; subsumes option 1.

Option 1 is the bug fix; option 3 is the API improvement. They are independent and can land in that order.

## 2. Fluent builder

> **Built**, see [FLUENT-BUILDER-PLAN.md](FLUENT-BUILDER-PLAN.md) (PLAN.md Phase 14): `Presto.Build()` sits beside the
> indexer, not in place of it. The survey below is kept for the rationale.

`AnimationBuilder` ([docs](https://learn.microsoft.com/en-us/windows/communitytoolkit/animations/animationbuilder)) is a **fire-and-forget schedule**: `Create()` configures, `Start(element)` runs it, and the builder is reusable across elements. Control after the fact is a `CancellationToken`; there is no seek, reverse or playback rate.

```csharp
AnimationBuilder.Create()
    .Opacity(from: 0, to: 1)
    .Translation(Axis.X, from: -20, to: 0)
    .RotationInDegrees(from: -30, to: 0)
    .Start(MyButton);
```

Presto's model is the opposite: `Animate()` returns a live `Animation` with `Seek`/`Reverse`/`Stretch`/`PlaybackRate`/`Then()`. So the parts worth taking are the *call shape*, not the lifecycle:

- named per-property methods with `from:`/`to:` (typed, discoverable, no magic strings)
- one axis overload per property (`Translation(Axis.X, …)` ↔ Presto's `X`/`Y`)
- per-property `delay`/`duration`/`easing` alongside a schedule-level default
- a nested keyframe builder (`.Opacity().NormalizedKeyFrames(b => b.KeyFrame(0.0, 0)…)`), which maps onto Presto's two keyframe forms
- reusable schedule applied to many targets

A Presto builder should terminate by producing the live object, not by starting a fire-and-forget:

```csharp
Presto.Build()
    .X(from: -20, to: 0)
    .Opacity(to: 1)
    .Rotate(to: 90, ease: Eases.OutBack())
    .Duration(1250)
    .Ease(Eases.OutExpo)
    .Animate(square);          // -> Animation, full playback control
```

This satisfies PLAN §7 decision 3 ("no fluent builder in v1; can be layered on later without changing the model") — it builds an `AnimationParams` and hands it to `Presto.Animate`. Note it also **solves §1 for free on the paths that go through it**, because `.Opacity(…)` writes one canonical key; the indexer would still need option 1 above.

Open question for the pass: whether the builder replaces the indexer or sits beside it. The indexer is what makes the params read like the anime.js object literal, which was a stated goal.

## 3. Compositor vs `RequestAnimationFrame`

> **Superseded** by [COMPOSITOR-PLAN.md](COMPOSITOR-PLAN.md), which re-checked this section against the 12.1.2 source and
> plans the work. Two corrections: a direct write to an *animated* composition property does remove a running server
> animation (the non-removing `SetValue` path quoted below is only used for non-animated properties), and `Opacity`
> animations survive Avalonia's per-frame sync because the generated client setter skips unchanged values, not because the
> write is harmless. `Translation`, `Scale`, `RotationAngle` and `CenterPoint` are never synchronised at all, which is
> what the plan builds on. The history below is kept for the rationale.

Today every animation ticks on the UI thread from `TopLevel.RequestAnimationFrame` and writes `AvaloniaProperty` values at `BindingPriority.Animation`. That matches PLAN §1, which lists composition-thread animation as a non-goal. What follows is what it would actually take to change that.

### What the compositor can animate

From `composition-schema.xml`, the `Animated="true"` properties of `CompositionVisual` are: `Visible`, `Opacity`, `ClipToBounds`, `Offset`, `Translation`, `Size`, `AnchorPoint`, `CenterPoint`, `RotationAngle`, `Orientation`, `Scale` (plus `TransformMatrix`, which is `Internal`). `CompositionSolidColorVisual.Color` is animatable too.

That covers Presto's transform shorthands and opacity — and nothing else. `Background`, `Margin`, `Width`/`Height`, `Thickness`, `CornerRadius`, `ProgressBar.Value` and every view-model property stay on the UI thread by necessity.

### The blocker for "write to the compositor, keep our clock"

The obvious hybrid — keep Presto's engine on the UI thread but write transform/opacity to `ElementComposition.GetElementVisual(v)` instead of to `RenderTransform`/`Opacity` — **does not work for an Avalonia-managed visual**. `Visual.Composition.cs`:

```csharp
internal virtual void SynchronizeCompositionProperties()
{
    // TODO: Introduce a dirty mask like WPF has, so we don't overwrite properties every time
    comp.Offset = new (Bounds.Left, Bounds.Top, 0);
    comp.Size = new (Bounds.Width, Bounds.Height);
    comp.Visible = IsVisible;
    comp.Opacity = (float)Opacity;
    ...
    comp.TransformMatrix = renderTransform;
}
```

`CompositingRenderer.UpdateCore()` calls this for every dirty visual on every render pass, clobbering direct writes — and only when the visual happens to be dirty, which is the worst kind of intermittent bug.

A **running composition animation does survive** that write: `ServerObject.SetValue` calls `_animations?.OnSetDirectValue(prop)`, which only invalidates subscribers and does not remove the animation. So `StartAnimation` is durable where a direct property write is not.

### What Presto would lose by offloading

A `KeyFrameAnimation` exposes `Duration`, `DelayTime`/`DelayBehavior`, `Direction`, `IterationCount`/`IterationBehavior`, `StopBehavior`. It has no scrub API. Handing an animation to the compositor therefore gives up: `Seek`, `Stretch`, mid-flight `Reverse`, `PlaybackRate` changes, `OnUpdate`/`OnRender` per-frame callbacks, replace/blend composition against other tweens, and reading the live value back for relative (`+=`) chaining. That is most of the anime.js model.

Easing does carry over: `IEasing` is `[NotClientImplementable]`, but the existing `PrestoEasing : Easing` already satisfies it, and keyframes take an `IEasing`. **Caveat:** the render thread calls `EasingFunction.Ease(progress)`, so a Presto ease would run off the UI thread. That is fine for the pure eases, but `Spring` mutates state (`Completed`, `OnComplete`) and would violate the "single-threaded by design, no locks" rule in CLAUDE.md.

### Realistic options

1. **Stay as-is (recommended default).** The tick is already allocation-free in the core and 64–160 B/frame in the bridge (Phase 8 measurements). The UI thread is the only place the full model works. Revisit if profiling shows jank that is actually caused by the animation writes rather than by layout.
2. **Compositor for a qualifying subset.** Detect animations that touch only transform/opacity, have no per-frame callbacks and are never seeked, and compile those into `KeyFrameAnimation`s. Real jank immunity for the common 300 ms fade/slide. Costs an eligibility check, a second code path, and a fallback when the user later calls `Seek`.
3. **Child visuals for effects.** `ElementComposition.SetElementChildVisual` attaches a visual Avalonia does **not** synchronise, so compositor animation there is safe and durable. This is the right foundation for future effect work (blur, shadow, acrylic), not for animating existing controls.

Option 1 today; option 3 is the natural home if compositor-backed effects are ever wanted; option 2 only if a measured jank problem justifies the second path.

## 4. Unrelated items found while investigating

- `EaseParser` accepts PascalCase (`"InOutQuint"`); upstream is case-sensitive. Flagged low-confidence in Phase 2 and kept.
- A chained **ease** (upstream passes `prevSiblingTween` into ease resolution) is unported, because `EaseValue.Resolve` takes a 3-argument `FunctionValue`. Nothing needs it today.
- `Animation.GetFunctionValue` takes the group's target list rather than a count, to let `Stagger` resolve auto-grids. Worth keeping in mind if `FunctionValue` is reshaped.

## 5. Value-driven animation: companion and follower

Phase 41 (`{presto:Follow}`, `Follow.Bind`, `Follow.*`) makes a bound property ease to each new value. The full
decisions and the spike that settled them are in [GAPS-PLAN.md §1](GAPS-PLAN.md); this is the shape in brief.

- **Input, a companion.** Avalonia 12 keeps `BindingBase.CreateInstance` and `TransitionBase.Apply` internal, so Presto
  cannot subclass a binding or a transition. Instead the wrapped binding is bound to a hidden `object`-typed attached
  property on the target control, registered once per target property (`CompanionProperties`). `DataContext`,
  `ElementName`, `RelativeSource`, `TemplatedParent` and converters therefore resolve exactly as on the real property,
  and a companion change is the "value changed" event. Untyped values are converted to the property type by the follower.
- **Output, a follower.** Each use gets an `Follower`, which runs its own core `Animatable` over an internal
  holder and exposes the displayed value as an observable. `{presto:Follow}` returns it as `ToBinding()`, and
  `Follow.Bind` binds it directly, both at `LocalValue`. Springs retarget from the displayed value with its velocity,
  per channel for colours, brushes and thicknesses. It snaps on the first value, on a new item (a `DataContext` change
  first, which is how a recycled row is recognised), while detached, under reduced motion without `WhenReduced`, and for
  values it cannot ease.
- **Layering.** The follower never registers the control's property with Presto's composition. So a
  `control.Animate(...)` of the same property runs at `Animation` priority over the followed `LocalValue`, and reverting
  it uncovers the latest followed value. `Follow.*` shorthands are not Avalonia properties, so the composers keep the
  followed value as a base layer per shorthand (`RenderTransformComposer.Follow`, `EffectComposer.Follow`) with the same
  rule: an animation overrides it while it runs, and reverting returns to the base, not the identity.
- **Limits.** One-way only (`TwoWay` and `OneWayToSource` throw), and not in a style `Setter`, which is shared by every
  control it matches; templates work because their content is built per instance.

The *Value-driven* Reference page in the sample shows each entry point.

## 6. Derived values: the `MotionValue` graph

Phase 42 (`MotionValue`, `MotionObjectValue`, the bridge sources and sinks) joins a live source to several properties
through ranges, eases and springs. The API and measurements are in [GAPS-PLAN.md §2](GAPS-PLAN.md); this is the shape in
brief.

- **Lazy, versioned evaluation.** A `Set` only bumps the source's version and marks its dependents dirty. Nothing
  computes until something reads. A dirty value compares each source's version with the one it last saw, recomputes only
  if one moved, and bumps its own version only if the result changed. So a diamond computes once per change, an
  unchanged result stops propagating, and a value read between frames is already fresh. Only observed values link into
  their sources, so a chain nobody listens to costs nothing and can be collected.
- **Sinks flush from an engine after-update list, not as tickables.** A tickable would sit in the child list beside the
  animations that feed it, so its place in that list would decide whether it saw this frame's values or last frame's.
  It would also inherit timer state (play, pause, completion) that a cell does not have. Instead sinks flush after the
  children and `Additive.Update()`, and a value set during a flush waits for the next frame.
- **`Follow.*` over a `MotionValue` ignores `Follow.Timing`.** A graph already states its smoothing (`.Spring(...)`), and
  a follower easing on top would lag a scroll- or drag-locked value and double-smooth a sprung one. So the shorthand
  writes each flushed value through the composers, keeping the decision-13 base layer. `Follow.Bind` is the one sink that
  takes options, because it is called with a value explicitly and can say so. `{presto:Follow}` smooths unless it is
  given `Duration=0`.

The *Derived values* Reference page in the sample shows one source driving five properties.

## 7. Scroll storytelling: GSAP-shaped extensions on a ported observer

Phase 45 (`ScrollObserverParams.Pin`/`PinSpacing`/`Snap`, `ScrollSnap`, `Scroll.Speed`, `Scroll.Smooth`) is a deliberate
deviation from the port: anime.js 4.5.0 has no pinning, snapping, parallax or wheel smoothing, so these follow GSAP
ScrollTrigger and ScrollSmoother instead. They sit on top of the ported `ScrollObserver` without changing its upstream
behaviour, and the ported `ScrollObserverTests` pass unchanged. Decisions are in [GAPS-PLAN.md §5](GAPS-PLAN.md) and
PLAN §7 decisions 44–48.

## 8. Small items: physics and eases from outside anime.js

Phase 49 adds GSAP-, Compose- and SwiftUI-shaped pieces with no anime.js 4.5.0 upstream, so parity does not constrain
them. Decisions are in [GAPS-PLAN.md §9](GAPS-PLAN.md) and PLAN §7 decisions 57–63; two deviations touch ported code.

- **`Projectile` rides `Inertia`'s slot in the `Animation` constructor.** It is resolved once the from-number is known,
  into an ordinary tween (or two, split at the apex) with a computed end and ease. So it composes by replace, is seeked
  and reversed, and hands over to the next `Animate` exactly as any tween does, with no per-frame simulation. It differs from
  `Inertia` in one place: its duration is read from the params before resolving, so `ReduceAll` collapses a resolved
  trajectory instead of skipping it. `Animatable` gained no projectile counterpart to `Fling`.
- **Ease strings take identifiers.** Upstream's `parseEaseString` only parses numbers. `EaseParser`'s factories now
  receive the raw argument text and its comma tokens, so `fromPath(M0,0 C…)`, `wiggle(6, anticipate)`,
  `slowMo(.5, .7, true)` and `expoScale(1, 10, outQuad)` parse. Numeric forms are unchanged, and the parser seeds the
  random eases with a fixed seed because it caches what it returns.

## 9. Idempotent sibling removal (Phase 51)

Flinging on the Draggable page could hang the UI thread. Upstream 4.5.0's `removeChild` assumes the child is in the list:
called again on an unlinked tween it sets head and tail to null, and `timer.cancel()` calls `removeTweenSliblings` on every
call. `Draggable` cancels its overshoot animations on every grab, so a blended tween left its additive siblings twice. That
orphaned the other blended tweens and left a stale tail. The next blend then appended a new lookup tween onto that tail
through its `_prev`/`_next` links, splicing the additive animation's list into another animation's, and a reverse
`ForEachChildren` walk looped forever. Upstream has the same shape and may have the same bug.

Two deviations close it, each covered by `TweenListIntegrityTests`:

- **`TweenList.RemoveChild` skips a tween that is not in the list**, that is a tween with no previous link that is not the
  head, or no next link that is not the tail.
- **A repeat cancel is a no-op for composition.** `Timer.DetachTweens` only removes the tweens' siblings while the timer is
  not already cancelled. This applies to `Cancel`, `Complete`, `Revert` and the engine's detach of completed timers.
  `Reset` and `Seek` re-compose and clear the flag as before.

`Draggable` also drops its overshoot and scroll animations once it cancels them, so later grabs do not cancel them again.
Upstream holds no references and removes the overshoot target instead (`DraggableTests` covers the fling).

In Debug builds every `TweenList` add, remove and walk checks the list it touches. A cycle, or a head or tail that
disagrees with its links, throws `InvalidOperationException` instead of hanging. The check is bounded by a tortoise-and-hare
walk, allocates nothing, and compiles out of Release.

## 10. Animations follow their controls out of the tree (Phase 60)

Decision 8 kept scoping explicit: `Animate` never auto-scoped, and only `CreateScope` reverted on detach. Phase 60
deviates from it. An animation on `Presto.Engine` now pauses by default once every visual it animates has left the
visual tree, and resumes from the same progress when one returns. Decisions are in [GAPS-2-PLAN.md §6](GAPS-2-PLAN.md)
and PLAN §7 decisions 89–91.

- **Why not `Continue`.** SwiftUI, Compose and Flutter tie an animation to the lifetime of its view, and users expect
  the same here. Under `Continue`, a loop on a dropped control kept ticking: the engine never idled, and its child list
  kept the control alive.
- **Why not `Revert`.** A detach is often brief: a reparent, a tab switch or a recycled container. Reverting writes the
  base values back and loses the progress, so the control comes back in the wrong state. Pausing can be undone, and
  reverting cannot. `Revert` stays available as opt-in auto-scoping.
- **Collectability.** A paused animation leaves the engine's list. The tracker hangs off each visual in an attached
  value, and the root-to-tracker table is weak, so nothing static holds the pair. A control detached for good is
  collected together with its paused animation.
- **Owner mapping cost.** A brush or transform the bridge creates for a visual must map back to that visual, so
  `TargetOwner` now records always, not only under diagnostics. The §6.3 spike measured 90 bytes and about 110 ns per
  created target, once per target and never per frame. That is cheap enough to keep one path.
- **Wake while hidden.** Upstream's `wake()` restarts the frame loop even while the document is hidden, which costs
  nothing in a browser that stops `requestAnimationFrame` in hidden tabs. A minimized Win32 window keeps delivering
  frames at the display rate, so `Engine.Wake` does not start the frame source while hidden. Hiding an idle engine also
  marks it paused, unlike upstream's `pause()`, so the show's `Resume` rebases timers created while hidden.

## 11. Premultiplied colour interpolation (Phase 68)

anime.js lerps each RGB channel as `sqrt(lerp(c₀², c₁², p))` with straight alpha, so a fade from a transparent colour
shows that colour's RGB at half opacity: transparent teal → gray reads green on the way. Avalonia's brush transitions
share the flaw. Presto weights each end by its alpha, as CSS Color 4 does, and deviates from upstream here (PLAN
decision 117):

`c = sqrt(lerp(a₀·c₀², a₁·c₁², p) / lerp(a₀, a₁, p))`, alpha `lerp(a₀, a₁, p)`.

- **Equal alphas** take the original expression rather than the formula, so opaque tweens and the upstream fixtures are
  unchanged to the bit, not merely within rounding.
- **Ends are exact,** and a frame whose lerped alpha is 0 or below falls back to the straight lerp, so nothing divides by
  zero.
- **Blend composition** sums straight per-channel deltas and stays straight. `BrushCodec.ColorAt` resamples gradient
  stops with the same weighting in plain gamma; colour slots in complex values stay straight until Phase 30b.
