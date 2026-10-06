# Prebuilts, round 2 — particles, sprite sheets and per-line text reveals

Phases 36–38 in [PLAN.md](PLAN.md). Status: Phases 36, 37 and 38 **implemented** (§5 lists where they depart from §1–3).
This plan follows the post-Phase-33 gap review. Three pieces of everyday motion still have to be
hand-built:

- A confetti or sparkle burst means writing a `CompositorScene` from scratch. The sample's `ParticleFieldScene` is a
  field, not an emitter: 5 000 long-lived particles, each a Presto animation target.
- Frame-by-frame animation (a sprite sheet, an exported image sequence) has no control. `Eases.Steps` exists, but
  nothing turns a frame number into the right sub-rectangle of a bitmap.
- Text reveals by line, word or character are written per page: `TypographyPage` staggers words by line through
  `TextSplitter.LineIndex` and `LineHeight`, and `Recipes.Typewriter` is the only split recipe (PLAN §8 "per-line reveals
  were not built").

| Phase | Scope | Size |
|---|---|---|
| 36 | Particles: a core `ParticleSystem`, a bridge `ParticleScene` on the render thread, `Recipes.Confetti`/`Sparkles`/`Burst` anchored to a control | large, **implemented** |
| 37 | Sprite sheets and image sequences: `SpriteSheet` and `ImageSequence` controls with an animatable `Frame`, `Play(fps)` and a `{presto:PlayFrames}` recipe | small, **implemented** |
| 38 | Per-line text reveals: piece retargeting for `TextBlock`s (`TextReveal.Pieces`), line/word/char recipes, masked and wiped line reveals | medium, **implemented** |

The three are independent; 37 is the cheapest and can go first.

## 1. Phase 36 — Particles

### 1.1 Decisions

1. **Emitted particles are simulated, not tweened.** The field sample gives each particle Presto tweens, which suits
   a fixed population but allocates an `Animation` and its tweens per spawn. An emitter spawns and retires particles
   every frame, so its particles are integrated directly: semi-implicit Euler for velocity and gravity, exponential drag
   (`v *= e^(−drag·dt)`, frame-rate independent), linear spin. Presto supplies what it is good at: every over-life
   curve (size, opacity, color, spin rate) is a `from → to` pair with an `EaseValue`, sampled at the particle's
   normalized age.
2. **The simulation lives in the core, the drawing in the bridge.** `ParticleSystem` and `EmitterParams` have no
   Avalonia dependency (colors are `PrestoColor`), so they are unit-testable with plain `Step(dt)` calls and usable by
   a custom renderer, per PLAN §1. Public core types sit in the root `Monaco.Presto` namespace, as `Spring` and
   `VelocityTracker` do, so one `using` still suffices.
3. **Pooled, allocation-free.** A system owns one preallocated `ParticleState[]` (a struct array) sized to its cap;
   spawning takes a dead slot, dying swaps with the last live slot. Steady-state stepping and drawing allocate nothing,
   pinned by `AllocationTests` in both test projects.
4. **Deterministic.** Every random draw goes through a `RandomGenerator` from `Utils.CreateSeededRandom(Seed)`; a null
   `Seed` draws a fresh seed. Two systems with the same seed and the same `dt` sequence produce identical states.
5. **Drawn on the render thread.** `ParticleScene` is a public sealed `CompositorScene` holding any number of systems,
   stepped by a `Timer` on the scene engine (decision 12) with `Engine.DeltaTime`, as `ParticleFieldScene` is.
   `ImmediateDrawingContext` has rectangles, ellipses, lines, glyph runs and bitmaps but no geometry, so the shapes
   are `Rect`, `RoundedRect`, `Circle`, `Streak` (a line along the velocity), `Glyph` (through `SceneText`) and
   `Bitmap`. An arbitrary path is rasterized once on the UI thread (`ParticleShape.FromGeometry(geometry, size)`) and
   drawn as a bitmap. Rotation and confetti flutter (a scale on one axis following `cos(phase)`) go through
   `PushPreTransform`, which returns a struct.
6. **Colors stay in the brush cache.** `CompositorScene.Brush` keys on the exact color and quantizes only opacity
   (clearing at 1 024 brushes, SAMPLES-PLAN §8 row 9). A palette is drawn from as-is; a color-over-life curve is
   quantized to 4 bits per channel, as the field sample's `Quantize` does, so its brushes are reused.
7. **One-call helpers use a shared overlay canvas.** `Recipes.Confetti(anchor)`, `Sparkles(anchor)` and
   `Burst(anchor)` measure the anchor on the UI thread and post an emitter to one `CompositorCanvas` per `TopLevel`,
   placed in its `OverlayLayer` (as exit ghosts are) with `IsHitTestVisible = false` and sized to the top level. The
   canvas is created on first use and removed once its scene has had no live particle for a second. Each helper
   returns a `ParticleEffect` handle: `Stop()` (stop emitting, let the live ones finish), `Cancel()` (clear now),
   `Then()`/awaitable (resolves when the last particle dies).
8. **Caps.** `EmitterParams.MaxParticles` (default 500) bounds one system; `ParticleScene.MaxLive` bounds a scene,
   defaulting to 4 000 on desktop and 1 000 where `OperatingSystem.IsBrowser()`, `IsAndroid()` or `IsIOS()`. These are
   starting points from the field sample's measurements (60 fps to ~7 500 drifting particles on the dev machine, lower
   on Browser), confirmed in the implementation's own measurement. Spawns over a cap are dropped, not queued.
9. **Reduced motion emits nothing.** Under `PrestoDefaults.ReducedMotion` the recipes spawn no particles and resolve
   `Then()` at once; each takes an optional `reduced` builder played on the anchor instead (for example
   `Recipes.Pulse()`'s reduced form). `ReduceAll` does not affect a hand-built `ParticleScene`, which is the app's own
   drawing, as `Presto.Set` and plain timers are exempt (decision 36).
10. **Not seekable.** A simulation stepped by `dt` cannot be sought or reversed like a timeline. `ParticleEffect`
    offers pause and resume through its timer, and a seeded burst can be replayed from the start.

### 1.2 API

```csharp
// Core
public sealed record EmitterParams
{
    public double Rate { get; init; }                 // particles per second while emitting; 0 = burst only
    public int Burst { get; init; }                   // spawned at once when started
    public double Duration { get; init; }             // ms of continuous emission; infinite when Rate > 0 and unset
    public (double Min, double Max) Lifetime { get; init; } = (800, 1400);
    public (double Min, double Max) Speed { get; init; } = (0.2, 0.6);   // units per ms
    public double Angle { get; init; } = -90;         // degrees, 0 = right, -90 = up
    public double Spread { get; init; } = 60;         // degrees either side of Angle
    public double Gravity { get; init; }              // units per ms², +down
    public double Drag { get; init; }                 // per second
    public (double Min, double Max) Spin { get; init; }   // degrees per ms
    public ParticleCurve Size { get; init; }          // from, to, ease over life
    public ParticleCurve Opacity { get; init; }
    public IReadOnlyList<PrestoColor> Colors { get; init; }
    public ParticleCurve? ColorMix { get; init; }     // lerp from a particle's color toward EndColor
    public PrestoColor? EndColor { get; init; }
    public double Flutter { get; init; }              // confetti flip rate, 0 = none
    public int MaxParticles { get; init; } = 500;
    public int? Seed { get; init; }
    public object? Shape { get; init; }               // resolved by the renderer; the bridge takes ParticleShape
}

public sealed class ParticleSystem   // Step(double dt), Start(), Stop(), Clear(), Live, IsFinished, ReadOnlySpan<ParticleState> Particles
```

```csharp
// Bridge
public sealed class ParticleScene : CompositorScene   // Add(EmitterParams, Point origin), MaxLive
public sealed record ParticleShape                    // Rect, RoundedRect(radius), Circle, Streak, Glyph(char), Bitmap(IImage), FromGeometry(...)
public sealed class ParticleEffect : IRevertible      // Stop, Cancel, Pause, Resume, Then, GetAwaiter

Recipes.Confetti(Control anchor, int count = 120, IReadOnlyList<Color>? colors = null, Edge? from = null, AnimationBuilder? reduced = null)
Recipes.Sparkles(Control anchor, double rate = 30, Color? color = null, AnimationBuilder? reduced = null)   // continuous until Stop()
Recipes.Burst(Control anchor, int count = 24, Color? color = null, AnimationBuilder? reduced = null)
```

`Confetti` defaults to rectangles and streaks in the accent palette with flutter, spin and gravity, launched upward
from the anchor's center (or from an `Edge`, e.g. the bottom of the window). `Sparkles` emits small glyph or circle
particles around the anchor's bounds with no gravity and a quick fade. `Burst` is a radial ring of circles for a
press or a like.

### 1.3 Implementation steps

1. **`ParticleSystem` (core).** Struct state, pool, seeded draws, integration, over-life curves, `Step`/`Start`/
   `Stop`/`Clear`. Pure unit tests.
2. **`ParticleScene` (bridge).** Timer-driven stepping, per-shape drawing, brush quantization, caps, a readout-free
   render path. Headless pixel and allocation tests on `CompositorCanvas`.
3. **Overlay host and recipes.** Shared per-`TopLevel` canvas, anchor measurement, `ParticleEffect`, `Confetti`/
   `Sparkles`/`Burst` with reduced forms; README rows.
4. **Sample.** See §1.5; `ParticleFieldScene` is left as it is (it is a field, and it demonstrates tweened particles).

### 1.4 Tests (`ParticleSystemTests`, `ParticleSceneTests`, `ParticleRecipeTests`)

- A burst of N spawns N and a system never exceeds `MaxParticles`; a scene never exceeds `MaxLive`.
- Same seed, same `dt` sequence → identical states; different seeds differ.
- With no gravity or drag a particle travels `speed · t` along its angle; gravity gives the closed-form parabola within
  1e-6 at 1 ms steps; drag halves speed after `ln 2 / drag` seconds regardless of step size (60 vs 144 steps per
  second agree within 1%).
- Over-life curves: size and opacity at age 0, 0.5 and 1 match `from`, the eased midpoint and `to`.
- A particle dies at its lifetime and its slot is reused; `IsFinished` once emission has stopped and none is live.
- `AllocationTests`: stepping 500 live particles allocates 0 bytes; drawing a scene of 500 allocates 0 bytes after the
  brush cache has warmed.
- Pixel test: a burst renders non-background pixels around the origin and none once finished.
- `Recipes.Confetti` places its origin at the anchor's center in top-level coordinates, removes the overlay canvas
  after the idle period, and under `ReducedMotion` spawns nothing, plays `reduced` and completes `Then()` at once.

### 1.5 Samples

- **New Reference page "Particles":** an emitter playground (rate, burst, lifetime, speed, spread, gravity, drag,
  spin, flutter, shape, palette) with the `EmitterParams` it corresponds to printed, and a live-count readout.
- **Micro-interactions:** the like button bursts on its press-in; **Submit flow:** confetti on success;
  **Storefront:** a small burst from the cart badge when an item lands.

## 2. Phase 37 — Sprite sheets and image sequences

### 2.1 Decisions

1. **Two controls with one frame model.** `SpriteSheet` takes one `Source` bitmap cut into a grid (`Columns`, `Rows`,
   optional `FrameCount` when the last row is partial, optional `FrameSize` when the grid has margins).
   `ImageSequence` takes `Frames`, a list of `IImage`. Both expose the same `Frame` property and `Play` method,
   through a shared internal helper rather than a base class.
2. **`Frame` is a `double` frame position, not an `int`.** The bridge rounds a number written to an `int` property
   (`AvaloniaObjectTargetAdapter`), which would give the first and last frames half the time of the others. The
   control instead shows `min(floor(Frame), FrameCount − 1)`, so a plain linear tween from 0 to `FrameCount` gives
   every frame equal time with no `Steps` ease, and `Seek`, `Reverse`, loops and timelines just work. `CurrentFrame`
   is a read-only `int` for bindings. `Wrap = true` shows `floor(Frame) mod FrameCount`, for a tween that runs past
   the end.
3. **Rendered from a source rect, no per-frame allocation.** `SpriteSheet` derives from `Image` (inheriting `Source`,
   `Stretch`, `StretchDirection`), measures and arranges to one frame's size, and draws with
   `DrawingContext.DrawImage(source, sourceRect, destRect)`. No `CroppedBitmap` is created per frame.
4. **`Play` is a thin wrapper.** `sheet.Play(fps, loop, reverse)` returns the `Animation` tweening `Frame` from 0 to
   `FrameCount` over `FrameCount · 1000 / fps` ms, linear. The recipe `Recipes.PlayFrames(fps, loop)` is the same as a
   builder, with `To` and `Duration` as function values reading the target's `FrameCount`, so it works from
   `Animate.OnAttached="{presto:PlayFrames Fps=24}"`.
5. **Decoding is eager and the caller's.** A `Bitmap` decodes on construction; a sequence of N frames of w × h costs
   `N · w · h · 4` bytes. `ImageSequence.FromAssets("avares://…/walk_{0:000}.png", count)` loads them up front on the
   calling thread and the XML docs state the memory cost. Lazy or streamed decoding is out of scope.
6. **Relation to Phase 28 is deferred.** `FilteredImage` is not built yet. If it lands first, `SpriteSheet` should
   derive from it so filters apply to frames, which only needs `FilteredImage` to draw a source rect; that choice is
   made when the second of the two phases starts. `SpriteSheet` keeps its frame logic in the source-rect computation so
   swapping the base class touches nothing else.
7. **Reduced motion** needs no special case: `PlayFrames`'s reduced alternative shows frame 0 still, and `ReduceAll`
   lands a hand-built tween on its last frame as it does any animation.

### 2.2 API

```csharp
public class SpriteSheet : Image      // Columns, Rows, FrameCount?, FrameSize?, Frame (double), CurrentFrame, Wrap, Play(...)
public class ImageSequence : Control  // Frames, Stretch, StretchDirection, Frame, CurrentFrame, Wrap, Play(...), FromAssets(...)
Recipes.PlayFrames(double fps = 24, bool loop = true, bool reverse = false)
```

```xml
<presto:SpriteSheet Source="/Assets/Sprites/run.png" Columns="8" Rows="2"
                    presto:Animate.OnAttached="{presto:PlayFrames Fps=12}" />
```

### 2.3 Tests (`SpriteSheetTests`, `ImageSequenceTests`)

- Frame 0..N−1 draws the matching grid cell (pixel test on a sheet whose cells are solid, distinct colors); a partial
  last row honors `FrameCount`; `FrameSize` with margins picks the right rects.
- A linear tween 0 → N at N frames gives every frame the same number of ticks (±1) on a `ManualFrameSource`, and the
  final frame is N−1; `Wrap` shows frame 0 at N.
- `Play(fps)` duration is `N · 1000 / fps`; `Reverse` and `Seek` show the expected frames; `Revert` restores `Frame`.
- `{presto:PlayFrames}` from XAML resolves `FrameCount` per target, for two sheets of different lengths.
- Measure returns one frame's size; `Stretch` applies to the frame, not the sheet.
- `AllocationTests`: a playing sheet holds the `double`-property budget.

### 2.4 Samples

- **New Reference page "Frames":** a sprite-sheet character with fps and direction controls and a scrub slider bound
  to `Frame`, and an image sequence (a short exported loop) played by a timeline alongside a text caption.
- **Assets:** one CC0 sprite sheet and one short PNG sequence under `samples/PrestoSample/Assets/Sprites/`, with source
  and license in a `README.md` beside them.

## 3. Phase 38 — Per-line text reveals

### 3.1 Decisions

1. **Piece retargeting makes every builder a text reveal.** A new bridge attached property, `TextReveal.Pieces`
   (`None`, `Lines`, `Words`, `Chars`) on a `TextBlock`, tells the triggers to play their builder on the block's split
   pieces instead of the block: `Presence.Initial`/`Enter`/`Arrive`/`Reveal`, `Animate.OnAttached` and `OnTapped`.
   So `{presto:FadeUp Stagger=True}` on a block with `TextReveal.Pieces="Words"` rises word by word, and the existing
   recipe catalog needs no text variants. `Motion` states are excluded: their per-property rest (decision 26) is
   defined on the element, and pieces come and go on re-split.
2. **The split waits for layout; the block waits hidden.** A split needs a measured width (SAMPLES-PLAN §8 rows 4 and
   23), and the triggers fire at attach. The block is held at `Opacity` 0 at `Animation` priority from the trigger until
   the split's first effect runs, then the builder plays on the pieces and the hold is released. Built inside
   `TextSplitter.AddEffect`, so a re-split on resize rebuilds it over the new pieces with `Utils.KeepTime`, the pattern
   documented on `AddEffect`.
3. **The split is put back when the reveal completes.** Like `Typewriter`, a non-looping reveal reverts its split on
   completion, returning a single text run to layout, accessibility and hit testing. A looping one keeps it.
4. **Masks use the splitter's own wrappers.** `SplitPieceParams.Wrap` already wraps each piece in a clipping `Border`
   (upstream's `wrap: 'clip'`). `TextReveal.Mask = true` splits with `Wrap` on the chosen pieces, so `LineMask` (each
   line rises from behind its own clip) is `Y` from the line height on wrapped lines. A per-line wipe uses the
   `ClipInset` shorthand on unwrapped line pieces instead. The host is already unclipped while split (row 25).
5. **Line-aware stagger and line height are exposed as helpers.** `TextSplitter.StaggerByLine(lineStep, pieceStep)`
   returns the function value `TypographyPage.BuildReveal` computes by hand (line index × line step + index in line ×
   piece step), and `TextSplitter.LineHeightOf(piece)` reads the owning split's `LineHeight`, so recipes can say "from
   one line down" before pieces have bounds.
6. **Recipes.** Builders meant for pieces, each with a `WhenReduced` alternative that fades the whole block without a
   split (retargeting is skipped when the params are the reduced form):
   - `LineRise` (lines from one line height down, fading, staggered);
   - `LineMask` (sets `Mask`, lines rise from behind their clips, no fade);
   - `LineWipe` (a `ClipInset` right-to-left wipe per line);
   - `WordFade` (words fade up with `StaggerByLine`);
   - `WordBlur` (words blur in, `BlurIn` per piece);
   - `CharCascade` (characters drop in with a short stagger and an emphasized ease).

   Each has a markup extension that also sets the pieces and mask it needs, so one attribute suffices:
   `presto:Presence.Initial="{presto:LineMask}"` on a `TextBlock` implies `Pieces="Lines"`, `Mask=true`.
7. **`Typewriter` stays as it is.** It returns an `Animation` and requires a laid-out block. Rebuilding it on the new
   machinery would change its return type; `Recipes.Typewriter` is documented as the code-first form and
   `CharCascade` as the trigger form.

### 3.2 API

```csharp
public static class TextReveal
{
    public static readonly AttachedProperty<TextPieces> PiecesProperty;   // None, Lines, Words, Chars
    public static readonly AttachedProperty<bool> MaskProperty;
}

TextSplitter.StaggerByLine(double lineStep, double pieceStep = 0, double? start = null)   // FunctionValue
TextSplitter.LineHeightOf(Control piece)
Recipes.LineRise / LineMask / LineWipe / WordFade / WordBlur / CharCascade (duration, delay, ease, lineStep, pieceStep)
```

```xml
<TextBlock Classes="headline" TextWrapping="Wrap" Text="{Binding Headline}"
           presto:Presence.Initial="{presto:LineMask}" />
<TextBlock Text="{Binding Body}" presto:TextReveal.Pieces="Words"
           presto:Presence.Reveal="{presto:FadeUp Stagger=True}" />
```

### 3.3 Implementation steps

1. **Retarget hook.** One internal `PieceTargets` step shared by the triggers named in decision 1: hold, split with
   `AddEffect`, play, release, revert on completion; skipped for reduced params and for non-`TextBlock` targets.
2. **Helpers** on `TextSplitter` (`StaggerByLine`, `LineHeightOf`); `TypographyPage` moves onto them.
3. **Recipes and markup extensions**, with reduced forms; README rows.
4. **Sample**, §3.5.

### 3.4 Tests (`TextRevealTests`, headless)

- `Pieces="Words"` with a `FadeUp` on `Presence.Initial` animates every word piece and not the block; the block is at
  opacity 0 before the first layout and 1 after the split's first effect.
- A resize mid-reveal re-splits and the rebuilt animation continues at the same time (`KeepTime`).
- On completion the block has no split pieces and shows its text; a looping reveal keeps them.
- `LineMask` wraps each line in a clipping border and ends with each line at `Y` 0; `LineWipe` ends with no clip.
- `StaggerByLine(100, 20)` gives the second word of the third line a delay of 220.
- Under `ReducedMotion` the block fades as a whole and is never split.
- A `Motion.Hover` state on a block with `Pieces` set still animates the block.

### 3.5 Samples

- **Typography showcase:** the hand-built reveal becomes `{presto:WordFade}`-style recipes; the page keeps its rotator.
- **New Reference page section "Text reveals"** on the Text page: the six recipes side by side on the same paragraph,
  replayable, with their XAML printed.
- **Scroll story:** section headings use `{presto:LineMask}` on `Presence.Reveal`.

## 4. Open questions (confirm before the relevant phase)

- Phase 36: whether continuous emitters need an XAML surface (an attached `Particles.Emitter` on a control, started
  while attached or hovered) or whether code plus `Recipes.Sparkles` is enough. **Resolved:** code plus
  `Recipes.Sparkles` was kept; no XAML emitter surface was built.
- Phase 36: exact default caps per platform, to be set from measurement on Browser and Android. **Partly resolved:**
  the desktop measurement (§5) supports 4 000. Browser and Android were not measured, so their 1 000 is still the
  starting point from decision 8.
- Phase 37: whether `SpriteSheet` derives from `FilteredImage` (decision 2.6), decided when the later of 28 and 37
  starts. **Deferred to Phase 28:** 37 landed first, so `SpriteSheet` derives from `Control` (§5), and its frame logic is
  confined to its private `SourceRect` method. Phase 28 decides whether to switch the base class.
- Phase 38: whether `Typewriter` should gain the deferred-split behavior in a later API pass, accepting the break.
  **Open:** `Typewriter` is unchanged and points to `CharCascade` as its trigger form.
- Phase 38: whether the retarget hook should also reach `Animate.OnChanged`, where the text itself is what changed and
  a re-split per change is the point (a ticker or rotator). **Open:** Phase 38 leaves `OnChanged` on the block.

## 5. Deviations as built

### Phase 36

- **Namespaces.** `ParticleScene` and `ParticleShape` are in `Monaco.Presto.AvaloniaBridge.Compositor`, beside
  `CompositorScene`. `ParticleEffect` is in the root bridge namespace. The recipes live in `Prebuilt.Particles`, and
  `Recipes.Confetti`/`Sparkles`/`Burst` forward to it.
- **`EmitterParams.Area`.** A new `(Width, Height)` spawn box centered on the origin, default zero (spawn at the
  origin). `Sparkles` sets it to the anchor's bounds, and `Confetti` with an `Edge` sets it to the length of that edge.
- **`EmitterParams` defaults.** `Size` defaults to `new(6, 6)`, `Opacity` to `new(1, 0)` (linear fade), and `Colors` to
  white. §1.2 left these unset. `Flutter` is in flips per second.
- **`ParticleCurve`.** A record built as `new(from, to, ease)`, with the ease linear when omitted. The `EaseValue` is
  resolved once when it is set, and a `Spring`'s settling duration is ignored because the curve spans the particle's life.
- **`ParticleSystem` extras.** It exposes `Params` and a settable origin (`X`, `Y`). The scene-wide cap reaches it
  through an internal `SpawnBudget`, which the scene recomputes before each system's step. `Clear()` removes the live
  particles but leaves emission running, so `ParticleEffect.Cancel` calls `Stop` then `Clear`. `IsFinished` is true
  before `Start`. A spinning particle starts at a random `Rotation`, and every particle at a random `FlutterPhase`.
- **Integration.** Decision 1's semi-implicit Euler drifts from the true parabola by 0.5·g·t·dt, so position also takes
  half a step of gravity (`p += (v + g·dt/2)·dt`, velocity-Verlet for constant acceleration). Gravity is then exact at
  any step size.
- **Scene message and completion path.** `ParticleScene.Add(params, origin)` is public but runs on the render thread,
  so call it from a scene callback or `CompositorCanvas.Invoke`. Each system is dropped once it is finished, and its `finished`
  callback then runs on the UI thread through `PostToUI`. The overload of `Add` that takes `finished` is internal.
  `Live` is public and read on the render thread; the Particles page uses it for its count.
- **Shapes.** `Glyph` accepts only printable ASCII (`!`…`~`), because it draws through `SceneText`. `Bitmap` takes a
  `Bitmap`, not any `IImage`, and applies opacity but not color. `FromGeometry(geometry, size)` rasterizes the geometry
  in white and has no fill parameter.
- **`ParticleEffect`.** `Pause`/`Resume` are per effect: each system carries an internal `Paused` flag, so only this effect's emitters
  freeze and other effects in the same overlay keep running. `Cancel` resumes, stops and clears its systems, and
  completes `Then()` at once. `Revert` is `Cancel`. `Then()` otherwise resolves on the UI thread when the effect's last
  system is dropped.
- **Overlay.** It is a `ParticleOverlay` held per `TopLevel` in a `ConditionalWeakTable`. It is placed in the
  `OverlayLayer`, offset so its coordinates are top-level coordinates, and follows the top level's `SizeChanged`. It is
  removed by a one-second idle timer after its last system finishes. An anchor outside a window emits nothing, does not
  play `reduced`, and returns an effect that is already complete.
- **Confetti.** It is two systems: three quarters of the pieces are fluttering, spinning rects and one quarter are
  streaks.
- **Default reduced forms.** When `reduced` is null, the recipes play `Recipes.Pulse()` on the anchor, and reduced motion
  turns it into its opacity blink. The Micro-interactions like passes `Recipes.Fade()` instead. Storefront skips the
  burst under reduced motion, because its badge's own `Pop` already covers it.
- **Samples.** The Micro-interactions like bursts when the heart turns on (in its tap handler), not on press-in, and not
  when unliked. It replaced the page's hand-built 8-dot burst. Submit flow throws `Recipes.Confetti(button, count: 80)`
  on success.
- **Cap measurement (desktop).** Measured on the Desktop sample's Particles page, in a Debug build on the dev machine
  (Windows 11, a 180 Hz display), by reading the live count and the scene's render-thread fps. Particles lived about a
  minute and stayed on stage.

  | Shape | Live particles | Render fps |
  |---|---|---|
  | Rect, spinning | 4 000 | 113–125 |
  | Rect, spinning and fluttering | 4 000 | 92 |
  | Circle | 4 000 | 104 |
  | Streak | 4 000 | 98 |
  | Bitmap (also `FromGeometry`) | 4 000 | 90 |
  | Glyph | 50 / 100 / 250 / 2 000 | 60 / 42 / 20 / 2 |

  Every shape except `Glyph` holds 60 fps at the 4 000 desktop default with room to spare, so the default is unchanged.
  `Glyph` is two orders of magnitude slower per particle, with or without rotation. Keep glyph emitters to a few dozen
  live particles, or use a bitmap made with `FromGeometry`. None of the recipes uses `Glyph`.

### Phase 37

- **Base class.** `SpriteSheet` derives from `Control`, not `Image`: Avalonia 12.1.2 seals `Image.Render`. `Source`,
  `Stretch` and `StretchDirection` come from `Image`'s properties through `AddOwner`, and measure, arrange and render
  follow `Image`'s rules for one frame.
- **Shared helper.** The frame model is the internal static `FramePlayer` (`Resolve`, `Play`); there is no shared
  interface.
- **Frame counts.** `SpriteSheet.FrameCount` is the nullable input, and the read-only `EffectiveFrameCount` is it or
  `Columns · Rows`. `ImageSequence.FrameCount` is read-only, `Frames.Count` or 0.
- **`FrameSize`.** Cells are always `Source` split evenly by `Columns` and `Rows`; `FrameSize` is the part of each cell
  drawn from its top-left corner, so margins sit on each cell's right and bottom.
- **`ImageSequence`.** Measures and arranges to the first frame's size. `FromAssets(uriFormat, count, start = 0)` takes a
  first index.
- **Recipe.** It lives in `Prebuilt.Sprites.PlayFrames(fps, loop, reverse, delay)`, and `Recipes.PlayFrames` forwards to
  it. A target that is neither control counts as one frame. Reduced, it drops the loop and the reverse and sets `Frame`
  to 0 with zero duration.
- **`{presto:PlayFrames}`.** Adds `Fps` (default 24), `Loop` (default true) and `Reverse` to the usual recipe members; an
  explicit `Duration` or `Ease` replaces the frame timing. It has no exit form.
- **Allocation budget.** A playing sheet is held to 112 bytes per tick, not a bare `double` tween's budget: each new
  frame also raises `CurrentFrame`'s change args (measured 104 at 24 fps on 16 ms ticks).
- **Samples.** The assets are Kenney's CC0 Platformer Characters: two of its tilesheets (adventurer and zombie, 9 × 3
  cells of 80 × 110 px, 24 poses, so `FrameCount="24"`) and eight of its separate female pose images renamed
  `female_000`…`female_007` as the sequence. The zombie is started from XAML with `{presto:PlayFrames Fps=12}`.
- **Cell bleed.** Filtered sampling reads up to half a source pixel past the source rect, so when a cell is scaled
  `SpriteSheet` insets its source rect by half a pixel to keep the neighboring cell out of the frame's edges.

### Phase 38

- **Trigger handle.** `PieceTargets` is an internal `IRevertible`, not an `Animation`, since the animation is rebuilt on
  every re-split. `Presence`'s tracker and `Animate`'s trigger now store an `IRevertible`, so re-trigger, detach and
  recycle revert the split and release the hold.
- **Piece batch.** A retargeted `Presence` arrival is not added to its panel's batch: its pieces are their own targets,
  so function values and staggers see each piece's index and the piece count.
- **Split shapes.** `Lines` splits lines only (no words), `Words` splits words, and `Chars` splits characters without
  words. `Mask` wraps the chosen pieces only.
- **Markup extensions.** They derive from an abstract `TextRevealExtension : RecipeExtension`, which adds `LineStep` and
  `PieceStep` and has no exit form. `RecipeExtension.ProvideValue` calls a new `protected virtual Configure(target)`
  hook, which sets `Pieces` (and for `LineMask`, `Mask`) on a `TextBlock` target unless already set. In a style
  `Setter` the target is not the block, so the block has to set `Pieces` itself.
- **Recipes.** They live in `Prebuilt.TextReveals`, forwarded from `Recipes`, and take no `stagger`: every one delays by
  `StaggerByLine(lineStep ?? 3 × StaggerStep, pieceStep, delay)`. `pieceStep` defaults to 0 for the line recipes, half
  `StaggerStep` for the word ones and a fifth for `CharCascade`. The reduced form is an `Opacity` fade over `Short`.
- **`LineWipe` direction.** Each line wipes in left to right, its `ClipInset` going from `"0 0 100% 0"` (right inset
  100%, in `Thickness` order) to `"0 0 0% 0"`, rather than the right-to-left wipe in decision 6.
- **`LineHeightOf`.** Returns 0 for a control that is not a split piece; each piece records its owning splitter.
- **Scroll story headings.** They sit in a plain `StackPanel` in a `ScrollViewer`, where `Presence.Reveal` never plays
  (it only classifies rows a virtualizing list realizes). Each chapter's existing `ScrollObserver` sets
  `Animate.OnAttached` to `Recipes.LineMask()` on its heading the first time it enters, which plays at once on the
  attached block; the headings set `TextReveal.Pieces="Lines"` and `Mask="True"` in XAML.
- **Typography paragraph.** It is `{presto:WordFade LineStep=120, PieceStep=30, Duration=1100, Ease=outExpo}` with
  `TextReveal.Mask="True"`, so each word rises from its own clip rather than from its line's, and fades while it rises
  `MotionTokens.Distance` instead of a line height. Replay clears and re-sets `Animate.OnAttached`.
