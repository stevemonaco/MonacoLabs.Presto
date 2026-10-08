using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class TargetOwnerTests : BridgeTestBase
{
    [AvaloniaFact]
    public void AComposedRenderTransformResolvesToItsVisual()
    {
        var box = Box();
        Seeked(box, new AvaloniaAnimationParams { ["X"] = 30, ["Rotate"] = 45 });

        var group = Assert.IsType<TransformGroup>(box.RenderTransform);
        Assert.Same(box, TargetOwner.Find(box));
        Assert.Same(box, TargetOwner.Find(group));
        Assert.All(group.Children, child => Assert.Same(box, TargetOwner.Find(child)));
    }

    [AvaloniaFact]
    public void ACodecCreatedBrushResolvesToItsVisual()
    {
        var box = Box();
        var original = box.Background;
        Seeked(box, new AvaloniaAnimationParams { ["Background"] = "#0000ff" });

        Assert.NotSame(original, box.Background);
        Assert.Same(box, TargetOwner.Find(box.Background!));
    }

    [AvaloniaFact]
    public void APlainObjectResolvesToNull() => Assert.Null(TargetOwner.Find(new object()));
}
