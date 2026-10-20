---
id: BRIDGE-SCROLL
title: Scrolling (parallax, smooth wheel, animated scrolling, edge cues)
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Scroll.cs
  - MonacoLabs.Presto.Avalonia/ScrollAlignment.cs
  - MonacoLabs.Presto.Avalonia/ScrollEdge.cs
  - MonacoLabs.Presto.Avalonia/ScrollToItemExtension.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollCue.cs
  - MonacoLabs.Presto.Avalonia/Targets/ScrollOffsetAccessor.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollWriter.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollMotion.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollGeometry.cs
  - MonacoLabs.Presto.Avalonia/Events/IndexScroller.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
  - MonacoLabs.Presto.Avalonia/PropertyBuilderExtensions.cs
types:
  - Scroll
  - ScrollAlignment
  - ScrollEdge
  - ScrollToItemExtension
tests:
  - ScrollParallaxTests
  - ScrollSmoothTests
  - ScrollShorthandTests
  - ScrollSpikeTests
  - ScrollToTests
  - ScrollIntoViewTests
  - ScrollToIndexTests
  - ScrollStickToEndTests
  - ScrollEdgeCueTests
  - PropertyNamingTests
  - BridgeAllocationTests
  - ScrollStoryPageTests
upstream: []
depends:
  - BRIDGE-SCROLLOBSERVER
  - BRIDGE-TARGETS
  - BRIDGE-TRANSFORMS
  - BRIDGE-TOKENS
  - BRIDGE-REVEAL
  - BRIDGE-REDUCED
  - CORE-ANIMATION
  - CORE-COMPOSITION
  - CORE-REDUCED
---

# Scrolling

## Purpose

Everything that moves a `ScrollViewer`'s content apart from observing it: `Scroll.Speed` parallax, `Scroll.Smooth` wheel easing (Win32), the `"ScrollX"`/`"ScrollY"` tween shorthands, `PrestoAvalonia.ScrollTo`/`ScrollIntoView`/`ScrollToIndex`, `Scroll.StickToEnd`, and the edge cues that show content lies past an edge: `Scroll.CueFor` shadows and the `Scroll.FadeEdges` mask. All offset writes use `SetCurrentValue` through one per-scroller `ScrollWriter`, so user input keeps working and each writer can tell its own writes from everyone else's. Parallax and the edge cues share the `ScrollContainer` of BRIDGE-SCROLLOBSERVER. Also specifies `PrestoAvalonia.ScrollTo`, `ScrollIntoView` and `ScrollToIndex`, `PropertyBuilderExtensions.ScrollX` and `ScrollY`, and `{presto:ScrollToItem}`, a `CueAction` (BRIDGE-ATTACH) that scrolls the list it sits on to a raised item.

## Requirements

### Parallax (`Scroll.Speed`)

- **BRIDGE-SCROLL-001** — While a control with `Speed` other than 1 is attached inside a `ScrollViewer`, the bridge shall translate it along `"Y"` by `(1 − speed) × max(0, scroll − entry)`, so below 1 it lags the content and above 1 it leads.
  - Tests: `ScrollParallaxTests.AHalfSpeedElementLagsByHalfTheScrollSinceItEnteredWithoutAFrame`, `ScrollParallaxTests.BeforeItEntersTheElementIsNotTranslated`, `ScrollParallaxTests.AFasterElementLeads`, `ScrollStoryPageTests.TheHeroLayersAreTranslatedByTheirSpeeds`
- **BRIDGE-SCROLL-002** — `entry` shall be the scroll at which the control's leading edge meets the far edge of the viewport, measured with the translate lifted and clamped at 0, so a control in view at load starts at its layout position.
  - Tests: `ScrollParallaxTests.AnElementInViewAtTheTopStartsUntranslatedAndMovesWithTheWholeScroll`
- **BRIDGE-SCROLL-003** — When the scroller's vertical scrollbar is disabled and its horizontal one is not, the parallax shall run along `"X"` and the horizontal offset.
  - Tests: `ScrollParallaxTests.AHorizontalScrollerTranslatesAlongX`
- **BRIDGE-SCROLL-004** — The parallax shall be written inside the scroller's offset notification, never trailing the content by a frame.
  - Tests: `ScrollParallaxTests.AHalfSpeedElementLagsByHalfTheScrollSinceItEnteredWithoutAFrame`
- **BRIDGE-SCROLL-005** — When the control's bounds or the scroller's viewport or extent change, the parallax shall measure `entry` again at once, not after the observers' debounce.
  - Tests: `ScrollParallaxTests.AViewportResizeMeasuresTheEntryAgain`, `ScrollParallaxTests.ALayoutChangeAboveTheElementMeasuresTheEntryAgain`
- **BRIDGE-SCROLL-006** — A control whose `Speed` was never changed from 1 shall keep its render transform untouched.
  - Tests: `ScrollParallaxTests.SpeedOneLeavesTheRenderTransformUntouched`
- **BRIDGE-SCROLL-007** — When `Speed` changes between two values other than 1, the new speed shall apply at the next scroll.
  - Tests: `ScrollParallaxTests.ChangingTheSpeedTakesEffectAtTheNextScroll`
- **BRIDGE-SCROLL-008** — When `Speed` returns to 1 or the control detaches, the bridge shall revert the parallax shorthand to the user's transform and release the scroller's container.
  - Tests: `ScrollParallaxTests.SettingTheSpeedBackToOneReverts`, `ScrollParallaxTests.DetachingRevertsTheUsersTransformAndReleasesTheContainer`
- **BRIDGE-SCROLL-009** (inherited) — When the control has no `ScrollViewer` ancestor as it attaches, the parallax shall do nothing until it attaches again.
  - Tests: `ScrollParallaxTests.AnElementAttachedOutsideAScrollViewerStaysPutUntilItAttachesInsideOne`
- **BRIDGE-SCROLL-010** — While `PrestoDefaults.ReducedMotion` is set, the parallax shall write a speed of 1 (the layout position) at each offset notification, reading the flag there, so a change shows at the next scroll.
  - Tests: `ScrollParallaxTests.UnderReducedMotionTheElementKeepsItsLayoutPositionAndResumesAtTheScrollAfterItClears`, `ScrollStoryPageTests.UnderReducedMotionTheHeroLayersScrollTogether`
- **BRIDGE-SCROLL-011** — `Speed` shall be settable from XAML.
  - Tests: `ScrollParallaxTests.TheSpeedCanBeSetInXaml`

### Smoothed wheel (`Scroll.Smooth`)

- **BRIDGE-SCROLL-012** — When `Smooth` is true on Windows, a wheel event whose delta is whole on both axes shall be handled and move the target by `−delta × 50` from the running target (or the offset when none), clamped to the scrollable range.
  - Tests: `ScrollSmoothTests.ANotchEasesToItsStepAndLandsExactly`, `ScrollSmoothTests.QuickNotchesAccumulateIntoOneTarget`, `ScrollSmoothTests.TheTargetClampsAtTheEnd`
- **BRIDGE-SCROLL-013** — The smooth scroll shall ease from the current offset to the target over 300 ms (× `Globals.TimeScale`) on `OutQuad` with `SetCurrentValue`, landing exactly. A notch at rest shall time the ease from the notch, with no head start. A notch during a running ease shall time it from the last frame, so the next frame advances it by a whole frame.
  - Tests: `ScrollSmoothTests.ANotchEasesToItsStepAndLandsExactly`, `ScrollSmoothTests.ANotchEasesFromItsOwnTimeWithoutAHeadStart`, `ScrollSmoothTests.ANotchMidEaseCarriesOnFromTheLastFrameWithoutAStall`
- **BRIDGE-SCROLL-014** — A wheel delta with a fractional part, touch, scrollbar and keyboard input shall pass through unsmoothed.
  - Tests: `ScrollSmoothTests.APreciseDeltaScrollsAtOnceUnsmoothed`
- **BRIDGE-SCROLL-015** — With Shift held and no horizontal delta, a vertical notch shall move the horizontal target.
  - Tests: `ScrollSmoothTests.AShiftedVerticalNotchEasesTheHorizontalTarget`
- **BRIDGE-SCROLL-016** — When a notch would not move the target (the range's end), the event shall be handled while an ease is running, and otherwise left unhandled so the presenter and outer scrollers get it.
  - Tests: `ScrollSmoothTests.ANotchPastTheEndWhileEasingIsSwallowedInsteadOfJumping`
- **BRIDGE-SCROLL-017** — Any offset change the smooth scroll did not write, including an extent shrink clamping its own write, shall stop it where it is.
  - Tests: `ScrollSmoothTests.ADirectOffsetSetCancelsTheEase`, `ScrollSpikeTests.SmoothTreatsItsOwnWriteClampedByAShrunkExtentAsForeignAndStops`
- **BRIDGE-SCROLL-018** — A `ScrollObserver` snap shall wait until the smooth scroll has settled.
  - Tests: `ScrollSmoothTests.ASnapWaitsForTheSmoothScrollToSettle`
- **BRIDGE-SCROLL-019** — When `Smooth` is set back to false, the bridge shall stop any running ease and restore native wheel steps.
  - Tests: `ScrollSmoothTests.TurningSmoothOffRestoresNativeNotches`
- **BRIDGE-SCROLL-020** — Off Windows and in design mode, `Smooth` shall do nothing.
  - Tests: manual — needs a non-Windows head (Browser, macOS); the property must leave native wheel scrolling unchanged there.
- **BRIDGE-SCROLL-071** — While `PrestoDefaults.ReducedMotion` is set at a notch, `Smooth` shall leave the wheel event unhandled, so the notch takes its native step at once.
  - Tests: `ScrollSmoothTests.UnderReducedMotionANotchTakesItsNativeStepAtOnce`
- **BRIDGE-SCROLL-085** — With Control or Meta held, `Smooth` shall leave the wheel event unhandled, so content inside the scroller can take it as a zoom.
  - Tests: `ScrollSmoothTests.AControlOrCommandNotchIsLeftForZoomableContent`

### `"ScrollX"`/`"ScrollY"` shorthands

- **BRIDGE-SCROLL-021** — `"ScrollX"` and `"ScrollY"` shall resolve as tween properties on a `ScrollViewer` only; animating them on any other target shall throw `ArgumentException`.
  - Tests: `ScrollShorthandTests.TheShorthandsResolveOnlyOnAScrollViewer`, `PropertyNamingTests.TransformShorthandsKeepTheirOwnName`
- **BRIDGE-SCROLL-022** — A shorthand tween shall read its from-value from the offset on its axis and write each frame with `SetCurrentValue`, never at `Animation` priority, clamped to `[0, ScrollBarMaximum]`, landing exactly.
  - Tests: `ScrollShorthandTests.AScrollYTweenLandsExactly`, `ScrollShorthandTests.AScrollXTweenLandsOnAHorizontalScroller`
- **BRIDGE-SCROLL-023** — After a shorthand tween, the scroller shall still take the wheel and other input.
  - Tests: `ScrollShorthandTests.TheScrollerStillTakesTheWheelAfterwards`
- **BRIDGE-SCROLL-024** — When an offset change the tween did not write arrives (input or another writer), the bridge shall remove only that scroller's tweens of that axis, leaving the animation's other tweens running.
  - Tests: `ScrollShorthandTests.UserInputMidFlightCancelsOnlyTheScrollTween`
- **BRIDGE-SCROLL-025** — When an extent shrink cuts the tween's last write back to the new `ScrollBarMaximum`, the tween shall keep running, clamped each frame, and move on if the extent regrows.
  - Tests: `ScrollShorthandTests.AnExtentShrinkMidFlightClampsEveryFrameWithoutCanceling`, `ScrollSpikeTests.TheLastWrittenValueAndTheMovedMaximumSeparateOwnWritesClampsAndInput`
- **BRIDGE-SCROLL-026** — Reverting a shorthand tween shall leave the offset where it is.
  - Tests: `ScrollShorthandTests.ARevertLeavesTheOffsetWhereItIs`
- **BRIDGE-SCROLL-027** — The shorthands shall work in timelines and through the builder's `ScrollX`/`ScrollY`.
  - Tests: `ScrollShorthandTests.TheShorthandRunsInATimelineAndThroughTheBuilder`
- **BRIDGE-SCROLL-028** — A shorthand tween's first write on an axis shall stop every observer's running snap and restart its wait; while the tween holds the axis no snap shall start; when it lets go (ended, reverted, stopped by input or canceled) the snaps shall wait their `Delay` again.
  - Tests: `ScrollToTests.ASnapWaitsForTheScrollToAndSnapsAfterItsDelayOnceItEnds`, `ScrollToTests.AScrollToStopsARunningSnapAndTheSnapRestartsAfterItsDelayOnceItEnds`, `ScrollToTests.ASnapDelayElapsingBetweenTheWritesOfALowFrameRateScrollToLeavesItRunning`, `ScrollToTests.ACanceledScrollToLetsTheSnapFollowAfterItsDelay`

### `ScrollTo`

- **BRIDGE-SCROLL-029** — `ScrollTo` shall return an `Animation` of the scroller carrying the builder's settings, callbacks and properties plus a `"ScrollX"`/`"ScrollY"` tween for each axis whose clamped target differs from the offset.
  - Tests: `ScrollToTests.LandsExactlyOnBothAxesAndLetsTheEngineIdle`
- **BRIDGE-SCROLL-030** (inherited) — The single-axis `ScrollTo` shall keep the other axis's offset.
  - Tests: `ScrollToTests.TheDefaultDurationGrowsWithTheViewportsTraveledAndCaps`
- **BRIDGE-SCROLL-031** — Without a builder duration, the scroll shall take `MotionTokens.Short` + 150 ms per viewport traveled (the larger axis), capped at `MotionTokens.Long`, × `Globals.TimeScale`; without a builder ease, `MotionTokens.Ease`.
  - Tests: `ScrollToTests.TheDefaultDurationGrowsWithTheViewportsTraveledAndCaps`
- **BRIDGE-SCROLL-032** — A builder's `Duration` and `Ease` shall win over the defaults.
  - Tests: `ScrollToTests.AnExplicitDurationAndEaseWin`
- **BRIDGE-SCROLL-033** — Each moving axis shall aim every frame at the requested offset held to the current extent, rebasing its start when that aim moves, so a shrink clamps without a jump and the requested offset is reached once the extent grows to fit it.
  - Tests: `ScrollToTests.AnExtentShrinkMidFlightClampsAndARegrowthCarriesOnToTheOffset`, `ScrollToTests.AnOffsetPastTheEndIsReachedOnceTheExtentGrowsToFitIt`
- **BRIDGE-SCROLL-034** — A wheel mid-flight shall stop the scroll.
  - Tests: `ScrollToTests.AWheelMidFlightCancels`
- **BRIDGE-SCROLL-035** — A second `ScrollTo` on the same scroller shall replace the first.
  - Tests: `ScrollToTests.ASecondScrollToReplacesTheFirst`
- **BRIDGE-SCROLL-036** (inherited) — When no axis moves, `ScrollTo` shall return an animation with no scroll tween, which completes on its first tick.
  - Tests: `ScrollToTests.WhenNoAxisMovesTheAnimationHasNoScrollTweenAndCompletesOnItsFirstTick`
- **BRIDGE-SCROLL-037** — When `ReducedMotion` is set at the call and the builder has no `WhenReduced` form, the scroll shall land on the first frame with no delay and let the engine idle; a `WhenReduced` form shall play instead, with the default motion filling its unset duration and ease.
  - Tests: `ScrollToTests.ReducedMotionJumpsOnTheFirstFrameAndTheEngineIdles`, `ScrollToTests.ReducedMotionPlaysTheBuildersReducedForm`

### `ScrollIntoView`

- **BRIDGE-SCROLL-038** — `ScrollIntoView` shall scroll every ancestor `ScrollViewer` of the element that has to move, all at once, each outer one aiming at the part of the element its inner one will show.
  - Tests: `ScrollIntoViewTests.NestedScrollersMoveTogetherAndShowTheTargetInBoth`
- **BRIDGE-SCROLL-039** — The element's rect shall come from summed layout slots, so render transforms do not count.
  - Tests: `ScrollIntoViewTests.TheTargetsRectIgnoresItsRenderTransform`
- **BRIDGE-SCROLL-040** — `Start`, `Center` and `End` shall align the margin-inflated rect's leading edge, center or trailing edge with the viewport's.
  - Tests: `ScrollIntoViewTests.EachAlignmentLandsTheTargetWhereItSays`, `ScrollIntoViewTests.TheMarginKeepsTheTargetClearOfTheEdge`
- **BRIDGE-SCROLL-041** — `Nearest`, the default, shall leave an axis alone when the rect is fully in view, and otherwise scroll the least distance, aligning the start when the rect is larger than the viewport.
  - Tests: `ScrollIntoViewTests.NearestScrollsTheLeastDistanceFromAboveOrBelow`, `ScrollIntoViewTests.NearestWithTheTargetInViewDoesNothingAndCompletesAtOnce`
- **BRIDGE-SCROLL-042** — Every target offset shall be clamped to `[0, ScrollBarMaximum]`.
  - Tests: `ScrollIntoViewTests.ATargetNearTheEndClampsToTheScrollableRange`
- **BRIDGE-SCROLL-043** — One animation shall drive the moving scrollers with `ScrollTo`'s motion over the largest distance, and input on one scroller shall stop only that scroller.
  - Tests: `ScrollIntoViewTests.AWheelOnTheOuterScrollerStopsOnlyTheOuter`
- **BRIDGE-SCROLL-044** — When nothing has to move, `ScrollIntoView` shall return an animation that has already completed, never null.
  - Tests: `ScrollIntoViewTests.NearestWithTheTargetInViewDoesNothingAndCompletesAtOnce`
- **BRIDGE-SCROLL-045** — Under `ReducedMotion`, `ScrollIntoView` shall land on the first frame and let the engine idle.
  - Tests: `ScrollIntoViewTests.ReducedMotionJumpsOnTheFirstFrameAndTheEngineIdles`

### `ScrollToIndex`

- **BRIDGE-SCROLL-046** — When `index` is negative or not below `ItemCount`, `ScrollToIndex` shall throw `ArgumentOutOfRangeException`.
  - Tests: `ScrollToIndexTests.AnIndexOutOfRangeThrows`
- **BRIDGE-SCROLL-047** — When the list's panel is not a `VirtualizingStackPanel`, `ScrollToIndex` shall `ScrollIntoView` the index's container with the given alignment, and throw `InvalidOperationException` when that container does not exist.
  - Tests: `ScrollToIndexTests.AListOnAnotherPanelScrollsTheIndexsContainerIntoView`, `ScrollToIndexTests.AListOnAnotherPanelWithoutTheIndexsContainerThrows`
- **BRIDGE-SCROLL-048** (inherited) — When the `VirtualizingStackPanel` has no `ScrollViewer` ancestor, `ScrollToIndex` shall throw `InvalidOperationException`.
  - Tests: `ScrollToIndexTests.AVirtualizingPanelWithoutAScrollViewerThrows`
- **BRIDGE-SCROLL-049** — When the item's aligned offset is more than 2 viewports away, the scroll shall first jump, in up to 3 laid-out passes, to 1 viewport short of `index × mean realized size` aligned, then animate the rest.
  - Tests: `ScrollToIndexTests.AFarIndexJumpsFirstThenAnimatesAboutAViewport`, `ScrollToIndexTests.AFarIndexLandsWithTheItemAligned`
- **BRIDGE-SCROLL-050** — Within 2 viewports, the scroll shall animate the whole way without a jump.
  - Tests: `ScrollToIndexTests.ANearIndexAnimatesTheWholeWayWithoutAJump`
- **BRIDGE-SCROLL-051** — The animated stretch shall aim every frame at the item's layout slot once realized, and before that at the nearest realized container plus the mean realized size per item; on completion it shall lay out and write the measured offset until it holds, then run the builder's `OnComplete`.
  - Tests: `ScrollToIndexTests.AFarIndexLandsWithTheItemAligned`
- **BRIDGE-SCROLL-052** — The scroll shall track the item rather than the index, following inserts and removals before it.
  - Tests: `ScrollToIndexTests.ItemsInsertedBeforeTheTargetMidFlightStillLandOnTheTargetItem`
- **BRIDGE-SCROLL-053** — With `Nearest`, the alignment shall be fixed at the call to `Start` or `End` by the side the item comes from.
  - Tests: `ScrollToIndexTests.NearestAlignsTheEndOfAnItemFromBelowAndTheStartOfOneFromAbove`
- **BRIDGE-SCROLL-054** — The first item shall land at offset 0 with `Start` and the last at `ScrollBarMaximum` with `End`.
  - Tests: `ScrollToIndexTests.TheLastIndexAtTheEndAndIndexZeroAtTheStartLandAtTheExtremes`
- **BRIDGE-SCROLL-055** — Input mid-flight shall stop the scroll.
  - Tests: `ScrollToIndexTests.AWheelMidFlightCancels`
- **BRIDGE-SCROLL-056** — Under `ReducedMotion` the scroll shall land on the first frame, settle aligned and let the engine idle.
  - Tests: `ScrollToIndexTests.ReducedMotionLandsAlignedAndTheEngineIdles`
- **BRIDGE-SCROLL-057** (inherited) — When the item is already aligned, `ScrollToIndex` shall return an animation that has already completed.
  - Tests: `ScrollToIndexTests.AnAlreadyAlignedItemReturnsACompletedAnimation`
- **BRIDGE-SCROLL-084** — `{presto:ScrollToItem}` shall provide a `CueAction` that, for a value that is not first, calls `ScrollToIndex` on its `ItemsControl` target with the value's index in `Items` and `Alignment` (default `Nearest`). It shall do nothing for a first value, a value not in `Items` or a list whose `ItemsPanelRoot` is null, and throw `InvalidOperationException` for a target that is not an `ItemsControl`.
  - Tests: `ScrollToIndexTests.ScrollToItemOnACueBringsAnAddedLastItemIntoViewIgnoresAValueThatIsNotAnItemAndThrowsOffAList`

### `Scroll.StickToEnd`

- **BRIDGE-SCROLL-058** — While `StickToEnd` is set, the scroller shall be armed whenever its offset is within 1 DIP of `ScrollBarMaximum` on its axis (vertical, or horizontal when only that scrolls), judged at creation and on every offset change whoever wrote it, so short content counts as at the end and any return re-arms it.
  - Tests: `ScrollStickToEndTests.ScrollingBackToTheEndReArms`, `ScrollStickToEndTests.ContentShorterThanTheViewportThatGrowsPastItFollows`
- **BRIDGE-SCROLL-059** — When `ScrollBarMaximum` changes while armed and leaves the view short of the end with no ease running, the scroller shall ease to the end with `ScrollTo`'s motion and land exactly.
  - Tests: `ScrollStickToEndTests.AppendsAtTheEndEaseToTheNewEndAndLandExactly`
- **BRIDGE-SCROLL-060** — Growth during a running ease shall retarget that ease rather than start another.
  - Tests: `ScrollStickToEndTests.ABurstMidEaseRetargetsWithoutJumping`
- **BRIDGE-SCROLL-061** — An offset change short of the end that the running ease did not write shall disarm it, so later growth leaves the offset alone.
  - Tests: `ScrollStickToEndTests.AWheelUpReleasesAndLaterAppendsLeaveTheOffsetAlone`
- **BRIDGE-SCROLL-062** — While armed, `Presence.ScrollAnchor` shall record no anchor, so at the end the scroller follows any growth and scrolled up the anchor holds the row.
  - Tests: `ScrollStickToEndTests.WithTheScrollAnchorAppendsAtTheEndFollowWithoutAnAnchorAndMoveForwardOnly`, `ScrollStickToEndTests.WithTheScrollAnchorAnInsertAtTheTopWhileScrolledUpKeepsTheRowStillAndMovesTheOffsetOnce`, `ScrollStickToEndTests.WithTheScrollAnchorAnInsertAtTheTopWhileAtTheEndIsLeftToStickToEnd`
- **BRIDGE-SCROLL-063** — Setting `StickToEnd` to false shall revert a running ease, leaving the offset where it is, and follow no more growth.
  - Tests: `ScrollStickToEndTests.TurningItOffMidEaseStopsWhereItIsAndFollowsNoMore`
- **BRIDGE-SCROLL-064** — Under `ReducedMotion` the follow shall jump to the new end and let the engine idle.
  - Tests: `ScrollStickToEndTests.ReducedMotionJumpsToTheNewEndAndTheEngineIdles`
- **BRIDGE-SCROLL-065** — In design mode, `Speed`, `StickToEnd`, `CueFor` and `FadeEdges` shall do nothing.
  - Tests: manual — open a view using them in the XAML previewer; nothing moves and no container is created.

### Edge cues (`Scroll.CueFor`, `Scroll.FadeEdges`)

- **BRIDGE-SCROLL-072** — While `CueFor` is set on an element attached to the visual tree, the bridge shall write the element's `Opacity` with `SetCurrentValue` as `clamp(distance past CueEdge ÷ CueRamp, 0, 1)` of the scroller `CueFor` names, where the distance past `Top` is the offset, past `Bottom` is extent − viewport − offset, and `Left`/`Right` likewise on the horizontal axis.
  - Tests: `ScrollEdgeCueTests.ABottomCueIsFullAtTheTopAndRampsOutOverTheLast24Units`, `ScrollEdgeCueTests.ATopCueMirrorsIt`, `ScrollEdgeCueTests.LeftAndRightCuesFollowAHorizontalScroller`
- **BRIDGE-SCROLL-073** — When `CueFor` names a control that is not a `ScrollViewer`, the scroller shall be the `ScrollViewer` whose `TemplatedParent` is that control, found once its template is applied; scrollers nested in its content shall be ignored.
  - Tests: `ScrollEdgeCueTests.AListBoxCueFollowsItsTemplatedScrollerAndIgnoresOneInsideAnItem`
- **BRIDGE-SCROLL-074** — `CueRamp` shall be read from the control `CueFor` names, and a cue shall be written again at once when its `CueEdge` or that control's `CueRamp` changes.
  - Tests: `ScrollEdgeCueTests.AListBoxCueFollowsItsTemplatedScrollerAndIgnoresOneInsideAnItem`, `ScrollEdgeCueTests.ChangingTheEdgeAppliesAtOnce`
- **BRIDGE-SCROLL-075** — Cues and masks shall be written inside the scroller's offset, extent and viewport notifications, never trailing the content by a frame.
  - Tests: `ScrollEdgeCueTests.ABottomCueIsFullAtTheTopAndRampsOutOverTheLast24Units`, `ScrollEdgeCueTests.EachEndOfTheMaskFadesByTheDistancePastIt`
- **BRIDGE-SCROLL-076** — When `CueFor` is cleared or changed, or the element detaches, the bridge shall drop the written `Opacity`, restoring the element's local value when it had one and its style or default value otherwise, and release the scroller's container.
  - Tests: `ScrollEdgeCueTests.ClearingCueForRestoresTheLocalOpacityAndReleasesTheContainer`, `ScrollEdgeCueTests.DetachingClearsTheOpacityAndReleasesTheContainer`, `ScrollEdgeCueTests.TheCueCanBeSetInXamlAndStyles`
- **BRIDGE-SCROLL-077** — A cue shall apply the same while `ReducedMotion` is set.
  - Tests: `ScrollEdgeCueTests.UnderReducedMotionTheCueStillFollows`
- **BRIDGE-SCROLL-078** — `FadeEdges`, `CueFor`, `CueEdge` and `CueRamp` shall be settable from XAML and from styles.
  - Tests: `ScrollEdgeCueTests.TheCueCanBeSetInXamlAndStyles`
- **BRIDGE-SCROLL-080** — While `FadeEdges` is above 0 on a `ScrollViewer` or a control templating one, the bridge shall set the control's `OpacityMask` to a linear gradient along the scroll axis (vertical, or horizontal when only that scrolls) whose ends ramp from transparent to opaque over `FadeEdges` DIPs, capped at half the control's length on that axis.
  - Tests: `ScrollEdgeCueTests.TheMaskRunsDownAVerticalScrollerAndAcrossOneThatOnlyScrollsSideways`, `ScrollEdgeCueTests.EachEndOfTheMaskFadesByTheDistancePastIt`, `ScrollEdgeCueTests.AListBoxMaskFollowsItsTemplatedScroller`, `ScrollEdgeCueTests.TheFadeIsCappedAtHalfTheLengthAndFollowsItsChanges`
- **BRIDGE-SCROLL-081** — Each end's transparency shall be `clamp(distance past that end ÷ CueRamp, 0, 1)`, so an end with nothing past it is fully opaque.
  - Tests: `ScrollEdgeCueTests.EachEndOfTheMaskFadesByTheDistancePastIt`, `ScrollEdgeCueTests.AListBoxMaskFollowsItsTemplatedScroller`, `ScrollEdgeCueTests.TheMaskRunsDownAVerticalScrollerAndAcrossOneThatOnlyScrollsSideways`
- **BRIDGE-SCROLL-082** — The mask shall be one brush, changed in place at each notification and when the control's length, its `FadeEdges` or its `CueRamp` changes, and each change shall be drawn without waiting for a scroll.
  - Tests: `ScrollEdgeCueTests.TheFadeIsCappedAtHalfTheLengthAndFollowsItsChanges`, `ScrollEdgeCueTests.AMaskMutatedInPlaceRendersWithoutAScroll`
- **BRIDGE-SCROLL-083** — When `FadeEdges` returns to 0 or the control detaches, the bridge shall clear the control's `OpacityMask` and release the scroller's container.
  - Tests: `ScrollEdgeCueTests.TheMaskReplacesTheControlsOwnAndReturningToZeroClearsItAndReleasesTheContainer`, `ScrollEdgeCueTests.DetachingClearsTheMaskAndReleasesTheContainer`

### Defaults

- **BRIDGE-SCROLL-066** — When `Smooth` is not set, wheel input shall pass through unsmoothed.
  - Tests: `ScrollSmoothTests.WithoutSmoothANotchTakesItsNativeStepAtOnce`
- **BRIDGE-SCROLL-067** (inherited) — When `StickToEnd` is not set, the scroller shall follow no growth.
  - Tests: `ScrollStickToEndTests.WithoutStickToEndGrowthAtTheEndLeavesTheOffsetAlone`
- **BRIDGE-SCROLL-068** (inherited) — When `axis` is not given, the single-axis `ScrollTo` shall scroll vertically.
  - Tests: `ScrollToTests.TheDefaultDurationGrowsWithTheViewportsTraveledAndCaps`
- **BRIDGE-SCROLL-069** (inherited) — When `margin` is not given, `ScrollIntoView` shall align the element's own rect, with no margin.
  - Tests: `ScrollIntoViewTests.EachAlignmentLandsTheTargetWhereItSays`
- **BRIDGE-SCROLL-070** (inherited) — When `alignment` is not given, `ScrollToIndex` shall align the item with `Start`.
  - Tests: `ScrollToIndexTests.ANearIndexAnimatesTheWholeWayWithoutAJump`
- **BRIDGE-SCROLL-079** — When not set, `FadeEdges` shall be 0 (no mask), `CueEdge` `Top` and `CueRamp` 24 DIPs.
  - Tests: `ScrollEdgeCueTests.ABottomCueIsFullAtTheTopAndRampsOutOverTheLast24Units`, `ScrollEdgeCueTests.ClearingCueForRestoresTheLocalOpacityAndReleasesTheContainer`, `ScrollEdgeCueTests.EachEndOfTheMaskFadesByTheDistancePastIt`

## Invariants

- Every offset write in this spec goes through the scroller's `ScrollWriter` with `SetCurrentValue`, clamped before the write and remembered per axis as (owner, value written, value replaced).
- A notification carrying a writer's last value is its own; one cutting it back to a shrunken `ScrollBarMaximum` is a clamp; anything else is foreign. Shorthand tweens keep running through a clamp; Smooth and the snap treat it as foreign.
- `ScrollTo`, `ScrollIntoView` and `ScrollToIndex` never return null.

## Edge cases

- An offset past the end in `ScrollTo` is clamped to decide which axes move and how long, but stays the aim, so it is reached if the extent grows.
- `ScrollTo(scroller, default)` is ambiguous between the `Vector` and `double` overloads; back to top is `ScrollTo(scroller, 0)`.
- A tween stopped by `Cancel()` never reaches the accessor; the writer drops its stale hold the next time a snap asks.
- During a smooth-wheel scroll with pacing on, a frame callback that runs late is paced as several intervals (BRIDGE-INIT pacing), so that frame moves two to five times as far; handling the wheel does not cause it.
- Content no longer than the viewport cues nothing (`ScrollEdgeCueTests.ContentNoLongerThanTheViewportCuesNothing`).
- A `FadeEdges` mask replaces any `OpacityMask` on the control and fades a visible scrollbar with the content (`ScrollEdgeCueTests.TheMaskReplacesTheControlsOwnAndReturningToZeroClearsItAndReleasesTheContainer`).
- `ScrollViewer` handles neither `Home` nor `End` (`ScrollSpikeTests.TheScrollViewerHandlesNeitherEndNorHomeButAListBoxEndAndHomeScrollToItsEnds`).

## Threading and lifetime

- UI thread only. Parallax and the shorthands' input detection run in the scroller's `Offset` notification; eases run on the engine.
- A parallax control holds the scroller's `ScrollContainer` until it detaches or its speed returns to 1. `Smooth` and `StickToEnd` hold handlers on the scroller until set back to false. An edge cue holds the container while its element is attached and `CueFor` is set, and a mask while its control is attached and `FadeEdges` is above 0.
- `StickToEnd` calls `PrestoAvalonia.EnsureInitialized` when set.

## Reduced motion

- Parallax keeps every element at its layout position while the flag is set, read per offset notification.
- `ScrollTo`, `ScrollIntoView`, `ScrollToIndex` and `StickToEnd` land on the first frame, reading the flag at the call; a builder's `WhenReduced` form plays instead.
- `Smooth` leaves each notch its native step while the flag is set, read per notch (BRIDGE-SCROLL-071).
- Edge cues follow the scroll position and apply unchanged (BRIDGE-SCROLL-077).

## Allocation

- A `"ScrollY"` tween ticks within the allocation budget (`BridgeAllocationTests.AScrollYTweenTicksWithinBudget`). Parallax updates allocate nothing per notification.
- An edge-cue update allocates nothing per notification beyond the property changes it raises (`BridgeAllocationTests.EdgeCuesAddOnlyTheirPropertyChangesToAnOffsetWrite`).

## Provenance

- No upstream counterpart: anime.js 4.5.0 has no parallax, wheel smoothing or animated scrolling (GSAP ScrollSmoother/ScrollToPlugin, Compose and Flutter were the models).

## Decisions

- **Scroll offsets are written with `SetCurrentValue`.** The shorthands, `ScrollTo`, `Smooth`, `StickToEnd` and the snap write `ScrollViewer.Offset` at its local priority, never as an `Animation`-priority property animation. Reason: `Animation` priority pins the offset against the user's own input; a spike confirmed that wheel, drag, keys and `BringIntoView` still move a `SetCurrentValue` offset. Rejected: an `Animation`-priority binding.
- **One `ScrollWriter` tells own writes, clamps and foreign writes apart.** It remembers per scroller and axis the last owner and the clamped value written; a matching notification is the owner's, one cut back to a shrunken `ScrollBarMaximum` is a clamp, and anything else, another writer included, is foreign. A foreign change removes only that axis's tweens, a clamp keeps a shorthand tween running (`Smooth` and the snap treat it as foreign), and a revert leaves the offset. Reason: priority never tells writers apart and the offset coerces inside the write, so exact comparison of the clamped value is the only reliable test; input keeps working during and after a tween.
- **The default scroll motion grows with the viewports traveled.** `Tokens.Short` + 150 ms per viewport of distance (the larger axis), capped at `Tokens.Long`, scaled by `TimeScale`, on `Tokens.Ease`; builder settings win; each axis tweens eased progress mapped to the requested offset held to the extent, re-aimed every frame and rebased so it never jumps; a new `ScrollTo` replaces the running one. Reduced, it lands on the first frame, read at the call, unless the builder has a `WhenReduced` form. Reason: a shrink clamps without a jump and a regrowth reaches the request; otherwise not recorded. Rejected: a spring default.
- **Scroll tweens and snaps never overlap.** A tween's first write cancels the observers' running snaps, no snap starts while a tween holds the axis, and the shorthand restarts every snap's wait when it lets go; a hold left by `Cancel()` is detected through `Presto.GetAnimations`. Reason: a tween stopped by `Cancel()` never reaches the accessor; otherwise not recorded.
- **`ScrollIntoView` scrolls every ancestor scroller at once.** Innermost first, each outer one aiming at the element's rect at the inner's final offset cut to the inner viewport; rects come from summed layout slots; one `Animation` drives them; nothing to move returns a completed animation. Reason: browser `scrollIntoView` and Flutter `ensureVisible` behave so.
- **`ScrollToIndex` jumps close, then animates a measured last stretch.** On a `VirtualizingStackPanel`, beyond 2 viewports it jumps to 1 viewport short of `index × mean size` (up to 3 layout passes), then eases the rest re-aimed every frame and writes the measured offset until it holds; it tracks the item, not the index; other panels scroll the container into view. Reason: Compose's `animateScrollToItem` does so; the item is not realized yet, the panel's extent estimate is stale and private, and the panel places a jump clear of its realized items.
- **`{presto:ScrollToItem}` skips a first watched value and a value it cannot place.** Reason: a scroll to an item marks a moment, like `{presto:Burst}` (BRIDGE-PARTICLES), and a cue raised for an item that is not in the list, or before the list has a panel, has nothing to scroll to, so it is not an error in XAML. Rejected: throwing `ScrollToIndex`'s `ArgumentOutOfRangeException` for a missing item.
- **The parallax entry is clamped at 0 and measured at once.** `Speed` translates by `(1 − speed) × max(0, scroll − entry)`, written in the same `Offset` notification, and re-measured immediately on layout, viewport or extent changes, not after the observers' debounce. Reason: without the clamp an element in view at load was pushed off its layout position before any scroll; the element is visible meanwhile, so the re-measure cannot wait.
- **Smoothed wheel is opt-in and Win32 only.** `Smooth` smooths whole notches on Windows only, accumulating into one target, and passes fractional deltas, touch, scrollbar, keyboard and end-of-range notches through; a touchpad delta that is an exact multiple of 120 is accepted as a known caveat. Reason: wheel events carry only `Delta`, and only Win32 makes a notch distinguishable; the Browser head divides DOM deltas by 50 ignoring `deltaMode` and macOS passes already-smoothed deltas. Passing end-of-range notches keeps scroll chaining; they are held back only while an ease is running, where the presenter would jump the rest of the way and cancel it. Each notch rebases the ease's start to the notch itself, because `Resume` adds a one-frame head start that jumped the scroll ahead at every notch of a long scroll. A notch during a running ease rebases instead to the last frame's time, because the ease restarts from the offset that frame rendered, so that frame is its matching origin and the next frame advances a whole interval rather than only the time since the notch; a notch at rest keeps `Engine.Now`, because the last frame can be arbitrarily old and backdating to it would jump ahead. Rejected: keeping `Engine.Now` and lowering the start offset by the expected movement (it guesses the speed); seeking the ease forward by the time since the last frame (it renders between frames); making `AvaloniaFrameSource.Now` return the last frame's time between frames (every input handler would read a frozen time, breaking pointer velocity tracking); a public `Engine.FrameTime` (no other caller needs it).
- **`StickToEnd` arms by position and never acts on the same change as `ScrollAnchor`.** It is armed within 1 DIP of `ScrollBarMaximum`, judged on every offset change; while armed a maximum change eases to the end, retargeting one running ease; a change short of the end not written by that ease releases it; `ItemChanges` records no anchor while armed. Reason: at the end it follows growth and scrolled up the anchor holds the row, so one change is never handled twice.
- **The edge cue drives an app-supplied element's opacity rather than drawing a shadow.** Reason: a shadow's color, height, margins and corner treatment are theme work (Tempo's differ inside the Done expander), and an opacity on the app's element keeps all of that in XAML styles. Rejected: `Scroll.EdgeShadows="True"` adorning bridge-drawn shadows (would need a styling surface of its own); a templated `ScrollEdgeHost` control (forces a wrapper on every list).
- **Edge cues follow scroll position, not time, so reduced motion leaves them on.** Reason: they are a static reading of where the content is, and they move only as fast as the user scrolls, like a scrollbar thumb. Rejected: suppressing them under `ReducedMotion` (removes information, not motion).
- **`CueFor` names a scroller or a control that templates one, and only that scroller counts.** The templated scroller is the `ScrollViewer` whose `TemplatedParent` is the control, looked up on attach and on each `TemplateApplied`. Reason: binding `#TaskList` (a `ListBox`) is how XAML reaches it, and a scroller inside an item template is never picked; the cue reads only that scroller's notifications, so a nested scroller's `ScrollChanged` never reaches it.
- **The cue writes `Opacity` with `SetCurrentValue` and drops it when `CueFor` is cleared or changed or the element detaches.** A local value is recorded on attach and set back, because `SetCurrentValue` overwrites a local value in place and `ClearValue` removes it; with none, `ClearValue` lets the style or default show again. Reason: the element's XAML or style value is its look before the first scroll reading, and `SetCurrentValue` overrides it without changing its priority, as scroll offset writes do. Rejected: a Presto tween per notification (the value already moves only with the scroll; easing it would lag the content).
- **`CueRamp` is read from the scroller or templating control and is a separate length from the mask fade.** Reason: one is how far the content must go past the edge, the other how wide a fade is drawn; Tempo uses 24 for both, which become the defaults, but a wide fade on short content wants a short ramp. Rejected: one length for both.
- **The mask fade is named `FadeEdges`, as on `Marquee`.** Reason: same meaning (a ramp length in DIPs at both ends of the axis, 0 = off), so one name to learn; the only difference is that the scroller's ends fade only while content lies past them.
- **One mask axis, picked as `StickToEnd` and parallax pick it.** Reason: a control has one `OpacityMask`, and a single linear gradient cannot fade two axes; the "vertical, or horizontal when only that scrolls" rule is already in BRIDGE-SCROLL-003 and -058. Rejected: a radial or composed mask for both axes (no use case; a two-axis scroller can take shadow cues on all four edges).
- **The mask lives on the scroller or templating control, and replaces any `OpacityMask` there.** Reason: on the control it stays put while the content scrolls, and its relative stops change only with the control's length, `FadeEdges` or the distance past each end. Accepted: a visible scrollbar inside the control fades with it, so the mask suits scrollers whose bars are hidden.
- **The mask is one brush with four stops, changed in place, and the scroller's presenter is invalidated after each change.** Reason: a new brush per notification would allocate on every scroll; the renderer repaints a mask changed in place only where something under it is invalidated, and the scroller itself draws nothing, so invalidating the control left the old mask on screen until the next scroll. Rejected: invalidating the control (its render is empty); re-setting `OpacityMask` (two property changes per notification).
- **Pin, spacing and snap follow GSAP ScrollTrigger, layered on the ported observer.** See BRIDGE-SCROLLOBSERVER.
- **The signal is an event on `Engine`, not on `PrestoDefaults`.** See CORE-REDUCED.
- **`Smooth` leaves Ctrl/Cmd+wheel to the content.** Its handler runs on the tunnel pass, before anything inside the scroller, so without this a zoomable child (a `Transformable` photo) inside a smoothed page scroller could never zoom. Reason: Ctrl/Cmd+wheel is the platform zoom gesture, never a scroll. Rejected: handling the wheel on the bubble pass (the scroller's own presenter handles it first, so nothing would be smoothed).

## Non-goals

- Smoothing wheel input off Win32 (Smoothed wheel is opt-in and Win32 only), touchpad or touch inertia.
- Spring motion for scroll-to.
- Animating `ScrollViewer.Offset` at `Animation` priority.

## Open items

- 020 and 065 need manual checks (non-Windows head; XAML previewer).
