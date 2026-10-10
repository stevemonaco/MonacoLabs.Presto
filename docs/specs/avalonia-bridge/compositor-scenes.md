---
id: BRIDGE-SCENES
title: Compositor scenes
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Compositor
sources:
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorCanvas.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorScene.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorSceneHandler.cs
  - MonacoLabs.Presto.Avalonia/Compositor/SceneText.cs
types:
  - CompositorCanvas
  - CompositorScene
  - SceneText
tests:
  - CompositorSceneTests
  - CompositorMessagingTests
  - CompositorRenderTests
  - SceneTextTests
  - LifetimeHiddenTests
  - BridgeAllocationTests
  - CompositorPageTests
upstream: []
depends:
  - BRIDGE-INIT
  - CORE-ENGINE
  - CORE-SCOPE
  - CORE-CALLBACKS
  - CORE-REDUCED
---

# Compositor scenes

## Purpose

A `CompositorCanvas` hosts a `CompositorScene` whose own Presto `Engine` is ticked by Avalonia's render loop and which draws with an `ImmediateDrawingContext`, so the whole Presto model (tweens, keyframes, springs, stagger, timelines, callbacks) keeps running while the UI thread is blocked. The scene's targets are plain objects it draws itself; it never writes an `AvaloniaProperty`. `SceneText` draws ASCII text there without allocating. Particle scenes (BRIDGE-PARTICLES) build on this.

## Requirements

### Canvas

- **BRIDGE-SCENES-001** — When a canvas attaches to the visual tree, it shall create a composition custom visual as its element child visual, sized to its bounds, and hand its `Scene`, if any, to the render thread.
  - Tests: `CompositorSceneTests.AttachCreatesTheChildVisualAndRunsOnAttachedOnTheSceneEngine`
- **BRIDGE-SCENES-002** — When the canvas's bounds change while attached, the scene's `Size` shall follow them.
  - Tests: `CompositorSceneTests.TheSceneRendersAtTheCanvasSizeAndFollowsItWhenItChanges`
- **BRIDGE-SCENES-003** (inherited) — When `Scene` changes while the canvas is attached, the old scene shall be detached as on detach and the new one, if not null, attached.
  - Tests: untested
- **BRIDGE-SCENES-004** — When the canvas detaches, the render thread shall revert the scene's `Scope`, pause its engine and drop its children, and the canvas shall remove its child visual.
  - Tests: `CompositorSceneTests.DetachRevertsTheSceneScopeAndEmptiesItsEngine`, `CompositorPageTests.ThePageRunsAndRevertsEverythingOnDetach`
- **BRIDGE-SCENES-005** — When a canvas attaches again, its scene shall get a new engine and scope and `OnAttached` shall run again.
  - Tests: untested
- **BRIDGE-SCENES-006** — `Post` on an attached canvas shall deliver the message to `OnMessage` on the render thread, in posting order, on the compositor frame after it was posted.
  - Tests: `CompositorMessagingTests.MessagesArriveInOrderIncludingTheOnesPostedBeforeAttach`
- **BRIDGE-SCENES-007** (inherited) — `Post` on a canvas outside the visual tree shall queue the message and send the queue, in order, right behind the attach message.
  - Tests: `CompositorMessagingTests.MessagesArriveInOrderIncludingTheOnesPostedBeforeAttach`
- **BRIDGE-SCENES-008** — `Post` called off the UI thread shall fail a `Debug.Assert`.
  - Tests: untested
- **BRIDGE-SCENES-009** — `Invoke<TScene>` shall run the action against the live scene on the render thread when the scene is a `TScene`, do nothing otherwise, and never reach `OnMessage`.
  - Tests: `CompositorMessagingTests.InvokeReachesTheTypedSceneOnItsOwnEngine`
- **BRIDGE-SCENES-010** — While the app is hidden, the canvas shall set its scene engine's `Hidden`, so the engine pauses and later resumes where it was.
  - Tests: `LifetimeHiddenTests.ASceneEnginePausesWhileHiddenAndResumesWhereItWas`

### Scene engine

- **BRIDGE-SCENES-011** — `OnAttached` shall run on the render thread with the scene's engine as `Engine.Current` and its `Scope` current, so animations it creates register with both.
  - Tests: `CompositorSceneTests.AttachCreatesTheChildVisualAndRunsOnAttachedOnTheSceneEngine`
- **BRIDGE-SCENES-012** — The scene engine shall start with a copy of `Presto.Defaults` taken when the scene attaches.
  - Tests: untested
- **BRIDGE-SCENES-013** — The scene engine's time shall be the compositor clock in milliseconds, advanced by the render loop whether or not the main engine ticks.
  - Tests: `CompositorSceneTests.TheSceneAdvancesWhileTheMainEngineNeverTicks`
- **BRIDGE-SCENES-014** — `OnMessage` and `Invoke` actions shall run under the scene's engine and scope, as `OnAttached` does.
  - Tests: `CompositorMessagingTests.InvokeReachesTheTypedSceneOnItsOwnEngine`
- **BRIDGE-SCENES-015** — When the scene engine runs out of children, the canvas shall stop requesting render-loop frames until the engine starts its frame source again.
  - Tests: `CompositorSceneTests.TheFrameSourceStopsRenewingOnceTheEngineIsIdle`
- **BRIDGE-SCENES-016** — When a scene tick throws, the exception shall be logged at `Error` in Avalonia's `Animations` area and later frames shall still run.
  - Tests: untested
- **BRIDGE-SCENES-017** — When `OnAttached` throws, the animations it created before the throw shall keep running and the UI thread's `Engine.Current` shall be unchanged.
  - Tests: `CompositorMessagingTests.AThrowingOnAttachedLeavesTheAnimationItCreatedRunning`
- **BRIDGE-SCENES-018** — When `OnMessage` throws, later messages and later frames shall still be processed.
  - Tests: `CompositorMessagingTests.AThrowingOnMessageStopsNeitherLaterMessagesNorLaterFrames`

### Drawing

- **BRIDGE-SCENES-019** — After every frame in which the scene engine ticked, the scene shall be redrawn: `OnRender` runs with the canvas size, clipped to it.
  - Tests: `CompositorRenderTests.AnAnimatingSceneRedrawsItselfEveryFrame`, `CompositorSceneTests.TheSceneRendersAtTheCanvasSizeAndFollowsItWhenItChanges`
- **BRIDGE-SCENES-020** — While the scene engine is idle and nothing calls `Invalidate`, the scene's pixels shall stay unchanged.
  - Tests: `CompositorRenderTests.ASceneThatNeverInvalidatesLeavesThePixelsAlone`
- **BRIDGE-SCENES-021** — `Invalidate` shall redraw an idle scene once.
  - Tests: `CompositorRenderTests.InvalidateRedrawsAnIdleSceneAndNothingElseDoes`
- **BRIDGE-SCENES-022** — `Now`, `Size` and `Invalidate` before the scene first attaches shall throw `InvalidOperationException`, and in Debug fail an assertion when called off the scene engine's thread.
  - Tests: untested
- **BRIDGE-SCENES-023** (inherited) — `Brush` shall return the same instance for the same color and opacity, with opacity clamped to 0..1 and rounded to 256 steps.
  - Tests: `CompositorRenderTests.TheBrushCacheReusesInstancesAndQuantizesOpacity`
- **BRIDGE-SCENES-024** — When the brush cache holds 1 024 brushes, the next new brush shall clear it first.
  - Tests: `CompositorSceneTests.TheBrushCacheNeverGrowsPastItsBoundAndHandsBackTheSameBrushForOneColor`
- **BRIDGE-SCENES-025** — Once the brush cache is warm, a scene frame that tweens and draws shall allocate no more than 64 bytes per frame (median).
  - Tests: `BridgeAllocationTests.CompositorSceneFramesWithinBudget`

### Reaching the UI thread

- **BRIDGE-SCENES-026** — `PostToUI` shall run its action on the UI thread through the dispatcher, never inline on the render thread.
  - Tests: `CompositorMessagingTests.PostToUIRunsItsActionOnTheUIThread`
- **BRIDGE-SCENES-027** — `Then()` on a timer the scene created shall complete its continuations on the thread pool.
  - Tests: untested

### Scene text

- **BRIDGE-SCENES-028** — `SceneText` shall build one immutable glyph run per character U+0020..U+007E of the default typeface at the given font size, with `Height` the typeface's line spacing at that size.
  - Tests: `SceneTextTests.GlyphsLandInsideTheMeasuredBoxAndAnEmptyStringLeavesItUntouched`
- **BRIDGE-SCENES-029** (inherited) — `Measure` shall sum each character's own advance, counting characters outside U+0020..U+007E as zero.
  - Tests: `SceneTextTests.GlyphsLandInsideTheMeasuredBoxAndAnEmptyStringLeavesItUntouched`
- **BRIDGE-SCENES-030** — `Draw` shall draw each character with the box's top-left at `origin`, advancing by its own width with no kerning or shaping, skip characters outside the range without advancing, and draw nothing for an empty string.
  - Tests: `SceneTextTests.GlyphsLandInsideTheMeasuredBoxAndAnEmptyStringLeavesItUntouched`, `SceneTextTests.TheReadoutFollowsTheStringTheSceneHolds`

### Defaults

- **BRIDGE-SCENES-031** (inherited) — When `Brush` is called without an opacity, the brush shall be fully opaque (opacity 1).
  - Tests: untested

## Invariants

- After attach the scene belongs to the render thread: `OnAttached`, `OnMessage`, `OnRender` and every callback of what they create run there, and none touches an `AvaloniaObject` except through `PostToUI`.
- The UI thread reaches an attached scene only through `Post` and `Invoke`.
- One engine and one scope per attachment; detaching leaves the engine with no children.

## Edge cases

- A canvas attached with `Scene` null drops messages it receives; messages are queued only while the canvas itself is outside the visual tree.
- A canvas without a composition visual on attach (no compositor) does nothing and queues posts.
- After detach, `Scene.Engine` and `Scope` still name the reverted engine and scope until the next attach.

## Threading and lifetime

- The scene is constructed on the UI thread (in code or XAML) and handed over untouched at attach; the compositor's batch queue publishes it.
- The render loop drives frames only while the engine has children; idle scenes cost nothing.
- Survives a blocked UI thread wherever the render loop has its own thread; Browser and headless render on the UI thread, a documented limitation (see BRIDGE-THREAD). Freeze-proofing is checked by hand on the sample's Compositor page with its freeze button.
- `SceneText` is built on the UI thread (the scene's constructor) and drawn only from the render thread.

## Reduced motion

- Not affected by the bridge: the scene engine copies `PrestoDefaults`, `ReducedMotion` included, at attach, and the scene's animations follow the core rules (CORE-REDUCED) on that copy. A later change of the main engine's flag does not reach an attached scene.

## Allocation

- A frame is the core tick plus `OnRender`; with brushes from `Brush` and text from `SceneText` it allocates nothing beyond the budget of BRIDGE-SCENES-025. `Post` allocates per message; `Invoke` allocates a wrapper delegate.

## Deviations from anime.js

- No upstream counterpart: anime.js has one engine on the page's main thread. A scene owns a second engine because that is the only way Presto's full model survives a stalled UI thread.

## Decisions

- **Render-thread work is a separate API named "Compositor", and a scene owns its engine.** `CompositorCanvas` and `CompositorScene` are explicit types; the UI thread never ticks a scene, the render thread never writes an `AvaloniaProperty`, and the scene engine is ambient while its code runs. Reason: "compositor" is Avalonia's own term; a scene engine is the only path where the full Presto model survives a stalled UI thread, and an ambient engine keeps the anime.js call shape. Rejected: `RenderThread*` names; `Composition*` names, since the core already uses `Composition` for replace/none/blend.
- **An idle scene stops requesting frames.** The handler renews its render-loop registration only while the engine has children, and invalidates once after a frame in which the engine ticked. Reason: an idle scene costs nothing and lets the render loop sleep, as the UI-thread frame source does.
- **The scene tick is wrapped in a try/catch that logs.** Reason: Avalonia does not catch exceptions from the animation-frame callback, so one would take the render thread down; message and render exceptions Avalonia already catches.
- **The bridge stays Skia-free; scenes draw with `ImmediateDrawingContext`.** Scenes cache immutable brushes and pens, and path drawing is the scene's own business through the Skia lease feature. Reason: `ImmediateDrawingContext` has no geometry drawing, and keeping Skia out of the bridge keeps it backend-neutral.
- **After attach a scene belongs to the render thread.** The UI thread reaches it only through `Post` and `Invoke`, the scene reaches back with `PostToUI`, callbacks must not touch controls, and `Then()` on a scene timer completes on the thread pool; a scene built on the UI thread (in XAML) is handed over untouched at attach. Reason: the render thread has no synchronization context, and handing over at attach is safe because the composition batch queue publishes the object.
- **Browser's blocked-UI-thread limitation is documented, not worked around.** See BRIDGE-THREAD.
- **Hidden pauses the engine, as upstream's `pauseOnDocumentHidden` does.** See CORE-ENGINE.

## Non-goals

- Path or geometry drawing in the bridge: `ImmediateDrawingContext` has none, and the bridge stays Skia-free (a scene may lease Skia itself).
- A design-mode or previewer placeholder for the canvas.
- Text beyond printable ASCII, kerning or shaping in `SceneText`.

## Open items

- Untested: replacing `Scene` while attached (003), re-attaching (005), the off-thread `Post` assertion (008), the defaults snapshot (012), tick exception logging (016), pre-attach `InvalidOperationException` (022), `Then()` completing on the thread pool (027).
- A scene instance shared by two attached canvases is not guarded: the second attach overwrites the handler the first one's callbacks read.
- After detach the scene keeps its handler, so `Now`, `Size` and `Invalidate` called from a stray callback read a detached handler instead of throwing.
