using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Tests;

/// <summary>Resets the <see cref="Presto.Engine"/> singleton before each test and drives it through a <see cref="ManualFrameSource"/>.</summary>
public abstract class EngineTestBase
{
    protected ManualFrameSource Frames { get; }

    protected EngineTestBase()
    {
        var engine = Presto.Engine;
        engine.FrameSource = Frames = new ManualFrameSource();
        Helpers.ForEachChildren(engine, static child => child.Cancel());
        engine.DropChildren();
        // The engine is reused across tests, so an additive tween left by an earlier one would keep rendering into this one.
        engine.Additive.Lookups.Clear();
        engine.Additive.Parked.Clear();
        if (engine.Additive.Animation is { } additive) additive._headTween = additive._tailTween = null;
        engine.Diagnostics.Enabled = false;
        engine.TimeDilation = 1;
        engine.PauseOnHidden = true;
        engine.Hidden = false;
        engine.TimerCreated = null;
        engine.Thaw();
        engine.Pause();
        engine.Paused = true;
        engine._timeOffset = 0;
        engine._currentTime = engine._lastTickTime = engine._startTime = engine._lastTime = Frames.Now;
        engine.TimeUnit = TimeUnit.Milliseconds;
        engine.Own(new());
        engine.Speed = 1;
        engine.Fps = 240;
        engine.Precision = 4;
        engine.UseDefaultMainLoop = true;
    }

    /// <summary>Advances <see cref="Frames"/> in <paramref name="step"/> ms frames until <paramref name="condition"/> holds or <paramref name="max"/> ms have elapsed.</summary>
    protected void RunUntil(Func<bool> condition, double step = 16, double max = 10000)
    {
        var limit = Frames.Now + max;
        while (!condition() && Frames.Now < limit) Frames.Advance(step);
    }

    /// <summary>
    /// Bytes allocated per 16 ms frame over 200 frames, measured once the tick path is warm, as the median of five 40-frame
    /// blocks so a one-off allocation on the thread (seen in full CI runs) cannot fail a budget.
    /// </summary>
    protected double BytesPerTick()
    {
        const int blockFrames = 40;
        Span<double> blocks = stackalloc double[5];
        for (var i = 0; i < 10; i++) Frames.Advance(16);
        for (var b = 0; b < blocks.Length; b++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < blockFrames; i++) Frames.Advance(16);
            blocks[b] = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)blockFrames;
        }
        blocks.Sort();
        return blocks[blocks.Length / 2];
    }
}
