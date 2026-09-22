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
        var isRunningBackwards = (parent != null ? parent.Backwards : tickableAbsoluteTime < tickablePrevAbsoluteTime) ? !isReversed : isReversed;

        tickable._currentTime = tickableAbsoluteTime;
        tickable._iterationTime = iterationTime;
        tickable.Backwards = isRunningBackwards;

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
            var tlChildrenTickTime = Presto.Engine.Now;

            var tlChildrenHaveCompleted = true;

            // A timeline that looped forward must trigger the callbacks its children skipped.
            if (!internalRender && tickable._currentIteration != _currentIteration)
            {
                var tlIterationDuration = tickable.IterationDuration;
                var child = tickable._head;
                while (child != null)
                {
                    var nextChild = child._next;
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
                    child = nextChild;
                }
                if (!muteCallbacks) tickable.OnLoop?.Invoke(tickable);
            }

            var activeChild = tlIsRunningBackwards ? tickable._tail : tickable._head;
            while (activeChild != null)
            {
                var nextActiveChild = tlIsRunningBackwards ? activeChild._prev : activeChild._next;
                // Rounding is needed when using seconds.
                var childTime = Helpers.Round((tlChildrenTime - activeChild._offset) * activeChild._speed, 12);
                // Past-end siblings are skipped on backward iteration so their end values do not overwrite the active sibling's write.
                if (!(tlIsRunningBackwards && childTime > activeChild._delay + activeChild.Duration))
                {
                    var childTickMode = activeChild._fps < tickable._fps ? activeChild.RequestTick(tlChildrenTickTime) : tickMode;
                    Render(activeChild, childTime, muteCallbacks, internalRender, childTickMode);
                    if (!activeChild.Completed && tlChildrenHaveCompleted) tlChildrenHaveCompleted = false;
                }
                activeChild = nextActiveChild;
            }

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
}
