# Showcase round 3 (13h)

Six new Showcase pages modelled on the UWP WindowsCompositionSamples and the Windows Community Toolkit animation
samples, plus two creative pieces. Each one is a heavy piece of UI rather than a feature in isolation, and none of them
repeats a page that already exists (Board, Carousel, Card gallery, Dashboard and so on).

Everything in [SAMPLES-PLAN.md](SAMPLES-PLAN.md) still applies:

- every page derives from `ShowcasePage` and builds its animations in `OnAttached`, so `Scope` reverts them;
- colours come from `Palette`;
- seeded data goes in `Datasets`;
- pages go in `samples/PrestoSample/Showcase/Pages/`, and each is registered in the Showcase section of `MainViewModel`
  with a description in the style of the existing entries (say what to try);
- each page gets a headless test class in `tests/PrestoSample.Tests/` (`<Page>Tests.cs`), starting with the
  `ThePageRunsAndRevertsItsScopeOnDetach` smoke test that `CarouselPageTests` uses.

Library gaps found while building these pages are **not** fixed in the library. Record each one as a new row in
SAMPLES-PLAN §8 (continue numbering from 29) with a recommendation, and let the page work around it. Before building,
read the §8 rows these pages are most likely to hit:

- 11: `AnimateLayout` inside a `ScrollViewer`;
- 12: `AnimateLayout` and the render-transform shorthands on the same control;
- 14: a container resizing while a drag is in flight;
- 15: `Draggable.Reset()` stopping other animations;
- 18: scope growth under repeated animation;
- 24: an `Opacity` overshoot making the control vanish (keep `Back`/`Spring`/`Elastic` eases off `Opacity`);
- 26: `MorphTo` needs a `Path`.

Pages must not scroll unless the spec says so (row 11). Size every stage to fit the page at the Desktop window's
default size.

## 1. Connected animation: `ConnectedPage`

*From the UWP ConnectedAnimation sample: going from a list to a detail view with several coordinated elements.*

The page is a 4×3 grid of twelve album cards that fits the stage and does not scroll. Each card has:

- an art tile: a `Border` with a two-stop `LinearGradientBrush` built from the album's palette hue, with a large
  monogram letter on it;
- a title;
- an artist line.

Clicking a card opens the album's detail view on top of the grid:

1. **Two elements fly independently.** The card's art tile and title are reparented into the detail layout. The art
   becomes the large hero at the top left and the title becomes the header beside it. Each element is armed with its
   own `AnimateLayout(..., LayoutAnimateMode.PositionAndSize)`: art at 560 ms `OutQuint`, title at 460 ms `OutExpo`.
   Because the durations differ, the two elements arrive at different moments.
   - `AnimateLayout` has no delay parameter, so different durations are the only way to separate the two flights. If a
     delay is needed, add a §8 row for it.
   - In the card, the art is replaced by a placeholder of the same size, so the grid does not reflow.
2. **The other eleven cards scatter.** One `AnimateOnCompositor` runs over them: `Opacity` to 0, `Scale` to 0.85, and
   `X`/`Y` pushed away from the selected card's centre by 40 px. The push direction comes from a function value per
   card, and a `Stagger` starts from the selected index. The selected card's own shell fades out as well.
3. **The detail content comes in.** Once the hero is under way:
   - the stage `Background` tweens to a dark tint of the album's hue;
   - a track list of 8–10 rows staggers in (`Y` 16 → 0 with `OutQuart`, and `Opacity` in its own tween with `OutQuad`);
   - a round play button pops in with `OutBack` on `Scale` only. Never put `OutBack` on `Opacity` (§8 row 24).
4. **Back is the whole thing in reverse.** A Back button (and Escape) does the following:
   - fades the track rows out, staggered from the last;
   - reparents the art and title into their card, where their live transitions invert them home from wherever they are
     currently drawn;
   - reverses the scatter animation. Calling `Reverse()` on it is preferred; retargeting it to identity is an acceptable
     fallback;
   - tweens the background back.

   Pressing Back while the page is opening, or clicking the same card while it is closing, must continue from the
   current position with no jump. This is the approach `GalleryFlipPage` settled on, because `AnimateSharedElement`
   cannot be reversed (§8 row 13). Do not call `AnimateSharedElement`.

- **Exercises:** `AnimateLayout` on two elements with different durations, `AnimateOnCompositor` with a stagger from the
  selected index and per-target function values, reversing a compositor animation, and a `Background` brush tween.
- **Data:** add `Datasets.Albums`, a seeded list of 12 `Album(Title, Artist, Year, Hue, Tracks)` records, where each
  track has a title and a length.
- **Done when:**
  - the hero and title visibly arrive at different moments;
  - opening and closing can be reversed at any point;
  - ten rapid open/back toggles leave every card at identity (no leftover transforms, all opacities back to 1);
  - detaching the page mid-flight reverts everything.
- **Tests:**
  - smoke test;
  - open card 5 and tick until settled: the art's parent is the detail host and the other cards are at opacity 0;
  - press Back and tick: the art is back in card 5 and every card's opacity is 1.

## 2. Colour bloom navigation: `BloomNavPage`

*From the UWP "ColorBloom" transition sample.*

A mock app window has a header of four tabs (Inbox, Photos, Music, Settings), each with its own hue. Add `Palette.Amber`
and `Palette.Violet` if the existing colours run out. Each tab's content is a small mock layout of rows or tiles built
from `Datasets`.

Clicking a tab plays a bloom:

1. **A circle of the new tab's hue grows out of the click point.** An `Ellipse` is added to an overlay `Canvas` over the
   stage, and the stage has `ClipToBounds` set. The circle's centre is the pointer position; for keyboard selection it is
   the tab's centre. Its diameter is twice the distance from that point to the farthest corner of the stage, so it
   covers the stage exactly when it finishes. It animates `Scale` from 0 to 1 over 620 ms `InOutQuart`.
2. **The content swaps under the bloom.** A timeline `Call` at about 65% swaps the content and sets the stage background
   to the new hue. The bloom then fades out over 260 ms while the new content staggers in (tiles rise 24 px, 40 ms
   apart, `From = Center`).
3. **The header follows along the whole time:**
   - the underline indicator moves to the new tab (`X` and `Width` at 420 ms `OutExpo`);
   - the old tab label's `Foreground` fades to dim and the new one's to ink;
   - the header background tweens toward the new hue.

**Rapid clicks stack.** Each click starts its own bloom above the previous one. A bloom that completes removes itself
and every bloom beneath it, so fast clicking never flashes old content and never leaves ellipses behind.

- **Exercises:** timelines with `Call` and overlapping relative positions, geometry computed from the pointer, `Scale`
  on the UI thread (or `AnimateOnCompositor`, whichever looks smoother on Desktop; record which one was chosen), and
  tweens on `Background`/`Foreground` brushes.
- **Done when:**
  - the bloom starts at the click and covers the farthest corner with no visible gap at its last frame;
  - clicking four tabs quickly ends on the last tab with an empty overlay;
  - the underline and labels always settle on the selected tab.
- **Tests:**
  - smoke test;
  - click tab 3 and tick until settled: the overlay has no children, the stage background equals tab 3's hue, and the
    content is tab 3's;
  - click tabs 2, 3 and 4 two frames apart and tick until settled: the page is on tab 4 and the overlay is empty.

## 3. Gestures: `GesturesPage`

*From the UWP InteractionTracker samples (pull-to-refresh, swipe to dismiss, the "curtain").*

Three panels side by side, each showing a live velocity readout.

### a. Pull to refresh

A clipped list of eight rows (not a `ScrollViewer`) sits on a y-only draggable.

- **Pull:**
  - **Rubber band:** a `Modifier` maps the raw pull to `max × (1 − e^(−y / max))`, so it stiffens as it goes.
  - **Snap:** `Y.Snap = y => y >= Threshold ? Threshold : 0`. With this snap, the release spring itself decides between
    holding at the threshold and springing home.
- **The indicator above the list reacts to the pull:**
  - an arrow rotates from 0 to 180° as the pull goes from 0 to the threshold;
  - a ring draws in step with it (`CreateDrawable`, with `Draw` set from `OnUpdate`).
- **Refresh:** `OnSettle` at the threshold does the following:
  1. disables the draggable;
  2. turns the arrow into a spinning ring (a looping `Rotate`);
  3. after about 1.2 s, inserts three new seeded rows at the top with a staggered entry;
  4. springs the list back to 0;
  5. re-enables the draggable.

### b. Swipe to dismiss

This is an inbox of eight rows, each on its own x-only draggable.

- **Reveal:** behind each row is an action strip: archive (aqua) on the left, delete (coral) on the right. The strip's
  icon grows with `OutBack` on `Scale` once the reveal passes 40% of the row's width.
- **Snap:** `Snap = x => |x| > 0.4 × width ? sign(x) × width : 0`. A fling therefore carries a short drag past the
  threshold through the release projection, without extra code.
- **Dismiss:** `OnSettle` at ±width does the following:
  1. collapses the row's `Height` to 0 (360 ms `OutQuart`), which closes the gap in its `StackPanel` naturally;
  2. removes the row;
  3. calls `Revert()` on its draggable. Never call `Reset()` (§8 row 15).
- **Undo:** an Undo button brings the last dismissed row back. Its height expands and it slides in from the side it
  left by.
- The rows' draggable containers must not resize mid-drag (§8 row 14). Give each row a container of fixed size.

### c. Curtain

A notification shade covers the panel, with a grab handle at its bottom edge. It sits on a y-only draggable.

- **Snap:** there are three rest positions: closed (up), half, and full. `ContainerFriction` gives the overshoot.
- **The content under the shade is linked to it.** In `OnUpdate`, `Presto.Set` derives three values from the shade's
  progress:
  - the content's parallax `Y` (30% of the shade's travel);
  - its `Scale` (0.94 → 1);
  - the `Opacity` of a dimming scrim.
- **Tapping the handle** moves the shade to the next rest position. Drive it through the draggable itself (its
  `Animate` animatable or its axes) so the draggable's state stays in sync. If that is not possible from public API, add
  a §8 row.

- **Exercises:** `CreateDraggable`, specifically:
  - `Modifier`;
  - a `Snap` function that decides the outcome;
  - `ContainerFriction`;
  - `Disable`/`Enable`/`Revert`;
  - `Velocity`;
  - `OnSettle`;

  plus `CreateDrawable` progress, linked values written with `Presto.Set`, and a looping spinner.
- **Done when:**
  - a short pull springs home and a pull past the threshold holds, refreshes and returns;
  - a swiped row collapses without leaving a gap, and Undo restores it;
  - the curtain rests at each of its three positions, and its linked parallax stays continuous through the release
    spring.
- **Tests:**
  - smoke test;
  - drive a pull past the threshold with `window.MouseDown/MouseMove/MouseUp`, then tick about 2 s: the list has 11
    rows;
  - swipe row 1 past 40% and tick: 7 rows remain;
  - press Undo and tick: 8 rows again.

## 4. Liquid navigation: `LiquidPage`

*Creative. Stretchy tab indicators and metaball buttons.*

### a. Liquid tab bar

A bottom navigation bar has five icons (`PathIcon`s with `Geometry.Parse` constants). A pill indicator sits behind the
active icon.

- **The pill's two edges are separate values** on a plain `Edges { Left, Right }` object, animated through the core's
  reflection adapter. `OnUpdate` writes `Canvas.Left` and `Width` from them.
- **The edge in the direction of travel leads:** 300 ms `OutExpo`. The trailing edge follows: 520 ms `OutQuart`, 70 ms
  later. This happens through per-property tween params on one animation, so the pill stretches and then contracts. A
  keyframed `ScaleY` (1 → 0.82 → 1) squashes it slightly while stretched.
- **Icons:**
  - the selected icon hops (`Y` −8 and back with `Eases.Spring`) and its `Foreground` tweens to ink;
  - the previous icon fades to dim;
  - a label under the selected icon rises into view.
- **Rapid clicks** retarget from wherever the edges are now, through the default replace composition. The edges never
  snap back.

### b. Gooey action button

This is a floating button with three child actions. Pressing it:

- turns its plus icon into a cross (`Rotate` 45°);
- springs the three children out on a staggered `Eases.Spring`, fanning upward and diagonally.

Each blob, including the parent, is a plain `Blob { X, Y, R }`. On each update, one `Path` is rebuilt from the blobs as
a single filled geometry. That geometry is:

- the circles;
- a metaball "neck" between the parent and each child whose distance is within `r1 + r2 + reach`.

The neck geometry follows the standard two-circle metaball construction (tangent points plus bezier handles, with a
spread factor). It goes in `Showcase/Pages/Metaball.cs`, a pure static helper that returns the contour points, so it can
be unit tested. Closing the button runs the children back with `InBack`, and the necks re-form as the children arrive.
Opening and closing can be interrupted at any point.

Allocating one `StreamGeometry` per frame for four blobs is acceptable in a sample. Note it in the page's code only if
the reason is not obvious from the code itself.

- **Exercises:** per-property tween params (different duration, delay and ease per property), animating plain objects
  through the core adapter, keyframes, spring eases, `OnUpdate`-driven geometry, and retargeting under replace.
- **Done when:**
  - the pill visibly leads with its travelling edge in both directions;
  - clicking tabs quickly never snaps the pill;
  - the necks join and break smoothly as the children leave and return;
  - toggling the button mid-flight reverses from where the blobs are.
- **Tests:**
  - smoke test;
  - click tab 4 and tick until settled: the pill's `Left`/`Width` match tab 4's slot;
  - a few frames after clicking, the pill is wider than at rest;
  - `MetaballTests`:
    - a neck exists when the circles are close and not beyond reach;
    - the neck is symmetric for equal radii;
    - its endpoints lie on the two circles.

## 5. Swipe deck: `SwipeDeckPage`

*Creative and real-world. A Tinder-style card stack.*

The deck holds twenty seeded destination cards (`Datasets.Places`: city, country, hue, a line of blurb) and always shows
a stack of three. Each card has gradient art, a title and "LIKE" / "NOPE" stamps.

- **Dragging the top card:** the top card has a free x/y draggable.
  - `OnUpdate` sets its `Rotate` to `X × 0.06°` about a bottom-centre origin, so the top of the card swings more than
    the bottom.
  - It also sets the stamps' `Opacity` to `clamp(|X| / 120, 0, 1)`, on the side of travel.
  - The two cards beneath move up in proportion to `|X|`: `Scale` 0.94 → 1 and `Y` 14 → 0 for the second card, and the
    same one step down for the third.
- **Releasing:**
  - **Home:** below the threshold, the card springs home to 0,0 and the cards beneath settle back. This is either the
    release snap or an explicit spring; pick whichever composes cleanly.
  - **Dismiss:** past 110 px, or on a fast fling in any direction, the card leaves in its direction of travel:
    1. its draggable is disabled;
    2. an animation takes it off-stage along its velocity with extra rotation (380 ms `OutQuad`);
    3. on completion the card is removed and its draggable reverted;
    4. the next card is promoted. It gets a draggable of its own, and its `Scale`/`Y` finish to 1/0 with a spring;
    5. a new card enters at the bottom of the stack at `Scale` 0.88.

    If the drag's own release tween fights the dismiss animation over `X`/`Y`, record it in §8 along with the workaround.
- **Buttons:**
  - ✕ and ♥ dismiss the top card programmatically along the same path;
  - ↺ Undo brings the last card back from where it left, flying in with a spring to the top of the stack and pushing
    the others down a step.
- **The deck refills** from the start once all twenty cards have gone.

- **Exercises:** free-axis `CreateDraggable`, values derived from the drag in `OnUpdate`, deciding a dismiss from
  velocity, `Disable`/`Revert`, springs, a promotion timeline, and reversing a dismiss with Undo.
- **Done when:**
  - the card follows the pointer with rotation and stamps;
  - a slow drag under the threshold springs home, and a short fast flick dismisses in the direction of the flick;
  - the stack promotes with no jump;
  - Undo restores the last card;
  - a hundred dismisses leave no leftover draggables or controls.
- **Tests:**
  - smoke test;
  - press ♥ and tick until settled: the top card is `Places[1]` and three cards are shown;
  - press Undo and tick: the top card is `Places[0]`;
  - drag the top card 60 px right and hold: the LIKE stamp's opacity is 0.5.

## 6. Chart morph: `ChartMorphPage`

*Creative. One dataset, four charts, and every data point keeps its identity through each change.*

The dataset is eight data points (`Datasets.ChartPoints`: label, value, secondary value, weight). Each point is a
`Path` with its own fill from a fixed categorical ramp. A segmented switch morphs every point between four views:

| View | Each point is drawn as |
|---|---|
| **Bars** | a rounded-rect bar in its column |
| **Donut** | an annular wedge with a sweep proportional to its value |
| **Scatter** | a circle at (value, secondary) with a radius set by its weight |
| **Line** | a small circle marker at (index, value) |

- **Shapes:** add `Showcase/Pages/ChartGeometry.cs` with static builders: `Bar`, `Wedge`, `Circle`, and the polyline
  for the line view. Each builder returns a single closed contour that starts from a consistent point with a consistent
  winding, so `MorphTo` does not twist between views.
- **Switching views:**
  - `MorphTo(next)` runs on every point at 720 ms `OutQuart` with a 45 ms stagger. The donut staggers from the first
    wedge; the other views stagger from the centre.
  - In the Line view, a separate stroke `Path` through the markers draws in (`Draw` "0 0" → "0 1", `CreateDrawable`)
    after the markers land. It undraws before any morph away from Line.
  - The y-axis tick labels count to the new scale's values through `TextModifier` + `Utils.RoundPad`. The number and
    the unit are in separate `TextBlock`s (§8 row 16).
  - Gridlines and axes fade out for the donut and back in for the others.
- **Shuffle** pushes a new seeded round of values. It retargets the current view: the points morph into the same chart
  with the new values, starting from the shapes as they are currently drawn.
- **Hover:** hovering a point lifts it (`OutBack` on `Scale` only) and shows a tooltip readout of its value.
- **Switching again mid-morph** must continue from the shape currently drawn. Check what `MorphTo` reads its start
  geometry from while a morph is live on the same `Path`, and record in §8 if it jumps.

- **Exercises:** `MorphTo` over many targets with a `Stagger` and `From`, `CreateDrawable` with sequencing,
  `TextModifier` counters, retargeting under replace, and function values per index.
- **Done when:**
  - each point keeps its colour through all four views;
  - switching views mid-morph is continuous;
  - the line stroke draws only once the markers are in place and undraws before leaving;
  - Shuffle retargets without snapping;
  - the labels count to the new scale.
- **Tests:**
  - smoke test;
  - switch to Donut and tick until settled: every point's `Data.Bounds` lies within the donut's outer radius around its
    centre;
  - switch Bars → Scatter → Line with a few frames between each, then tick until settled: no exception, and the line
    stroke's `Draw` end is 1;
  - `ChartGeometryTests`: the builders return closed contours, and wedge sweeps sum to 360°.

## 7. Phase

- [ ] **13h — Round 3.** Pages 1–6 above, their `Datasets`/`Palette` additions, `MainViewModel` registration, the test
      classes, and any new §8 rows. Verify with:
  - a full `slnx` build;
  - `dotnet test --solution Monaco.Presto.slnx` passing;
  - a Desktop pass over every new page against its "done when" clause.

  Implemented, with SAMPLES-PLAN §8 rows 29 (`ConnectedPage`) and 30 (`GesturesPage`). The core, bridge and sample
  test projects pass (946, 207 and 131 tests). Still pending: a full `slnx` build, blocked by a locked Desktop bin
  while the sample was running, and the Desktop pass.

Every page can ship on its own, and the order does not matter.
