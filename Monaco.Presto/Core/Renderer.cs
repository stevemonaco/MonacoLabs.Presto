using Monaco.Presto.Values;

namespace Monaco.Presto.Core;

internal static class Renderer
{
    public static bool Render(Timer tickable, double time, bool muteCallbacks, bool internalRender, TickMode tickMode)
    {
        var parent = tickable.Parent;
        var duration = tickable.Duration;
        var completed = tickable.Completed;
        var iterationDuration = tickable.IterationDuration;
        var iterationCount = tickable.IterationCount;
        var _currentIteration = tickable._currentIteration;
        var _loopDelay = tickable._loopDelay;
        var _reversed = tickable._reversed;
        var _alternate = tickable._alternate;
        var _hasChildren = tickable._hasChildren;
        var tickableDelay = tickable._delay;
        var tickablePrevAbsoluteTime = tickable._currentTime;
        var tickableEndTime = tickableDelay + iterationDuration;
        var tickableAbsoluteTime = time - tickableDelay;
        var tickablePrevTime = Helpers.Clamp(tickablePrevAbsoluteTime, -tickableDelay, duration);
        var tickableCurrentTime = Helpers.Clamp(tickableAbsoluteTime, -tickableDelay, duration);
        var deltaTime = tickableAbsoluteTime - tickablePrevAbsoluteTime;
        var isCurrentTimeAboveZero = tickableCurrentTime > 0;
        var isCurrentTimeEqualOrAboveDuration = tickableCurrentTime >= duration;
        var isSetter = duration <= Constants.MinValue;
        var forcedTick = tickMode == TickMode.Force;

        var isOdd = false;
        var iterationElapsedTime = tickableAbsoluteTime;
        var hasRendered = false;

        if (iterationCount > 1)
        {
            var period = iterationDuration + (isCurrentTimeEqualOrAboveDuration ? 0 : _loopDelay);
            var currentIteration = Math.Truncate(tickableCurrentTime / period);
            tickable._currentIteration = (int)Helpers.Clamp(currentIteration, 0, iterationCount);
            if (isCurrentTimeEqualOrAboveDuration) tickable._currentIteration--;
            isOdd = tickable._currentIteration % 2 != 0;
            // Derived from the same truncation that gave currentIteration; `% period` can disagree with it under float drift at iteration boundaries.
            iterationElapsedTime = tickableCurrentTime - currentIteration * period;
            if (double.IsNaN(iterationElapsedTime)) iterationElapsedTime = 0;
        }

        var isReversed = _reversed ^ (_alternate && isOdd);
        var iterationTime = isCurrentTimeEqualOrAboveDuration ? isReversed ? 0 : duration : isReversed ? iterationDuration - iterationElapsedTime : iterationElapsedTime;
        if (tickable._ease is { } playbackEase)
        {
            iterationTime = iterationDuration * playbackEase(iterationTime / iterationDuration);
            if (double.IsNaN(iterationTime)) iterationTime = 0;
        }
        var isRunningBackwards = (parent != null ? parent.Backwards : tickableAbsoluteTime < tickablePrevAbsoluteTime) ? !isReversed : isReversed;

        tickable._currentTime = tickableAbsoluteTime;
        tickable._iterationTime = iterationTime;
        tickable.Backwards = isRunningBackwards;
        tickable._progressValue?.Assign(tickable.Progress);

        if (isCurrentTimeAboveZero && !tickable.Began)
        {
            tickable.Began = true;
            if (!muteCallbacks && !(parent != null && (isRunningBackwards || !parent.Began)))
            {
                tickable.OnBegin?.Invoke(tickable);
            }
        }
        else if (tickableAbsoluteTime <= 0)
        {
            tickable.Began = false;
        }

        // Timers with children fire onLoop from Tick instead, after their children have rendered.
        if (!muteCallbacks && !_hasChildren && isCurrentTimeAboveZero && tickable._currentIteration != _currentIteration)
        {
            tickable.OnLoop?.Invoke(tickable);
        }

        if (
            forcedTick ||
            tickMode == TickMode.Auto && (
                // Timeline children render from their offset instead of their delay so the gap left by a truncated sibling is covered on seek.
                time >= (parent != null && tickableDelay > 0 ? 0 : tickableDelay) && time <= tickableEndTime ||
                time <= tickableDelay && tickablePrevTime > tickableDelay ||
                time >= tickableEndTime && tickablePrevTime != duration
            ) ||
            iterationTime >= tickableEndTime && tickablePrevTime != duration ||
            iterationTime <= tickableDelay && tickablePrevTime > 0 && !isCurrentTimeEqualOrAboveDuration ||
            time <= tickablePrevTime && tickablePrevTime == duration && completed ||
            isCurrentTimeEqualOrAboveDuration && !completed && isSetter
        )
        {
            if (isCurrentTimeAboveZero)
            {
                tickable.ComputeDeltaTime(tickablePrevTime);
                if (!muteCallbacks) tickable.OnBeforeUpdate?.Invoke(tickable);
            }

            if (!_hasChildren)
            {
                // Time jumped by more than the tick threshold, so this tick is treated as manual and every tween re-renders.
                var forcedRender = forcedTick || (isRunningBackwards ? -deltaTime : deltaTime) >= Globals.TickThreshold;
                // Rounded to the precision of Tween._absoluteStartTime so equal-time boundary checks compare cleanly.
                var absoluteTime = Helpers.Round(tickable._offset + (parent?._offset ?? 0) + tickableDelay + iterationTime, 12);

                var tween = tickable._headTween;
                while (tween != null)
                {
                    var tweenComposition = tween._composition;
                    var tweenCurrentTime = tween._currentTime;
                    var tweenChangeDuration = tween._changeDuration;
                    var tweenAbsEndTime = tween._absoluteStartTime + tweenChangeDuration;
                    var tweenNextRep = tween._nextRep;
                    var tweenPrevRep = tween._prevRep;
                    var tweenHasComposition = tweenComposition != Composition.None;
                    // The previous sibling stops writing at its truncated end, so this tween takes over the hold from that point.
                    var tweenPrevRepEndTime = tweenPrevRep != null ? tweenPrevRep._absoluteStartTime + tweenPrevRep._changeDuration : 0;
                    var tweenPrevRepIsCrossParent = tweenPrevRep != null && tweenPrevRep.Parent != tween.Parent;
                    // Same parent keyframes take over at their own start, cross parent siblings at their update start, negative delay siblings at their own start.
                    var tweenNextRepTakeover =
                        tweenNextRep == null || tweenNextRep._isOverridden ? tweenAbsEndTime :
                        tweenNextRep.Parent == tween.Parent ? tweenAbsEndTime + tweenNextRep._delay :
                        tweenNextRep._absoluteStartTime < tweenNextRep._absoluteUpdateStartTime ? tweenNextRep._absoluteStartTime : tweenNextRep._absoluteUpdateStartTime;

                    if ((forcedRender || (
                            // Tail keyframes always re-evaluate the gate, so an earlier keyframe writing past its own range cannot leave the target stale after a backward seek.
                            (tweenCurrentTime != tweenChangeDuration || absoluteTime <= tweenNextRepTakeover ||
                                (tweenPrevRep != null && !tweenPrevRepIsCrossParent && (tweenNextRep == null || tweenNextRep.Parent != tween.Parent))) &&
                            // A cross parent tween re-renders its from value from the previous sibling's truncated end so the handoff gap holds,
                            // and a keyframe re-renders its from revert while the next keyframe time is stale.
                            (tweenCurrentTime != 0 || absoluteTime >= tween._absoluteStartTime ||
                                (tweenPrevRepIsCrossParent && !tween._hasFromValue && !tweenPrevRep!._isOverridden && absoluteTime >= tweenPrevRepEndTime) ||
                                (tweenNextRep != null && !tweenNextRep._isOverridden && tweenNextRep.Parent == tween.Parent && tweenNextRep._currentTime != 0 && iterationTime < tweenNextRep._startTime))
                        )) &&
                        // Non-first keyframes wait until the iteration reaches their own start, so the previous keyframe can handle the from-revert of a backward scrub.
                        (tweenPrevRep == null || tweenPrevRepIsCrossParent || iterationTime >= tween._startTime) &&
                        (!tweenHasComposition || (
                            !tween._isOverridden &&
                            (!tween._isOverlapped || absoluteTime <= tweenAbsEndTime) &&
                            // The next sibling owns the value past its takeover point, the previous one up to its truncated end; explicit from values wait for their own start.
                            (tweenNextRep == null || tweenNextRep._isOverridden || absoluteTime <= tweenNextRepTakeover) &&
                            (tweenPrevRep == null || tweenPrevRep._isOverridden || (!tweenPrevRepIsCrossParent
                                ? absoluteTime >= tweenPrevRepEndTime + tween._delay
                                : absoluteTime >= tween._absoluteStartTime || (!tween._hasFromValue && absoluteTime >= tweenPrevRepEndTime)))
                        )))
                    {
                        var tweenNewTime = tween._currentTime = Helpers.Clamp(iterationTime - tween._startTime, 0, tweenChangeDuration);
                        var tweenTime = tweenNewTime / tween._updateDuration;
                        var tweenProgress = tween._ease(tweenTime);
                        var tweenModifier = tween._modifier;
                        var tweenValueType = tween._valueType;
                        var tweenIsNumber = tweenValueType == ValueKind.Number;
                        // Only the in-between frames of a string value are rounded; a number target consumes the raw lerp.
                        var tweenPrecision = tweenIsNumber || tweenProgress == 0 || tweenProgress == 1 ? -1 : Globals.Precision;

                        double number;
                        if (tweenIsNumber || tweenValueType == ValueKind.Unit)
                        {
                            number = tweenModifier(Helpers.Round(Helpers.Lerp(tween._fromNumber, tween._toNumber, tweenProgress), tweenPrecision));
                        }
                        else if (tweenValueType == ValueKind.Color)
                        {
                            var ns = tween._numbers;
                            var fn = tween._fromNumbers;
                            var tn = tween._toNumbers;
                            var channelEases = tween._channelEases;
                            // RGB channels lerp in pseudo-linear space (square inputs, square-root result) to approximate gamma correct blending.
                            ns[0] = tweenModifier(SquaredLerp(fn[0], tn[0], channelEases == null ? tweenProgress : channelEases[0](tweenTime)));
                            ns[1] = tweenModifier(SquaredLerp(fn[1], tn[1], channelEases == null ? tweenProgress : channelEases[1](tweenTime)));
                            ns[2] = tweenModifier(SquaredLerp(fn[2], tn[2], channelEases == null ? tweenProgress : channelEases[2](tweenTime)));
                            ns[3] = tweenModifier(Helpers.Lerp(fn[3], tn[3], channelEases == null ? tweenProgress : channelEases[3](tweenTime)));
                            number = double.NaN;
                        }
                        else
                        {
                            tween.ComposeComplexNumbers(tweenProgress, tweenTime, tweenPrecision);
                            number = double.NaN;
                        }

                        if (tweenHasComposition) tween._number = number;

                        if (!internalRender && tweenComposition != Composition.Blend)
                        {
                            tween.Accessor.SetValue(tween.Target, number, tween);
                            if (isCurrentTimeAboveZero) hasRendered = true;
                        }
                        else
                        {
                            // Composing tweens without a real render is the only path that pays for the composed value.
                            tween._value = tween.ComposeValue(number);
                        }
                    }
                    else if (tweenCurrentTime != 0 && tweenPrevRep != null && !tweenPrevRepIsCrossParent && iterationTime < tween._startTime)
                    {
                        // The playhead moved before this keyframe's start; the previous keyframe owns the from revert and writes it once.
                        tween._currentTime = 0;
                    }

                    tween = tween._next;
                }

                if (!muteCallbacks && hasRendered) tickable._onRender?.Invoke(tickable);
            }

            if (!muteCallbacks && isCurrentTimeAboveZero)
            {
                tickable.OnUpdate?.Invoke(tickable);
            }
        }

        if (parent != null && isSetter)
        {
            // tickableAbsoluteTime > 0 rather than >= duration avoids floating point misses, see anime issue #1088.
            if (!muteCallbacks && (
                (parent.Began && !isRunningBackwards && tickableAbsoluteTime > 0 && !completed) ||
                (isRunningBackwards && tickableAbsoluteTime <= Constants.MinValue && completed)
            ))
            {
                tickable.OnComplete?.Invoke(tickable);
                tickable.Completed = !isRunningBackwards;
            }
        }
        else if (isCurrentTimeAboveZero && isCurrentTimeEqualOrAboveDuration)
        {
            if (double.IsPositiveInfinity(iterationCount))
            {
                tickable._startTime += tickable.Duration;
            }
            else if (tickable._currentIteration >= iterationCount - 1)
            {
                // Paused tells the engine loop to stop rendering this tickable and drop it from the list on the next tick.
                tickable.Paused = true;
                if (!completed && !_hasChildren)
                {
                    tickable.Completed = true;
                    if (!muteCallbacks && !(parent != null && (isRunningBackwards || !parent.Began)))
                    {
                        tickable.OnComplete?.Invoke(tickable);
                        tickable.Resolve();
                    }
                }
            }
        }
        else
        {
            tickable.Completed = false;
        }

        return hasRendered;
    }

    public static void Tick(Timer tickable, double time, bool muteCallbacks, bool internalRender, TickMode tickMode)
    {
        var _currentIteration = tickable._currentIteration;
        Render(tickable, time, muteCallbacks, internalRender, tickMode);
        if (tickable._hasChildren)
        {
            var tlIsRunningBackwards = tickable.Backwards;
            var tlChildrenTime = internalRender ? time : tickable._iterationTime;
            var tlChildrenTickTime = tickable.OwnerEngine.Now;

            var tlChildrenHasRendered = false;
            var tlChildrenHaveCompleted = true;

            // A timeline that looped forward must trigger the callbacks its children skipped.
            if (!internalRender && tickable._currentIteration != _currentIteration)
            {
                var tlIterationDuration = tickable.IterationDuration;
                var child = tickable._head;
                tickable._nextChildIsPrev = false;
                while (child != null)
                {
                    tickable._nextChild = child._next;
                    if (!tlIsRunningBackwards)
                    {
                        if (!child.Completed && !child.Backwards && child._currentTime < child.IterationDuration)
                        {
                            Render(child, tlIterationDuration, muteCallbacks, true, TickMode.Force);
                        }
                        child.Began = false;
                        child.Completed = false;
                    }
                    else
                    {
                        var childDuration = child.Duration;
                        var childStartTime = child._offset + child._delay;
                        var childEndTime = childStartTime + childDuration;
                        if (!muteCallbacks && childDuration <= Constants.MinValue && (childStartTime == 0 || childEndTime == tlIterationDuration))
                        {
                            child.OnComplete?.Invoke(child);
                        }
                    }
                    child = tickable._nextChild;
                }
                if (!muteCallbacks) tickable.OnLoop?.Invoke(tickable);
            }

            var activeChild = tlIsRunningBackwards ? tickable._tail : tickable._head;
            tickable._nextChildIsPrev = tlIsRunningBackwards;
            while (activeChild != null)
            {
                tickable._nextChild = tlIsRunningBackwards ? activeChild._prev : activeChild._next;
                // Rounding is needed when using seconds.
                var childTime = Helpers.Round((tlChildrenTime - activeChild._offset) * activeChild._speed, 12);
                // Past-end siblings are skipped on backward iteration so their end values do not overwrite the active sibling's write.
                if (!(tlIsRunningBackwards && childTime > activeChild._delay + activeChild.Duration))
                {
                    var childTickMode = activeChild._fps < tickable._fps ? activeChild.RequestTick(tlChildrenTickTime) : tickMode;
                    tlChildrenHasRendered |= Render(activeChild, childTime, muteCallbacks, internalRender, childTickMode);
                    if (!activeChild.Completed && tlChildrenHaveCompleted) tlChildrenHaveCompleted = false;
                }
                activeChild = tickable._nextChild;
            }

            // Renders on a timeline are triggered by its children, so this follows the child loop.
            if (!muteCallbacks && tlChildrenHasRendered) tickable._onRender?.Invoke(tickable);

            if ((tlChildrenHaveCompleted || tlIsRunningBackwards) && tickable._currentTime >= tickable.Duration)
            {
                tickable.Paused = true;
                if (!tickable.Completed)
                {
                    tickable.Completed = true;
                    if (!muteCallbacks)
                    {
                        tickable.OnComplete?.Invoke(tickable);
                        tickable.Resolve();
                    }
                }
            }
        }
    }

    private static double SquaredLerp(double from, double to, double progress) => Math.Sqrt(from * from * (1 - progress) + to * to * progress);
}
