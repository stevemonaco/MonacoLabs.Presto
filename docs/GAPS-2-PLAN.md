# Gaps 2 — show and hide, scrolling, lifetime, popups and prebuilts

Phases 55–65 in [PLAN.md](PLAN.md). This plan comes from the post-Phase-54 review of traditional UI animation gaps
(3D and shaders excluded), checked against the committed code (`fe128fe`) and the Avalonia 12.1.2 assemblies. Every
phase is a design sketch: it settles its open decisions in its section here, or in its own plan document if it
outgrows one, before it starts. Phases 57, 59, 60, 61 and 62 begin with a spike, because they rest on Avalonia
behaviour nobody has verified yet.

| Phase | Gap | Scope | Size |
|---|---|---|---|
| 55 | Reduced-motion holes | core + bridge | small |
| 56 | Discrete values and `AutoAlpha` | core + bridge | small–medium |
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

- **`Scroll.Speed`** behaves as speed 1 (no translation) while the flag is set. The flag has no change notification
  today (`PrestoDefaults.ReducedMotion` is a plain property), so either add one, or re-read it on the next `Offset`
  notification, so a flipped flag takes effect at the next scroll. Decide here; Phases 57–63 need the same notification.
- **Duration overloads** land at once under the flag, as `Recipes.Reflow`'s reduced form does.
- **Dictionary params** gain `WhenReduced`, an `AnimationParams?` (and an `AvaloniaAnimationParams?` in the bridge)
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

## 2. Phase 56 — Discrete values and `AutoAlpha`

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
- **Strings are not affected.** Non-numeric strings keep upstream's complex-value path. Step 1 checks what upstream
  4.5.0 renders for a non-numeric string tween and pins it with a test.
- **Flip rule, to decide.** The candidate is CSS's default: flip at progress 0.5. `IsVisible` (like CSS `visibility`)
  instead stays `true` for the whole tween when either end is `true`, so a fade-out stays visible until it ends and a
  fade-in shows from its first frame. `IsHitTestVisible` and `IsEnabled` may want the reverse (`false` throughout), so a
  fading-out element stops taking clicks at once. Decide between:
  - a fixed per-property table in the bridge;
  - a `TweenParams.Discrete` rule (`Midpoint`, `AtStart`, `AtEnd`, `TrueThroughout`, `FalseThroughout`).
- **Keyframes** flip per segment by the same rule. Seeks and reversals work unchanged, because the value is a function
  of progress.
- **What rejects discrete values:** relative values (`+=`) and `Blend` composition throw with a clear message. An
  `Animatable` retarget snaps. A compositor animation throws, as it does today for any UI-thread-only property.
- **`AutoAlpha` shorthand** (name to settle) in the bridge: one tween over `Opacity` that holds `IsVisible = false` at
  `Animation` priority while the opacity is 0, and clears the hold otherwise. The `{presto:Fade}` family can opt in.
  This is the hold Phase 57 builds on.

### 2.3 Tests and sample

- **Tests:**
  - Midpoint flip in both directions; `IsVisible` true throughout; seek and reverse across the flip; keyframes.
  - `Revert` restores the value; `Presto.Set` on a `bool` and an enum; a plain object's `bool` in the core.
  - Relative values and `Blend` throw.
  - `AutoAlpha` hides at 0 and shows above it.
  - Both allocation suites stay at budget.
- **Sample:** Value types page rows for `IsHitTestVisible`, `FontWeight` and `HorizontalAlignment`, and an
  `AutoAlpha` hide.

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
- **`Presence.HideMode`, to decide:** `Hold` (the live path above) or `Ghost` (layout collapses at once and the exit
  plays on a ghost, as for a detach, so neighbours close up during the exit). Pick the default here.
- **Shown again mid-exit** retargets from where the element stands (replace composition), with no snap.
- **Revealed by an ancestor (closes the §7 item).** When a host's own `IsVisible` flips to true (an expander, a
  collapsed section), its descendants with `Presence.Enter` or `Arrive` replay as one batch, so order staggers work
  (decision 23). This is opt-in on the host (`Presence.EnterOnShow`), so expanding a deep subtree does not replay
  everything by default. Hiding an ancestor plays nothing beyond the host's own `Exit`.
- **Reduced:** `ReduceAll` hides and shows at once; builders pick their `WhenReduced`.

### 3.3 Spike first

- Re-asserting `IsVisible` at `Animation` priority inside its change notification: no hidden frame is rendered, and no
  layout invalidation loop.
- A style setter or binding on `IsVisible` that changes during the hold wins when the hold is released.
- What the 12.1.2 Fluent `Expander` and `TreeViewItem` templates actually toggle (`IsVisible`, height or a transition).

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

## 4. Phase 58 — Text exits

### 4.1 Gap

PLAN §7 (Phase 53d): `Presence.Exit` plays on one ghost bitmap, so a word cannot leave letter by letter. The Typography
rotator splits the outgoing word with `SplitText` and drops its characters imperatively.

### 4.2 Design sketch

- **One ghost per piece.** When a leaving element has `TextReveal.Pieces` and an exit builder, the tracker records the
  pieces' slots while attached (Presence already records slots). On detach it snapshots each piece into its own ghost
  in one overlay host, and applies the exit builder to the set of ghosts as its targets, so index staggers and
  `StaggerByLine` work. Line masks snapshot the splitter's per-piece clipping wrapper, so each ghost keeps its line box.
- **Cost cap, to decide:** beyond some number of pieces, fall back to words, then lines, then a single ghost.
- **Hidden text** (Phase 57, `Hold`) plays on the live pieces instead, with no ghost.
- **Recipes:** `LineRise`, `LineMask`, `LineWipe`, `WordFade`, `WordBlur` and `CharCascade` gain exit forms, which
  their markup extensions already pick under `Presence.Exit` (decision 34).
- **Reduced:** the recipes' existing reduced forms (a plain fade on the whole ghost).

### 4.3 Tests and sample

- **Tests:** ghost positions match the pieces' slots; stagger order by index and by line; the cap falls back; the host
  is dismissed when the last ghost completes; `ReduceAll`.
- **Sample:** the Typography rotator becomes declarative and loses its imperative drop.

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
- **Code API:**
  - `PrestoAvalonia.ScrollTo(scroller, offset, builder?)`.
  - `ScrollIntoView(element, alignment: Start/Center/End/Nearest, margin, builder?)`, through the nearest scroller
    first. Nested scrollers are a decision for this section.
  - `ScrollToIndex(list, index, alignment, builder?)` for a virtualized list. The item is not realized yet, so: jump
    close when it is far (Compose's approach; the threshold is to settle), animate the last stretch, and retarget as
    realization corrects the extent.
  - Each returns an `Animation`.
- **Default motion:** a token duration scaled by distance and capped, or a spring; to settle.
- **`Scroll.StickToEnd`** on a `ScrollViewer`: while the view is at the end, growth of the extent eases to the new end.
  Scrolling away releases it, and returning to the end re-arms it. It must not fight `Presence.ScrollAnchor`.
- **Interplay:** a running `ScrollTo` cancels a pending snap, and a snap waits for a `ScrollTo` to end. `ScrollObserver`
  needs nothing, because it reads `Offset`.
- **Reduced:** jump.

### 5.3 Tests and sample

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

  Default to decide; `Pause` is the candidate, recorded as a deviation from decision 8 in DESIGN-NOTES.
  - A paused animation leaves the engine's list, so a control that never comes back becomes collectable with its
    animation. The reattach hook must therefore hang off the control (a handler or attached value), never a static
    table.
  - Targets the bridge creates for a visual (brushes, transforms, effects) need an owner mapping that is always on;
    `TargetOwner` records only while diagnostics are active. Measure what keeping it on costs.
  - A timeline pauses when all its children's visual targets are detached. Plain-object targets always continue.
  - Presence, Motion, page transitions, Connected and ghosts manage their own animations and are unaffected.
- **`Engine.PauseOnHidden`**, upstream's `pauseOnDocumentHidden` (default `true`, as upstream). The bridge drives it:
  - every window `Minimized`;
  - the app moved to the background (`IActivatableLifetime.Deactivated` with a background kind, on mobile);
  - the Browser page hidden.

  `Engine.Pause`/`Resume` already carry upstream's time-base handling, so playback resumes without a jump. Whether
  compositor scene engines pause too is a decision here.

### 6.3 Spike first

- Per platform: what `RequestAnimationFrame` delivers while minimized, and the CPU cost.
- Whether `IActivatableLifetime` fires on desktop minimize (expected not, hence `WindowState`).
- Whether the Browser lifetime reports page visibility.

### 6.4 Tests and sample

- **Tests:**
  - A detached loop pauses and the engine idles; reattaching resumes at the same progress.
  - A control detached for good is collected (`WeakReference` plus a forced GC).
  - `Revert` reverts and `Continue` is unchanged.
  - The timeline rule.
  - A minimized window pauses the engine and resumes where it was (headless, through a hook); `Then()` still completes.
- **Sample:** the DevTools overlay labels detached-paused rows.

## 7. Phase 61 — Popups and flyouts

### 7.1 Gap

Nothing in the bridge handles `Popup`, flyouts, `ContextMenu` or `ToolTip`.

- **Open:** a popup's content probably classifies as `Initial`, so `Presence.Initial` may already play. Unverified.
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

- **Attached properties on the popup owners:** `Popover.Open` and `Popover.Close` builders (names to settle), settable
  on `FlyoutBase` (an `AvaloniaObject`, not a `Control`), `ContextMenu`, `Popup` and `ToolTip`, and from styles.
- **Open** plays on the presenter, with its start values applied in the turn it opens (the page-transition rule), so
  the first rendered frame is never at rest.
- **Close where cancellable** (`PopupFlyoutBase`, `ContextMenu`): cancel the first `Closing`, play `Close` on the live
  presenter, then close for real and let the second `Closing` through. Reopening mid-close retargets.
- **`Popup`** (no `Closing`): hold `IsOpen` at `Animation` priority while `Close` plays (Phase 57's technique), if
  the spike shows that works. **`ToolTip`**: the same hold on `ToolTip.IsOpen`, or a ghost in the anchor window's
  overlay.
- **Placement-aware recipes:** `Recipes.PopoverIn/Out` read the placement, so the presenter scales and slides from the
  anchored edge. They are transform and opacity only, so they run on the compositor (decision 34).
- **Theme conflicts:** check the 12.1.2 Fluent presenter and tooltip templates for built-in transitions, and document
  them.
- **Reduced:** close at once; open with the recipes' reduced fade.

### 7.3 Spike first

- What `Presence` classifies popup content as on open.
- Native and overlay popups on Win32, and overlay-only heads (Browser, Android).
- Cancel and re-close on `PopupFlyoutBase` for each dismiss path: light dismiss, Escape, `Hide()`, the owner detaching.
- The `Popup.IsOpen` and `ToolTip.IsOpen` holds.

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

### 8.4 Tests and sample

- **Tests:**
  - Origin at the press point, and at the centre for a key press.
  - Clipped to the corner radius.
  - Hold, then release.
  - A cancelled drag or scroll shows nothing.
  - Many concurrent ripples within the allocation budget.
  - Reduced.
- **Sample:** ripples on the Micro-interactions buttons and on a list.

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
- **Rotating headline word** (Typography 86–118). It becomes a few lines of XAML once Phase 58 lands.
- **Scatter siblings from the selected item** (Connected 176–199, ConnectedNav 117–132). Needs a stagger "from element or
  point".
- **Skeleton → content** (Storefront 40–52, Activity feed). `Shimmer` plus `Presence.Exit` already compose; an
  `IsLoading` presenter saves little.
- **Chart helpers** (Dashboard, ChartMorph). Niche.
- **Arc-flight preset** (`Showcase/PhotoFlight.cs`, 13 lines). A one-line `Recipes.ArcFlight`.
- **`AfterFirstArrange`** (`Showcase/ShowcasePage.cs` 27–41), used by five pages to place indicators after layout. A
  `PrestoAvalonia` utility; fold it into 63a if the indicator needs it.
- **Tooltip at an anchor** (ChartMorph 55–58 and 194–206) is Phase 61.
