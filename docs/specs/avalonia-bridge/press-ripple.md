---
id: BRIDGE-RIPPLE
title: Press ripple
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Ripple.cs
  - Monaco.Presto.AvaloniaBridge/RippleHandler.cs
types:
  - Ripple
tests:
  - RippleTests
  - RippleGestureTests
  - RippleSpikeTests
  - AllocationTests
  - RipplePageTests
upstream: []
depends:
  - BRIDGE-TOKENS
  - BRIDGE-REDUCED
  - CORE-REDUCED
  - CORE-EASES
---

# Press ripple

## Purpose

`Ripple.*` attached properties give any `Control` Material's press ripple: ink grows from the press point while the press is held, until it covers the element, and fades on release; Enter or Space ripples from the center. An optional state layer tints the element while it is hovered, keyboard-focused or pressed. The ink is drawn over the content by one render-thread composition visual per host, so it never lays out the element or takes its input. Its timing comes from `Recipes.Tokens` (BRIDGE-TOKENS). Unrelated to `PrestoImageBase.Ripple` (BRIDGE-IMAGE), a noise displacement.

## Requirements

### Switching on

- **BRIDGE-RIPPLE-001** — When `Ripple.Enabled` becomes true on a control, the control shall ripple on press; when it becomes false, its ink, state layer and input handlers shall be removed.
  - Tests: `RippleTests.DisablingOrDetachingRemovesTheInk`
- **BRIDGE-RIPPLE-002** — While the control is attached, the ripple shall draw through one composition visual of its own, sized to the control and resized with it, over the control's content.
  - Tests: `RippleSpikeTests.AnElementChildVisualDrawsOverTheHostsChildren`, `RippleTests.TheInkGrowsFromThePressPoint`
- **BRIDGE-RIPPLE-003** — When the control detaches, its ripple shall end any press, clear the state layer and remove its visual; attaching again shall recreate it.
  - Tests: `RippleTests.DisablingOrDetachingRemovesTheInk`
- **BRIDGE-RIPPLE-004** (inherited) — In design mode, `Ripple.Enabled` shall have no effect.
  - Tests: untested

### Pressing

- **BRIDGE-RIPPLE-005** — A left-button or touch press shall start a ripple, seen even when the control marks the press handled.
  - Tests: `RippleTests.TheInkGrowsFromThePressPoint`
- **BRIDGE-RIPPLE-006** (inherited) — Other mouse buttons shall not ripple.
  - Tests: untested
- **BRIDGE-RIPPLE-007** — When `Bounded` is true, the ink shall grow from the press point, which stays put, from radius 0 to the distance to the control's farthest corner, clipped to the control's rounded rectangle.
  - Tests: `RippleTests.TheInkGrowsFromThePressPoint`, `RippleTests.AGrownRippleCoversTheHostButNotItsRoundedCorners`
- **BRIDGE-RIPPLE-008** — The rounded rectangle shall take its corners from `Border.CornerRadius` or `TemplatedControl.CornerRadius`, and be square for any other control.
  - Tests: `RippleTests.AGrownRippleCoversTheHostButNotItsRoundedCorners`
- **BRIDGE-RIPPLE-009** — When `Bounded` is false, the ink shall grow from the control's center, whatever the press point, to half its diagonal, clipped only by ancestors.
  - Tests: `RippleTests.TheInkIsClippedToTheHostOnlyWhenBounded`
- **BRIDGE-RIPPLE-010** — The radius shall grow over `Recipes.Tokens.Medium` with `Recipes.Tokens.Ease`, read at the press.
  - Tests: `RippleTests.TheInkGrowsFromThePressPoint`
- **BRIDGE-RIPPLE-011** — While the press is held, a grown ripple shall cover the control and stay.
  - Tests: `RippleTests.AHeldRippleCoversTheHostAndStaysThenReleaseFadesItAndTheHandlerIdles`
- **BRIDGE-RIPPLE-012** — When the pressing pointer is released, the ink shall fade linearly over `Recipes.Tokens.Short` while its radius keeps growing.
  - Tests: `RippleTests.AHeldRippleCoversTheHostAndStaysThenReleaseFadesItAndTheHandlerIdles`
- **BRIDGE-RIPPLE-013** — Once no ink is live and no state-layer fade is running, the ripple shall stop requesting frames.
  - Tests: `RippleTests.AHeldRippleCoversTheHostAndStaysThenReleaseFadesItAndTheHandlerIdles`
- **BRIDGE-RIPPLE-014** — A new press while one is held shall release the held ripple and start another, and several ripples shall draw at once.
  - Tests: `RippleTests.SeveralRipplesRunAtOnce`
- **BRIDGE-RIPPLE-015** — When Enter or Space goes down with the control itself as the event source, it shall ripple from the control's center, ignoring key repeats, and the key going up shall release it.
  - Tests: `RippleTests.EnterAndSpaceRippleFromTheCenter`

### Canceling

- **BRIDGE-RIPPLE-016** — When the pressing pointer moves out of the control, loses its capture, or an ancestor `ScrollViewer` scrolls, the press shall be canceled, which fades the ink like a release.
  - Tests: `RippleGestureTests.DraggingOutOrScrollingCancelsAHeldRipple`
- **BRIDGE-RIPPLE-017** — Inside a `ScrollViewer`, a touch press shall wait 100 ms (scaled by the engine time unit) before its ink shows.
  - Tests: `RippleGestureTests.ATouchPressInAScrollerShowsOnlyAfterTheTapTimeout`
- **BRIDGE-RIPPLE-018** — When a waiting touch press is canceled (a fling takes the capture, a scroll, or a drag out), no ink shall show.
  - Tests: `RippleGestureTests.ATouchFlingInAScrollerShowsNothing`
- **BRIDGE-RIPPLE-019** — When a waiting touch press is released before the wait ends, the ink shall show from the press point and fade at once.
  - Tests: `RippleGestureTests.AQuickTouchTapInAScrollerStillRipplesAndFades`
- **BRIDGE-RIPPLE-020** — Mouse and pen presses shall never wait, inside a scroller or not.
  - Tests: `RippleGestureTests.AMousePressInAScrollerShowsAtOnce`

### Color

- **BRIDGE-RIPPLE-021** — When `Ripple.Color` is set, the ink shall use it with its own alpha.
  - Tests: `RippleTests.TheInkGrowsFromThePressPoint`
- **BRIDGE-RIPPLE-022** — When `Ripple.Color` is null, the ink shall use the control's `SystemAccentColor` resource (`SystemAccentColorLight2` on the dark theme variant), looked up at each press, at 24 % alpha.
  - Tests: `RippleTests.TheDefaultColorIsTheThemeAccentAtLowAlpha`
- **BRIDGE-RIPPLE-023** — When the accent resource is missing, the control's `TextElement.Foreground` solid color, else black, shall stand in, at 24 % alpha.
  - Tests: untested

### State layer

- **BRIDGE-RIPPLE-024** — While `StateLayer` is true, the control shall be tinted with the ink color, made opaque, at 10 % while pressed, else 10 % while focused with `:focus-visible`, else 8 % while the pointer is over it, else not at all.
  - Tests: `RippleGestureTests.TheStateLayerTintsTheHostWhileHoveredFocusedOrPressedAndClearsAfterwards`
- **BRIDGE-RIPPLE-025** — The tint shall fade between levels over `Recipes.Tokens.Short`, under the control's rounded corners.
  - Tests: `RippleGestureTests.TheStateLayerTintsTheHostWhileHoveredFocusedOrPressedAndClearsAfterwards`
- **BRIDGE-RIPPLE-026** — While `StateLayer` is false and motion is not reduced, the control shall show no tint.
  - Tests: untested

### Reduced motion

- **BRIDGE-RIPPLE-027** — While `PrestoDefaults.ReducedMotion` is set at the press, the press shall show no ink and shall show the 10 % pressed tint at once for as long as it is held, whatever `StateLayer` says.
  - Tests: `RippleGestureTests.UnderReducedMotionAPressShowsOnlyThePressedTintAtOnce`, `RipplePageTests.PressingGrowsInkFromThePressPointOrOnlyTintsUnderReducedMotion`
- **BRIDGE-RIPPLE-028** — While `PrestoDefaults.ReducedMotion` is set when the tint changes, the state layer shall change without fading.
  - Tests: `RippleGestureTests.UnderReducedMotionAPressShowsOnlyThePressedTintAtOnce`

### Defaults

- **BRIDGE-RIPPLE-029** — When `Ripple.Enabled` is not set, the control shall not ripple.
  - Tests: untested
- **BRIDGE-RIPPLE-030** — When `Ripple.Bounded` is not set, the ripple shall be bounded.
  - Tests: `RippleTests.TheInkGrowsFromThePressPoint`
- **BRIDGE-RIPPLE-031** (inherited) — When `Ripple.StateLayer` is not set, the control shall have no state layer.
  - Tests: untested

## Invariants

- The UI thread only posts press, release and tint messages; the ink and tint are drawn on the render thread and never touch layout or input.
- An unbounded ripple's visual draws outside the control only because it turns off its own clip and widens its render bounds to the circle.

## Edge cases

- A host whose child-visual slot is used by something else (such as a `CompositorCanvas`) cannot carry a ripple: the ripple takes the slot.
- A theme switch applies to the next press; a running ripple keeps its color.
- `ReducedMotion` is read at each press (and at each tint change), never subscribed to (CORE-REDUCED: the flag has no change notification).

## Threading and lifetime

- Attached-property handlers and input run on the UI thread; each host's `RippleHandler` runs on the render thread with the compositor's clock.
- The tracker is held by the control through a private attached property and released when `Enabled` turns false; detaching drops the visual but keeps the tracker for the next attach.

## Reduced motion

- No ink; the pressed tint shows at once while held, and the state layer changes without fading (BRIDGE-RIPPLE-027, -028).

## Allocation

- Fifty concurrent ripples stay within 20 000 bytes per frame (measured about 16 900, mostly Avalonia redrawing invalidated custom visuals); brushes are cached per faded color.
  - Pinned by `AllocationTests.FiftyConcurrentRipplesDrawWithinTheirBudget`.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **One composition custom visual per host draws the ripples.** Each host gets one `CompositionCustomVisual` through `SetElementChildVisual` that draws all its ripples on the render thread under the host's rounded clip, while the UI thread only posts press, release and cancel; a `CompositorCanvas` host cannot carry one. Reason: on the Win32 bench every candidate held the frame rate, so allocation and clipping decided: the per-host visual costs only what it draws (+13.5 KB for 50 buttons), allocates nothing idle and clips correctly. A solid circle is the default because a radial-gradient soft edge doubled the cost. Rejected: a UI-thread layer (most allocation, per-frame drawings); compositor ellipses (a control and a mirror per press); one shared window overlay (cheapest, but clipped only to the host rectangle, so scrolled-out ink draws below the scroller); a soft edge by default; Compose's 30 % start radius and center drift, since the bench read as a ripple without them.
- **Ink and state layer draw over the content.** Both draw translucent (24 % ink, 8–10 % state) above the host's content. Reason: as Compose's indication does; the child visual composes after the host's children, drawing between background and content would need a template change, and translucent ink keeps text readable.
- **Switches, color, geometry and timing.** An explicit `Ripple.Enabled` (default false) turns ripples on and `Ripple.Color` overrides a theme-accent default (`SystemAccentColor`, `SystemAccentColorLight2` on dark, looked up per press; ink at 24 % alpha, state layer at Material 3's 8/10/10 %). Bounded ink grows from the fixed press point to the farthest corner, unbounded ink from the center to half the diagonal with only ancestor clips; growth takes `Medium` and `Ease`, the release fade `Short`, linear. Reason: `Enabled` lets a style turn ripples on without picking a color, the lighter shade reads on dark surfaces, and the per-press lookup applies a theme switch on the next press.
- **Input sees handled events; touch in a scroller waits for the tap timeout; the reduced form only tints.** Pointer and key events are handled with `handledEventsToo`, Enter and Space ripple from the center, leaving the host or losing capture cancels, and a touch press inside a `ScrollViewer` waits 100 ms before showing ink (mouse and pen never wait). Under reduced motion there is no ink, the pressed tint shows at once and the state layer changes without fading. Reason: `Button` marks its presses handled; 100 ms is Android's (and Compose's) tap timeout, so a fling does not ripple, and a touch scroll takes the capture from the pressed child.
- **The flag has no change notification.** See CORE-REDUCED.

## Non-goals

- A soft-edged (radial gradient) ink, Compose's start radius and center drift.
- Drawing under the content, which would take a template change.
- One shared window overlay for all ripples, which escapes ancestor clips.

## Open items

- Design mode (BRIDGE-RIPPLE-004), other mouse buttons (-006), the foreground fallback color (-023) and no tint without `StateLayer` (-026) have no test.
- manual — Browser and Android were never measured; the per-host render-thread visual assumes a separate render thread, which a single-threaded Browser build may not have.
