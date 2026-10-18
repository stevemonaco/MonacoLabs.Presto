# MonacoLabs.Presto.Avalonia specs

One spec per feature; format and usage in [../README.md](../README.md). Find a type's spec by its \	ypes\ list, or search this folder for the type name.

## Foundation

| Spec | Id | Owns |
|---|---|---|
| [Bridge initialization, frame source and hidden signal](initialization.md) | BRIDGE-INIT | `AppBuilderExtensions`, `AvaloniaFrameSource` |
| [Avalonia property resolution and writes](property-resolution.md) | BRIDGE-TARGETS | `AvaloniaObjectTargetAdapter`, `AvaloniaAnimationParams`, `AvaloniaAnimatableParams` |
| [Transform shorthands](transforms.md) | BRIDGE-TRANSFORMS | — |
| [Avalonia value codecs](value-codecs.md) | BRIDGE-CODECS | `ColorExtensions` |
| [Control helpers, typed builder methods, scopes and WhenDetached](control-helpers.md) | BRIDGE-HELPERS | `PrestoAvalonia`, `AnimateExtensions`, `AnimationBuilderExtensions`, `PropertyBuilderExtensions`, `ControlScope`, `WhenDetached` |
| [Effect, reveal and 3D shorthands, and Tilt](effects.md) | BRIDGE-EFFECTS | `Tilt` |
| [Avalonia easing adapter](eases-and-springs.md) | BRIDGE-EASES | `PrestoEasing` |

## Threads and the compositor

| Spec | Id | Owns |
|---|---|---|
| [Animation thread and compositor animations](animation-thread.md) | BRIDGE-THREAD | `AnimationThread`, `ThreadedAnimation`, `CompositorAnimation`, `CompositorAnimationParams` |
| [Compositor scenes](compositor-scenes.md) | BRIDGE-SCENES | `CompositorCanvas`, `CompositorScene`, `SceneText` |

## Layout

| Spec | Id | Owns |
|---|---|---|
| [Layout transitions (FLIP on the compositor)](layout-transitions.md) | BRIDGE-LAYOUT | `LayoutTransition`, `LayoutAnimateMode` |
| [Connected animations from XAML](connected.md) | BRIDGE-CONNECTED | `Connected` |
| [Content size transitions](content-size.md) | BRIDGE-CONTENTSIZE | `ContentSizeTransition` |
| [Declarative layout animation](declarative-layout.md) | BRIDGE-DECLLAYOUT | — |

## Lifecycle and XAML triggers

| Spec | Id | Owns |
|---|---|---|
| [XAML attach triggers (Animate.*)](xaml-attach.md) | BRIDGE-ATTACH | `Animate`, `CueAction` |
| [Presence lifecycle (classification, arrivals, show and hide, modes)](presence.md) | BRIDGE-PRESENCE | `Presence`, `PresenceHideMode`, `PresenceMode` |
| [Presence exits and ghosts](presence-exit.md) | BRIDGE-PRESENCE-EXIT | — |
| [Scroll reveals and scroll anchoring (Presence.Reveal, RevealOnce, ScrollAnchor)](presence-reveal.md) | BRIDGE-REVEAL | — |
| [Page transitions (PrestoPageTransition)](page-transitions.md) | BRIDGE-PAGES | `PrestoPageTransition`, `PageStacking`, `PageAxis` |
| [Popover open and close motion](popovers.md) | BRIDGE-POPOVER | `Popover` |

## State and value-driven motion

| Spec | Id | Owns |
|---|---|---|
| [Motion states and orchestration](motion-states.md) | BRIDGE-MOTION | `Motion`, `MotionState`, `MotionStates`, `MotionOrder`, `MotionExtensions`, `StateTokensExtension` |
| [Value-driven animation and motion value sources](follow.md) | BRIDGE-FOLLOW | `Follow`, `FollowExtension`, `FollowParams` |

## Gestures

| Spec | Id | Owns |
|---|---|---|
| [Draggable](draggable.md) | BRIDGE-DRAGGABLE | `Draggable`, `DraggableParams`, `DraggableAxisParams`, `DraggableCursorParams`, `Drop`, `DropHit`, `DropMiss` |
| [Transformable (pan, pinch-zoom, rotate)](transformable.md) | BRIDGE-TRANSFORMABLE | `Transformable`, `TransformableParams`, `PanBounds`, `WheelZoom` |
| [Drag-to-reorder lists](reorder.md) | BRIDGE-REORDER | `Reorder`, `ReorderExtension`, `ReorderMovingEventArgs` |
| [Magnetic pull](magnetic.md) | BRIDGE-MAGNETIC | `Magnetic` |
| [Gesture arbitration](gesture-arbitration.md) | BRIDGE-GESTURES | — |

## Scrolling

| Spec | Id | Owns |
|---|---|---|
| [Scroll observer](scroll-observer.md) | BRIDGE-SCROLLOBSERVER | `ScrollObserver`, `ScrollObserverParams`, `ScrollAxis`, `ScrollContainer`, `ScrollSnap`, `ScrollSync`, `ScrollThreshold` |
| [Scrolling (parallax, smooth wheel, animated scrolling, edge cues)](scroll.md) | BRIDGE-SCROLL | `Scroll`, `ScrollAlignment`, `ScrollEdge`, `ScrollToItemExtension` |

## Shapes and text

| Spec | Id | Owns |
|---|---|---|
| [Shapes (drawable, motion path, morph)](shapes.md) | BRIDGE-SHAPES | — |
| [Text splitting](text.md) | BRIDGE-TEXT | `TextSplitter`, `SplitTextParams`, `SplitPieceParams` |
| [Text reveals and text exits](text-reveal.md) | BRIDGE-TEXTREVEAL | `TextReveal`, `TextRevealExtension`, `TextPieces` |
| [Text on a path](text-path.md) | BRIDGE-TEXTPATH | `TextPath`, `TextPathAlign` |
| [Glyph outlines](glyph-outlines.md) | BRIDGE-GLYPHS | `GlyphShards`, `GlyphPoints`, `GlyphSampling` |
| [Text warps](text-warp.md) | BRIDGE-WARP | `TextWarp`, `TextWarpKind` |

## Recipes and tokens

| Spec | Id | Owns |
|---|---|---|
| [Recipes](recipes.md) | BRIDGE-RECIPES | `Recipes`, `Attention`, `Entrances`, `Exits`, `Interactions`, `Layouts`, `Loading`, `Loops`, `Pages`, `Popovers`, `Reveals`, `TextReveals`, `Edge`, `FadeDirection`, `MorphGlyphsExtension` |
| [Media recipes (draws, particles, sprites, tints)](recipes-media.md) | BRIDGE-RECIPES-MEDIA | `Draws`, `Particles`, `Sprites`, `Tints` |
| [Recipe markup extensions](markup-extensions.md) | BRIDGE-MARKUP | `RecipeExtension`, `BlurExtension`, `CharCascadeExtension`, `FadeExtension`, `GlowExtension`, `LiftExtension`, `LineMaskExtension`, `LineRiseExtension`, `LineWipeExtension`, `PageBloomExtension`, `PageFadeExtension`, `PageFadeThroughExtension`, `PagePushExtension`, `PageSharedAxisExtension`, `PageSlideExtension`, `PlayFramesExtension`, `PopExtension`, `PopoverExtension`, `PressInExtension`, `RecolorExtension`, `ReflowExtension`, `ScaleExtension`, `ScatterExtension`, `ShakeExtension`, `ShimmerExtension`, `SlideExtension`, `WordBlurExtension`, `WordFadeExtension` |
| [Motion tokens](motion-tokens.md) | BRIDGE-TOKENS | `MotionTokens` |
| [Reduced motion in the bridge](reduced-motion.md) | BRIDGE-REDUCED | — |

## Images, frames and particles

| Spec | Id | Owns |
|---|---|---|
| [Image effects and pixel transitions](image-effects.md) | BRIDGE-IMAGE | `PrestoImageBase`, `FilteredImage`, `ImageTransition`, `ImageTransitionKind` |
| [Raster distortion](distort.md) | BRIDGE-DISTORT | `Distort` |
| [Sprite sheets and image sequences](sprite-frames.md) | BRIDGE-FRAMES | `SpriteSheet`, `ImageSequence` |
| [Particle drawing and particle effects](particles.md) | BRIDGE-PARTICLES | `ParticleScene`, `ParticleFormation`, `ParticleShape`, `ParticleEffect`, `BurstExtension` |
| [Press ripple](press-ripple.md) | BRIDGE-RIPPLE | `Ripple` |

## Controls

| Spec | Id | Owns |
|---|---|---|
| [Pager carousel and indicator](pager.md) | BRIDGE-PAGER | `Pager`, `PagerIndicator`, `PagerIndicatorMode` |
| [Sheet and drawer](sheet.md) | BRIDGE-SHEET | `Sheet`, `SheetDetents`, `SheetPresentation`, `Drawer` |
| [Toast host](toast-host.md) | BRIDGE-TOAST | `Toast`, `ToastHost`, `ShowToastExtension` |
| [Modal host](modal.md) | BRIDGE-MODAL | `Modal` |
| [Accordion and collapsible pane](accordion-and-pane.md) | BRIDGE-ACCORDION | `Accordion`, `Pane` |
| [Selection indicator](selection-indicator.md) | BRIDGE-SELECTION | `Selection`, `SelectionIndicatorMode` |
| [Swipe actions and swipe-to-dismiss](swipe-actions.md) | BRIDGE-SWIPE | `Swipe` |
| [Pull to refresh](pull-to-refresh.md) | BRIDGE-PULL | `PullToRefresh` |
| [Flip card](flip-card.md) | BRIDGE-FLIP | `Flip` |
| [Split-flap display](split-flap.md) | BRIDGE-SPLITFLAP | `SplitFlap` |
| [Progress ring and submit button](progress-and-submit.md) | BRIDGE-SUBMIT | `ProgressRing`, `SubmitButton`, `SubmitState` |
| [Marquee](marquee.md) | BRIDGE-MARQUEE | `Marquee`, `MarqueeDirection`, `MarqueeMode` |

