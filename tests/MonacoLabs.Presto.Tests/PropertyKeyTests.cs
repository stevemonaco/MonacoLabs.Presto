namespace MonacoLabs.Presto.Tests;

public sealed class PropertyKeyTests : EngineTestBase
{
    [Fact]
    public void ConversionWrapsTheString()
    {
        PropertyKey key = "X";

        Assert.Equal("X", key.Name);
        Assert.Equal("X", key.ToString());
    }

    [Fact]
    public void NullThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => { PropertyKey _ = (string)null!; });
        Assert.Throws<ArgumentNullException>(() => new PropertyKey(null!));
    }

    [Fact]
    public void KeysCompareByOrdinalName()
    {
        Assert.True(new PropertyKey("X") == "X");
        Assert.Equal(new PropertyKey("X").GetHashCode(), ((PropertyKey)"X").GetHashCode());
        Assert.True(new PropertyKey("X") != "x");
    }

    [Fact]
    public void DefaultKeyThrowsArgumentException()
    {
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new() { ["X"] = 0 });

        Assert.Throws<ArgumentException>(() => new AnimationParams { [default] = 1 });
        Assert.Throws<ArgumentException>(() => Presto.Build().Property(default, 1));
        Assert.Throws<ArgumentException>(() => animatable.Get(default));
    }
}
