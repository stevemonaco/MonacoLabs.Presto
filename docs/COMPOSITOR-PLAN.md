# Compositor plan: running Presto on Avalonia's render thread

Scope study and implementation plan for driving animations from Avalonia's compositor instead of the UI thread. The
non-negotiable case is a busy indicator that keeps moving while the UI thread is blocked. Everything below was checked
against the Avalonia **12.1.2** source (`src/Avalonia.Base/Rendering/Composition/**`, the composition source generator
in `src/tools/DevGenerators/CompositionGenerator/`, and the per-platform render timers); paths cite that tree. A sparse
checkout of it is worth keeping next to anime.js, git-ignored like `tools/anime/`:

```powershell
git clone --depth 1 --branch 12.1.2 --filter=blob:none --sparse https://github.com/AvaloniaUI/Avalonia tools/avalonia
git -C tools/avalonia sparse-checkout set src/Avalonia.Base src/tools src/Headless samples/ControlCatalog/Pages
```

## 1. Verdict

Three things are realistic, in this order of value:

1. **Render-thread scenes** (custom visuals). A `CompositionCustomVisualHandler` is ticked by the render loop every
   frame while it asks to be, entirely independently of the UI thread. A whole Presto `Engine` can live inside one: every
   core feature (tweens, keyframes, springs, stagger, timelines, callbacks, `Seek`, composition) works unchanged because
   the targets are plain objects and the maths is pure. The user draws the scene with `ImmediateDrawingContext`. This is
   the busy indicator, and the only path where Presto's full model survives the UI thread stalling. **It needs one core
   change: the engine must stop being a process-wide singleton** (§4.1).
2. **Compositor-driven transform and opacity on existing controls.** Avalonia never synchronises `Translation`,
   `Scale`, `RotationAngle`, `CenterPoint` and `AnchorPoint` of an element's composition visual, and a server animation on
   `Opacity` survives as long as `Visual.Opacity` itself does not change. Presto can compile an animation on `X`, `Y`,
   `Rotate`, `Scale*` and `Opacity` into server-side `KeyFrameAnimation`s. It is the anime.js `waapi.animate()` story:
   a separate, reduced object (`CompositorAnimation`) with its own registry, not a flag on `Animate`. Playback control is
   emulated by recompiling from a UI-side mirror timer; per-frame callbacks and blend composition are not available.
3. **Layout transitions for free.** An `ImplicitAnimationCollection` on an element visual animates `Offset` whenever
   layout moves the element, on the render thread, with a Presto ease. That is the unported anime.js `layout/*` (FLIP)
   niche.

Not realistic: anything that is an `AvaloniaProperty` write. Layout properties, brushes and colours of controls, text,
`Draw`, `MorphTo`, split/scramble text, `Draggable`, `ScrollObserver`, view-model targets and `Blend` composition all
stay on the UI thread, by necessity rather than by effort (§3).

## 2. How the compositor behaves (verified)

### 2.1 Two threads, one batch queue, a self-sustaining render loop

- UI-thread `CompositionObject`s serialise their changed fields into a `CompositionBatch` on commit
  (`Compositor.CommitCore`); the render thread applies batches in `ServerCompositor.ApplyPendingBatches`, then runs
  `Animations.Process()`, then renders (`ServerCompositor.RenderCore`).
- After a frame the loop asks for another tick while `Animations.NeedNextTick` is true, i.e. while any server animation
  instance or any custom visual that called `RegisterForNextAnimationFrameUpdate()` is on the clock
  (`ServerCompositorAnimations`). **No UI-thread commit is required for the next frame.** This is what makes both paths
  above freeze-proof.
- `ServerCompositor.ServerNow` is a `Stopwatch` read once per frame (`UpdateServerTime`); `KeyFrameAnimation`s and
  `CompositionCustomVisualHandler.CompositionNow` both read it.

Where the render loop runs (`IRenderTimer.RunsInBackground`):

| Platform | Timer | Own thread |
|---|---|---|
| Windows | `SleepLoopRenderTimer`, or the DComp / DXGI / WinUI connections | yes |
| macOS | `ThreadProxyRenderTimer(AvaloniaNativeRenderTimer)` | yes |
| Linux X11 | `SleepLoopRenderTimer` | yes |
| Android | `ChoreographerTimer` | yes |
| iOS | `DisplayLinkTimer` | yes |
| Browser | `BrowserRenderTimer(isBackground: false)` (`BrowserSharedRenderLoop`) | **no**: single-threaded WASM; compositor animations run but get no frames while the UI thread is busy |
| Headless | `HeadlessRenderTimer` on the UI thread (`ShouldRenderOnUIThread` defaults to true) | **no**: `ForceRenderTimerTick()` runs a full compositor frame synchronously, which is what makes it testable |

Any platform that opts into `ShouldRenderOnUIThread` (Win32/X11 options) loses the guarantee too.

### 2.2 What Avalonia writes to an element's composition visual, and what survives it

`Visual.SynchronizeCompositionProperties` (`src/Avalonia.Base/Visual.Composition.cs`) runs for every dirty visual on
every render pass and assigns `Offset`, `Size`, `Visible`, `Opacity`, `ClipToBounds`, `Clip`, `OpacityMask`,
`CacheMode`, `Effect`, `RenderOptions`, `TextOptions` and `TransformMatrix` (from `RenderTransform` +
`RenderTransformOrigin`). It never touches `Translation`, `Scale`, `RotationAngle`, `Orientation`, `CenterPoint` or
`AnchorPoint`.

Two generator facts decide what a write does (`Generator.cs`):

- The **client setter has an equality guard**: assigning the value a field already holds registers nothing and clears
  nothing. So the per-frame sync of an unchanged `Opacity` is a no-op on the wire.
- On the server, an **animated property's setter is `SetAnimatedValue(prop, out field, value)`**, which removes the
  running animation for that property (`ServerObjectAnimations.RemoveAnimationForProperty`). The non-removing
  `SetValue` overload is only used for non-animated properties. Client-side, a direct set also drops the pending
  animation and re-arms implicit ones.

So the durability rule is: **a server animation lives until the UI-side value of the same property actually changes.**

| Composition property | Written by Avalonia's sync | Server animation durable | Presto use |
|---|---|---|---|
| `Translation` (Vector3D) | never | yes | `X`, `Y` |
| `Scale` (Vector3D) | never | yes | `Scale`, `ScaleX`, `ScaleY` |
| `RotationAngle` (float, **radians**) | never | yes | `Rotate` (degrees → radians) |
| `CenterPoint` (Vector3D) | never | yes | transform origin; set via an `ExpressionAnimation` on `this.Target.Size` so it follows resizes |
| `Opacity` (float) | every dirty pass, but only re-sent when `Visual.Opacity` changes | yes, while Presto owns `Visual.Opacity` | `Opacity` |
| `Offset` | every layout change | no (killed by layout) | implicit layout animation only |
| `Size`, `Visible`, clips, effects, `TransformMatrix` | yes | no | none |

The server composes `TransformMatrix`, the anchor, `Scale` and `RotationAngle` about `CenterPoint`, `Orientation`, then
`Offset + Translation` (`MatrixUtils.ComputeTransform`, `ServerCompositionVisual.ComputedProperties.cs:150`), so
compositor transforms stack on top of any `RenderTransform` rather than replacing it.

**Correction to `DESIGN-NOTES.md` §3**: it states a running composition animation survives a direct write. That holds
for the `SetValue` overload only, which animated properties do not use; the equality guard is the actual reason
`Opacity` animations survive the sync.

### 2.3 What a `KeyFrameAnimation` can and cannot do

Has: typed key frames (`Scalar`/`Double`/`Vector`/`Vector2`/`Vector3`/`Vector3D`/`Vector4`/`Quaternion`/`Color`/`Boolean`)
inserted with `InsertKeyFrame(float key, T value, IEasing)`, expression key frames (`this.StartingValue`,
`this.FinalValue`, `this.CurrentValue`, `this.Target.<Prop>`), `Duration` (≥ 1 ms), `DelayTime` + `DelayBehavior`,
`IterationCount` / `IterationBehavior.Forever`, `Direction` (`Normal`, `Reverse`, `Alternate`, `AlternateReverse`). The
implicit key frame 0 is the property's value at commit time (`KeyFrameAnimationInstance.Initialize`), so `from` may be
omitted. Each segment eases with the *right-hand* key frame's easing, the same convention as anime.js keyframes.
`IEasing` is `[NotClientImplementable]` but `Easing` is subclassable and the bridge already has `PrestoEasing : Easing`;
the render thread calls `Ease(progress)` per frame, so the delegate must be pure. Presto eases are; `Spring.Ease` is not
(it mutates `Completed`), so springs compile through `Spring.Solve` instead.

Has not: pause, seek, playback rate, reverse in flight, a completion event, `loopDelay`, per-frame value readback.
`StopBehavior` is stored but never read by `KeyFrameAnimationInstance`; a finished animation stays attached holding its
last key frame, and `StopAnimation(name)` detaches it leaving the last evaluated value in the field. `Color`
interpolates linearly in RGBA bytes, not in Presto's square-root space (only relevant to `CompositionSolidColorVisual`).

### 2.4 The custom visual handler contract

`CompositionCustomVisualHandler` (`CompositionCustomVisualHandler.cs`, `ServerCompositionCustomVisual.cs`):

- `OnMessage(object)`: messages posted from the UI thread with `CompositionCustomVisual.SendHandlerMessage`, batched
  through `Compositor.RequestCompositionUpdate` and delivered on the render thread in order, after that frame's
  `Animations.Process()`. Exceptions are caught and logged.
- `OnAnimationFrameUpdate()`: called once per render frame after `RegisterForNextAnimationFrameUpdate()`; the
  registration is one-shot and must be renewed every frame. **Exceptions here are not caught** and would take the render
  thread down, so Presto's tick must be wrapped.
- `OnRender(ImmediateDrawingContext)`: called when the visual is dirty (`Invalidate()` / `Invalidate(Rect)`), clipped to
  `Size` (`ClipToBounds` defaults to true). Exceptions are caught and logged.
- `CompositionNow` (= `ServerNow`), `EffectiveSize` (the client-set `Size`), `GetRenderBounds()`; all require the
  compositor lock, i.e. must be read inside the three callbacks above.
- `ImmediateDrawingContext` draws bitmaps, lines, rectangles (rounded, box shadows), ellipses, glyph runs
  (`IImmutableGlyphRunReference`, created on the UI thread with `GlyphRun.TryCreateImmutableGlyphRunReference`), fill
  rectangles, and pushes clips, opacity, opacity masks and transforms. Brushes must be `IImmutableBrush`, pens
  `ImmutablePen`. **No geometry/path drawing**; for paths take `ISkiaSharpApiLeaseFeature` via `TryGetFeature`, which is
  what `Avalonia.Skia.Lottie` and the ControlCatalog `CompositionPage` do. The bridge stays Skia-free; the sample may not.
- `ElementComposition.SetElementChildVisual(control, visual)` appends the visual as the **last** child of the control's
  composition visual (drawn above the control's own content and children) and nothing in Avalonia ever writes its
  properties, so both direct writes and animations on it are durable. The compositor is
  `ElementComposition.GetElementVisual(control).Compositor`, available from `OnAttachedToVisualTree` on (ControlCatalog's
  `CompositionPage` relies on the same timing).

## 3. Feasibility matrix

| Presto feature | UI thread (today) | Render-thread scene (§4.2) | Compositor animation on a control (§4.3) |
|---|---|---|---|
| Number tweens on plain objects, relative values, units | yes | **yes**, unchanged | n/a (targets are controls) |
| Keyframes (duration and percentage based), per-keyframe ease/delay | yes | yes | yes, sampled (§4.3) |
| Eases incl. `Spring`, `Steps`, `Irregular`, `Linear` stops, parser | yes | yes | yes: exact via `PrestoEasing` for single-segment tweens, sampled otherwise |
| `Modifier` | yes | yes | yes, sampled |
| Stagger, function-based values | yes | yes | yes, resolved at compile time on the UI thread |
| `Loop`, `Alternate`, `Reversed`, `Delay` | yes | yes | yes, native (`IterationCount`, `Direction`, `DelayTime`) |
| `LoopDelay` | yes | yes | yes via hold key frames (two-iteration expansion when alternating) |
| Timelines, labels, `Call`, `Sync` | yes | yes | no in v1 (compile each child separately later) |
| `Seek`, `Reverse`, `Stretch`, `PlaybackRate`, `Progress` setter | yes | yes | emulated: stop, write exact value, recompile the remainder (§4.3 step 6); ≤ 1 frame of skew |
| `Pause`/`Resume` | yes | yes | emulated the same way |
| `OnBegin`/`OnLoop`/`OnComplete`, `Then()`/`await` | yes | yes, on the render thread | yes, from the UI-side mirror timer (late while the UI thread is blocked) |
| `OnUpdate`/`OnRender`/`OnBeforeUpdate` per frame | yes | yes, on the render thread | **no** |
| `Composition.Replace` | yes | yes | yes, within compositor animations only (own registry, as upstream `waapi`) |
| `Composition.Blend` (additive) | yes | yes | **no** |
| `Animatable`, `Scope`, `Presto.Get/Set/Remove` | yes | yes | `Remove`/`Revert`; `Get` returns the compile-time value, not the live server value |
| `X`, `Y`, `Rotate`, `Scale`, `ScaleX`, `ScaleY` on controls | yes | n/a | **yes** |
| `SkewX`, `SkewY` on controls | yes | n/a | no (no server property) |
| `Opacity` on controls | yes | n/a | **yes**, Presto owns `Visual.Opacity` for the duration |
| Colours, brushes, `Margin`, `Width`/`Height`, `Thickness`, `CornerRadius`, any other `AvaloniaProperty` | yes | in a scene, if the scene draws them itself | no |
| `Draw` (stroke dash), `MorphTo`, split/scramble text | yes | a scene can draw the equivalent with Skia | no |
| `Draggable`, `ScrollObserver` | yes | no (pointer and scroll are UI-thread) | no |
| View-model / INotifyPropertyChanged targets | yes | only render-thread-owned objects | no |
| Survives a blocked UI thread | no | **yes** (except Browser, §2.1) | **yes** (except Browser) |

## 4. Design

### 4.1 Core: engine instancing (prerequisite)

Today the engine is `Presto.Engine`, a static singleton, and the core leans on process-wide state. A second engine on
the render thread needs the following, all mechanical and small (10 `Presto.Engine` references: 7 in `Timer.cs`, 1 in
`Renderer.cs`, 2 in `Engine.cs`; 18 `Globals.Defaults`/`Globals.CurrentScope` references):

- `Engine` gets a public constructor (`new Engine(IFrameSource)`) and an ambient accessor: `[ThreadStatic]` backing
  field, `Engine.Current` falling back to `Presto.Engine`, and `IDisposable Engine.MakeCurrent()` to install one for a
  block. Every `Timer` captures `Engine.Current` at construction into `Clock.Engine` and uses it afterwards (`Resume`,
  `ResetTime`, `Renderer.Tick`'s `tlChildrenTickTime`), so a UI-thread call on a render-thread timer never resolves to
  the wrong clock. `Presto.Animate` and friends need no new overloads: the ambient engine is set by whoever owns the
  thread, exactly as `Scope.Execute` installs defaults.
- Per-engine state, moved off `Globals`: `Defaults` (`Presto.Defaults` becomes `Presto.Engine.Defaults`; a scene engine
  starts with a copy of the main engine's defaults, the same snapshot rule `Scope` already applies), `CurrentScope`,
  the additive lookup table and additive animation (`Additive`, `Composer.AdditiveLookups`). `Composer`'s replace lookups
  are a `ConditionalWeakTable` keyed by target and can stay static.
- Stays process-wide, documented as "configure before creating scenes": `Globals.TimeScale`, `Precision`,
  `TickThreshold` (21 references, all reads on hot or construction paths). `Engine.TimeUnit` remains a setter on the main
  engine only.
- Thread safety of statics that a second thread would now hit: `Animation.s_fromValue/s_toValue/s_originalValue` and
  `Presto.s_getValue` scratch `DecomposedValue`s become `[ThreadStatic]`; `Timer.s_timerId`, `Animation.s_animationId`,
  `Tween.s_tweenId`, `Utils.s_seed` use `Interlocked`; `ObjectTargetAdapter.s_accessors` and `EaseParser._cache` get a
  lock or `ConcurrentDictionary`. `Presto.Adapters`/`Presto.Codecs` are read on the render thread and written only by
  `EnsureInitialized`; documented, not locked. `Spring` instances stay single-threaded (one animation each), as today.
- `Engine.PinThread` already asserts affinity per engine; it now also catches UI code touching a scene's timers.

Tests: every existing core suite runs unchanged against `Presto.Engine`. New `EnginesTests`: two engines with manual
frame sources tick independently; `MakeCurrent` restores on dispose; blend/additive state does not leak across engines;
a timer created under a scene engine ignores `Presto.Engine` when resumed from another thread. `EngineTestBase` can
later switch to a fresh engine per test instead of resetting the singleton.

### 4.2 Bridge: render-thread scenes (`Monaco.Presto.AvaloniaBridge.Compositor` namespace)

```
CompositorCanvas : Control          UI thread. Owns a CompositionCustomVisual set as its element child visual,
                                    keeps visual.Size = Bounds.Size, posts messages, tears down on detach.
CompositorScene (abstract)          Render thread after attach. OnAttached() builds Presto objects, OnRender() draws,
                                    OnMessage() reacts to the UI thread. Has Engine, Scope, Now, Size, Invalidate().
CompositorSceneHandler (internal)   CompositionCustomVisualHandler + IFrameSource glue.
```

Mechanics:

1. `CompositorCanvas.OnAttachedToVisualTree`: `compositor = ElementComposition.GetElementVisual(this)!.Compositor`,
   `visual = compositor.CreateCustomVisual(handler)`, `SetElementChildVisual(this, visual)`, send `Attach(scene)`.
   `Bounds` changes push `visual.Size`. Detach sends `Detach`, sets the child visual to null and drops the visual.
2. The handler implements `IFrameSource`: `Now => CompositionNow.TotalMilliseconds`, `Start` →
   `RegisterForNextAnimationFrameUpdate()`, `Stop` → stop renewing. `OnAnimationFrameUpdate` runs
   `using engine.MakeCurrent()` → `onFrame()` (i.e. `Engine.Update`) inside a try/catch that logs, then renews the
   registration only while the engine still has children, so an idle scene costs nothing and the render loop can sleep,
   mirroring the UI path's `AvaloniaFrameSource`. After a frame in which the engine ticked, `Invalidate()` once.
3. `OnMessage(Attach)`: create `new Engine(this)` with a copy of `Presto.Engine.Defaults`, a `Scope`, then
   `scope.Execute(_ => scene.OnAttached())` under `MakeCurrent`, so `Presto.Animate(...)`, `Presto.CreateTimeline(...)`,
   staggers and springs inside `OnAttached` register with the scene engine untouched. `OnMessage(Detach)`:
   `scope.Revert()`, `engine.Pause()`, drop the scene. Other messages: `Action<TScene>` delegates from
   `canvas.Invoke(s => …)` and arbitrary objects from `canvas.Post(o)` to `scene.OnMessage`, also under `MakeCurrent`.
4. `OnRender(ctx)`: `scene.OnRender(ctx, EffectiveSize)`. The scene's animated state is plain fields; brushes and pens
   are immutable objects the scene caches. Paths are the user's business through the Skia lease feature.
5. Scene → UI: `Dispatcher.UIThread.Post`, exposed as `scene.PostToUI(Action)`. `Then()` on a scene timer completes on
   the thread pool (`RunContinuationsAsynchronously`, no sync context on the render thread); documented.
6. Rules, enforced in Debug by the engine's thread pin and a matching pin on `CompositorScene`: after `Attach` the scene
   belongs to the render thread; the UI thread talks to it only through `Post`/`Invoke`. Callbacks (`OnUpdate` etc.)
   run on the render thread and must not touch controls. A scene set in XAML (`<presto:CompositorCanvas.Scene>`) is
   constructed on the UI thread and handed over untouched at attach, which is safe because the batch queue publishes it.

Allocation budget: the scene tick is the core tick, already allocation-free; `Invalidate()` per frame is a flag. Add the
scene case to the bridge `AllocationTests` (headless, Skia).

Sample page "Compositor": a ring of dots with a centre-out opacity stagger looping forever (the anime.js "staggering"
example rendered as a busy indicator) beside the identical animation on the UI-thread path, a `PathIcon` spun through
§4.3, a `ProgressBar IsIndeterminate` for reference, and a **"Freeze UI thread for 2 s"** button (`Thread.Sleep` on the
dispatcher). Expected: the compositor items keep moving, the UI-thread ones stop — including the indeterminate
`ProgressBar`, whose Fluent-theme animation runs on the UI-thread clock (measured in Phase 10: zero changed pixels
across the freeze), so it is the reference for what freezing looks like, not a second survivor.

Tests (`tests/Monaco.Presto.AvaloniaBridge.Tests`, headless + Skia, `ForceRenderTimerTick()` drives frames
synchronously): attach creates the child visual and runs `OnAttached` with `Engine.Current` being the scene engine;
values advance across ticks (real `ServerNow`, so assert monotonic progress, not exact times); `Invalidate` changes the
pixels of `GetLastRenderedFrame`; messages arrive in order; a throwing `OnAttached`/`OnMessage` does not stop later
frames; detach reverts the scope and the engine has no children; the frame source stops renewing when idle.

### 4.3 Bridge: compositor animations for controls (`CompositorAnimation`, the `waapi.animate` analogue)

Entry point `PrestoAvalonia.AnimateOnCompositor(targets, CompositorAnimationParams)` (or `control.AnimateOnCompositor(…)`).
`CompositorAnimationParams` accepts the same indexer and the anime.js WAAPI parameter set: per-property `To`/`From`/
keyframes/`Duration`/`Delay`/`Ease`, plus `Duration`, `Delay`, `Ease`, `Loop`, `LoopDelay`, `Alternate`, `Reversed`,
`Autoplay`, `PlaybackRate`, `OnBegin`, `OnComplete`; function values and staggers everywhere upstream allows them.
Supported properties: `X`, `Y`, `Rotate`, `Scale`, `ScaleX`, `ScaleY`, `Opacity` (typed `Visual.OpacityProperty` too);
anything else throws `NotSupportedException` naming the list, since a silent UI-thread fallback would defeat the
purpose.

Compile, per target:

1. Build a **mirror**: a normal Presto `Animation` on a private scratch object with one `double` per shorthand, using
   the same params with `Autoplay = false`. It never touches the control; it is the sampler and, later, the playback
   model (its `Seek` is exact anime.js maths, so keyframes, per-keyframe eases, modifiers, springs and staggers all fall
   out for free).
2. **Sample** one iteration of the mirror (`Seek(t)` for `t` over `[0, IterationDuration]`, default 100 samples per
   iteration like upstream's `easingToLinear`, capped; `LoopDelay` appends hold samples, and an alternating loop with a
   loop delay samples two iterations) into a `Vector3DKeyFrameAnimation` for `Translation` (`X`, `Y`), one for `Scale`
   (`Scale*ScaleX`, `Scale*ScaleY`, 1), a `ScalarKeyFrameAnimation` for `RotationAngle` (radians) and one for `Opacity`,
   each key frame `LinearEasing`. Optimisation for the common case: a property with a single two-value tween and no
   modifier compiles to one segment with `PrestoEasing(tween ease)` (springs via a pure `Solve` wrapper), exact.
3. Map `Loop` → `IterationCount`/`Forever`, `Alternate`/`Reversed` → `Direction`, `Delay` → `DelayTime` with
   `SetInitialValueBeforeDelay` (anime.js renders `from` at creation). Set `CenterPoint` once with an
   `ExpressionAnimation("Vector3(this.Target.Size.X*0.5, this.Target.Size.Y*0.5, 0)")` so rotation and scale follow the
   control's size, matching `RenderTransformOrigin = Center`. `StartAnimation` for each property.
4. For `Opacity`, bind `Visual.Opacity` at `BindingPriority.Animation` to its *current* value first, so no other writer
   changes it mid-flight (a change would remove the server animation), and on completion push the end value through that
   binding, which replaces the finished server animation with the same number. `Revert` disposes the binding. Transforms
   have no UI-side property: completion leaves the finished instance holding the end value; `Revert`/`Cancel` calls
   `StopAnimation` and writes identity (`Translation = 0`, `Scale = 1`, `RotationAngle = 0`).
5. Register in a compositor-only registry keyed by (element visual, composition property) with replace semantics, as
   `waapi/composition.js` does; a new compositor animation on the same property cancels the old one, and
   `Presto.Remove(control)` reaches it through the same hook. Mixing a UI-thread shorthand and a compositor animation on
   one control is unsupported (they compose multiplicatively on the server) and gets a Debug warning.
6. Playback control by recompilation: the mirror is `Resume`d in step with `StartAnimation`, so `CurrentTime`,
   `Progress`, `Began`, `Completed`, `OnBegin`/`OnLoop`/`OnComplete` and `Then()` come from it (late while the UI thread
   is blocked, correct afterwards). `Pause` stops the server animations and writes the mirror's exact current values as
   direct properties. `Resume`/`Seek`/`Reverse`/`PlaybackRate`: re-sample from the mirror's position to the end for
   finite animations, or rotate the sampled cycle so key frame 0 is the current phase for infinite ones, and start
   again. The one-frame skew between the mirror (resumed now) and the server (started at commit) is bounded by a frame
   and snaps on the next control operation; acceptable, documented. Deliver 6 as a second step; steps 1–5 with only
   `Cancel`/`Revert`/`Then` are already the busy-indicator-on-a-control case and are what `RefreshVisualizer` does today.

Tests (headless + Skia, pixel assertions on `GetLastRenderedFrame`): a red `Border` fading through the compositor loses
alpha across ticks; calling `InvalidateVisual()` on it mid-way (forces `SynchronizeCompositionProperties`) does not
stop the fade, which is the test that pins the Avalonia semantics §2.2 relies on; `X` moves the pixels; `Rotate` rotates
about the centre after a resize; `Cancel` freezes, `Revert` restores; `Then()` completes; two compositor animations on
one property replace; an unsupported property throws.

### 4.4 Bridge: layout transitions via implicit animations

`PrestoAvalonia.AnimateLayout(control, duration, ease)` sets `ImplicitAnimations["Offset"]` on the element visual
(`Vector3DKeyFrameAnimation`, `this.FinalValue`, `PrestoEasing`), optionally `Size` too. Every layout move then eases on
the render thread with no per-frame UI work: reordering items, expanding panels. It is the closest Presto can get to the
unported anime.js `layout` module and it is a dozen lines once §4.3's easing adapter exists. Caveat: `Offset` is
re-sent only on real change (§2.2), so the implicit animation retargets exactly once per layout change.

## 5. Public API sketch

```csharp
// A busy indicator that survives a blocked UI thread: the animation runs on the render thread.
public sealed class SpinnerScene : CompositorScene
{
    private readonly Dot[] _dots = Enumerable.Range(0, 12).Select(_ => new Dot()).ToArray();

    protected override void OnAttached() =>
        Presto.Animate(_dots, new()                       // registers with this scene's engine
        {
            ["Opacity"] = new double[] { 1, 0.15 },
            Duration = 900,
            Delay = Presto.Stagger(75),
            Ease = Eases.InOutSine,
            Loop = true,
        });

    protected override void OnRender(ImmediateDrawingContext ctx, Size size)
    {
        for (var i = 0; i < _dots.Length; i++)
        {
            var angle = i * Math.Tau / _dots.Length;
            var p = new Point(size.Width / 2 + Math.Cos(angle) * 18, size.Height / 2 + Math.Sin(angle) * 18);
            ctx.DrawEllipse(Brush(Colors.SteelBlue, _dots[i].Opacity), null, p, 3, 3);   // cached immutable brushes
        }
    }

    private sealed class Dot { public double Opacity { get; set; } = 1; }
}

// XAML: <presto:CompositorCanvas Width="48" Height="48"><presto:CompositorCanvas.Scene><local:SpinnerScene/></...>
var spinner = new CompositorCanvas { Width = 48, Height = 48, Scene = new SpinnerScene() };
spinner.Invoke<SpinnerScene>(s => s.Engine.Speed = 2);     // marshalled to the render thread

// An existing control animated by the compositor (anime.js waapi.animate): reduced model, freeze-proof.
var spin = icon.AnimateOnCompositor(new() { ["Rotate"] = 360, Duration = 1200, Ease = Eases.None, Loop = true });
await busyWork;
spin.Revert();

// Layout moves ease on the render thread.
PrestoAvalonia.AnimateLayout(listItem, duration: 300, ease: Eases.OutQuint);
```

## 6. Phases

Checklist entries live in `docs/PLAN.md` §5 (Phases 9–12) so `implement-plan` can pick them up; details are here.

- [ ] **Phase 9 — Engine instancing (core).** §4.1: `Engine` constructor, `Engine.Current`/`MakeCurrent`,
  `Clock.Engine`, per-engine `Defaults`/`CurrentScope`/additive state, thread-safe statics, thread-static scratch,
  `Interlocked` ids. `Presto.Defaults` forwards to the main engine. All existing tests green; new `EnginesTests`.
  Bridge: `Draggable`, `ScrollContainer`, `ScrollObserver`, `TextScrambler`, `TextSplitter` switch their `Presto.Engine`
  reads to `Engine.Current` (they stay UI-thread objects). Docs: PLAN §3.1 thread model paragraph.
- [ ] **Phase 10 — Compositor scenes (bridge + sample).** §4.2: `CompositorCanvas`, `CompositorScene`,
  `CompositorSceneHandler`, brush/pen caching helper, sample page with the freeze button, headless tests including the
  allocation budget. README: a "Render thread" section and the Platforms table row for Browser.
- [ ] **Phase 11a — Compositor animations, fire-and-forget (bridge).** §4.3 steps 1–5: params, mirror + sampler,
  property mapping, `CenterPoint` expression, `Opacity` ownership, registry, `Cancel`/`Revert`/`Then`, pixel tests.
  README: anime.js → Presto table row for `waapi.animate`.
- [ ] **Phase 11b — Compositor animations, playback control (bridge).** §4.3 step 6: `Pause`/`Resume`/`Seek`/
  `Reverse`/`PlaybackRate` by recompilation, sample controls page variant, tests for phase rotation of infinite loops.
- [ ] **Phase 12 — Layout transitions (bridge).** §4.4: `AnimateLayout`, sample (reorderable list), test that a
  `Margin` change eases the rendered position across ticks.

## 7. Decisions and open questions

Decided here, to be lifted into PLAN §7 when the phases land:

1. Scenes get their own `Engine`; the UI thread never ticks a scene and the render thread never writes an
   `AvaloniaProperty`. Ambient `Engine.Current` rather than an `engine` parameter on every factory, so the anime.js call
   shape survives.
2. Compositor animations on controls are a separate type with a reduced surface, as upstream's `waapi` is, and share
   no registry with UI-thread animations. Unsupported properties throw instead of falling back.
3. Compile by sampling the Presto mirror (upstream's `linear()` approach), exact single-segment easing as an
   optimisation. `RotationAngle` is radians on the wire; Presto keeps degrees at the API.
4. `Opacity` is owned through an `Animation`-priority binding for the life of the compositor animation.
5. Browser is documented as "runs but does not survive a blocked UI thread"; nothing to build there.

Open:

- Naming: `Compositor*` (Avalonia's term) vs `RenderThread*` (what users care about). `Composition*` is out because
  the core already has `Composition` (replace/none/blend).
- Should `CompositorCanvas` also draw a UI-thread placeholder for the previewer / design mode?
- Whether §4.3 should also cover `CompositionSolidColorVisual.Color` for solid backgrounds, by inserting a child solid
  visual under a `Border`. Cheap, but it draws above the control's content; probably not.
- Skew between mirror and server clocks in §4.3 step 6: measure on Windows and macOS before deciding whether to
  compensate with `CompositionBatch.Processed`.
- The semantics in §2.2 are Avalonia internals (equality guard, `SetAnimatedValue(out)`, `Offset + Translation`). The
  pixel test in §4.3 guards them; bumping `AvaloniaVersion` in `Directory.Packages.props` must re-run it.
