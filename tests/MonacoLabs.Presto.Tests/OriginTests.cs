using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto.Tests;

public sealed class OriginTests : EngineTestBase
{
    [Fact]
    public void TheTagAppliesInsideTheScopeAndNests()
    {
        Presto.Engine.Diagnostics.Enabled = true;
        Timer outer, inner;
        using (PrestoDiagnostics.Origin("Outer"))
        {
            using (PrestoDiagnostics.Origin("Inner")) inner = Presto.CreateTimer(new() { Autoplay = false });
            outer = Presto.CreateTimer(new() { Autoplay = false });
        }
        var untagged = Presto.CreateTimer(new() { Autoplay = false });

        Assert.Equal("Outer", outer.Origin);
        Assert.Equal("Inner", inner.Origin);
        Assert.Null(untagged.Origin);
    }

    [Fact]
    public void ChildrenInheritTheirRootsOrigin()
    {
        Presto.Engine.Diagnostics.Enabled = true;
        Timeline timeline;
        using (PrestoDiagnostics.Origin("Feature")) timeline = Presto.CreateTimeline();
        timeline.Add(new TimerParams { Duration = 100 });

        foreach (var child in timeline.Children) Assert.Equal("Feature", child.Origin);
    }

    [Fact]
    public void InactiveOriginAllocatesNothingAndTagsNothing()
    {
        long bytes;
        Timer timer;
        var before = GC.GetAllocatedBytesForCurrentThread();
        using (PrestoDiagnostics.Origin("Feature"))
        {
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Presto.Engine.Diagnostics.Enabled = true;
            timer = Presto.CreateTimer(new() { Autoplay = false });
        }

        Assert.Equal(0, bytes);
        Assert.Null(timer.Origin);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CreationTraceNeedsBothFlags(bool enabled, bool capture)
    {
        Presto.Engine.Diagnostics.Enabled = enabled;
        PrestoDiagnostics.CaptureCreationTrace = capture;
        try
        {
            var timer = Presto.CreateTimer(new() { Autoplay = false });

            if (enabled && capture) Assert.Contains(nameof(CreationTraceNeedsBothFlags), timer.CreationTrace!.ToString());
            else Assert.Null(timer.CreationTrace);
        }
        finally
        {
            PrestoDiagnostics.CaptureCreationTrace = false;
        }
    }

    [Fact]
    public void TheScopeIsRecorded()
    {
        Presto.Engine.Diagnostics.Enabled = true;
        var scope = new Scope();

        var timer = scope.Execute(_ => Presto.CreateTimer(new() { Autoplay = false }));

        Assert.Same(scope, timer.Scope);
    }

    [Fact]
    public void TheAdditiveAnimationIsTagged()
    {
        var engine = new Engine(new ManualFrameSource());
        using (engine.MakeCurrent())
        {
            engine.Diagnostics.Enabled = true;
            try
            {
                Presto.Animate(new TestObject(), new() { ["PlainValue"] = 100, Composition = Composition.Additive });

                Assert.Equal("Additive", engine.Additive.Animation!.Origin);
            }
            finally
            {
                engine.Diagnostics.Enabled = false;
            }
        }
    }
}
