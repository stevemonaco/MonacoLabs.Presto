# Backlog

The single list of work not done in MonacoLabs.Presto: known bugs, gaps, checks, candidates and undecided questions.
Small items are taken straight from here and fixed with their spec updated; larger ones become a change proposal in `docs/changes/` first (see [docs/changes/README.md](changes/README.md)).
When an item is closed, its line is deleted.

## Likely bugs

Each line was traced in code (2026-10). Lines marked **decision:** name the design choice a change proposal must settle; the rest are direct fixes.

- [BRIDGE-CODECS] A solid brush's `Opacity` is dropped by every brush tween: solid→solid writes 1, solid→gradient snaps to the gradient's opacity, gradient→solid ends on the gradient's (BRIDGE-CODECS-005/-008 encode the drop); **decision:** fold opacity into alpha at decode (as `Tints` does; a color end then ends opaque) or carry it beside the channels; done when that is decided and `CodecTests` pins solid→solid, solid→gradient and gradient→solid.
- [BRIDGE-THREAD] An alternating compositor animation with `LoopDelay` compiles ⌈n/2⌉ forward-and-back cycles (BRIDGE-THREAD-023), so an odd count plays one extra iteration plus two loop delays past the mirror's end and rests on the start value while the mirror reports the end (even counts trail by one invisible delay); **decision:** unroll finite expanded loops into one sampled track spanning the mirror's duration (cycles kept for `Forever`); done when compiled tracks last exactly the mirror's duration and end on its end value, -023 is rewritten, and an odd-count `CompositorCompileTests` case exists.
- [CORE-ANIMATABLE] Each `Animatable.ValueOf` call adds an `_onRender` subscription that is never removed (CORE-ANIMATABLE-016 asks for a new value per call), so values and their subscribers accumulate and every render assigns all of them; **decision:** one cached `MotionValue` per property (recommended) vs. weak or disposable subscriptions; done when repeated calls add no subscriptions and -016 is updated.
- [CORE-SCRAMBLE] `Presto.ScrambleText` caches a target's first text forever (as upstream does, so interrupting a running scramble still settles on the real text), so an unchained scramble without `Text` after the app changed the text settles on the old one; **decision:** refresh the cache unless the current text is the scrambler's own last write; done when that deviation is recorded and tested both ways.
- [BRIDGE-PAGER] `Pager.SelectedIndex` set directly is never coerced (BRIDGE-PAGER-014 says so), so without `Loop` an out-of-range value sends the strip past the slides and with `Loop` the view is right but the index and dots are not; slide removal leaves it past the end too; **decision:** coerce the styled property (re-coerced on `Slides`/`Loop` change, value kept with no slides, two-way binding checked) vs. normalize only at travel; done when it reads in range like `GoTo`.
- [BRIDGE-SHEET] `Sheet.DetentIndex` is never coerced (BRIDGE-SHEET-010, inherited), so it can read past the detents (also after `Detents` shrinks), and on a single-detent sheet `IsOpen = true` sets the index to 1, leaving `IsOpen` true and the scrim taking hits while the sheet rests closed; **decision:** whether `IsOpen` means "index above 0" or "resolved extent above 0" (an `Auto` detent over empty content); done when the index is coerced to the declared detents and `IsOpen` re-syncs after its handler writes the index.
- [CORE-BUILDER] [CORE-REDUCED] [CORE-KEYFRAMES] Builders resolve at build time against `Engine.Current` by requirement (CORE-BUILDER-020, CORE-REDUCED-004, CORE-KEYFRAMES-037): `Timeline.Add(builder)` on another engine's timeline picks the reduced form by the wrong engine (the dictionary path uses the owner engine), and `TweenKeyframesBuilder.Color` ignores the timeline's `Defaults`; **decision:** `ToParams` emits both forms for `Resolve` to pick, and whether the animation's own `Ease`/`Duration` reach a color expansion; done when both resolve against what they are added under and the requirements are rewritten.
- [CORE-ANIMATION] A zero-duration animation with a `Delay` lands at once, because `Resume` force-renders `MinValue`-duration animations without waiting out `_delay` (upstream does the same), so a zero-duration state return under `StaggerChildren`/`DelayChildren` ignores its orchestration delay; done when a decision keeps or fixes it.
- [BRIDGE-RECIPES] Unverified since BRIDGE-RECIPES-096 (20b8d54 completes a text morph whose block detaches): a `MorphGlyphs` started in a native popup as its content moved away left the block split and blank; Tempo's `Stepper` still morphs only on a step. Done when the morph completes on Windows with the `Stepper` gate removed, plus a headless test of a morph started as its block leaves a second `Window` (headless has no native popups); delete if both pass.
- [BRIDGE-REVEAL] [BRIDGE-TEXT] A split arrival freezes a bound `Text` that changes mid-arrival: `TextSplitter` captures the text at trigger time, splits from it, and on completion `Revert` writes it back as a local value (BRIDGE-TEXT-021), over the binding's newer value (Tempo's Stats best-day line); `Split`/`Revert` and the text morphs may also be dropping the binding itself; **decision:** re-split on the new text (keeping time) vs. end the reveal, and `SetCurrentValue` for every splitter/morph write; done when the block shows its current `Text` once the arrival ends and its binding still updates afterward, with a test.

## Missing validation

- [CORE-EASES] `Eases.Steps` accepts 0 or negative step counts and returns NaN; done when it throws `ArgumentOutOfRangeException`.
- [LOTTIE-PLAYBACK] `speed <= 0` in `Play`, `PlaySegment`, `PlayOnRenderThread` and the recipe, and a composition with `"fr": 0`, produce infinite, negative or NaN durations; done when both are rejected.
- [CORE-PARTICLES] Particle lifetime 0, a negative `dt` and a negative `MaxParticles` are not validated; done when each throws.
- [CORE-ENGINE] `Engine.Speed` is not clamped, unlike `Clock.Speed`, so 0 or a negative speed produces infinite or NaN start times; done when it is clamped or rejected.
- [CORE-PRESTO] Null inputs to `EaseParser.Parse` and the `Presto` methods throw `NullReferenceException`; done when they throw `ArgumentNullException`.

## Reduced-motion gaps

- [BRIDGE-SCROLLOBSERVER] The `ScrollObserver` snap's 400 ms ease ignores `ReducedMotion`; done when it has a stated reduced form.
- [BRIDGE-FOLLOW] The `Follow.*` shorthands cannot opt into `WhenReduced` (or `AnimateInitial`); `Follow.Timing` is their only option; done when they state and honor a reduced form.
- [BRIDGE-RECIPES] `PageBloom`'s exit (`ClipCircle` to 1 over `Short`) has no `WhenReduced` alternative; done when it has one.
- [CORE-ANIMATABLE] Under `ReduceAll`, `Animatable.Set` with a duration, momentum retargets and `Fling` animate rather than snap, and the callbacks animation completes on its first frame; done when animatables honor `ReduceAll` on every retarget, with tests.

## Undecided deviations from anime.js

- [CORE-EASES] [CORE-SCRAMBLE] [CORE-UTILS] [BRIDGE-DRAGGABLE] [BRIDGE-SHAPES] Inherited upstream defaults (marked `(inherited)` in the eases, scramble, utils, draggable and shapes specs) need a keep-or-change decision; done when each spec's Decisions section records one.

## Stale XML docs

- [BRIDGE-DRAGGABLE] `DraggableParams.OnDrag` says it runs "on the first movement past DragThreshold"; it runs on every move after the threshold.
- [CORE-SCOPE] `Scope.Defaults` says it is "a copy of its engine's" defaults; without `ScopeParams.Defaults` it shares the engine's instance.
- [LOTTIE-PLAYBACK] `LottieSegment.Name` says it is null for an unnamed range; a marker with no `cm` yields `""`.
- [LOTTIE-PLAYBACK] `PlayLottieExtension` says it "has no exit form", but `Presence.Exit` accepts it and plays the forward recipe; fix the doc or reject it there.
- [CORE-EASES] `Eases.FromPath` says the path is normalized to end at (1, 1); a flat path ends at 0.
- [BRIDGE-RECIPES] `Interactions.PressIn`'s summary says it shrinks to 0.96; the full form also dims `Opacity` to 0.8.
- [BRIDGE-RECIPES] `Recipes.Loading`'s remarks say each iteration lasts twice `Long`; `Spinner` defaults to one `Long`.
- [BRIDGE-TOKENS] `MotionTokens.ColorSpace` says it is "the space color states interpolate in"; only `Recolor` reads it.
- [BRIDGE-REVEAL] `Presence.GetReveal`/`SetReveal` describe only the list path; `Reveal` also works outside an item container.

## Manual and platform checks

- [DEVTOOLS-OVERLAY] On Desktop, drive by hand the Ctrl+Shift+F12 gesture, hover outlines, drag-scrubbing the track, and freeze/dilation during a `Draggable` or `Transformable` drag (DevTools MCP sends no modifier chords, hover or drags).
- [BRIDGE-RIPPLE] Measure the per-host render-thread ripple visual on Browser and Android, where a single-threaded Browser build may have no render thread.
- [BRIDGE-SWIPE] Hand-check the swipe-action, `Sheet` and drawer drags on Desktop.
- [BRIDGE-MOTION] Watch orchestration (stagger, `Order` and `StaggerFrom` timings, the `Presence.Mode=Wait` hold, the double-click case) on the Win32 Desktop head; it is verified headlessly only.
- [BRIDGE-REORDER] Check the drag, Escape and Alt+arrow paths by hand on the Win32 Desktop head.
- [SAMPLE] Watch the Eases page's new eases, the Toss card, the Shapes spline demo and the sidebar Follow system switch on the Win32 Desktop head.
- [BRIDGE-FOLLOW] Sweep a real mouse across the Typography wordmark on the Win32 Desktop head, with `--motion full` and `--motion reduced`: the letters nearest the pointer swell and brighten, spring back when it leaves the stage, and nothing jitters (DevTools MCP sends no hover).
- [BRIDGE-MAGNETIC] Sweep a real mouse past the Micro-interactions magnetic buttons on the Win32 Desktop head, with `--motion full` and `--motion reduced`: they lean towards the pointer and spring back, nothing jitters while hovering a pulled button's edge, and reduced motion leaves them still (DevTools MCP sends no hover).
- [BRIDGE-TRANSFORMABLE] Smoke-run `Transformable` with real touch on Android and Browser (iOS only compiles).
- [BRIDGE-TRANSFORMABLE] On a real Android device, check whether an ancestor `ScrollViewer`'s `ScrollGestureRecognizer` captures the first touch before the second lands and cancels the pinch; fix by capturing the first touch or document the opt-out.
- [BRIDGE-TRANSFORMABLE] Verify on macOS hardware that the trackpad rotation sign (`-Delta.X`) is right.
- [BRIDGE-IMAGE] Smoke-run a filtered image and the pixel transitions on the Browser and Android heads, then add a Platforms note to the README.
- [BRIDGE-IMAGE] Verify the mixed-aspect source transition visually and measure the Storefront stutter on Desktop (neither possible through DevTools).
- [BRIDGE-IMAGE] Check `PrestoImageBase`'s 120-DPI raster path at a non-100% display scale.
- [BRIDGE-REVEAL] Drive the Activity feed on Desktop with a real mouse wheel, touchpad fling and scrollbar-thumb drag to confirm the reveal and scroll-anchor fixes (the scroll-anchor cause is inferred, not observed).
- [BRIDGE-EFFECTS] Measure blur cost (Effects page blur stage, Card gallery blur-behind) on the Browser head and update the README Platforms note.
- [LOTTIE-PLAYBACK] Drive the Lottie gallery page in the Browser head.
- [BRIDGE-DRAGGABLE] Fling and re-grab cards repeatedly on the Desktop Draggable page to confirm the drag freeze is gone (only a headless test covers it).
- [SAMPLE] On Desktop, in both motion modes, sweep the nav with a real pointer, drag and double-click the splitter, and press Ctrl+B and Ctrl+F.
- [SAMPLE] On Desktop at `--size 400x850`, in both motion modes: watch the Home and Page push for a one-frame glitch, go back with Alt+Left and the mouse back button, scroll Home with a real wheel before going back, press Ctrl+F at Home (focuses search) and on a page (does nothing), press Ctrl+B (does nothing), confirm Alt+Left on a focused Board priority goes back rather than reordering, drag the window live across 720 DIP, and judge the full-motion `PagePush` mid-slide (DevTools sends no modifier chords or mouse back button).
- [SAMPLE] Run Tempo's outstanding Desktop checks by hand on `dotnet run --project samples/Tempo/Tempo`: hover, press and task-row selection with no double-easing; reorder by dragging a row with a mouse, and on a touch screen or pen by holding until the row lifts (let go in place to open its details), swiping fully both ways (a short swipe springs back), Del then Undo, and the double-click details drawer; skip's hover fast-forward and its sweep, the stop button's cross and ring collapse on a confirmed hold, the start halo and the play icon's half-turn; the backdrop blobs leaning, swelling and brightening under the pointer; scroll shadows and smooth wheel scrolling in the task list and Stats; the intro skipped and at reduced motion, with the ring and wordmark landing with no pop; switching to Reduced mid-session; and the Stats bars replaying on entry (seen only as end states). With touch and a pen (BRIDGE-GESTURES): a vertical touch scroll of the task list never lifts or opens a row, a touch that scrolls a few DIPs then rests does not lift, a pen swipe needs the touch slop.
- [SAMPLE] Run Tempo's compact checks by hand on Desktop at `--size 360x780` and `--size 320x640`, in both motion modes (only headless tests cover them): the bottom bar's pill and icon pop and the page transitions both ways; a minute of countdown with only the changed digits moving; the details sheet dragged between its half and full detents and flung shut, and the settings sheet's rows arriving; swipe actions, the hold-to-open details and the ⋯ button on the Tasks page, and the chip row's faded ends; the Focus dial fitting a short window (`--size 685x604`); the toast above the bar; dragging the window live across 720 DIP with details open and from the Tasks page; and the wide Stats page at `--size 960x640`.
- [BRIDGE-PARTICLES] Measure the default `ParticleScene.MaxLive` of 1 000, and the Particle type and Liquid type frame rates, on Browser, Android and iOS (only desktop's 4 000 was measured).
- [PLATFORM] Run iOS on a simulator (needs a macOS build host; only compiles for `iossimulator-x64`).
- [PLATFORM] Check the Browser and Android heads against the showcase pages and update the README Platforms table with what is true.
- [SAMPLE] By hand on Desktop: the Board drag and insertion gap, the Interruption lab hover tiles and the Typography pointer-driven wordmark (today they rest on their `*PageTests`), Warp studio's Magnifier, Jelly and Follow pointer, and the Liquid type heading's hover glitch.
- [SAMPLE] By hand on Desktop: the Carousel pointer drag.
- [SAMPLE] By hand on Desktop with a real mouse: the Type lab elastic-letter drag (neighbors follow, springs home with overshoot, no jitter at rest) and Bézier control-point drag (text stays on the path); DevTools sends no drags.
- [SAMPLE] By hand on Desktop: Connected and Connected navigation (open a card on `PointerPressed`) and the Gestures page drags, plus on a touch screen its curtain and bottom sheet between detents (handed off from the curtain's list at its top) and the pull-to-refresh list pulled at its top and scrolled away (BRIDGE-GESTURES).
- [SAMPLE] Run the Activity feed live for ten minutes and confirm the reveal memory stays bounded (today it rests on `ActivityFeedPageTests`).
- [BRIDGE-INIT] Check the hidden signal off the desktop: hide the tab in the Browser head and background the app on Android (and iOS where it runs), confirming `Presto.Engine.Hidden` pauses the engine and showing it again resumes at the same progress (only desktop minimize and headless `WindowState` are checked); done when each head is checked or its gap is recorded in the README Platforms table.
- [CORE-MOTIONVALUE] By hand on the Desktop Gestures page's drag-to-open card: slow drags rest on the nearest point, flicks advance, re-grabbing mid-settle does not jump, and `--motion reduced` lands at once (DevTools sends no drags; only the slow-drag and flick cases have a headless test).
- [CORE-MOTIONVALUE] By hand on the Desktop Gestures page's trails row: the spring trail and the Lag (40 ms) trail chase the pointer, the engine idles once the dots settle (DevTools frame stats), and the Resist indicator stretches a fifth as far past ±80 (DevTools sends no pointer moves; only `GesturesPageTests` covers it).
- [BRIDGE-REVEAL] With the Activity feed's Reveal set to "Every time" on Desktop, scroll up and judge whether rows rising from below look wrong; done when judged, and if they do, a change proposal reopens mirroring a reveal by scroll direction (a non-goal today).

## Candidate features

- [CORE-CALLBACKS] An opt-in `Then` variant whose task is canceled when the timer is canceled or reverted (Flutter's `TickerFuture.orCancel`), for code that must not hang on an interrupted animation; done when a spelling is chosen and built.
- [CORE-COLORS] HSL, HWB and CIE Lab/LCH interpolation spaces beside OKLCH, for CSS completeness, once something asks.
- [CORE-COMPOSITION] A relative/delta `To` for additive tweens ("add this much") that does not move the additive group's base, so a layered effect need not know the animation beneath it; done when a spelling is chosen and built.
- [CORE-TIMELINE] A `Timeline.Set` overload that takes a stagger for its position, as upstream allows; add if a sample needs it.
- [CORE-STAGGER] A stagger "from element or point" (a pixel position or a target element rather than a normalized grid cell), so Connected and ConnectedNav can scatter siblings from the selected item.
- [CORE-BUILDER] `AnimationBuilder.Freeze()`, if users report a shared builder being mutated under them.
- [CORE-PARTICLES] A spin-rate over-life curve; spin is a constant per-particle rate today.
- [CORE-SCOPE] `Scope.Register` prunes only superseded animations, so timelines created repeatedly inside a scope (the Typography word rotator is kept outside its scope for this reason) grow its revertible list until `Revert`; done when finished or superseded timelines are pruned too, or the limit is stated in the spec.
- [CORE-INERTIA] Inertia-driven `ScrollViewer` flinging in place of `ScrollGestureRecognizer`'s own inertia (`IsScrollInertiaEnabled`) is unbuilt and would be a control-level change; done when built because a sample needs it, or recorded as a CORE-INERTIA non-goal.
- [BRIDGE-DRAGGABLE] Let a Draggable join the gesture arena (BRIDGE-GESTURES) and claim after its threshold instead of capturing on press, so a touch on a Draggable inside a ScrollViewer can still scroll; done when a vertical touch drag on a horizontal-only Draggable in a ScrollViewer scrolls it.
- [BRIDGE-MOTION] Play transform/opacity-only `Motion.*` states through the compositor, if a sample shows the need for smoothness under load.
- [BRIDGE-PRESENCE] Total-aware staggers (`From = Center`/`Last`, grids) in initial-load batches, which would need deferring a batch to the first tick plus a spike on rest-frame flashes.
- [BRIDGE-SCROLL] A `Scroll.Progress` convenience for a scroll progress bar.
- [BRIDGE-RECIPES] A one-line `Recipes.ArcFlight` to replace the sample's `Showcase/PhotoFlight.cs`.
- [BRIDGE-RECIPES] A text-shaped mask reveal and `MorphTo` clip-geometry reveals.
- [BRIDGE-HELPERS] Lift `AfterFirstArrange` (`Showcase/ShowcasePage.cs`, used by ChatLog, GalleryFlip and Liquid) into a `PrestoAvalonia` utility.
- [BRIDGE-IMAGE] Decide whether `FilteredImage` should resize to an incoming image of another aspect only when the source transition ends, rather than at its start.
- [BRIDGE-IMAGE] A pre-baked crossfade mode (a filtered copy faded with `Opacity`, compositor-capable), if UI-thread filtering proves too costly on Browser or Android.
- [BRIDGE-IMAGE] A per-control native filter cache keyed by matrix, if per-frame `SKColorFilter`/`SKPaint` allocation shows up in measurement.
- [BRIDGE-IMAGE] Document colorizing what lies beneath with an overlay `Image` and `RenderOptions.BitmapBlendingMode` (Color, Luminosity, Multiply; bitmaps and one layer only) as a technique in the README's image-effects section, deliberately not an API; done when documented.
- [SAMPLE] A skeleton-to-content `IsLoading` presenter (Storefront, Activity feed); judged low value.
- [SAMPLE] Chart helpers for the Dashboard and ChartMorph pages; judged niche.
- [RIVE] Rive playback; not planned while Rive has no official .NET runtime outside Unity.

## Sample gallery

- [SAMPLE] `GalleryFlipPage`'s mode toggle could bind `Animate.LayoutMode` once its cards are templated rather than built in code.
- [SAMPLE] ChartMorph's tooltip at an anchor is hand-built (`ShowTip`); move it to `Popover`.
- [SAMPLE] Audit the pages at 400 DIP in the compact layout and list the ones that clip or overflow, with a fix or a minimum-width note per page; Eases, Playground and Springs are known to clip.
- [SAMPLE] The compact Page bar's back button is a 36 DIP touch target, below the 44 DIP of the nav rows, and its About toggle stays open when another page opens; done when the button is 44 DIP and About closes on a page change.
- [SAMPLE] Add a README showcase section with captures of the showcase pages.
- [SAMPLE] At 127 px a Card gallery grid card's `#06 · C. Lindqvist 711` line runs into its right border; done when it fits or trims.
- [SAMPLE] Swipe-to-dismiss on the Activity feed's recycled rows (`SwipeActions` or `Draggable`) beside the dismiss button; done when a swiped row collapses out through `Presence.Exit` and a recycled row carries no stale swipe offset.
- [SAMPLE] The Gestures page trail and resist stages share the panel's background, so their edges are hard to see; done when the stages read as distinct.
- [SAMPLE] A Kinetic poster showcase page chaining every glyph-geometry block (outline pieces and `WriteIn`, multi-contour morph, warps, shards and particle text, `Distort`), deferred until those pieces have settled; done when the page exists with its page test.

## Tooling and tests

- [SAMPLE] Cover the orchestration sample's `Order`, `From` and Presence mode pickers with a sample test.
- [SAMPLE] Cover the Shapes page's spline (motion path through points) demo with a sample test; only `ShapesTests` covers the API.
- [PROJECT] The sample test suite runs about 92 s against a 60 s target; reaching it needs a framebuffer retained between frames, which requires a change in Avalonia's headless platform.
- [PROJECT] Run the test suites in parallel via per-test engines (`AsyncLocal` or a per-session `Presto.Engine`); touches every static entry point.
- [PROJECT] File an Avalonia issue asking for a `TimeProvider` on `CompositionOptions` / `AvaloniaHeadlessPlatformOptions`, so headless tests can drive the compositor clock without reflection.
- [BRIDGE-LAYOUT] File an Avalonia issue: implicit `Offset` composition animations interpolate under the headless render timer but snap on the Win32 backend (12.1.2).
- [SAMPLE] Most of Tempo's view-model tests never await `FlushAsync`, so a debounced save can still be writing `tempo.json.tmp` when the test ends; `TempoTestBase.Dispose` retries the temp folder's deletion and then leaves it. Done when those tests flush (or the store can be told to stop) and the retry is removed.

## Tempo findings

Friction found while building the Tempo sample (`samples/Tempo/Tempo/`): first the calls that have no XAML spelling, then what Semi.Avalonia's templates exposed that Fluent's do not, then the rest. Its bugs are under Likely bugs.

- [BRIDGE-PARTICLES] `Recipes.Burst` is code-only: no XAML trigger fires on a view-model event, so Tempo's `FocusView` bursts from the ring on `FocusViewModel.SessionCompleted` in code.
- [BRIDGE-TOAST] Showing a toast is code-only: Tempo's `ShellView` code-behind handles `MainViewModel.TaskDeleted` and calls `ToastHost.Show`, and its Undo button needs a `Click` handler to reach `ToastHost.GetToast(…).Dismiss()`, since nothing in XAML binds a view-model event or command to a toast.
- [BRIDGE-REDUCED] The reduced-motion flag (`Presto.Defaults.ReducedMotion`, `PrestoAvalonia.FollowSystemReducedMotion`) has no XAML spelling, and an app setting of System / Full / Reduced is the same hand-written switch in every app (dispose the follower handle, then follow again or write the flag); Tempo's `App.ApplyMotionSetting` repeats the Gallery's `MainViewModel.Motion` setter line for line.
- [CORE-TIMELINE] A timeline has no XAML spelling, so Tempo's intro (`Motion/TempoIntro.cs`) lays out its `TempoMotion` steps on a `Timeline` in code and `IntroView` starts it from `OnAttachedToVisualTree`.
- [BRIDGE-POPOVER] A flyout has no `IsOpen` to bind, so opening it from a view-model command (Tempo's Ctrl+, `KeyBinding` and the settings button share `OpenSettingsCommand`) needs code-behind calling `FlyoutBase.ShowAttachedFlyout`, and a state that should hold while it is open (Tempo's settings button keeping its quarter turn) needs code-behind setting a class from `Opened`/`Closed`.
- [BRIDGE-THREAD] A compositor animation has no XAML spelling (XAML entry points stay on the UI thread by design), so Tempo's ambient backdrop drift is an `AnimateOnCompositor` call in `ShellView` code-behind, started on load and reverted on unload by hand; nothing like `Animate.OnAttached` takes a thread.
- [BRIDGE-REDUCED] Nothing signals a change of `PrestoDefaults.ReducedMotion`, so an animation that resolves its `WhenReduced` form once at start keeps it: Tempo's `ShellView` restarts the compositor backdrop drift on the view model's `Motion` change (under System, an OS setting change while the app runs never reaches it), and `SettingsFlyout` replays the `Animate.OnAttached` motion preview by clearing and resetting `OnAttached`, which only works because `App.ApplyMotionSetting` subscribed first and has already written the flag.
- [BRIDGE-RECIPES] `Recipes.MorphGlyphs` is code-only: no XAML trigger morphs a block on a binding change, so Tempo's `FocusView` watches `FocusViewModel.RemainingText` and `StartPauseLabel` while loaded and calls it on each change. The start/pause label also needs a code-side rule a trigger would have to spell: morph only a word into a word, and set an appearing or hiding label at once. The settings `Stepper` repeats the pattern for its `Value`, completing a running morph on unload (the detach bug under Likely bugs).
- [BRIDGE-POPOVER] A popover's content cannot leave in a reversed stagger: `Close` plays only on the presenter, and the content unloads with the popup, so its `Presence.Exit` never plays. Tempo's settings rows arrive in turn but leave with the presenter's `PopoverOut`; a `Close` that ran the content's exits (or a reversed arrival batch) before closing would mirror the Popups-style entrance.
- [BRIDGE-MODAL] Opening a modal from an element is code-only: `Open(from)` is a method and a bound `IsOpen` opens without an origin, so Tempo's `ShellView` handles `MainViewModel.ConfirmRequested` and calls `Open`. And `Open(from)` finds no origin for an element under another visual root (BRIDGE-MODAL-004), as a button in a desktop flyout is, so Tempo converts the button's center through `PointToScreen`/`PointToClient` and calls `Open(Point)`; it also closes the flyout first, since the popup window draws above the scrim.
- [BRIDGE-MODAL] Nothing lets a modal veto or redirect a dismissal: Escape, back and a scrim tap always close it (BRIDGE-MODAL-019–020). Tempo's confirm dialog shakes on Escape once its hold has been pressed by handling `KeyDown` in the dialog before `Dismissal`'s top-level handler, which works only because focus is inside the panel; the shake itself is a code-behind `Animate` (`ConfirmDialog.OnKeyDown`).
- [BRIDGE-PRESENCE] A list removal cannot be choreographed: each removed row plays its own `Presence.Exit` in the removal's pass, so a staggered exit that must finish before the next step (Tempo's clear: rows scatter, then the bars collapse, then the empty line) is an `Animate` over the live rows from code (`ShellView.PlayClear`), awaited before the view model clears. The scatter must be reverted after the clear, or the recycled containers keep its end values and a restored row arrives at opacity 0; nothing ties an animation on an item's element to the item's lifetime.
- [BRIDGE-SUBMIT] `ProgressRing` raises `Confirmed` but has no `Command`, so a hold-to-abandon ring needs a code-behind handler to reach the view model's command. Nor does it raise anything when a hold ends short of confirming, so Tempo's shake on an early release handles the ring's `PointerCaptureLost` and Space/Enter `KeyUp` and tracks whether `Confirmed` came first; nor when a hold starts, so Tempo's confirm dialog treats any `Value` above 0 as pressed through `PropertyChanged`.
- [BRIDGE-ACCORDION] On Semi's `Expander`, `Accordion.Enabled` finds only Fluent's chevron parts (`ExpandCollapseChevronBorder`/`ExpandCollapseChevron`, BRIDGE-ACCORDION-007), so the app must point `Accordion.Chevron` at `PART_PathIcon` from a `/template/` style (Tempo's `Expander.done`) and clear the theme's `Template`-priority 250 ms `RenderTransform` transition on it with a selector that has an activator (`:not(.x)`), or the chevron double-eases. Unlike `Popover` (BRIDGE-POPOVER-008), nothing lifts theme transitions on the properties Presto animates.
- [BRIDGE-MOTION] An app folder named `Motion` (the proposal's `Motion/TempoMotion.cs`) makes a `Tempo.Motion` namespace that shadows `MonacoLabs.Presto.Motion` in C# anywhere under `Tempo.*`, so code-behind and tests need an alias for `Motion.SetState`/`SetStates`.
- [BRIDGE-MOTION] `Motion.State` takes one value, so states that depend on two view-model properties (Tempo's mode and whether the clock runs) need a combined token string from the view model (`FocusViewModel.MotionState`) or a converter; nothing composes tokens from several bindings.
- [BRIDGE-MOTION] `Motion.States` is one property, so a style's states (the ring's mode `Fill`) and a local state (`Running` breathe) cannot both sit on one element; Tempo moves the breathe onto a wrapper `Panel`.
- [CORE-KEYFRAMES] `TweenKeyframesBuilder.Color` needs an explicit `from`, so a state that recolors in OKLCH from wherever the property stands needs a hand-written `FunctionValue` reading the target (Tempo's `TempoMotion.Accent`); `Tints.Recolor` has that reader but keeps it private, and the recipe has no `WhenReduced` to restate.
- [BRIDGE-HELPERS] There is no typed `Fill` (or `Stroke`) builder method, so the `ProgressRing.Fill` mode accent is `Property("Fill", k => k.Color(…, ColorSpace.Oklch))` by name; it does recolor the `IBrush` through OKLCH as a `MotionState`.
- [BRIDGE-CONTENTSIZE] A content size host lays its sole child out at the final size and only clips it, so a `Button` whose theme draws its background on the template's presenter (Semi's) shows the new width's fill at once while the host eases; Tempo's start/pause pill moves the accent onto a `Border` host around a transparent button. Nothing says which part of a control eases.
- [BRIDGE-SELECTION] `Selection.Indicator` draws behind the items but leaves the theme's own `:pointerover`/`:selected` item backgrounds, so on Semi (as on Fluent) the app clears them itself (Tempo's `TaskRow`, `Segment` and `Chip` themes).
- [BRIDGE-SWIPE] `Swipe.Command` gets only the `Edge`, not the row's item, and there is one command per element, so a row whose sides do different things needs an edge-mapping command on the item's view model (`TaskItemViewModel.SwipeCommand`) plus an event back to the list owner to delete.
- [BRIDGE-DECLLAYOUT] `Animate.ItemLayout` and `Presence` follow the `ItemsSource`'s change events, so filtering a list with motion needs a hand-kept filtered `ObservableCollection` that inserts and removes row by row (Tempo's `MainViewModel.VisibleTasks`), and a `Reorder` on it moves the copy, which the app must replay in its source; nothing filters a source while keeping per-row changes.
- [BRIDGE-ATTACH] A session beyond a task's estimate adds a dot that is already filled, and `Animate.Watch` treats a binding's first value as seen (BRIDGE-ATTACH-015), so Tempo's `DotPop` on the dot's `IsFilled` pops only dots filled in place, not that new one; nothing plays `OnChanged` for an element's first value.
- [BRIDGE-DECLLAYOUT] A row that moves to another list (open → Done) is a removal from one `ItemsControl` and an add to another, so it exits and re-enters instead of moving, and a `Follow` on it never shows its change because the new row's first value snaps (BRIDGE-FOLLOW-008). Tempo holds the move for `MainViewModel.DoneMoveDelay` so the strike line draws first.
- [BRIDGE-SPLITFLAP] A `SplitFlap` steps only forward through `Charset` (a non-goal), so a countdown on the default charset wraps each falling digit through the letters (9 to 8 is 36 steps, over 2 s at 60 ms, longer than the second it shows); Tempo's countdown needed `Charset="9876543210"` (which leaves ':' outside the charset to take one step) until it moved to `MorphGlyphs`.
- [BRIDGE-RECIPES] The text splits and glyph outlines ignore `FontFeatures` (`TextSplitter` copies family, size, style, weight, stretch and letter spacing; `GlyphOutlines` shapes with none), so a block with tabular figures (`+tnum`) would morph on proportional slots and jump when it unsplits. Inter's figures are proportional, so Tempo's countdown puts each digit in its own fixed-width, centered block instead.
- [BRIDGE-RECIPES] `MorphGlyphs` once a second for a whole session (Tempo's countdown, per changed digit block) accumulates nothing: each call completes the last morph, which drops its `Morphs` entry, reverts the split, releases the width hold and dismisses its overlay host, and between morphs nothing ticks. Each call still splits the block twice, reads an outline for every old and new character and adds an overlay host `Canvas` even when no character needs a ghost; skipping the empty host would make a per-second clock cheaper.
- [BRIDGE-RECIPES] `MorphGlyphs` re-renders pairs whose characters are equal, reshaping them onto themselves as unhinted outlines, so a whole-block clock moves its unchanged glyphs and its colon every second. Tempo's countdown is one `TextBlock` per character (`FocusView.ShowRemaining`) and morphs only the blocks that changed; a morph that left equal pairs as text would let one block do.
- [BRIDGE-RECIPES] Reduced, `MorphGlyphs` crossfades the whole block, which suits a label that changes now and then but makes a ticking counter flicker every second. Tempo writes `Text` directly under `Presto.Defaults.ReducedMotion` for the countdown, the start/pause label and the settings `Stepper`; nothing chooses an instant reduced form.
- [BRIDGE-ATTACH] `Animate.OnAttached` keeps a loop running while its element is hidden, so a guide shown only during breaks would loop all session; Tempo sets it from a class style bound to `IsBreak`, so the loop starts and is reverted with the class.
- [BRIDGE-PRESENCE] Content that arrives with a page under a Presto transition classifies as `Initial` (BRIDGE-PRESENCE-009), so the proposal's `Presence.Enter` on the stats bars would never play; Tempo uses `Arrive`. Arrivals batch under their nearest `Panel` (BRIDGE-PRESENCE-020), so a bar's item template must hold no `Panel` of its own or every bar staggers at index 0, and the day labels go in a second `ItemsControl`.
- [BRIDGE-PRESENCE] An arrival batch supports order staggers only (BRIDGE-PRESENCE-022: the total is the arrivals so far), so `Presto.Stagger` with `Grid` and `From = Last` cannot reveal a grid outward from its last cell: every arrival is the last so far. Tempo's heatmap computes each cell's distance from today in `StatsViewModel` and delays by it with a `FunctionValue` reading the cell's `DataContext`.
- [BRIDGE-FOLLOW] There is no XAML spelling for a shape slice that eases between two start–end pairs: `Follow.Draw` follows only `0 p`, and `Animate.OnChanged` reverts its `Draw` tween on completion, which restores the full stroke. Tempo's time-by-tag donut stacks full circles, each following its cumulative share from the top under the ones after it.
- [BRIDGE-PAGES] A `Carousel` hides the pages it is not showing instead of detaching them, so nothing inside a page is classified again on return and its `Presence` arrivals never replay; Tempo swaps Focus and Stats in a `TransitioningContentControl` over a view-model `CurrentPage`, which rebuilds each view per swap.
- [BRIDGE-PAGES] A page host builds every page from a template, so a page that must reuse a live control (Tempo's compact Tasks page and the wide column share one `TasksView`, so the list's state is never doubled) needs a code-only `FuncDataTemplate` that adopts the control, and a move back to its column posted after the outgoing page detaches, since the exit still holds it (`ShellView.TasksPage`); nothing hosts an existing element as a page.
- [BRIDGE-SHEET] An adaptive side-or-bottom surface is hand-built: a `Drawer` wide and a bottom `Sheet` compact bound to one `IsOpen` would open together, so Tempo's `ShellView.PlaceSurfaces` rebinds `IsDetailOpen` to the live surface and moves the one `TaskDetail` between them on each breakpoint flip, and hides the drawer in compact, or its edge strip still opens it empty; the settings move between the flyout's `Content` and a sheet the same way, and `ShowSettings`/`ShowConfirm` branch on the arrangement. Nothing picks a sheet's edge and detents by width.
- [BRIDGE-PARTICLES] A `ParticleEffect` cannot be a timeline child, so `Recipes.Assemble` starts from a `Timeline.Call` at its position and does not follow the timeline's seek or `Complete()`. A skip has to `Cancel()` it separately, and `Cancel()` leaves the text block at `Opacity` 0, so Tempo's skip writes `Opacity` 1 itself (`TempoMotion.IntroWordmarkLand`) to land the wordmark drawn. Nothing completes an effect *to* its end state.
- [BRIDGE-SHAPES] `CreateMotionPath(Shape)` reads the shape's rendered geometry, so a builder that rides a path cannot be a `static readonly` value or come from XAML; Tempo's tick dot uses a `TempoMotion.IntroTick(Shape)` method that the intro timeline calls once the view is attached.
- [BRIDGE-CONNECTED] A handoff that should fade everything except the flying elements cannot use one `Presence.Enter` fade on the arriving view, because an ancestor's opacity fades the flight with it. Tempo marks each non-flying part with a `handoff` class (`Connected.Anchor="ring"` plus `HandoffFade`), and a key set for a one-time handoff stays live: every later swap that rebuilds the Focus view flew the ring from its last slot and replayed those anchors' fades, so Tempo binds the ring's key to `FocusViewModel.RingKey` and clears it on the first page swap. Nothing scopes a key to one arrival.
- [BRIDGE-CONNECTED] `Connected.Motion` is read only from the arriving element (BRIDGE-CONNECTED-004), so the intro cannot own its flight's motion. `IntroFlight` sits on the Focus ring and the title-bar wordmark, and so it also applies to every later arrival of those keys.
- [BRIDGE-SCROLL] Nothing cues that a scroller has more past an edge: `ScrollValue` gives the offset but not the distance left, so Tempo's `Views/ScrollEdge.cs` hand-rolls edge shadows (opacity ramped by the distance left, from `ScrollChanged`) and a sideways opacity-mask fade for its chip row. A bridge `Scroll.EdgeCue` (or a "remaining" scroll value) would replace it.
- [BRIDGE-SCROLL] Bringing a newly added item into view is code-only: Tempo raises `MainViewModel.TaskAdded` and `TasksView` calls `PrestoAvalonia.ScrollToIndex` with `Nearest`; nothing in XAML scrolls to an item on its arrival.
- [BRIDGE-MAGNETIC] A magnet and a hover lift cannot share an element: the lift's `"Y"` overrides the followed pull (BRIDGE-FOLLOW-038), so a hover at the pull's edge moved the button out from under the pointer and looped. Tempo's start pill first got a wrapper, then lost its magnet; the magnetic spec now states the edge case.
- [BRIDGE-SWIPE] Where a touch or pen long press's context request comes from on Windows (Avalonia's holding gesture or the OS press-and-hold) was not pinned down: raising `Holding` alone raises no request in a headless test. Swipe now leaves any context request during or just after a touch alone (BRIDGE-SWIPE), which cured Tempo's rows opening on their actions when held, but the source is still unverified.
- [SAMPLE] Tempo's page host is unclipped so the page scrollbars can sit in the window gutter (`edge-scroll`), which lets the page swap's slide draw a few DIPs past the card edges mid-transition; clipping to the card plus the right gutter would keep the edge scrollbar without it.
- [SAMPLE] In a short compact window the Focus page keeps the summary row's 64 DIP below the controls even when no session has finished, so the dial is smaller than it needs to be; the summary could float over the bottom instead.

## Per-spec open items

Each spec's Open items section lists its untested requirements and other loose ends; these specs have some.

- [BRIDGE-ACCORDION](specs/avalonia-bridge/accordion-and-pane.md): 10 untested requirements.
- [BRIDGE-THREAD](specs/avalonia-bridge/animation-thread.md): 11 untested requirements, plus the odd-count alternation bug, settings `RunOnCompositor` drops silently, and the `Visual.Opacity` hold after `Cancel`.
- [BRIDGE-SCENES](specs/avalonia-bridge/compositor-scenes.md): 8 untested requirements, plus no guard on a scene shared by two canvases and a detached scene keeping its handler.
- [BRIDGE-CONNECTED](specs/avalonia-bridge/connected.md): 6 untested requirements, plus late `Motion` validation and the registry keeping the last flight's control alive.
- [BRIDGE-CONTENTSIZE](specs/avalonia-bridge/content-size.md): 8 untested requirements, plus the `ClipToBounds` decision text not matching the code's `Clip` hold.
- [BRIDGE-HELPERS](specs/avalonia-bridge/control-helpers.md): 1 untested requirement, plus no test that every shorthand has a typed builder method.
- [BRIDGE-DECLLAYOUT](specs/avalonia-bridge/declarative-layout.md): 7 untested requirements, plus two arms on one container being undecided.
- [BRIDGE-DISTORT](specs/avalonia-bridge/distort.md): 6 untested requirements, plus the scaled-away child's hit-testing, a static `Distort` requesting frames, `Turbulence` fraying display text, and the manual Liquid type hover and Browser frame-rate check.
- [BRIDGE-DRAGGABLE](specs/avalonia-bridge/draggable.md): 20 untested requirements, plus the stale `OnDrag` doc, two draggables claiming one target, and `Reset` writing 0 outside the container.
- [BRIDGE-EASES](specs/avalonia-bridge/eases-and-springs.md): 1 untested requirement.
- [BRIDGE-GESTURES](specs/avalonia-bridge/gesture-arbitration.md): the by-hand touch and pen checks on Tempo and the Gestures page.
- [BRIDGE-EFFECTS](specs/avalonia-bridge/effects.md): 7 untested requirements.
- [BRIDGE-FLIP](specs/avalonia-bridge/flip-card.md): 4 untested requirements, plus `Motion` cloning the whole builder unvalidated and an `Orientation` change while flipped leaving the old axis turned.
- [BRIDGE-FOLLOW](specs/avalonia-bridge/follow.md): 8 untested requirements, plus never-attached targets easing, shorthands lacking `AnimateInitial`/`WhenReduced`, and a non-numeric shorthand string silently releasing the layer.
- [BRIDGE-GLYPHS](specs/avalonia-bridge/glyph-outlines.md): 1 untested requirement, plus the missing-glyph case of BRIDGE-GLYPHS-004.
- [BRIDGE-IMAGE](specs/avalonia-bridge/image-effects.md): 19 untested requirements, plus the resize-at-start question, `ImageTransition.Parse` accepting numeric kinds, a `Source` change mid-transition retargeting silently, and Browser/Android smoke runs.
- [BRIDGE-INIT](specs/avalonia-bridge/initialization.md): 3 untested requirements.
- [BRIDGE-LAYOUT](specs/avalonia-bridge/layout-transitions.md): 6 untested requirements, plus `Then()` before the first move resolving at once and an off-screen source degrading silently.
- [BRIDGE-MARKUP](specs/avalonia-bridge/markup-extensions.md): 6 untested requirements, plus no XAML way to set `Lift`'s distance or `Glow`'s color, and entrance-only extensions silently accepted on `Presence.Exit`.
- [BRIDGE-MODAL](specs/avalonia-bridge/modal.md): 9 untested requirements, plus `Open` while open ignoring its new origin and a replaced `Backdrop` staying blurred.
- [BRIDGE-MOTION](specs/avalonia-bridge/motion-states.md): 12 untested requirements, plus the `Leave` per-property `Delay` mismatch, returns not resuming a lower state's loop, a function `Delay` dropped under orchestration, late rest-less-property errors, and `Motion.Add` running in design mode.
- [BRIDGE-TOKENS](specs/avalonia-bridge/motion-tokens.md): 2 untested requirements, plus the stale `ColorSpace` doc.
- [BRIDGE-PAGES](specs/avalonia-bridge/page-transitions.md): 4 untested requirements.
- [BRIDGE-PAGER](specs/avalonia-bridge/pager.md): 20 untested requirements, plus the uncoerced `SelectedIndex`.
- [BRIDGE-PARTICLES](specs/avalonia-bridge/particles.md): 13 untested requirements, plus the unmeasured 1 000 cap on Browser, Android and iOS, a capped formation losing its bottom rows' right end, single-color formations, and the manual Particle type Browser frame-rate check.
- [BRIDGE-POPOVER](specs/avalonia-bridge/popovers.md): 5 untested requirements, plus builders on a plain popup's child doing nothing.
- [BRIDGE-PRESENCE-EXIT](specs/avalonia-bridge/presence-exit.md): 5 untested requirements, plus `AnimateExit` ghosting hidden or transparent controls.
- [BRIDGE-REVEAL](specs/avalonia-bridge/presence-reveal.md): 5 untested requirements, plus the stale `GetReveal`/`SetReveal` docs and the manual Activity feed check.
- [BRIDGE-PRESENCE](specs/avalonia-bridge/presence.md): 10 untested requirements, plus a partial builder change leaving the arrival playing.
- [BRIDGE-RIPPLE](specs/avalonia-bridge/press-ripple.md): 6 untested requirements, plus the Browser/Android measurement.
- [BRIDGE-SUBMIT](specs/avalonia-bridge/progress-and-submit.md): 15 untested requirements, plus no guard against overlapping `Run` calls.
- [BRIDGE-TARGETS](specs/avalonia-bridge/property-resolution.md): 3 untested requirements.
- [BRIDGE-PULL](specs/avalonia-bridge/pull-to-refresh.md): 13 untested requirements, plus a faulted `Refresh` task rethrowing onto the UI thread, undocumented.
- [BRIDGE-RECIPES-MEDIA](specs/avalonia-bridge/recipes-media.md): 6 untested requirements.
- [BRIDGE-RECIPES](specs/avalonia-bridge/recipes.md): 5 untested requirements, plus the stale `PressIn` and `Loading` docs, `PageBloom`'s exit lacking `WhenReduced`, `PageSharedAxis` taking no duration, `Develop` missing from the UI-thread-only list, dark specks from `MorphGlyphs` at body size, `MorphText`'s fixed shard count, `WriteIn` lacking an all-at-once option, and `WriteIn`/`Shatter` not re-seekable in a timeline.
- [BRIDGE-REDUCED](specs/avalonia-bridge/reduced-motion.md): 0 untested requirements, plus the follower capturing `Engine.Current`.
- [BRIDGE-REORDER](specs/avalonia-bridge/reorder.md): 10 untested requirements, plus a refused step re-raising `Moving` on every move and a spacing typo in `ReorderList.Raise`.
- [BRIDGE-SCROLLOBSERVER](specs/avalonia-bridge/scroll-observer.md): 11 untested requirements, plus the snap's missing velocity check and reduced form, and the missing-target exception thrown from a tick.
- [BRIDGE-SCROLL](specs/avalonia-bridge/scroll.md): 11 untested requirements, plus two manual checks (non-Windows head, XAML previewer).
- [BRIDGE-SELECTION](specs/avalonia-bridge/selection-indicator.md): 17 untested requirements, plus the stale `Motion` doc, `X`/`Y`/`Scale` silently ignored in `Motion`, and a late `NotSupportedException`.
- [BRIDGE-SHAPES](specs/avalonia-bridge/shapes.md): 10 untested requirements.
- [BRIDGE-SHEET](specs/avalonia-bridge/sheet.md): 11 untested requirements, plus the uncoerced `DetentIndex` and `Drawer` docs naming only Left and Right.
- [BRIDGE-FRAMES](specs/avalonia-bridge/sprite-frames.md): 8 untested requirements, plus in-place `Frames` mutation not being observed.
- [BRIDGE-SWIPE](specs/avalonia-bridge/swipe-actions.md): 12 untested requirements, plus a later `Command` widening the range, undocumented.
- [BRIDGE-TEXTREVEAL](specs/avalonia-bridge/text-reveal.md): 7 untested requirements.
- [BRIDGE-WARP](specs/avalonia-bridge/text-warp.md): 1 untested requirement, plus the per-block field, no per-letter stagger, and the manual pointer warps.
- [BRIDGE-TEXT](specs/avalonia-bridge/text.md): 5 untested requirements.
- [BRIDGE-TOAST](specs/avalonia-bridge/toast-host.md): 6 untested requirements, plus `Items`/`ItemsSource` desync and a re-prepared container doubling handlers.
- [BRIDGE-TRANSFORMABLE](specs/avalonia-bridge/transformable.md): 17 untested requirements, plus shared target ownership, capture loss running a full release, a horizontal wheel handled under zoom, and the macOS, Android and Browser hardware checks.
- [BRIDGE-TRANSFORMS](specs/avalonia-bridge/transforms.md): 1 untested requirement, plus a local `RenderTransform` set while held not being picked up.
- [BRIDGE-CODECS](specs/avalonia-bridge/value-codecs.md): 5 untested requirements, plus the solid-brush opacity bug.
- [BRIDGE-ATTACH](specs/avalonia-bridge/xaml-attach.md): 9 untested requirements, plus `OnTapped`/`OnChanged` ignoring builder swaps and `OnTapped` inert on non-input visuals.
- [CORE-ANIMATABLE](specs/core/animatable.md): 7 untested requirements, plus the callbacks animation's 1000 ms default, `ReduceAll` handling, the `Fling` velocity fallback, channel `Set` versus a saved fling, `Fling` on colors, and the `ValueOf` leak.
- [CORE-BUILDER](specs/core/animation-builder.md): 0 untested requirements, plus a cross-engine builder reading the current engine's `ReducedMotion`.
- [CORE-ANIMATION](specs/core/animation.md): 11 untested requirements, plus `GetCurrent` on an `Overlap` tween returning the from value.
- [CORE-CALLBACKS](specs/core/callbacks.md): 10 untested requirements, plus `Then` never completing after `Cancel`/`Revert` or a throwing callback.
- [CORE-COLORS](specs/core/colors.md): 5 untested requirements, plus the `rgb()` decimal bug and the achromatic `hsl` deviation.
- [CORE-COMPOSITION](specs/core/composition.md): 3 untested requirements.
- [CORE-DIAGNOSTICS](specs/core/diagnostics.md): 4 untested requirements, plus untagged animatable property animations.
- [CORE-EASEPARSER](specs/core/ease-parser.md): 6 untested requirements, plus the case-sensitive deprecated-prefix check, `Parse(null)`, and an unbounded cache.
- [CORE-EASES](specs/core/eases.md): 20 untested requirements, plus the stale `FromPath` doc and unvalidated `Steps`.
- [CORE-ENGINE](specs/core/engine.md): 23 untested requirements, plus `TimeUnit` settable on any engine and the unclamped `Speed`.
- [CORE-INERTIA](specs/core/inertia.md): 7 untested requirements, plus the unscaled 60000 decay cap in seconds mode, `ModifyTarget` within `RestDelta`, and unvalidated `interval`, `Friction` and `Min <= Max`.
- [CORE-KEYFRAMES](specs/core/keyframes.md): 7 untested requirements, plus percentage keyframes dropping per-frame settings and `Color` reading `Engine.Current` defaults.
- [CORE-MOTIONPATH](specs/core/motion-path.md): 5 untested requirements.
- [CORE-MOTIONVALUE](specs/core/motion-values.md): 6 untested requirements, plus no thread-affinity check on `Set`.
- [CORE-PARTICLES](specs/core/particles.md): 15 untested requirements, plus the null-seed counter differing from the decision text and unvalidated lifetime, `dt` and `MaxParticles`.
- [CORE-PRESTO](specs/core/presto.md): 12 untested requirements, plus `Set` raising no timer-created hook, `Get` not canonicalizing names, and null arguments throwing `NullReferenceException`.
- [CORE-REDUCED](specs/core/reduced-motion.md): 9 untested requirements, plus a cross-engine builder's `ReducedMotion` and `Animatable` under `ReduceAll`.
- [CORE-SCOPE](specs/core/scope-and-defaults.md): 21 untested requirements, plus the stale `Scope.Defaults` doc and timeline defaults snapshotted versus read live.
- [CORE-SCRAMBLE](specs/core/scramble.md): 18 untested requirements, plus the first-text cache that never refreshes.
- [CORE-SPRING](specs/core/spring.md): 8 untested requirements, plus seconds-mode spring durations and physical and designer settings not updating each other.
- [CORE-STAGGER](specs/core/stagger.md): 2 untested requirements, plus the cached start offset on reuse.
- [CORE-TARGETS](specs/core/targets.md): 3 untested requirements, plus codecs registered after first resolution never applying.
- [CORE-THREADING](specs/core/threading.md): 14 untested requirements, plus `TimeUnit` settable on any engine and `Precision`'s missing thread assertion.
- [CORE-TIMELINE](specs/core/timeline.md): 5 untested requirements, plus `Remove` keeping `Duration`, the zero-duration shift reading only explicit durations, and positions under `ReduceAll`.
- [CORE-TIMER](specs/core/timer.md): 17 untested requirements, plus `Complete()` firing `OnPause` after `OnComplete`.
- [CORE-UTILS](specs/core/utils.md): 18 untested requirements, plus the `Damp` NaN deviation.
- [CORE-VALUES](specs/core/values.md): 5 untested requirements, plus `Refresh` skipping reconciliation and the implicit `PrestoColor` conversion's rounding.
- [DEVTOOLS-OVERLAY](specs/devtools/overlay.md): 21 untested requirements, plus the manual Desktop checks, per-frame tween snapshots, and closing the overlay leaving `CaptureCreationTrace` on and root speeds changed.
- [LOTTIE-PLAYBACK](specs/lottie/lottie.md): 12 untested requirements, plus the stale `LottieSegment.Name` and `PlayLottieExtension` docs, unvalidated `speed` and `FrameRate`, reduced-motion and DevTools coverage of render-thread playback, and a Browser run.
