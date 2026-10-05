# Gaps 2 — show and hide, scrolling, lifetime, popups and prebuilts

Phases 55–65 in [PLAN.md](PLAN.md). This plan comes from the post-Phase-54 review of traditional UI animation gaps
(3D and shaders excluded), checked against the committed code (`fe128fe`) and the Avalonia 12.1.2 assemblies. Every
phase is a design sketch: it settles its open decisions in its section here, or in its own plan document if it
outgrows one, before it starts. Phases 57, 59, 60, 61 and 62 begin with a spike, because they rest on Avalonia
behaviour nobody has verified yet.

| Phase | Gap | Scope | Size |
|---|---|---|---|
| 55 | Reduced-motion holes | core + bridge | small |
| 56 | Discrete values | core + bridge | small–medium |
| 57 | Show and hide through `IsVisible` | bridge | medium |
| 58 | Text exits | bridge | small–medium |
| 59 | Animated scrolling | bridge | medium |
| 60 | Lifetime and idling | core + bridge | medium |
| 61 | Popups and flyouts | bridge | medium |
| 62 | Press ripple | bridge | medium |
| 63 | Prebuilts 3: selection indicator, swipe actions, sheets and drawers | bridge + sample | large |
| 64 | Hosts lifted from the samples: carousel, toasts, modal, accordion, collapsible pane | bridge + sample | large |
| 65 | Effects and gestures lifted from the samples: scroll reveal, progress ring and draw recipes, pull to refresh, circular reveal, flip card | bridge + sample | large |

Phase 57 needs Phase 56's visibility hold. Phase 58 uses Phase 57's live path for hidden text, and Phase 61 uses it for
`Popup.IsOpen`. Phase 63's swipe-to-dismiss ends in Phase 57 or a `Presence.Exit`; 64c and 64d use Phase 57's hide and `EnterOnShow`,
and 64e builds PLAN §7's `GridLength` codec. The other phases are independent.
Phase 55 comes first because each later phase states its reduced form against it.

## 0. Not in this plan

- **Shaders** stay a non-goal (IMAGE-EFFECTS-PLAN §3). Phase 62 builds the ripple without them and measures first.
- **Codecs and shorthands already listed in PLAN §7** (`GridLength`, `Matrix`, `Follow.RotateX/RotateY/Draw`) stay
  there as candidates.
- **Colour interpolation spaces** stay Phase 30 ([COLOR-SPACES-PLAN.md](COLOR-SPACES-PLAN.md)).
- **The rest of PLAN §7's open items** that this plan does not name stay where they are.

## 1. Phase 55 — Reduced-motion holes

### 1.1 Gap

PLAN §7 lists four places where `PrestoDefaults.ReducedMotion` is ignored, each with a sample working around it:

- `Scroll.Speed` keeps its parallax under the flag (ScrollStory resets its layers' speeds in `OnAttached`).
- The `(duration, ease)` overloads of `AnimateLayout`, `AnimateContentSize` and `AnimateSharedElement` build a
  `LayoutMotion` directly and keep easing (the Layout page switched to `Recipes.Reflow` builders).
- `AnimateSharedElement` has no reduced alternative (Storefront checks the flag by hand before its cart flight).
- Dictionary-form params (`AvaloniaAnimationParams` and the core `AnimationParams`) have no `WhenReduced`
  (MorphLab, Typography, TitleSequence, Dashboard and Carousel read the flag by hand).

### 1.2 Design sketch

- **`Scroll.Speed`** behaves as speed 1 (no translation) while the flag is set.
- **Decided (PLAN §7 decision 71): no change notification;** the flag is read when each trigger fires (`Offset`
  notification, move start).
- **Duration overloads** land at once under the flag, as `Recipes.Reflow`'s reduced form does.
- **Dictionary params** (PLAN §7 decision 72) gain `WhenReduced`, an `AnimationParams?` (and an `AvaloniaAnimationParams?` in the bridge)
  with the builder's semantics (decision 35): when the flag is set, its property tweens replace this one's, and the
  settings it sets override this one's. Timers have no properties, and a timeline's children each pick their own form,
  so `TimerParams` and `TimelineParams` get nothing.
- **Rule for the rest of this plan:** every new trigger, recipe and control in Phases 56–65 states its reduced form in
  its own section, and its tests run under both settings.

### 1.3 Tests and sample

- **Tests:** a parallax element stays at its layout position under the flag and resumes when it clears; each duration
  overload lands on the first frame; `WhenReduced` on dictionary params replaces the properties and keeps the
  callbacks, the stagger and the settings it does not override.
- **Sample:** remove the workarounds in ScrollStory, Storefront, Layout, MorphLab, Typography, TitleSequence,
  Dashboard and Carousel. `GalleryPageTests` already runs every page under Reduced.

## 2. Phase 56 — Discrete values

### 2.1 Gap

`AvaloniaPropertyAccessor` throws `NotSupportedException` for any property type that has no codec
(`AvaloniaObjectTargetAdapter.cs:244`), and the core's `ObjectTargetAdapter` does the same (`ObjectTargetAdapter.cs:74`).
So none of these work:

- `bool`: `IsVisible`, `IsHitTestVisible`, `IsEnabled`, `ClipToBounds`.
- Enums: `HorizontalAlignment`, `FontWeight`, `Dock`.
- Any other non-interpolable type.

That means a timeline cannot `Set(IsVisible, false)`, a keyframe cannot switch hit-testing off, and `Presto.Set` throws
on them. Other libraries handle this: CSS animates discrete properties with `transition-behavior: allow-discrete`, and
GSAP's `autoAlpha` fades opacity and switches `visibility` with it.

### 2.2 Design sketch

- **A core `DiscreteCodec`** is the fallback for any type the codecs do not cover, in both adapters, replacing the
  throw. It holds one channel that runs 0 → 1, keeps the from and to objects, and its `Encode` picks one of them.
  Boxed values are cached per tween, so a frame allocates nothing.
- **Strings are not affected.** Non-numeric strings keep upstream's complex-value path. *Checked:* upstream 4.5.0
  decomposes `'left'` → `'right'` as a complex value with no numbers and writes the end string `'right'` from the first
  frame; `ValuesTests.NonNumericStringsStayComplexAndWriteTheEndStringFromTheFirstFrame` pins it.
- **Flip rule, decided (PLAN decision 73): a hybrid.** A `TweenParams.Discrete` rule (`DiscreteRule`: `Midpoint`,
  `AtStart`, `AtEnd`, `TrueThroughout`, `FalseThroughout`) overrides, per property or keyframe, the default the
  property's `DiscreteCodec` was built with. The core's default is CSS's `Midpoint` (switch at eased progress 0.5); the
  bridge builds its codecs with its own defaults (`IsVisible` → `TrueThroughout`, so a fade-out stays visible until it
  ends and a fade-in shows from its first frame). *Decided (PLAN decision 74):* every other bridge property, including
  `IsHitTestVisible` and `IsEnabled`, keeps `Midpoint`; a tween that wants hit-testing off for its whole run sets
  `Discrete = FalseThroughout`. The throughout rules apply only when exactly one end is that `bool`, and act as
  `Midpoint` otherwise.
- **Keyframes** flip per segment by the same rule. Seeks and reversals work unchanged, because the value is a function
  of progress.
- **What rejects discrete values:** relative values (`+=`) and `Blend` composition throw with a clear message. An
  `Animatable` retarget snaps (its tween starts and ends on the current value with no relative `"+=0"`, and `Set`
  writes the new value to both ends; a number given for a `bool` or an enum is converted to it). A compositor animation throws, as it does today for any UI-thread-only property.
- **No `AutoAlpha` shorthand (PLAN decision 74).** GSAP's `autoAlpha` was built and then removed: `IsVisible = false`
  takes the element out of layout, unlike CSS `visibility: hidden`, so a fade that ends hidden jumps, and Phase 57's
  `Hold` mode covers the same need from the `IsVisible` side. The internal `VisibilityHold` it was built on stays: an
  owner-keyed `Animation`-priority binding where the owner that last changed its value wins, and releasing the last one
  disposes the binding and uncovers the base value. Phase 57's hide holds `true` through it.

### 2.3 Tests and sample

- **Tests:**
  - Midpoint flip in both directions; `IsVisible` true throughout; seek and reverse across the flip; keyframes.
  - `Revert` restores the value; `Presto.Set` on a `bool` and an enum; a plain object's `bool` in the core.
  - Relative values and `Blend` throw.
  - Both allocation suites stay at budget.
- **Sample:** Value types page rows for `IsHitTestVisible`, `FontWeight` and `HorizontalAlignment`.

## 3. Phase 57 — Show and hide through `IsVisible`

### 3.1 Gap

`Presence` classifies attach and detach only (LIFECYCLE-PLAN §2). Most Avalonia apps show and hide with
`IsVisible="{Binding …}"`, and templates collapse the same way (an `Expander`'s content, a `TreeViewItem`'s
`ItemsPresenter`). Today a fade-out needs a `Motion` state plus code that hides the element at the end. PLAN §7's
"expanding a section does not re-enter its pages" is the same gap seen from the children's side: the gallery nav works
around it with a `collapsed` state whose `Leave` is `MainView.PageUnfolded`.

### 3.2 Design sketch

- **An element's own `IsVisible` flip is a lifecycle moment.** false → true plays `Enter` (fallback `Arrive`), or
  `Initial` while its view is still in its first load. true → false plays `Exit`. The classification log reports them
  as `Show` and `Hide`.
- **Hide plays on the live element.** The tracker sees the effective value change, in the same dispatcher turn so no
  frame renders the element hidden, holds `IsVisible = true` at `Animation` priority (Phase 56's hold), plays the exit,
  then releases the hold, which uncovers the base `false`. The slot stays in layout during the exit, as with CSS;
  `Animate.ItemLayout` or `Animate.ContentSize` closes the gap afterwards.
- **`Presence.HideMode`, decided (PLAN decision 75): `Hold` by default.** The spike (§3.3) found the live path
  workable, so `Ghost` (layout collapses at once and the exit plays on a ghost, as for a detach, so neighbours close up
  during the exit) is opt-in. A hidden element snapshots as nothing, so `Ghost` takes its snapshot under a momentary
  hold in the same notification; this is the one exception to Phase 31's "an element leaving invisible is never
  ghosted", and an element already hidden by an ancestor or at opacity 0 still plays nothing. Phase 56's
  `VisibilityHold` stays as built.
- **Self-inflicted flips are ignored by the base value (PLAN decision 76).** The tracker records
  `GetBaseValue(IsVisible)` (the default `true` when nothing below `Animation` is set) and, on each `IsVisible` change,
  skips args at `Animation` priority and otherwise acts only when the base differs from the one recorded. Neither test
  works alone: a hold's release carries the priority of the value it uncovers (`LocalValue`, `Template`, `Unset`), and
  a hold pushed inside a template-priority flip still reads the old base in its nested notification. Nothing reports a
  base change under a hold, so while it holds its own exit the tracker reads the base each frame and at the end.
- **Shown again mid-exit** retargets from where the element stands, with no snap: under `Hold` the held exit plays back
  (`Reverse`) to the values it started from, since an entrance's `from` would snap and a from-less one would land on the
  mid-exit value; under `Ghost` the ghost is dismissed and `Enter` plays (PLAN decision 78).
- **Revealed by an ancestor (closes the §7 item), decided (PLAN decision 77).** When a host's own base `IsVisible`
  flips to true, its descendants with `Presence.Enter` or `Arrive` (`Enter ?? Arrive`) that are effectively visible
  after the flip replay as one batch keyed by the host, in visual-tree order, so order staggers work (decision 23).
  This is opt-in on the host (`Presence.EnterOnShow`), so expanding a deep subtree does not replay everything by
  default; a descendant gets no notification of its own when an ancestor shows it. Hiding an ancestor plays nothing
  beyond the host's own `Exit`. In the Fluent templates the host is a template part (`Expander` →
  `Border#ExpanderContent`, `TreeViewItem` → `PART_ItemsPresenter`), set through a `/template/` style. The batch plays
  laid out, in the pass that shows it; the host's own entrance is not part of it and starts in the same frame. A host
  shown in the same pass as an `EnterOnShow` ancestor joins that ancestor's batch, so nothing replays twice.
- **Reduced:** `ReduceAll` hides and shows at once; builders pick their `WhenReduced`.

### 3.3 Spike (done)

Pinned by `VisibilitySpikeTests` (Avalonia 12.1.2):

- **Re-asserting `IsVisible` at `Animation` priority inside its change notification works.** The element is
  effectively visible again before anything renders, keeps its slot, and costs at most one extra measure, after which
  layout settles. `PropertyChanged` handlers subscribed after the one that holds see the nested `true` before the
  outer `false`; Avalonia's bindings and `GetObservable` read the current value and stay right.
- **A value changed under the hold wins when it is released.** A local value, style trigger or template binding
  changed during the hold raises no change notification (`Animatable` seals `OnPropertyChangedCore`, and
  `Visual.IsEffectivelyVisibleChanged` is internal), and the release uncovers it.
- **Telling a base flip from Presto's own writes** needs the base value and the priority together (§3.2).
- **The Fluent templates toggle `IsVisible` on a template part, at `Template` priority.** An `Expander` hides
  `Border#ExpanderContent`; its `PART_ContentPresenter` and content stay attached and visible, and its `ContentTransition`
  is null by default. A `TreeViewItem` hides `PART_ItemsPresenter`
  and keeps its child containers attached, which is why expanding never re-enters them. Holding the part keeps the
  collapsing content drawn.
- **The gallery nav flips nothing.** Its custom `TreeViewItem` template collapses with `MaxHeight = 0` on
  `PART_ItemsPresenter` (`Showcase.axaml`), so dropping its `collapsed` workaround means collapsing through `IsVisible`.

### 3.4 Tests and sample

- **Tests:**
  - A bound hide plays the exit and then hides; the slot is held, and `ItemLayout` closes it afterwards.
  - Show, then show again mid-exit.
  - Hidden at load and shown later classifies as `Enter`.
  - `EnterOnShow` staggers revealed children; without it nothing replays.
  - `Ghost` mode collapses at once.
  - `ReduceAll`.
  - Detaching mid-hold releases the hold.
- **Sample:** a Visibility page (a bound toggle, an expander-style settings list), and the gallery nav drops its
  `collapsed` state workaround.
  - Done: the Visibility page binds a `Hold` card and a `Ghost` card to their switches and replays a Fluent
    `Expander`'s rows through `EnterOnShow`, logging each moment. The nav hides a section's `PART_ItemsPresenter`
    instead of `MaxHeight = 0`, wrapped in a `Panel` because `ContentSize` cannot hold the size of a hidden sole
    child; the presenter's held `Exit` fades the pages before the fold, and `EnterOnShow` replays each page's
    `Presence.Enter`. An `Expander` opened for the first time replays nothing, since its rows attach in that pass
    (PLAN §7), so the page's list starts open.

## 4. Phase 58 — Text exits

### 4.1 Gap

PLAN §7 (Phase 53d): `Presence.Exit` plays on one ghost bitmap, so a word cannot leave letter by letter. The Typography
rotator splits the outgoing word with `SplitText` and drops its characters imperatively.

### 4.2 Design sketch

- **One ghost per piece (PLAN decision 80).** `Presence.Ghost` snapshots each piece of a leaving split `TextBlock`
  into its own ghost in one frame canvas, in reading order, so index staggers, `StaggerByLine` and `LineHeightOf` work;
  the detached block is split for the call (`PieceGhosts.Snapshot`) and reverted, so no styled clone was needed.
- **Cost cap, decided (PLAN decision 80):** at most 256 per-piece ghosts (`PieceGhosts.Max`). Above it the split
  coarsens chars → words → lines, then falls back to the single whole-element ghost.
- **One host, dismissed once (PLAN decision 81).** The host and every ghost's bitmap are dismissed together when the
  exit completes, that is when the last ghost's tween ends, at once when the exit is cancelled, and inside the call
  for an instant exit (`ReduceAll`).
- **Hidden text (PLAN decision 82).** Under `Hold` the exit plays on the block's live split pieces with no ghost, and
  the block is whole again once hidden; shown again mid-hide, the piece exit plays back (decision 78). Under `Ghost`
  the hide goes through the per-piece ghosts.
- **Recipes:** `LineRiseOut` (lines fade and rise one line height), `LineMaskOut` (lines rise one line height behind
  their clips, without fading), `LineWipeOut` (each line's `ClipInset` left inset opens to 100%), `WordFadeOut` (words
  fade and rise `Distance`), `WordBlurOut` (words fade and blur to `BlurRadius`) and `CharCascadeOut` (characters fade
  and drop `Distance` on `EmphasizedEase`). Each states only to-values and keeps its entrance's signature, token
  defaults and `StaggerByLine` spacing; the markup extensions return them under `Presence.Exit` (decision 34).
- **Reduced:** an exit built from its `WhenReduced` form plays on one whole-element ghost, and the recipes' reduced
  form is a plain fade of it over `Tokens.Short`. A held hide's reduced form plays on the unsplit block.

### 4.3 Tests and sample

- **Tests:** ghost positions match the pieces' slots; stagger order by index and by line; the cap falls back; the host
  is dismissed when the last ghost completes; `ReduceAll`.
- **Sample:** the Typography rotator becomes declarative and loses its imperative drop.
  - Done: `PresenceTextExitTests` pins the ghost slots per piece kind, both staggers, the cap, the host dismissal,
    masks, leaving mid-reveal, the six exit recipes and their reduced forms, and `Hold`/`Ghost` hides;
    `RecipeMarkupTests` pins the markup pick-up. The rotator is declarative: each headline sets `Presence.Exit` to the
    page's staggered `DropOut` under a `Presence.Mode="Wait"` slot, and `Rotate` only swaps `Slot.Child`.

## 5. Phase 59 — Animated scrolling

### 5.1 Gap

There is no public scroll-to. Animating `ScrollViewer.Offset` as a property writes at `Animation` priority and pins
the offset against the user's own input, which is why `ScrollSnap` and `Scroll.Smooth` use `SetCurrentValue`
(decisions 46 and 48). Common needs: back to top, jump to a section, bring an item into view (virtualized included), and
a chat log that follows new messages. Other libraries: GSAP's ScrollToPlugin (with `autoKill`), Compose's
`animateScrollTo`/`animateScrollToItem`, Flutter's `animateTo`/`ensureVisible`.

### 5.2 Design sketch

- **`ScrollX`/`ScrollY` shorthands on a `ScrollViewer`** write with `SetCurrentValue`, so they are ordinary tween
  targets in builders and timelines. Any offset change the tween did not write cancels it (`autoKill`), the same rule
  as Snap and Smooth, which share one helper. The value is clamped to the extent every frame.
  **Settled (decision 83):** Snap, Smooth and the ScrollX/ScrollY shorthands share one autoKill helper,
  `Events/ScrollWriter`. It writes with `SetCurrentValue` and remembers, per scroller and axis, the last owner and value
  written. A notification carrying that value is the owner's own, one cutting it back to a shrunken `ScrollBarMaximum`
  is a clamp, and anything else, another writer included, is foreign (the §5.3 rule). A foreign change removes only
  that axis's tweens; a clamp keeps the tween running. Snap and Smooth keep treating a clamp as foreign, as before. A
  revert leaves the offset. Pinned by `ScrollShorthandTests`, `ScrollSnapTests`, `ScrollSmoothTests` and the §5.3
  spike tests.
- **Code API:**
  - `PrestoAvalonia.ScrollTo(scroller, offset, builder?)`.
  - `ScrollIntoView(element, alignment: Start/Center/End/Nearest, margin, builder?)`, through the nearest scroller
    first. **Settled (decision 86):** every ancestor `ScrollViewer` scrolls, innermost first and all at once, as
    browser `scrollIntoView` and Flutter `ensureVisible` do. Each outer scroller aims at the element's rect at the inner
    scroller's final offset, cut to the inner viewport, not at the inner scroller's bounds. Rects come from summed
    layout slots (as `LayoutTransition.Measure` does), so render transforms do not count. Start/End align the
    margin-inflated rect's leading/trailing edge, Center centres it, and Nearest does nothing when it is fully in view,
    otherwise scrolls the least distance, Start when it is larger than the viewport; all clamp to
    `ScrollBarMaximum`. One `Animation` drives the scrollers that move through function-based ScrollX/ScrollY values,
    with decision 84's motion over the largest distance in viewports; a scroll on one scroller stops only its tweens.
    When nothing moves, the animation is returned already completed. Reduced motion jumps. Pinned by
    `ScrollIntoViewTests`.
  - `ScrollToIndex(list, index, alignment, builder?)` for a virtualized list. The item is not realized yet, so: jump
    close when it is far (Compose's approach), animate the last stretch, and retarget as realization corrects the
    extent. **Settled (decision 87):** on a `VirtualizingStackPanel` (any other panel scrolls its container into view),
    the item's aligned offset is measured from its container's layout slot once realized, and estimated before that from
    the nearest realized container plus the mean realized size per item. When that estimate is more than 2 viewports
    from the offset, the call jumps through `ScrollWriter` to 1 viewport short of `index × mean size` aligned, the
    coordinates in which the panel places a jump clear of its realized items (§5.3), and lays out at once, up to 3
    passes (the first layout's stale extent can clamp the first jump). The last stretch rides decision 84's tween
    aimed every frame at the measured or estimated offset, and on completion it lays out and writes the measured
    offset until it holds, so it ends exactly aligned. The item is tracked, not the index: an insert or removal
    before it is followed. Nearest fixes to Start or End by the side the item comes from. The first item's slot starts
    at 0 and the last's ends at the extent, so they land at the extremes. The panel never writes the offset (§5.3), so
    only input cancels. Reduced motion jumps on the first frame, then settles. An index out of range throws
    `ArgumentOutOfRangeException`. Pinned by `ScrollToIndexTests`.
  - Each returns an `Animation`.
- **Default motion. Settled (decision 84):** `Recipes.Tokens.Short` + 150 ms per viewport of distance (distance ÷
  viewport length on the moving axis, the larger of the two when both move), capped at `Tokens.Long`, scaled by
  `Globals.TimeScale`, on `Tokens.Ease`; no spring. Under `MotionTokens.Standard` that is 200 ms + 150 ms per viewport,
  capped at 650 ms, on OutCubic. A builder's Duration or Ease wins. The rule lives in `Events/ScrollMotion` for the
  later calls. Each moving axis tweens its eased progress 0 to 1 and maps it to the offset, aiming every frame at the
  requested offset held to the extent, so a shrink clamps and a regrowth carries on to the requested offset; when that
  aim moves, the start is rebased so the offset never jumps. A new `ScrollTo` replaces the running one through
  `Replace` composition. Pinned by `ScrollToTests`.
- **`Scroll.StickToEnd`** on a `ScrollViewer`: while the view is at the end, growth of the extent eases to the new end.
  Scrolling away releases it, and returning to the end re-arms it. It must not fight `Presence.ScrollAnchor`.
  **Settled (decision 88):** the axis is vertical, or horizontal when only that scrolls. It is armed while the offset
  is within 1 DIP of `ScrollBarMaximum`, judged by position on every offset change whoever wrote it, so content
  shorter than the viewport counts as at the end and any return to the end re-arms it. While armed, a
  `ScrollBarMaximum` change that leaves the view short of the end starts decision 84's tween aimed at the maximum
  every frame, so a burst of growth mid-ease retargets the one running ease; reduced motion jumps. An offset change
  short of the end releases it unless that running ease wrote it (`ScrollWriter.TweenWrote`); a wheel, a drag, a
  `ScrollTo` (which replaces the ease) or an anchor correction all count. Turning it off reverts a running ease,
  which leaves the offset where it is. With `Presence.ScrollAnchor`, `ItemChanges` records no anchor while the
  scroller is armed, and the follower only eases while armed, so one change is never handled by both: at the end
  StickToEnd follows any growth, an insert above included; scrolled up, the anchor holds the row. Appends never
  record an anchor anyway, since they land after the first visible row. Pinned by `ScrollStickToEndTests`.
- **Interplay:** a running `ScrollTo` cancels a pending snap, and a snap waits for a `ScrollTo` to end. `ScrollObserver`
  needs nothing, because it reads `Offset`.
  **Settled (decision 85):** the scroll tween's first write is foreign to a snap, which cancels it and restarts the
  wait; `StartSnap` returns while `ScrollWriter.Tweening` says a shorthand tween holds the observer's axis, and the
  shorthand restarts every observer's wait when it lets go, whether it ended, reverted or was stopped by input, so the
  snap comes the snap Delay after the scroll to ends. Smooth's ease stops at a scroll to's write, as at any write it did
  not make. Pinned by `ScrollToTests.ASnapWaitsForTheScrollToAndSnapsAfterItsDelayOnceItEnds` and `ScrollSnapTests`.
- **Reduced:** jump. **Settled (decision 84):** read at call time (decision 71), the scroll to lands on the first frame
  with no delay and the engine idles afterwards; a builder's `WhenReduced` form plays instead, with its unset duration
  and ease taken from the default motion.

### 5.3 Spike (done)

Pinned by `ScrollSpikeTests` (Avalonia 12.1.2); `VirtualListStage` is the virtualized stage for later items:

- **A `SetCurrentValue` offset holds and reads back exactly.** `123.37` survives layout passes, an extent shrink that
  still holds it, and a viewport resize, with no rounding and no further notification. Thumb drags write
  fractional offsets themselves (`142.16666666666669`). The wheel (50 per notch), a thumb drag, `PageDown` (one viewport)
  and focusing a child (`BringIntoView`, nearest edge) all still move it, so decisions 46 and 48 hold. `ScrollViewer`
  handles neither `End` nor `Home` (with or without Ctrl). A `ListBox` handles both: it selects the item and scrolls to
  the end or the top.
- **`OffsetProperty` is a styled property that coerces inside the write.** A write outside `[0, ScrollBarMaximum]` is
  clamped before its notification, which carries the clamped value (`5000` → `800`, `-5` → `0`). A writer must
  clamp before it writes and remember the clamped value. Otherwise its own notification reads as foreign.
- **An extent shrink below the offset clamps it inside the presenter's arrange.** The `Offset` notification arrives
  inside `ScrollContentPresenter.ArrangeOverride`, before the `Extent` notification, but `Extent` and `ScrollBarMaximum`
  already read the new values there. Growing the extent back does not restore the old offset. Neither public arrange
  flag tells this apart from input, because both see the `ScrollViewer` arrange valid and the presenter's invalid.
- **Priority never tells writers apart.** `SetCurrentValue`, the wheel, a drag, keys and `BringIntoView` all notify at
  `Unset` while the offset has no local value. Once it has one, every change notifies at `LocalValue`, `SetCurrentValue`
  included.
- **A writer sees its own change inside its call, and a write from the handler nests.** The notification arrives
  synchronously inside `SetCurrentValue` with exactly the value written. A `SetCurrentValue` from inside the handler
  raises its own nested notification, and the nested value wins.
- **The rule for the shared helper.** Remember the last clamped value written. A notification whose new value equals
  it is the helper's own write. A value below it that equals the current `ScrollBarMaximum` is an extent-shrink clamp.
  Anything else is foreign. Exact comparison suffices, so no tolerance is needed.
- **Snap and Smooth today compare exactly against their last write and treat a clamp as foreign.** `Smoother` cancels
  when `Offset != _written` (whole `Vector`) while it has a target. `ScrollObserver.HandleOffsetChanged` restarts
  `WaitToSnap` when the axis offset `!= _snapWritten`, which is `NaN` when no snap runs, and a pointer release also
  restarts it. Both clamp their targets to `ScrollBarMaximum` first. When an extent shrink clamps its next write,
  Smooth stops at the new end and does not resume when the extent regrows. A snap stops too, waits again, and
  re-snaps to a step inside the new range. The helper keeps both behaviours unless §5.2 decides that a clamp only
  retargets.
- **The panel's first layout reports a stale extent, and the next measure corrects it.** With heights of 20 + 10 ×
  (index mod 5), the first extent is 100 100, about 20 per item, although the realized containers average 40. Any
  later measure (a re-measure or a scroll step) brings it to `Count` × the realized average (200 000). Before that
  measure, an offset past the stale extent is clamped to it: 3000 × 40 lands at 99 900. The panel's
  estimate is private (`EstimateElementSizeU`, `_lastEstimatedElementSizeU`). The practical estimate is
  `Extent / Count` after a measure, which equals the average `Bounds` of the realized containers
  (`GetRealizedContainers`).
- **A jump by offset settles in one layout pass, and the panel never writes the offset.** With 50 items of 20 and
  the rest 80, a `SetCurrentValue` to 3000 × 20 realizes item 3000 at the top of the viewport in the next pass. The extent
  is then 100 180, `ContainerFromIndex` returns non-null, and further passes change nothing. The item's true position is
  237 000, so the estimate is about 4 times too short.
- **Realized items keep their positions while the extent estimate swings.** Stepping 30 at a time through the
  varied-height list, only the helper's own `Offset` notifications arrive. The extent swings by up to about 6 % either
  side of 200 000 as the realized average changes. An item's content position (`Top + Offset`) is unchanged after
  scrolling 600 away and back. The last stretch of a `ScrollToIndex` can therefore aim at the realized container,
  with retargeting only when it first realizes, while clamping copes with the moving maximum.
- **`ItemsControl.ScrollIntoView(index)` realizes and aligns inside the call.** The offset change arrives
  synchronously, outside arrange, and the container is laid out before the call returns. It aligns to the nearest edge:
  an item below ends at the viewport bottom, and an item above starts at its top.

### 5.4 Tests and sample

- **Tests:**
  - Lands exactly.
  - A wheel or drag mid-flight cancels.
  - An extent change mid-flight clamps and retargets.
  - The four alignments.
  - A far virtualized index lands with the item aligned.
  - `StickToEnd` follows inserts and releases on a user scroll.
  - Reduced jumps; the engine idles afterwards.
- **Sample:** "Back to top" and section jumps on the Scroll page; "Jump to newest" on the Activity feed; a chat-style
  log.
  - Done: the Scroll page's Back to top calls `ScrollTo(scroller, 0)`, since `default` is ambiguous between the
    `Vector` and `double` overloads, and its card buttons call `ScrollIntoView` at `Start` with a 12 px top margin. The
    named thresholds fire and the sync timeline scrubs along the eased scroll. The Activity feed's "N new above" pill
    calls `ScrollToIndex(Feed, 0)`. On the 2,000-row feed it jumps close and eases the rest, and a live insert mid-flight
    still lands at the top with the newest event shown.
  - Done: the Chat log page sets `StickToEnd` on its virtualized `ListBox` through a `/template/ ScrollViewer` style.
    Timer and sent messages of one to three lines slide in from their side through `Presence.Enter` while the log
    follows them. A wheel up releases it and shows a Jump to newest pill, which calls
    `ScrollToIndex(log, last, ScrollAlignment.End)` and lands where `StickToEnd` re-arms. `ScrollToIndex` follows the
    item it was given, so a message landing mid-jump makes the page call it again for the new last. The page tracks the pill
    itself, since the armed state is internal: an upward offset change short of the end shows it, and reaching the end
    hides it.

## 6. Phase 60 — Lifetime and idling

### 6.1 Gap

- **Detached targets.** `control.Animate(… Loop = true)` on a control that leaves the tree keeps ticking: the engine
  never idles, and its child list keeps the control alive. This follows decision 8 (explicit scoping), but SwiftUI,
  Compose and Flutter tie animations to the view's lifetime.
- **Hidden windows.** Upstream's `engine.pauseOnDocumentHidden` (default `true`, `engine.js:161`) is not ported.
  `AvaloniaFrameSource` keeps requesting frames while every window is minimized or the app is in the background.
  Whether Avalonia delivers those frames on each platform is unverified.

### 6.2 Design sketch

- **`WhenDetached`**: a bridge default `PrestoAvalonia.WhenDetached`, overridable per animation through
  `AvaloniaAnimationParams.WhenDetached` and the builder. Values:
  - `Continue`: today's behaviour.
  - `Pause`: pause once every visual target has left the visual tree, and resume from where it stood on reattach.
  - `Revert`: cancel and revert, i.e. auto-scoping.

  **Settled (decision 89):** the default is `Pause`, a deviation from decision 8 recorded in DESIGN-NOTES §10. The
  bridge tracks each root as `Engine.TimerCreated` reports it, unless its effective `WhenDetached` is `Continue`, and
  pauses or reverts it when the last of its attached visual targets detaches. Only that transition counts, so an
  animation created before its control attaches keeps running and pauses on a later detach. A root the user paused
  before the detach stays paused on reattach; the bridge resumes only what it paused. `Revert` is opt-in auto-scoping.
  The params and builder overrides win over the default, and a reduced form keeps the override. Pinned by
  `WhenDetachedTests`.
  - A paused animation leaves the engine's list, so a control that never comes back becomes collectable with its
    animation. The reattach hook must therefore hang off the control (a handler or attached value), never a static
    table.
    **Settled (decision 89):** the trackers live in an attached value on each watched visual, with one shared
    `AttachedToVisualTree`/`DetachedFromVisualTree` handler pair, and the root-to-tracker table is a
    `ConditionalWeakTable`. A control detached for good is collected with its paused animation. Pinned by
    `WhenDetachedTests`.
  - Targets the bridge creates for a visual (brushes, transforms, effects) need an owner mapping that is always on;
    `TargetOwner` records only while diagnostics are active. Measure what keeping it on costs.
    **Settled (decision 89):** `TargetOwner.Record` is always on, as the §6.3 spike recommends (90 bytes and about
    110 ns per created target), so a brush the bridge made follows its visual. Pinned by
    `WhenDetachedTests` and `LifetimeSpikeTests`.
  - A timeline pauses when all its children's visual targets are detached. Plain-object targets always continue.
    **Settled (decision 90):** a root timeline gathers its child animations' visuals as `Timeline.AddChild` adds them.
    It pauses or reverts once every one has detached, and resumes when any reattaches. A child never pauses on its
    own. A target that is neither a visual nor a bridge-made target of one makes its animation, or the timeline
    holding it, continue for good, resuming it if the tracker had paused it. Pinned by `WhenDetachedTests`.
  - Presence, Motion, page transitions, Connected and ghosts manage their own animations and are unaffected.
    **Settled (decision 90):** each of them passes `WhenDetached.Continue` at its call site; `LayoutTransition` and the
    compositor mirrors animate a plain `CompositorScratch`, so the tracker already lets them continue. Pinned by
    `WhenDetachedTests`.
- **`Engine.PauseOnHidden`**, upstream's `pauseOnDocumentHidden` (default `true`, as upstream). The bridge drives it:
  - every window `Minimized`;
  - the app moved to the background (`IActivatableLifetime.Deactivated` with a background kind, on mobile);
  - the Browser page hidden.

  `Engine.Pause`/`Resume` already carry upstream's time-base handling, so playback resumes without a jump.
  **Settled (decision 91):** the host sets `Engine.Hidden`, and with `PauseOnHidden` hiding pauses the engine and
  showing calls `Resume`, as upstream's `visibilitychange` handler does, even after a manual pause. The bridge's
  `HiddenSignal` sets it while every desktop window is `Minimized` or a `Background` activation is outstanding;
  per §6.3 the latter covers mobile and the Browser page, and `File`/`OpenUri` activations are ignored. Two
  deviations from upstream: while hidden, `Wake` does not start the frame source, because a minimized Win32 window
  keeps delivering frames (§6.3); and hiding an idle engine marks it paused, so the show rebases timers created
  while hidden. Compositor scene engines follow: an attached `CompositorCanvas` posts the state to its scene handler,
  which sets `Hidden` on its engine, including one created while hidden. Pinned by `EngineHiddenTests` and
  `LifetimeHiddenTests`.

### 6.3 Spike (done)

Pinned by `LifetimeSpikeTests` (Avalonia 12.1.2); the Win32 numbers come from the Desktop sample on the Loaders page
(nine infinite loops) with a throwaway `RequestAnimationFrame` counter, minimized and restored through DevTools:

- **A minimized Win32 window keeps delivering every frame.** `RequestAnimationFrame` ran at 180 per second (the
  display's refresh rate) restored and minimized alike, so `AvaloniaFrameSource` and the engine keep ticking at full
  rate. Process CPU fell from 0.31–0.33 to 0.056 cores per second while minimized: Avalonia skips the drawing, and what
  is left is the frame callbacks (the counter's own included), the engine's ticks and the property writes. Pausing therefore saves about 5 % of a
  core per minimized app with a few loops running, plus the wake-ups that keep the machine from idling.
- **The headless window behaves the same way.** It takes `WindowState.Minimized`, raises the `WindowStateProperty`
  change synchronously (`Minimized`, then `Normal`), stays `IsVisible`, raises no `Activated`/`Deactivated`, and keeps
  delivering `RequestAnimationFrame` callbacks on each render timer tick. A headless test can therefore drive the
  hidden state through `WindowState`, but cannot show that frames stop.
- **Desktop minimize goes through `WindowState`, not `IActivatableLifetime`.** `TryGetFeature<IActivatableLifetime>()`
  returns null under the Win32 classic desktop lifetime (Avalonia.Win32 binds none) and headlessly, where
  `ApplicationLifetime` is null too. Minimizing raised only the state change. `Activated`/`Deactivated` are focus
  events: restoring activated the window and it deactivated again as focus moved on, so they cannot stand for
  visibility. On macOS, Avalonia.Native binds a lifetime that raises `Background` when the application is hidden
  (Cmd+H), not when a window is minimized.
- **The Browser reports page visibility.** `BrowserActivatableLifetime` (internal, bound as the `IActivatableLifetime`
  singleton) is fed by a `visibilitychange` listener in `avalonia.js` through `DomHelper.DocumentVisibilityChanged`. A
  `visibilityState` of `"visible"` raises `Activated(ActivationKind.Background)` and any other value raises
  `Deactivated(Background)`. The script also reports `"visible"` once at startup when the page is visible, so the
  first event can be an `Activated` with nothing hidden before it. The Browser head was not built.
- **Mobile backgrounding arrives as `ActivationKind.Background`.** Android's `AvaloniaActivity.OnStop`/`OnStart` raise
  `Deactivated`/`Activated(Background)` through `AndroidActivatableLifetime` (main activity), and iOS raises them on
  `DidEnterBackground`/`WillEnterForeground`. `File` and `OpenUri` activations share the events, so the driver must
  filter on `Kind == Background`. `ActivatableLifetimeBase` raises both through `Dispatcher.UIThread.Send`, so
  handlers run on the UI thread.
- **An always-on owner mapping is cheap.** `TargetOwner.Record` costs 90 bytes and about 110 ns per brush or
  transform with the table on (one `ConditionalWeakTable` entry plus a `WeakReference<Visual>`, amortized over 10 000
  records), against 0 bytes and about 5 ns off. A `SolidColorBrush` alone is 616 bytes, and a record happens once per
  created target, never per frame. **Verdict:** no need to stay lazy or to record only for animations whose effective
  `WhenDetached` is not `Continue`. Recording can be always on, which keeps `TargetOwner` a single path.
- **A per-visual detach subscription is cheaper still.** Adding one shared `DetachedFromVisualTree` handler allocates
  nothing (about 50 ns). A capturing closure per visual costs 96 bytes (about 120 ns). Every subscription fired once
  when 10 000 children were removed.

### 6.4 Tests and sample

- **Tests:**
  - A detached loop pauses and the engine idles; reattaching resumes at the same progress.
  - A control detached for good is collected (`WeakReference` plus a forced GC).
  - `Revert` reverts and `Continue` is unchanged.
  - The timeline rule.
  - A minimized window pauses the engine and resumes where it was (headless, through a hook); `Then()` still completes.
  - Done: `WhenDetachedTests` pins the pause and idle, reattaching at the same progress, collection, `Revert`,
    `Continue`, the overrides, user pauses, the timeline and plain-object rules and the exemptions.
    `EngineHiddenTests` pins `Engine.Hidden` and `PauseOnHidden` in the core, and `LifetimeHiddenTests` pins
    minimizing through `WindowState`, a `Then()` completing after the show and scene engines.
    `HostHookTests` pins the `HostLifetime` slot and `Engine.TimerCreated`.
- **Sample:** the DevTools overlay labels detached-paused rows.
  - Done: a root the bridge paused on detach shows as `detached`, and the transport bar shows "hidden · paused"
    while the engine is hidden. Pinned by `DevToolsListTests` (`ALoopOnARemovedControlShowsDetachedUntilItIsReattached`,
    `AUserPausedRowStaysPausedWhenItsControlIsRemoved`).

## 7. Phase 61 — Popups and flyouts

### 7.1 Gap

Nothing in the bridge handles `Popup`, flyouts, `ContextMenu` or `ToolTip`.

- **Open:** `Presence` already plays on a popup's content, since every open attaches it into a new popup host, but the
  moment depends on the template: `Initial` on a window's first popup and inside the Fluent flyout and menu templates,
  `Enter` when a `Popup` child or tooltip content is opened again (§7.3). It never plays on the presenter itself.
- **Close:** the popup host is hidden at once. `Presence.Ghost` draws in `OverlayLayer.GetOverlayLayer(anchor)`, which a
  native (non-overlay) popup does not have.

Flyout and menu open/close is among the most common UI motion (WinUI's placement slide, Material menus scaling from the
anchor).

Verified against the 12.1.2 assemblies (reflection and XML docs):

- `PopupFlyoutBase.Closing` is cancellable (`CancelEventArgs`), and so is `ContextMenu.Closing` (`CancelEventHandler`).
- `Popup` raises only `Opened`/`Closed`, and has `ShouldUseOverlayLayer`/`IsUsingOverlayLayer`.
- `ToolTip.ToolTipClosing` cannot cancel ("Marking the ToolTipClosing event as handled does not cancel closing the
  tooltip").

### 7.2 Design sketch

- **A static `Popover` class with attached `Popover.Open` and `Popover.Close` builders**, settable on `FlyoutBase` (an
  `AvaloniaObject`, not a `Control`), `ContextMenu`, `Popup` and `ToolTip`. `FlyoutBase` is not a `StyledElement`, so
  styles reach a flyout through its presenter: a style on `FlyoutPresenter` or `MenuFlyoutPresenter`, narrowed by a
  class from the flyout's `FlyoutPresenterClasses`, sets the builders on the presenter, which accepts them too; a value
  on the flyout itself wins. `ContextMenu` and `ToolTip` are styled directly, and a templated `Popup` through
  `/template/`, such as `ComboBox /template/ Popup#PART_Popup`.
- **Open plays on the presenter** (`FlyoutPresenter`, `MenuFlyoutPresenter`, the `ContextMenu`, the `Popup`'s child, the
  `ToolTip`), with its start values applied in the turn it opens (the page-transition rule), so the first rendered frame
  is never at rest. Presenters are reused across opens and hosts are not, so state is kept on the presenter.
- **Cancellable close** (`PopupFlyoutBase`, `ContextMenu`): cancel the first `Closing`, play `Close` on the live
  presenter, then close for real through `Popup.Close()` (the flyout's `Popup`, the context menu's logical parent),
  letting the second `Closing` through. `Hide()` and `ContextMenu.Close()` cannot be used for that: after Escape or a
  cancelled `ContextMenu.Close()` the popup's `IsOpen` is already false. Reopening mid-close plays `Close` back from
  where the presenter stands (`Reverse`, as decision 78) and drops the pending close. As built (PLAN decision 96), only
  `ContextMenu` can: a flyout's `Hide()` or a `ShowAt` on the same target mid-close counts as a repeated close, and a
  `ShowAt` on another target closes it at once and replays `Open` as a fresh open. An owner detaching mid-close closes
  at once, and Avalonia closes it then whatever `Closing` says.
- **`Popup` uses the `IsOpen` hold, held from the open.** A hold pushed in `IsOpen`'s change notification comes too late
  (§7.3), so `Popover` pushes `IsOpen = true` at `Animation` priority when the popup opens. A write underneath (a local
  value, a binding, a `ComboBox`'s `IsDropDownOpen` through its template binding) then raises nothing, so while the
  popup is open `Popover` reads the base value on a 30 ms dispatcher timer, so the engine can idle; when it turns false `Close` plays on the child and the hold
  is released at the end, which closes the popup. Light dismiss and an owner detaching close at once, since they bypass
  `IsOpen`; as built, light dismiss also plays `Close` on a ghost, as a tooltip does (PLAN decision 95). A `ClearValue` under the hold is lost, so `IsOpen` must be closed by writing false.
- **`ToolTip` uses a ghost in the anchor window's overlay**, since neither hold works for it (§7.3). In `ToolTip.IsOpen`'s
  change notification the closed `ToolTip` is detached but keeps its bounds; it is snapshotted with `Opacity` held at 1
  (losing `:open` has dropped it to 0 in Fluent), at the window position recorded while it was open, and `Close` plays on
  the ghost. A window without an overlay layer closes at once. The ghost is clipped to the window, unlike a native tooltip.
- **Placement-aware recipes:** `Recipes.PopoverIn/Out` scale and slide the presenter from the anchored edge, and are
  transform and opacity only, so they run on the compositor (decision 34). The placement comes from
  `PopupFlyoutBase.Placement`, `Popup.Placement`, `ContextMenu.Placement` or the attached `ToolTip.Placement`; nothing
  reports a constraint flip, so the recipes take the edge from the presenter's and the anchor's rects at open.
- **Theme conflicts:** only the Fluent `ToolTip` animates its own open (§7.3). Where `Popover.Open` or `Popover.Close` is
  set, its `Opacity` transition is cleared and `Opacity` held at 1 while `Popover` runs; nothing else is touched.
- **Reduced:** close at once; open with the recipes' reduced fade.

### 7.3 Spike (done)

Pinned by `PopupSpikeTests` (Avalonia 12.1.2); the platform findings come from the 12.1.2 assemblies (decompiled, the
heads not built), and the Win32 ones from the Desktop sample on the Visibility page with a throwaway flyout, context menu
and tooltip, inspected through DevTools and console output:

- **`Presence` plays on popup content, as a moment the template decides.** Every open attaches the content into a new
  host. The first popup in a window creates its `PopupOverlayLayer`, so it arrives as `Initial`; opened again, a
  `Popup` child or tooltip content arrives as `Enter`, while flyout and menu content stays `Initial`, because a
  `ScrollViewer` or the items panel in the Fluent template stops the walk first. On Win32 every open is a new
  `PopupRoot`. `Presence` is therefore no route to popup motion, and the presenter gets nothing.
- **Presenters are reused, hosts are not.** A flyout creates its presenter once, as `Popup.Child`, and a `ContextMenu` and
  a tooltip's `ToolTip` are reused; each open creates a new `OverlayPopupHost` or `PopupRoot`.
- **Headless, Browser, Android and iOS popups live in the window's overlay.** `CreatePopup()` returns null in
  `HeadlessWindowImpl`, `BrowserTopLevelImpl`, Android's `TopLevelImpl` and iOS's `AvaloniaView.TopLevelImpl`, so every
  popup there is an `OverlayPopupHost` in the window's `PopupOverlayLayer`: `ShouldUseOverlayLayer` is false,
  `IsUsingOverlayLayer` true, and the presenter's `TopLevel` is the window.
- **Win32 uses native popups by default.** `WindowImpl.CreatePopup()` returns a native `PopupImpl` unless
  `Win32PlatformOptions.OverlayPopups` is set (default false), as does Avalonia.Native with its own `OverlayPopups`. In
  the Desktop sample a `Flyout`, a `ContextMenu`, a tooltip and the `ComboBox` drop-down each opened in a `PopupRoot`,
  with `IsUsingOverlayLayer` false; the main window drew none of them, and DevTools lists no popup roots.
- **Cancelling the first `Closing` keeps a flyout drawn, and a second `Hide()` closes it.** For a `Flyout` and a
  `MenuFlyout`, light dismiss, Escape and `Hide()` each raise `Closing` once; cancelled, the presenter stays attached
  and drawn, and `Hide()` raises `Closing` again and closes. Escape leaves `Popup.IsOpen` false under the open popup,
  since `FlyoutPresenter` and the menu handler write it before the cancelled `Closing`. On Win32 a cancelled native
  flyout and context menu stayed visible until the second close.
- **A cancelled `ContextMenu` closes again only through its `Popup`.** Light dismiss, Escape and `Close()` each raise
  `Closing` once and leave `Popup.IsOpen` false; a second `ContextMenu.Close()` does nothing, since it only writes that
  false again. `Popup.Close()` (the menu's logical parent) raises `Closing` again and closes; it also works for flyouts.
- **An owner detaching closes whatever `Closing` says.** A flyout raises `Closing` once and ignores the cancel
  (`HideCore(canCancel: false)`); a `ContextMenu` raises it three times and closes.
- **A hold in `IsOpen`'s change notification comes too late.** The owners' class handlers (`Popup.IsOpenChanged`,
  `ToolTip.IsOpenChanged`) run before any `PropertyChanged` handler, so the popup has closed by then and the hold
  reopens it in a new host: the content detaches and attaches again, and `Closed` and `Opened` are raised. For a
  `Popup` or `ComboBox` the reopen also outlives the hold, so releasing it leaves the popup open. For a tooltip the
  release closes it.
- **A hold pushed while the popup is open works for writes.** Setting `Popup.IsOpen` or `ComboBox.IsDropDownOpen` to
  false under it raises nothing and keeps the same host; releasing closes the popup. So do `ToolTip.SetIsOpen(false)`
  and the pointer leaving the anchor for a tooltip. A `ClearValue` under the hold is lost: the base value still reads
  true, so releasing leaves the popup open. `ToolTipService` clears `IsOpen` on a pointer press and when the pointer
  leaves the window, so a held tooltip stays open for good after those. The service has no timeout close in 12.1.2.
- **Light dismiss closes a `Popup` at once under either hold.** It calls the popup's close directly, not through
  `IsOpen`; a hold made in the notification is ignored and leaves `IsOpen` reading true over the closed popup.
- **A closed tooltip is still snapshot-ready.** In `ToolTip.IsOpen`'s change notification the `ToolTip` is detached,
  keeps its bounds and has `Opacity` 0. `ToolTipClosing` is not raised on close; only reopening raises it, for the
  popup it closes first.
- **Each owner carries its own placement**: `PopupFlyoutBase.Placement` (default `Bottom`), `Popup.Placement`
  (`Bottom`), `ContextMenu.Placement` (`Pointer`) and the attached `ToolTip.Placement` (`Pointer`). A constraint flip
  is not reported: a `Bottom` flyout with no room below opens above its anchor with `Placement` still `Bottom`, which
  only the presenter's and the anchor's rects show.
- **`FlyoutBase` cannot be styled.** It is an `AvaloniaObject`, not a `StyledElement`; styles on `FlyoutPresenter`, and
  on `MenuFlyoutPresenter` with a class from `FlyoutPresenterClasses`, reach the presenters, whose `Popup` is parented to
  the placement target.
- **Only the Fluent `ToolTip` animates its own open.** Its theme sets `Opacity` 0 with a 150 ms `DoubleTransition` on
  `Opacity` and `Opacity` 1 on `:open`, so it fades in and drops to 0 as it closes. The `FlyoutPresenter`,
  `MenuFlyoutPresenter`, `ContextMenu`, `OverlayPopupHost` and `PopupRoot` themes and the `ComboBox` drop-down have no
  transitions, animations or `:open` setters. The `ComboBox`'s `PART_Popup` binds `IsOpen` two-way to
  `IsDropDownOpen` through a template binding, with `IsLightDismissEnabled` and `InheritsTransform` set.

### 7.4 Tests and sample

- **Tests:**
  - Open renders start values on its first frame.
  - Close plays and then closes, for each dismiss path.
  - Reopen mid-close.
  - The owner detaches mid-close (closes at once).
  - `ContextMenu` and `ToolTip`.
  - `ReduceAll`.
- **Sample:** a Popups page with a `MenuFlyout`, a `Flyout`, a `ContextMenu`, a `ToolTip` and a `ComboBox` dropdown
  (styled through its template's `Popup`).

## 8. Phase 62 — Press ripple

### 8.1 Gap

Material's ink ripple (Flutter `InkWell`, Compose `indication`). The risk is cost: a visual per ripple, or per-frame
UI-thread work on every pressed button, without shaders to draw it cheaply. So this phase measures before it picks.

### 8.2 Candidates to measure

A ripple is a filled circle (solid, or a radial gradient for a soft edge) clipped to the host's rounded rectangle. It
grows from the press point and fades on release. Material also has an optional state layer (a hover, focus or press
tint).

- **A: UI-thread layer.** One `RippleLayer` per host draws all its ripples in `Render` (clip, then `DrawEllipse`),
  invalidating only the visual and never layout.
- **B: Render thread.** A `CompositionCustomVisual` draws ripples with `ImmediateDrawingContext`, either one per host or
  one shared overlay per window (the Phase 36 particle overlay precedent). The UI thread only posts press and release.
- **C: Compositor transforms.** An `Ellipse` per ripple, scaled and faded with `AnimateOnCompositor`, inside a
  clipping host. Simplest, but one control per ripple.

Measure 50 buttons pressed rapidly and 200 list rows: UI and render fps, and allocations per frame, on Win32, Browser
and Android. Record the choice as a decision.

### 8.3 API sketch

- `Ripple.Color` (default: theme accent at low alpha), `Ripple.Bounded` and `Ripple.StateLayer`, attached to any
  `Control`.
- Holding the press grows the ripple to cover the host, and release fades it. Enter or Space ripples from the centre.
- Dragging out or starting a scroll cancels it. Inside a scroller, touch waits briefly before showing (Android's tap
  timeout), so a fling does not ripple.
- Whether the ripple draws over or under the content is a decision (Compose draws its indication over content).
- **Reduced:** the state-layer tint only.

### 8.4 Spike (done)

Pinned by `RippleSpikeTests` (Avalonia 12.1.3, re-run on the bump from 12.1.2 with the same allocations). The Win32
numbers come from the Desktop sample's Ripple bench page with the auto-presser on (a press every 20 ms, so 50 a second,
with the 20 most recent held), driven and read through DevTools after 5 s on each setting. DevTools needs `DEBUG`, so
the sample was built as Debug with `Optimize=true` rather than Release; the window was 1400x1000 on a 180 Hz display.
Bytes are what the whole process allocated per UI frame, over one-second windows; the page idles at 9.0–9.1 KB per
frame with no gen-0 collections (its frame graph and readouts), with or without 50 idle hosts carrying B per-host visuals.

**Win32**, ui fps / render fps / gen-0 per second / bytes per UI frame (above idle):

| Candidate | 50 buttons | 200 list rows |
|---|---|---|
| A: UI-thread layer | 180 / 178 / 1 / 34.9 KB (+25.9) | 180 / 180 / 0 / 32.0 KB (+23.0) |
| B: custom visual per host | 180 / 180 / 0 / 22.5 KB (+13.5) | 178 / 180 / 0 / 10.3 KB (+1.3) |
| B: window overlay | 176–178 / 174–178 / 0–1 / 14.4 KB (+5.4) | 180 / 180 / 0–1 / 13.1 KB (+4.1) |
| C: compositor ellipses | 180 / 180 / 0 / 19.8 KB (+10.8) | 180 / 180 / 0 / 16.3 KB (+7.3) |
| B per host, soft edge | 178 / 180 / 1 / 35.0 KB (+26.0) | — |

**Browser: not measured.** The avalonia_devtools tools, the only input and inspection this spike may use, attach only to
desktop processes (`attach-to-app` lists none other), the Browser head has no launch options to pick a candidate or start
the auto-presser, and reading its readouts would mean capturing a browser window. **Android: not measured,** for the
same reasons, and because `samples/PrestoSample.Android` does not build on this machine: D8 fails with
`Type androidx.core.animation.AnimatorKt$addListener$1 is defined multiple times` across the `androidx.core` and
`androidx.core-ktx` libraries (seen after the dependency bump; nothing in this phase touches the head). Both stay open
for the sample item: the choice below assumes a separate render thread, which a single-threaded Browser build may not
have, so measure there before Phase 62 closes.

Findings:

- **No candidate costs a frame on Win32.** Every setting held the display's 180 fps on both threads, so the choice
  rests on allocations, on which thread does the per-frame work and on what each can clip.
- **A allocates most and works on the UI thread.** Each live host records a new drawing every frame: +23–26 KB per
  frame on Win32, and 40 KB per growing frame for 50 hosts headless.
- **B per host costs only what is drawn.** With most of the pressed rows scrolled out of view, 200 rows cost +1.3 KB
  against +13.5 KB for 50 visible buttons. Its scene timer cancels once the ink has faded, so idle hosts allocate
  nothing. A radial-gradient soft edge doubles its cost (+26.0 KB), so the solid circle stays the default.
- **The window overlay is cheapest but escapes clips.** It places ink by the host's window position at the press and
  clips only to the host's own rounded rectangle, so the ink of a row scrolled out of view drew over the frame graph below
  the scroller, and held ink would stay behind while the list scrolls. Fixing that means walking every
  ancestor clip and following scrolls, which the per-host visual gets for free.
- **C pays per press.** Each press adds an `Ellipse` to the tree and a compositor animation with its UI-thread mirror:
  growing frames are cheap (7.5 KB headless), but a press-and-release cycle costs 33.8 KB per frame headless, near A.
- **A custom visual needs no layer control.** `ElementComposition.SetElementChildVisual` takes any visual, and the child
  visual is composed after the host's visual children, so it draws over the content
  (`AnElementChildVisualDrawsOverTheHostsChildren`). Its clip is `ClipToBounds` only, with no geometry, so the rounded
  corners are a clip the handler pushes itself (`AGrownRippleCoversTheHostButNotItsRoundedCorners`).
- **A custom visual draws outside its size only when unclipped and within widened render bounds.** It starts with
  `ClipToBounds` on, and unclipped it is still drawn only within `GetRenderBounds`, which defaults to its size
  (`AnElementChildVisualDrawsOutsideItsSizeOnlyUnclippedAndWithinWidenedRenderBounds`).
- **A touch scroll takes the pointer from the pressed child.** When a touch drag starts a scroll, the
  `ScrollGestureRecognizer` captures the pointer: the child gets `PointerCaptureLost` and never `PointerReleased`
  (`ATouchDragThatStartsAScrollTakesThePointerFromThePressedChild`).

Decisions for the API items:

- **Candidate: B per host** (PLAN decision 100). Each host gets one `CompositionCustomVisual` through
  `SetElementChildVisual`, sized to the host's bounds and resized with them, which draws its ripples on the render thread
  under the host's rounded clip; the UI thread only posts press, release and cancel. The host's
  child-visual slot is the ripple's, so a `CompositorCanvas` cannot carry one.
- **Over the content** (PLAN decision 101), as Compose's indication is: the child visual composes after the host's
  children, and drawing under the content of an arbitrary control would take a template change. Ink and state layer are
  translucent, so text stays readable.
- **Switched on by an explicit `Ripple.Enabled`** (bool, default false), so a style can turn ripples on without picking
  a colour, and `Ripple.Color` (`Color?`, default null) stays an override of the theme default.
- **Default colour: the theme accent.** `SystemAccentColor` on the light variant and `SystemAccentColorLight2` on the
  dark one (a lighter shade that still reads on dark surfaces), looked up on the host for its `ActualThemeVariant` at each press, so a
  theme switch applies to the next press. Ink is drawn at 24 % alpha; the state layer at Material 3's 8 % hover, 10 %
  focus and 10 % pressed, fading between them over `Tokens.Short`. Without the resource, the host's
  `TextElement.Foreground` colour stands in. An explicit `Ripple.Color` keeps its own alpha.
- **Bounded (the default):** the ink grows from the press point, which stays put, from radius 0 to the distance to the
  host's farthest corner, clipped to the host's rounded rectangle (`CornerRadius` from `Border` or `TemplatedControl`,
  square otherwise). Compose and Flutter also start at about 30 % of that radius and drift the centre to the middle; the
  bench did neither and read as a ripple, so neither is built.
- **Unbounded (`Ripple.Bounded` false):** the ink grows from the host's centre, whatever the press point, to half the
  host's diagonal, with no clip of the host's own: the visual turns `ClipToBounds` off and widens `GetRenderBounds` to
  the circle, so only ancestors' clips apply.
- **Timing from `Recipes.Tokens`:** the radius grows over `Medium` with `Ease` (450 ms `OutCubic` in `Standard`, as the
  bench), and release fades the ink over `Short` (200 ms), linear, while the radius keeps growing; a ripple released
  early finishes growing as it fades.
- **Press and cancel:** pointer and key events are handled with `handledEventsToo`, since `Button` marks its presses
  handled. Enter or Space down ripples from the centre and the key up releases. Moving the pointer out of the host or
  `PointerCaptureLost` cancels, which fades the ink like a release.
- **Tap timeout:** a touch press inside a `ScrollViewer` waits 100 ms (Android's `ViewConfiguration.getTapTimeout()`,
  which Compose also uses) before its ink appears. A scroll that starts first takes the capture and shows nothing; a
  release first shows the ink from the press point and fades it at once. Mouse and pen presses never wait.
- **Reduced motion** (read at the press): no ink; the pressed tint shows at once for as long as the press is held,
  whatever `Ripple.StateLayer` says, and `StateLayer` adds the hover and focus tints without fading.

### 8.5 Tests and sample

- **Tests:**
  - Origin at the press point, and at the centre for a key press.
  - Clipped to the corner radius.
  - Hold, then release.
  - A cancelled drag or scroll shows nothing.
  - Many concurrent ripples within the allocation budget.
  - Reduced.
- **Sample:** ripples on the Micro-interactions buttons and on a list.

**Built against the sketch:** everything in §8.3, plus the explicit `Ripple.Enabled` switch that §8.4 decided on;
nothing sketched was dropped. The ink is a solid circle with no soft edge and no Compose-style start radius or centre
drift. The sample has a Ripple reference page (filled, default-colour and unbounded buttons, focusable cards in three
colours, and a bounded, state-layered ripple on a 20-row `ListBox`), and the Micro-interactions buttons keep white ink
through one `Button.specimen` style. The Ripple bench page, its candidates and the measurements of them
were removed; the tests that pin Avalonia behaviour stay, and `AllocationTests.FiftyConcurrentRipplesDrawWithinTheirBudget`
replaced the per-candidate budget. Browser and Android are still unmeasured.

## 9. Phase 63 — Prebuilts 3: selection indicator, swipe actions, sheets and drawers

Each of these exists today only as sample code, and each is a separate sub-phase (63a–63c).

### 9.1 Selection indicator (63a)

- **Gap.** A pill or underline that glides between the selected items of a tab strip, segmented control, list or nav.
  Framer Motion does it with `layoutId` on the indicator. The gallery nav's pill is hand-built (Phase 52): an
  `Animatable` over `Y`/`Height` in a re-templated `TreeView`, re-placed without easing on resize, scroll-into-view and
  collapse, and hidden while its item is filtered out.
- **Sketch.** `Selection.Indicator` on a `SelectingItemsControl` (and `TreeView`) draws one indicator inside the items
  host and places it from the selected container's bounds, so it scrolls with the items and survives virtualization.
  - Modes: `Fill` (behind the item) and `Underline` (an edge bar), with an optional stretch where the leading edge leads
    and the trailing edge follows, like Material's tab indicator.
  - It snaps without easing on resize, scroll-into-view and collapse, and hides when the selected container is not
    realized or not visible.
  - The alternative, a `Connected` key on a per-item element shown only when selected, depends on Phase 57 and breaks
    under virtualization. Decide here; the dedicated indicator is the candidate.
- **Reduced:** the indicator moves without easing.
- **Sample:** the copies migrate: `Views/NavSelectionPill.cs` (76 lines), MicroInteractions' stretching underline
  (43–51, 92–109), BloomNav (48–54, 102–103), Liquid's squashing pill (77–130) and the shared `Showcase/TabIndicator.cs`.
  Liquid's gooey squash may stay sample-side if the stretch option cannot express it.

### 9.2 Swipe actions and swipe-to-dismiss (63b)

- **Gap.** Flutter's `Dismissible`, SwiftUI's `swipeActions`. The Swipe deck throws cards in sample code; there is
  nothing for list rows.
- **Sketch.** `Swipe.Leading`/`Swipe.Trailing` action templates on an item container or any control:
  - A horizontal drag reveals the actions. A partial reveal snaps open or closed through `Inertia` with snapping, and a
    full swipe past a threshold commits (`Swipe.Command`).
  - A dismiss plays `Presence.Exit` (or Phase 57's hide), and `Animate.ItemLayout` closes the gap.
  - It rubber-bands past the ends and locks to one axis, with one-finger vertical drags passing through to the
    scroller (Transformable's rule and Draggable's threshold).
  - One row is open per list. Tapping elsewhere or pressing Escape closes it.
  - The actions must also be reachable without swiping (keyboard or a context menu), which the docs state.
- **Reduced:** reveal and dismiss without easing.
- **Sample:** the Gestures inbox (`GesturesPage.axaml.cs` 152–211, ≈60 lines) migrates. The Swipe deck
  (`SwipeDeckPage.axaml.cs` 104–260) stays sample code; it is a card throw, not a row action.

### 9.3 Sheets and drawers (63c)

- **Gap.** A bottom sheet with detents and a side drawer with edge-swipe-to-open exist only in the Gestures page.
- **Sketch.** A `Sheet` control (control or attached form to decide), hosted in the window's overlay or a page:
  - Detents as fractions or absolute heights. A drag releases through `Inertia` to the nearest detent.
  - A scrim whose opacity is mapped from the sheet's position through a `MotionValue`.
  - Scroll handoff: an inner `ScrollViewer` at its top passes the drag to the sheet.
  - Escape, back and a scrim tap close it. Open and close follow `Presence`.
  - `Drawer` is the side variant (`Edge.Left`/`Right`), with an edge swipe to open.
- **Reduced:** snap to detents without easing; the scrim fades.
- **Sample:** the Gestures curtain (`GesturesPage.axaml.cs` 76–112, three stops and a tapped handle) migrates.

### 9.4 Tests

Per sub-phase, headless with pointer sequences and velocities, including Reduced, plus a Desktop check of the drags by
hand: DevTools input cannot drag (as in Phases 51 and 52).

## 10. Phases 64–65 — Patterns lifted from the samples

A read-only survey of the gallery found patterns hand-built on several pages. Sizes count motion code only. Each item
is a sub-phase that migrates the pages it names, so the sample keeps proving the prebuilt. Each states its reduced form
(§1.2).

### 10.1 Phase 64 — Hosts

- **64a Carousel / pager.** `CarouselPage.axaml.cs` 59–101 and 173–240 (≈140 of 245 lines):
  - An x-only `Draggable` with `Utils.Snap(slideWidth)` and `ReleaseInertia`, whose bounds are redone by hand on resize
    (216–228).
  - Autoplay through a looping `Draw` ring, pausing on hover.
  - Keyboard navigation, a spring `GoTo`, and dots that stagger their width.
  - Parallax layers through `Follow`/`Map`.

  The prebuilt is a `Carousel`-like host (or an attached form on Avalonia's `Carousel`, to decide) with `SelectedIndex`,
  snap, autoplay, looping, a dot or progress indicator, and a parallax `MotionValue` per slide.
- **64b Toast / snackbar host.** Two copies: `Builder/ToastsPage.axaml.cs` 21–61 and `MicroInteractionsPage.axaml.cs`
  130–160 (≈70 lines).
  - Entrance and exit through `Presence`, restacking through `{presto:Reflow}`.
  - A countdown bar whose `OnComplete` removes the toast, with hover to pause and swipe to dismiss.
  - A queue cap.

  The prebuilt is a host with a queue and the countdown built in. It also clears two workarounds: a trim posted out of
  `CollectionChanged` (ToastsPage:51), and a countdown kept in `Tag` because `GetAnimations` omits paused animations
  (PLAN §7). Decide whether to fix that §7 item here.
- **64c Modal / dialog host.** Three copies of ≈40–50 lines: Storefront 74–96, GalleryFlip 81–126, Connected 47–53
  and 78–159.
  - A scrim and a background blur.
  - A reveal from an origin (`ClipCircle` from a card's centre).
  - Escape and click-outside to close; hit-testing and `IsVisible` toggled in `OnComplete`.

  The prebuilt is an overlay host with `Open`/`Close` builders, reveal-from-element and focus return, closing through
  Phase 57's hide.
- **64d Accordion.** `MainView.axaml` 41–62 with `MainView.axaml.cs` 22–34 (`ContentSize` fold plus a chevron state);
  MicroInteractions 111–128 (single-open by hand, and the body itself not size-animated). The prebuilt is an
  `Expander` style or attached form with an eased body (`Animate.ContentSize`), a chevron spring and a single-open
  group, with children revealed through Phase 57's `EnterOnShow`.
- **64e Collapsible pane.** `Views/SidebarPane.cs` (whole file) with `MainView.axaml.cs` 85–90. It tweens a helper
  `double` and writes a `GridLength` and a negative margin in `OnUpdate`. Needs PLAN §7's pixel-only `GridLength` codec,
  built here. The prebuilt is a pane recipe for a `Grid` column or a `SplitView`: collapse, restore the dragged width,
  and clip rather than reflow below `MinWidth`.

### 10.2 Phase 65 — Effects and gestures

- **65a Scroll reveal outside virtualizing lists.** `ScrollStoryPage.axaml.cs` 81–106 (comment at 94: `Presence.Reveal`
  only plays in a virtualizing list) and `ScrollPage.axaml.cs` 21–31: an observer linked to a paused fade per block.
  Make `Presence.Reveal` work for any element inside a plain `ScrollViewer` (once per element, with a viewport
  threshold), or add `Scroll.Reveal`. Decide here; the first keeps one name for one idea.
- **65b Progress ring, draw recipes and the async button.**
  - Value-bound `Draw` is written by hand in Gestures 62–66 (comment at 65: no `Follow.Draw`), Carousel 62–70,
    Dashboard 64–74/129–157/198–208 and Loaders 68–86. `DrawIn`/`DrawOut` and check/cross geometries are rewritten in
    SubmitFlow (≈100 lines), MicroInteractions 55–72, TitleSequence, MorphLab and ChartMorph.
  - Build: a `Follow.Draw` shorthand (PLAN §7 candidate); a determinate ring / segmented arc control, which also gives
    hold-to-confirm; `Recipes.DrawIn`/`DrawOut`; and a submit-button recipe (collapse to a spinner, morph to a check or
    cross, expand back).
- **65c Pull to refresh.** `GesturesPage.axaml.cs` 50–74 and 115–150 (≈60 lines):
  - A y-only `Draggable` with a rubber-band modifier.
  - A threshold, an arrow rotation mapped from the pull, and a ring that turns into a spinner.
  - Staggered row insertion and a spring back.

  Avalonia ships `RefreshContainer`, so decide between restyling it with Presto motion and a Presto attached form. The
  modifier re-applied on release (SAMPLES-PLAN §8; the page's `_pulling` gate) is fixed or documented here.
- **65d Circular reveal transition.** `BloomNavPage.axaml.cs` 56–108 (≈50 lines): a scaled `Ellipse` from the pointer
  (the centre for keyboard), the content swapped at 65 %, the new tiles staggered centre-out, and stacked blooms cleaned
  up. The prebuilt is a `PageBloom` page transition and a `CircularReveal` recipe from a point or element, built on
  `ClipCircle`/`ClipCircleCenter`; also a theme-toggle reveal.
- **65e Flip card.** `EffectsPage.axaml.cs` 84–94: `RotateY` and `Perspective`, with the back face's visibility swapped
  past 90° in `OnRender`. `FlipIn`/`FlipOut` exist, but nothing manages two faces. The prebuilt is `Flip.IsFlipped` with
  front and back content.

### 10.3 Found but not lifted (yet)

Too narrow for now, already covered, or waiting on another phase. Revisit after Phase 65.

- **Cross-list drag (Kanban).** `BoardPage.axaml.cs` 72–204 (≈130 lines). `Reorder` has no group or cross-list mode.
  This is the biggest single saving but a narrower audience; a candidate for a `Reorder.Group` phase.
- **Pointer proximity / magnetic pull** (Typography 120–134 and 163–182; the Animatable page's follower).
- **Scroll progress bar** (`ScrollValue(progress: true)` into `Follow.SetScaleX`, about 3 lines; a `Scroll.Progress`
  convenience at most).
- **Rotating headline word** (Typography). Declarative since Phase 58: `Presence.Exit` per headline, a `Wait` slot.
- **Scatter siblings from the selected item** (Connected 176–199, ConnectedNav 117–132). Needs a stagger "from element or
  point".
- **Skeleton → content** (Storefront 40–52, Activity feed). `Shimmer` plus `Presence.Exit` already compose; an
  `IsLoading` presenter saves little.
- **Chart helpers** (Dashboard, ChartMorph). Niche.
- **Arc-flight preset** (`Showcase/PhotoFlight.cs`, 13 lines). A one-line `Recipes.ArcFlight`.
- **`AfterFirstArrange`** (`Showcase/ShowcasePage.cs` 27–41), used by five pages to place indicators after layout. A
  `PrestoAvalonia` utility; fold it into 63a if the indicator needs it.
- **Tooltip at an anchor** (ChartMorph 55–58 and 194–206) is Phase 61.
