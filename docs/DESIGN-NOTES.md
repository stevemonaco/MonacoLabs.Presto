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
