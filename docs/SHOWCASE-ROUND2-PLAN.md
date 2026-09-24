# Showcase round 2 (13g)

Follow-up to [SAMPLES-PLAN.md](SAMPLES-PLAN.md) and [SHOWCASE-POLISH-PLAN.md](SHOWCASE-POLISH-PLAN.md) after another
hands-on pass. Four things read badly in use — two typography glitches, a glitchy card-gallery load and a compositor
page that undersells itself — the reference pages animate shapes so plain that nobody can tell a real Avalonia control
is moving, and the showcase is missing the demos that would show the library off best.

Sample-only work: the library does **not** change. Any gap the work hits becomes a new row in SAMPLES-PLAN §8.

Applies to every item:

- Captions are **2–3 sentences**: what to try and what to notice. Rationale stays in code comments and SAMPLES-PLAN.
- Glitch fixes are **diagnosed before they are changed**, headlessly through `tests/PrestoSample.Tests/SceneHarness.cs`
  (attach the page, tick frames, sample the properties involved every frame). The confirmed cause goes in the item's
  summary, and a test pins the fix.
- Everything new uses `ShowcasePage`, `Palette` and the `Styles/Showcase.axaml` classes; new shared styles go there.
- Every new page gets an entry in `MainViewModel` and a smoke test in `tests/PrestoSample.Tests` (attach, tick 60
  frames, detach, no exception, scope reverted) in the style of the existing `*PageTests`.
- Visual items are checked on the Desktop sample (DevTools screenshots).

## 1. Typography — `MOTION` is clipped when its letters swell

**Problem.** Hovering the wordmark lifts a letter up to 30 px and scales it to 1.35×; the scaled glyphs are cut off.

- Find the clipping ancestor. Suspects: the chars live in an `InlineUIContainer` inside the host `TextBlock`, so the
  host's own bounds (or the inline container's line box) may clip; `Border.stage` has `ClipToBounds="True"`; the
  `Margin="0,24,0,8"` leaves less headroom than lift + swell (≈30 px + ~18 px of scale about the centre, plus the
  horizontal growth of the outer letters).
- Fix in the sample: give the wordmark room for its maximum excursion (padding/margin on the host sized from `Lift`
  and `Swell`, or `ClipToBounds="False"` on whichever ancestor clips) so a fully lifted, fully swollen letter — including
  `M` and `N` at the ends and the Replay wave — draws whole. If the clip turns out to be the bridge's (an inline
  container clipping its content), record it in §8 rather than changing the bridge.
- **Done when** a DevTools screenshot mid-Replay-wave shows every letter unclipped.

## 2. Typography — the word rotator blinks

**Problem.** Each word change flashes the text several times instead of reading as one drop-out / rise-in.

- Diagnose per frame across one rotation: the host `Word`'s `Text`/`Inlines`, and each char piece's `Opacity` and `Y`.
  Suspects, in order:
  - `ShowWord` calls `_word.Revert()`, which puts the **old** word's original text back on the host at full opacity for
    a frame, then sets the new text unsplit (a second flash), then splits;
  - the incoming chars are created at `Opacity = 1` and only reach their `From = 0` once their staggered delay starts,
    so later letters show, vanish and rise;
  - the reveal's `Opacity` tween (300 ms `OutQuad`) and `Y` (`OutBack`) disagree, or the rotation's `Cancel` leaves the
    outgoing chars half-faded when `ShowWord` runs.
- Fix so the outgoing word only ever fades/drops and the incoming only ever rises from invisible: e.g. hide the host
  across the swap (or split the new word before it is shown), and make the incoming animation render its from-values
  on creation. Keep the slot `Width` ease.
- **Test** in `TypographyPageTests`: across a full rotation no char's opacity rises then falls within the incoming
  phase, and the host never shows unsplit text at non-zero opacity.

## 3. Card gallery — the first load is glitchy

**Problem.** Opening the page, the cards populate with visible jumps rather than one clean staggered entry.

- Diagnose the first ~40 frames after attach: each card's `Opacity`, composition scale/opacity and the layout
  transition's translation. Suspects:
  - cards sit at full opacity in the panel until their staggered `AnimateOnCompositor` entry starts (delays up to
    24 × 16 ms), so they appear, vanish and fade back in;
  - `AnimateLayout` treats the first layout pass as a move and flies cards in from the panel origin, or from where the
    page-switch fade/rise put them;
  - the page-switch animation in `MainView` and the entry run at once and fight.
- Fix: cards start invisible before they are inserted (or their entry renders its from-values immediately), the first
  placement is not animated by FLIP, and the entry is a single tidy wave — grid-ordered stagger (`Grid`/`From = First`),
  a small rise plus fade and a gentle scale, `OutQuint`/`OutBack(1.2)`. Filter/sort/churn behaviour is unchanged.
- **Test** in `GalleryFlipPageTests`: from attach, every card's effective opacity is non-decreasing and no card is
  translated by a layout transition on the first pass.

## 4. Reference pages — animate controls that are visibly real

**Problem.** Many reference demos move a plain rounded `Border`, so a viewer cannot tell a real Avalonia control is
being animated rather than a drawn shape. The whole point of the bridge is that the targets are live controls.

Per page, replace the anonymous swatch/chip target with a recognisable control that **stays interactive while it
animates**, and say so in the caption ("it's a real Button — click it while it moves"):

| Page | Target becomes |
|---|---|
| Animation basics | a `Button` with a `PathIcon` and a click counter in its content |
| Value types | a small profile card (avatar initials, name, `ProgressBar`) whose Thickness/CornerRadius/colour rows drive its real properties; the brush rows paint a `Button`'s background |
| Keyframes | a `ToggleSwitch` or `Button` travelling the keyframe path |
| Playback settings / Controls / Callbacks | a `Button` ("Click me · n") as the animated target |
| Timeline | a row of real controls: `Button`, `ToggleSwitch`, `Slider`, `TextBox` as the timeline's children |
| Draggable | cards with a title, caption and a `CheckBox` or `Button` on them — still draggable by their body |
| Layout | the numbered cards gain a title and an icon so they read as UI cards |

Leave the abstract visualisations alone — stagger grids, the ease plots' dots, dot rings, the animatable dot grid and
the `Shapes` paths are data viz, not stand-ins for controls. Keep each page's behaviour and parameters identical; only
the target's visuals and the caption change. Add any shared look (e.g. a `specimen` class) to `Styles/Showcase.axaml`.
Existing sample tests that find targets by type or name are updated, not deleted.

## 5. Compositor page — rebuild the stage

**Problem.** Two 64 px dot rings, a small icon and a `ProgressBar` in a row: correct, but it reads as a unit test.

Rebuild `Pages/CompositorPage` as a proper demo of the render-thread story (it stays in the Reference section):

- **Hero comparison, large.** Two ~200 px panels side by side, identical content: left a `CompositorScene`, right the
  same drawing over real controls on the UI thread. Richer than dots: e.g. concentric staggered rings of dots in the
  accent ramp plus an orbiting comet with a fading tail, each panel labelled *render thread* / *UI thread*.
- **Live fps for each thread** — `FpsMeter` for the UI thread, and the scene's own fps drawn inside it with `SceneText`.
- **Visible freeze.** The UI thread posts a heartbeat timestamp to the scene each frame (`CompositorCanvas.Post`); while
  frozen the scene draws "UI thread frozen · 1.4 s" counting up, so the divergence is shown, not claimed.
- **Real controls on the compositor.** A row of genuine controls — a `Button`, a card with text, a gear `PathIcon`, a
  `ToggleSwitch` — each running a looping `AnimateOnCompositor` (Rotate, Scale pulse, Translation bob, Opacity breathe)
  that keeps playing through the freeze while Avalonia's own indeterminate `ProgressBar` beside them stops.
- Keep Freeze, Pause/Resume/Reverse and the speed slider (now driving every compositor control), with a clear
  "Freeze UI thread for 2 s" primary button.

`DotRingScene` may be replaced by the new scene; update anything that references it.

## 6. New showcase pages

Four new pages in the Showcase section, in this order after Typography.

### 6.1 Micro-interactions — `MicroInteractionsPage`

*Real-world, and the direct answer to §4: nothing on it is a drawn shape.* A grid of tiles, each a small UI pattern on
real controls:

- **Submit button** — click → the label collapses, the button narrows to a circle (`Width` + `CornerRadius`), a spinner
  `PathIcon` rotates, then `MorphTo` a check mark and springs back to the label.
- **Like** — a heart `PathIcon` that pops with `Eases.Spring` and throws a staggered burst of small dots; a counter
  ticks up through `TextModifier`.
- **Tab indicator** — a real `TabStrip`; an underline follows the selected tab's measured bounds through a
  `CreateAnimatable` over X and Width, and stretches mid-flight.
- **Accordion** — three `Expander`-style sections; siblings reflow with `AnimateLayout(PositionAndSize)`.
- **Toasts** — "Notify" stacks toasts that enter from the edge, restack with FLIP, auto-dismiss with a shrinking
  progress bar (`Timer`), pause on hover, and can be swiped away with `CreateDraggable` (x-only, released past a
  threshold dismisses).
- **Validation shake** — a `TextBox` that shakes on an invalid submit with keyframes and tints its border.

### 6.2 Loaders — `LoadersPage`

*Showpiece.* A 3×3 grid of loading indicators in the anime.js-homepage spirit, each built from a few lines and shown
with those lines under it in the `code` class: staggered bar equaliser, orbiting dots, a square grid wave
(`Stagger` with `Grid` + `From = Center`), a morphing blob (`MorphTo` loop), a drawing/undrawing ring (`"Draw"`
keyframes), a bouncing ball with squash (`Keyframes` on ScaleX/ScaleY), a typing dots row, a pulsing ripple set and a
progress counter. A shared speed slider sets each loader's own `Speed` (not the global engine's, which would slow the
rest of the gallery), and a *Pause all* toggle pauses them.

### 6.3 Carousel — `CarouselPage`

*Real-world. Draggable with snapping and linked motion.* A full-width strip of five slides dragged with
`CreateDraggable` (x-only, `Snap` to slide width, spring release). Each slide has parallax layers (background, title,
image stand-in) whose offsets are derived from the strip's X in `OnUpdate`, so a half-dragged slide shows its layers
mid-parallax. Dot indicators animate their width to the active slide; arrow buttons and the Left/Right keys animate to
a slide; an autoplay `Timer` advances every 4 s and pauses while hovered or dragged, with a ring on the active dot
showing its progress.

### 6.4 Stress bench — `StressBenchPage` + `FrameGraphScene`

Implements SAMPLES-PLAN §4.9 (phase 13d) as specified there: count slider 100 → 20 000, mode switch (UI-thread
animations / compositor animations per visual / compositor scene), property-set switch, and readouts for UI and render
fps, mean and p95 frame time, gen-0 collections and managed-heap delta over the last ten seconds; the frame-time graph is
a `CompositorScene`. The worker-thread `Engine` toggle is included if it fits; otherwise it is recorded in §4.9 as left
out. Measure on the dev machine, set defaults from the measurement, and write disappointing figures into §8. Tick 13d.

## 7. Phase

- [ ] **13g — Round 2.** Items 1–6 above. Verify: full `slnx` build, `dotnet test --solution Monaco.Presto.slnx` green,
      and a Desktop pass over every touched page.
