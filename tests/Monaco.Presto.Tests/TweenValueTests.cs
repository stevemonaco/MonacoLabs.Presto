namespace Monaco.Presto.Tests;

public sealed class TweenValueTests
{
    public static TheoryData<object?, object?> Coercions => new()
    {
        { null, 0.0 },
        { double.NaN, 0.0 },
        { 5.0, 5.0 },
        { "10", 10.0 },
        { "1e3", 1000.0 },
        { "", 0.0 },
        { "  ", 0.0 },
        { "10px", "10px" },
    };

    [Theory]
    [MemberData(nameof(Coercions))]
    public void CoerceFollowsGetFunctionValue(object? raw, object? expected) => Assert.Equal(expected, TweenValue.Of(raw).Coerce().Raw);

    [Fact]
    public void CoerceKeepsObjects()
    {
        var parameters = new TweenParams();

        Assert.Same(parameters, ((TweenValue)parameters).Coerce().Raw);
    }

    [Fact]
    public void EaseValueResolvesEachForm()
    {
        var spring = Eases.Spring();
        var target = new object();

        Assert.Same(Eases.OutQuad, ((EaseValue)Eases.OutQuad).Resolve(target, 0, 1, out var none));
        Assert.Null(none);
        Assert.Same(spring.Ease, ((EaseValue)spring).Resolve(target, 0, 1, out var resolvedSpring));
        Assert.Same(spring, resolvedSpring);
        Assert.Same(Eases.None, ((EaseValue)"linear").Resolve(target, 0, 1, out _));
        Assert.Same(EaseParser.Parse("inOutQuad"), ((EaseValue)"inOutQuad").Resolve(target, 0, 1, out _));
        Assert.Same(Eases.None, default(EaseValue).Resolve(target, 0, 1, out _));
    }

    [Fact]
    public void EaseValueFunctionsResolvePerTarget()
    {
        var spring = Eases.Spring();
        object? seenTarget = null;
        var target = new object();
        EaseValue byIndex = new FunctionValue((t, index, total) =>
        {
            seenTarget = t;
            return index == total - 1 ? spring : index == 0 ? Eases.InQuad : "linear";
        });

        Assert.Same(Eases.InQuad, byIndex.Resolve(target, 0, 3, out _));
        Assert.Same(target, seenTarget);
        Assert.Same(Eases.None, byIndex.Resolve(target, 1, 3, out _));
        Assert.Same(spring.Ease, byIndex.Resolve(target, 2, 3, out var resolvedSpring));
        Assert.Same(spring, resolvedSpring);
    }
}
