namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class MotionStateValidationTests
{
    private static void Validate(AnimationBuilder? motion = null, AnimationBuilder? leave = null) =>
        new MotionState { When = ":pointerover", Motion = motion, Leave = leave }.Validate();

    [Theory]
    [InlineData("+=4")]
    [InlineData("-=4")]
    [InlineData("*=2")]
    public void ARelativeValueThrows(string relative)
    {
        Assert.Throws<NotSupportedException>(() => Validate(Presto.Build().Property("X", relative)));
        Assert.Throws<NotSupportedException>(() => Validate(Presto.Build().Property("X", to: relative, duration: 100)));
        Assert.Throws<NotSupportedException>(() => Validate(Presto.Build().Keyframes(k => k.Frame(f => f.Property("X", relative)))));
    }

    [Fact]
    public void AFromWithoutAToThrows() =>
        Assert.Throws<NotSupportedException>(() => Validate(Presto.Build().Property("X", from: 10)));

    [Fact]
    public void AbsoluteAndKeyframedValuesAreAccepted() =>
        Validate(Presto.Build()
            .Property("X", from: 0, to: 10)
            .Property("Y", new double[] { 0, -4, 0 })
            .Keyframes(k => k.Frame(f => f.Property("Opacity", 0.5)).Frame(f => f.Property("Opacity", 1))));

    [Fact]
    public void ALeaveThatGivesAPropertyAValueThrows()
    {
        Assert.Throws<NotSupportedException>(() => Validate(leave: Presto.Build().Property("X", 0)));
        Assert.Throws<NotSupportedException>(() => Validate(leave: Presto.Build().Property("X", to: 0, ease: "linear")));
        Assert.Throws<NotSupportedException>(() => Validate(leave: Presto.Build().Keyframes(k => k.Frame(f => f.Property("X", 0)))));
    }

    [Fact]
    public void ALeaveWithOnlyTimingIsAccepted() =>
        Validate(leave: Presto.Build().Duration(120).Delay(20).Ease("outQuad").Property("X", ease: "linear").Property("Y", duration: 80));
}
