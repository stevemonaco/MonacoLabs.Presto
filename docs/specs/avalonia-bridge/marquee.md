---
id: BRIDGE-MARQUEE
title: Marquee
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/Marquee.cs
  - MonacoLabs.Presto.Avalonia/Controls/MarqueeDirection.cs
  - MonacoLabs.Presto.Avalonia/Controls/MarqueeMode.cs
types:
  - Marquee
  - MarqueeDirection
  - MarqueeMode
tests:
  - MarqueeTests
  - BridgeAllocationTests
  - MarqueePageTests
upstream: []
depends:
  - BRIDGE-TRANSFORMS
  - BRIDGE-TOKENS
  - BRIDGE-INIT
  - CORE-ANIMATABLE
  - CORE-TIMER
  - CORE-REDUCED
---

# Marquee

## Purpose

`Marquee` is a `ContentControl` for continuously scrolling content: logo walls, tickers, a title too long for its label. Its content is laid out once and repeated end to end, `Gap` apart, on a private strip that moves along the axis through the `X`/`Y` shorthand (BRIDGE-TRANSFORMS) at a speed held in an `Animatable` channel (CORE-ANIMATABLE), so speed changes, direction flips and pauses ease. It has fade edges, a `WhenOverflowing` mode and a static reduced-motion form.

## Requirements

### Layout and copies

- **BRIDGE-MARQUEE-001** — The marquee shall measure its content unconstrained along the scroll axis (horizontal for `Left` and `Right`, vertical for `Up` and `Down`) and constrained to the available size across it.
  - Tests: `MarqueeTests.TheContentIsMeasuredUnconstrainedAlongTheAxis`
- **BRIDGE-MARQUEE-002** — The period shall be the content's length along the axis plus `Gap`.
  - Tests: `MarqueeTests.ControlContentHasOneVisualParentAndCopiesStartAPeriodApart`, `MarqueeTests.TheCopyCountFollowsAResizeAndAGapChangeReusingTheCopies`
- **BRIDGE-MARQUEE-003** — While scrolling, the marquee shall show `ceil(viewport / period) + 1` instances of the content, each one period after the previous and each the content's length along the axis by the marquee's size across it; for `Left` and `Up` the content is the first instance, and for `Right` and `Down`, which move toward the content's end, the first instance is one period before the content.
  - Tests: `MarqueeTests.ControlContentHasOneVisualParentAndCopiesStartAPeriodApart`, `MarqueeTests.VerticalDirectionsLayCopiesOutDownTheStrip`
- **BRIDGE-MARQUEE-004** — When `Content` is not a `Control`, each copy shall be built from the resolved `ContentTemplate` over the same `Content`, so elements in it take input.
  - Tests: `MarqueeTests.DataContentCopiesAreRealButtonsThatTakeClicks`, `MarqueePageTests.ClickingANameInACopyUpdatesTheReadout`
- **BRIDGE-MARQUEE-005** — When `Content` is a `Control`, it shall have exactly one visual parent, and each copy shall be a `VisualBrush` mirror of it, sized to it, that is not hit-test visible.
  - Tests: `MarqueeTests.ControlContentCopiesAreMirrorsThatTakeNoHits`, `MarqueeTests.ControlContentHasOneVisualParentAndCopiesStartAPeriodApart`
- **BRIDGE-MARQUEE-006** — Each copy host, and not the content, shall set `AutomationProperties.AccessibilityView` to `Raw` and `KeyboardNavigation.TabNavigation` to `None`, so Tab never lands in a copy.
  - Tests: `MarqueeTests.CopiesAreRawAndOutOfTheTabOrder`
- **BRIDGE-MARQUEE-007** — When the marquee is arranged at a new size, or the content's length, `Gap` or the axis changes, the marquee shall recompute the period and the copy count, keeping the copy hosts it already has and adding or removing only the difference.
  - Tests: `MarqueeTests.TheCopyCountFollowsAResizeAndAGapChangeReusingTheCopies`, `MarqueeTests.WhenOverflowingFittingContentIsShownOnceAlignedAndOverflowingContentGetsCopies`
- **BRIDGE-MARQUEE-008** (inherited) — When `Content` changes between a `Control` and data, the marquee shall replace its copies with the other kind.
  - Tests: `MarqueeTests.SwitchingBetweenControlAndDataContentReplacesTheCopies`
- **BRIDGE-MARQUEE-009** (inherited) — The marquee shall measure as the content's desired size, its length along the axis capped at the available size.
  - Tests: `MarqueeTests.TheMarqueeMeasuresAsItsContentCappedAlongTheAxis`
- **BRIDGE-MARQUEE-010** — When `ClipToBounds` is not set, the marquee shall clip its content to its bounds.
  - Tests: `MarqueeTests.TheDefaults`

### Motion

- **BRIDGE-MARQUEE-011** — While scrolling, the marquee shall advance the phase every engine frame by the current speed times the elapsed engine time in seconds, whatever `Engine.TimeUnit` is, wrapped into [0, period); `Engine.Speed` and `TimeDilation` scale it as they scale any engine time.
  - Tests: `MarqueeTests.ThePhaseAdvancesAtSpeedDipsPerSecondInEitherTimeUnit`, `MarqueeTests.ThePhaseWrapsSeamlesslyAtThePeriod`, `MarqueePageTests.TheLogoWallScrolls`
- **BRIDGE-MARQUEE-012** — The marquee shall write the phase to the strip's `X` shorthand (horizontal) or `Y` shorthand (vertical), negated for `Left` and `Up`.
  - Tests: `MarqueeTests.ThePhaseAdvancesAtSpeedDipsPerSecondInEitherTimeUnit`, `MarqueeTests.ASameAxisDirectionFlipPassesThroughZeroAndKeepsThePhase`, `MarqueeTests.AnAxisChangeResetsThePhase`
- **BRIDGE-MARQUEE-013** — When the period changes, the marquee shall keep the phase modulo the new period.
  - Tests: `MarqueeTests.APeriodChangeKeepsThePhaseModuloTheNewPeriod`
- **BRIDGE-MARQUEE-014** — When `Speed` changes, the current speed shall ease from its displayed value to the new one over `Recipes.Tokens.Medium` with `Recipes.Tokens.Ease`.
  - Tests: `MarqueeTests.ASpeedChangeEasesFromTheSpeedShown`
- **BRIDGE-MARQUEE-015** — When `Direction` changes along the same axis, the current speed shall ease through 0 to the opposite sign, keeping the strip where it is (the phase re-wrapped into the new direction's period).
  - Tests: `MarqueeTests.ASameAxisDirectionFlipPassesThroughZeroAndKeepsThePhase`
- **BRIDGE-MARQUEE-016** — When `Direction` changes axis, the marquee shall reset the phase to 0, clear the old axis's shorthand and re-lay out the copies along the new axis.
  - Tests: `MarqueeTests.AnAxisChangeResetsThePhase`
- **BRIDGE-MARQUEE-017** — When `Speed` is 0, the marquee shall ease to rest as for a pause; when the period is 0 or less, it shall rest at phase 0; either way it shall stop requesting frames.
  - Tests: `MarqueeTests.ZeroSpeedOrAZeroPeriodRests`
- **BRIDGE-MARQUEE-032** — On its first attach, the current speed shall start at its target rather than ease up from 0.
  - Tests: `MarqueeTests.ThePhaseAdvancesAtSpeedDipsPerSecondInEitherTimeUnit`
- **BRIDGE-MARQUEE-033** — While scrolling, the loop shall show no seam: at the wrap the strip lands where an identical instance stood, and a resize while scrolling neither opens a gap nor jumps the content.
  - Tests: manual — watch the logo wall's wrap (Fieldnotes into Lumen Parade) and resize the window while it scrolls on the Desktop sample, `--page marquee`.

### Pausing

- **BRIDGE-MARQUEE-018** — While `IsPaused` is true, the current speed shall ease to 0, and when it becomes false, back to `Speed`.
  - Tests: `MarqueeTests.APauseDeceleratesThenStopsRequestingFrames`, `MarqueePageTests.ThePauseToggleSetsIsPausedAndTheWallSettles`
- **BRIDGE-MARQUEE-019** — While `PauseOnHover` is true and the pointer is over the marquee, or keyboard focus inside it arrived by Tab or directional navigation, the marquee shall pause as for `IsPaused`, without changing `IsPaused`.
  - Tests: `MarqueeTests.HoverAndKeyboardFocusPauseWithoutWritingIsPaused`
- **BRIDGE-MARQUEE-020** — Focus that arrives by pointer or programmatically shall not pause the marquee.
  - Tests: `MarqueeTests.HoverAndKeyboardFocusPauseWithoutWritingIsPaused`
- **BRIDGE-MARQUEE-021** — Once paused and settled at speed 0, the marquee shall pause its timer, so the engine stops requesting frames, and resume it when the pause ends.
  - Tests: `MarqueeTests.APauseDeceleratesThenStopsRequestingFrames`
- **BRIDGE-MARQUEE-034** — A pause and its release shall read as a deceleration to a stop and an acceleration back, not a snap.
  - Tests: manual — hover the logo wall and press Pause on the Desktop sample, `--page marquee`; DevTools input cannot send hover.

### Overflow mode

- **BRIDGE-MARQUEE-022** — While `Mode` is `WhenOverflowing` and the content's length is no greater than the viewport's, the marquee shall show the content once with no copies, placed on the axis by `HorizontalContentAlignment` (horizontal) or `VerticalContentAlignment` (vertical).
  - Tests: `MarqueeTests.WhenOverflowingFittingContentIsShownOnceAlignedAndOverflowingContentGetsCopies`
- **BRIDGE-MARQUEE-023** (inherited) — While the content fits as in BRIDGE-MARQUEE-022 and its alignment on the axis is `Stretch`, the content shall fill the viewport along the axis.
  - Tests: `MarqueeTests.FittingContentStretchedOnTheAxisFillsTheViewport`
- **BRIDGE-MARQUEE-024** — While the content fits as in BRIDGE-MARQUEE-022, the marquee shall rest at phase 0 and not tick.
  - Tests: `MarqueeTests.WhenOverflowingRestsWhileTheContentFitsAndStartsFromZeroOnceItOverflows`
- **BRIDGE-MARQUEE-025** — When the content starts overflowing in that mode, the marquee shall start from phase 0, and when it stops overflowing, it shall return the strip to rest at 0 and stop ticking.
  - Tests: `MarqueeTests.WhenOverflowingRestsWhileTheContentFitsAndStartsFromZeroOnceItOverflows`, `MarqueePageTests.TheOverflowTitleRestsWhileShortAndScrollsOnceLong`

### Fade edges

- **BRIDGE-MARQUEE-026** — While `FadeEdges` is above 0, the marquee shall apply an opacity mask along the axis that ramps from transparent to opaque over `FadeEdges` DIPs at both ends, with the ramp capped at half the viewport.
  - Tests: `MarqueeTests.TheFadeRampIsCappedAtHalfTheViewport`
- **BRIDGE-MARQUEE-035** — The fade shall show at both ends of the axis and stay still while the content moves under it.
  - Tests: manual — watch the logo wall's and the ticker's edges on the Desktop sample, `--page marquee`.

### Reduced motion

- **BRIDGE-MARQUEE-027** — While `ReducedMotion` is set, read when it is next checked (each attach, size change and frame), the marquee shall set the phase to 0, stop ticking and show the content once with no copies: placed as in BRIDGE-MARQUEE-022 when it fits, and otherwise start-aligned and clipped.
  - Tests: `MarqueeTests.UnderReducedMotionTheMarqueeRestsAtZeroWithNoCopies`, `MarqueePageTests.UnderReducedMotionNothingMoves`

### Lifetime

- **BRIDGE-MARQUEE-028** — When the marquee is detached, it shall stop its timer and keep its phase and current speed; when attached again, it shall continue from that phase.
  - Tests: `MarqueeTests.DetachAndReattachKeepThePhase`
- **BRIDGE-MARQUEE-029** — While the engine is hidden or frozen, the phase shall not advance, and it shall continue without a jump afterwards; the first frame after the marquee's timer resumes only takes the time.
  - Tests: `MarqueeTests.HiddenAndFrozenHoldThePhaseAndResumeWithoutAJump`

### Defaults

- **BRIDGE-MARQUEE-030** — When not set: `Speed` 60, `Direction` `Left`, `Gap` 24, `FadeEdges` 0, `PauseOnHover` true, `IsPaused` false, `Mode` `Always`.
  - Tests: `MarqueeTests.TheDefaults`
- **BRIDGE-MARQUEE-031** — A binding to `IsPaused` that names no mode shall bind it two-way.
  - Tests: `MarqueeTests.AnIsPausedBindingWithNoModeIsTwoWay`

## Invariants

- The strip's first child is the content's presenter; every other child is a copy host.
- Copy hosts are all of one kind, matching whether `Content` is a `Control`.
- While scrolling, the strip's offset lies in (-period, 0] for `Left` and `Up` and in [0, period) for `Right` and `Down`; at rest the shorthand is cleared.

## Edge cases

- Content of zero length, a period of 0 or less (a negative `Gap`), or a 0-long viewport: no copies, the content shown once.
- With `Mode` `Always` and content that fits, the marquee still scrolls with copies.
- A `FadeEdges` above 0 replaces any `OpacityMask` set on the marquee, and returning it to 0 clears it.
- Overflowing content shown once (reduced motion, or a period of 0 or less) is start-aligned at its full length whatever the content alignment, so `Stretch` never squeezes it into the viewport.
- A negative `Speed` runs against `Direction`.
- A property set while detached takes effect at the next attach, easing from the speed the marquee had.
- Hover and keyboard focus pause only while `PauseOnHover` is true; turning it off resumes.

## Threading and lifetime

- UI thread only. The copy hosts live as long as the marquee and are reused across layouts.
- The speed channel (an `Animatable` over a private one-property target, resolved through its member table rather than reflection, CORE-TARGETS) is created at the first attach, and the endless engine timer at the first start; both live as long as the marquee. The timer runs only while attached, scrolling and not settled at speed 0; detaching pauses it and keeps the phase.
- Engine time stops while `Engine.Hidden` or `Frozen`, so the phase holds and resumes from where it was; `Engine.Resume` rebases the timer, and the marquee ignores the frame its own resume skips ahead (BRIDGE-MARQUEE-029).

## Reduced motion

- Static, not slower: the marquee rests at phase 0 with the content shown once, start-aligned and clipped when it overflows (BRIDGE-MARQUEE-027). The flag is read in one place at each attach, arrange and frame, so a flag turned on mid-scroll stops the strip on its next frame; one turned off is seen at the next attach or size change.

## Allocation

- The per-frame tick allocates nothing of its own: it reads the speed channel's target field and writes the strip's shorthand, whose property change is the 80 bytes Avalonia raises (`BridgeAllocationTests.AScrollingMarqueeTicksWithinBudget`). A speed change allocates only the `Animatable` retarget. The fade mask is rebuilt only when the viewport, `FadeEdges` or the axis changes, and a layout allocates only the copy hosts it adds.

## Provenance

- No upstream counterpart.

## Decisions

- **One `ContentControl`, not an `ItemsControl`.** Most marquees are one row, and a list is a `ContentTemplate` holding an `ItemsControl`. Reason: a single content repeated end to end is the unit of the loop; an `ItemsControl` would need its item containers generated several times over, which its generator does not support. Rejected: `ItemsSource`/`ItemTemplate` on the marquee itself.
- **The marquee is untemplated.** It builds its strip in code and holds `Template` at a local null. Reason: Avalonia 12 applies a theme's `ContentControl` template to a derived control that has no theme of its own, which would replace the strip and give a `Control` content a second parent. Rejected: shipping a control theme for one strip.
- **Data content is copied as real elements; control content as `VisualBrush` mirrors.** A mirror paints the content's presenter, so it lines up with the content's slot whatever its margin or alignment. Reason: real copies keep interaction working in a logo wall of links, and a control instance cannot be parented twice. Rejected: always mirroring (copies dead to the pointer); requiring data content (breaks `<presto:Marquee><TextBlock …/></presto:Marquee>`).
- **Copies are invisible to accessibility and keyboard.** Reason: duplicated announcements and tab stops are the classic marquee accessibility failure.
- **`Right` and `Down` start one period before the content.** Reason: the strip moves toward the content's end, so the period before the content scrolls into view first; with it, `ceil(viewport / period) + 1` instances cover the viewport at every phase in either direction.
- **The fade is the marquee's own opacity mask, with relative stops.** Reason: on the marquee it stays still while the strip moves, and relative stops change only with the viewport, `FadeEdges` or the axis, so it is never rebuilt per frame.
- **The engine drives the phase, not the compositor.** The marquee runs an engine timer that advances the phase by the current speed times elapsed engine time, and the current speed is an `Animatable` channel, so every speed change eases from the speed shown and a pause decelerates. Reason: compositor loops change speed only by recompiling (BRIDGE-THREAD-035/037), so pausing would snap; engine-driven motion also tests deterministically under `ManualFrameSource`, pauses with `Engine.Hidden` and the DevTools freeze, and resumes without a jump. Rejected: an infinite linear `X` loop on the compositor.
- **No `RunOnCompositor` mode in v1.** Reason: on the render thread, pauses and speed changes could not ease (BRIDGE-THREAD-035/037). Revisit if the sample stutters under UI-thread load.
- **`Speed` is in DIPs per second whatever the engine's `TimeUnit`.** Reason: it is a physical rate, not a duration, so seconds mode must not rescale it.
- **Direction is signed velocity.** Switching between `Left` and `Right` (or `Up` and `Down`) eases through 0 and keeps the phase; switching axis resets the phase to 0. Reason: a direction toggle should look like a reversal, not a jump.
- **Pausing on hover and focus follows `Pager`'s autoplay rules.** Focus that arrives by Tab or directional navigation pauses; focus by pointer or programmatically does not (BRIDGE-PAGER-023/024). Reason: one consistent hold rule across controls, and a pause mechanism is what WCAG 2.2.2 (Pause, Stop, Hide) asks of auto-moving content.
- **Hover stops the marquee rather than slowing it.** Reason: a slowdown does not satisfy WCAG 2.2.2. Rejected: a `HoverSpeed` factor.
- **Reduced motion is static, not slower.** Reason: continuous motion is exactly what reduced-motion users opt out of; a slower crawl still moves.
- **The phase is advanced from an endless timer's own time.** The timer's `CurrentTime` delta, divided by 1000 in milliseconds mode, is the elapsed seconds. Reason: a timer's time already carries `Engine.Speed`, `TimeDilation` and the hidden and frozen rebasing, so the marquee inherits them for free. Rejected: the engine's `DeltaTime`, which is not rebased after a hidden interval and ignores `Engine.Speed`.
- **The speed channel is read from its target, not through `Animatable.Get`.** Reason: the animation writes the private target's field every frame, so reading it costs nothing, where `Get` canonicalizes the property name per call.
- **Speed 0 eases to rest like a pause.** Reason: a `Speed` binding driven to 0 should decelerate as any other speed change does; only a period of 0, where nothing can scroll, rests at once.
- **The defaults are `Speed` 60 DIPs per second and `Gap` 24 DIPs**, kept after the gallery's Marquee page. Reason: at 60 a 16 DIP title and a row of pill buttons stay readable while they move, and 24 is about one and a half ems at body size, enough to mark where the loop restarts without leaving a hole.
- **The first attach starts at speed.** Reason: a marquee that appears already moving reads as continuous; easing up from 0 on every page load would be a start-up flourish nobody asked for.

## Non-goals

- Drag or wheel to scrub the strip.
- A bounce (back-and-forth) mode for overflowing labels.
- An `ItemsSource` on the marquee itself.
- Per-item speed or parallax lanes.
- A `MotionValue` exposing the phase.
- Bringing a keyboard-focused copy or element into view.

## Open items

None.
