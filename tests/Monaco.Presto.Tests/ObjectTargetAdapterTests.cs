using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.Tests;

/// <summary>The built-in fallback adapter: typed members, dictionary entries, codecs, revert and accessor caching.</summary>
public sealed class ObjectTargetAdapterTests : EngineTestBase
{
    private readonly Timer _parent;
    private readonly Poco _target = new();

    public ObjectTargetAdapterTests() => _parent = Presto.CreateTimer(new() { Autoplay = false });

    [Fact]
    public void WritesNumericMembersInTheirOwnType()
    {
        var opacity = CreateTween("Opacity");
        var scale = CreateTween("Scale");
        var x = CreateTween("X");

        opacity.Accessor.SetValue(_target, .25, opacity);
        scale.Accessor.SetValue(_target, 1.5, scale);
        x.Accessor.SetValue(_target, 12.5, x);

        Assert.Equal(.25, _target.Opacity);
        Assert.Equal(1.5f, _target.Scale);
        Assert.Equal(12.5, _target.X);
    }

    [Theory]
    [InlineData(41.6, 42)]
    [InlineData(1.5, 2)]
    [InlineData(-1.5, -1)]
    public void RoundsIntegralMembersHalfUp(double number, int expected)
    {
        var count = CreateTween("Count");

        count.Accessor.SetValue(_target, number, count);

        Assert.Equal(expected, _target.Count);
    }

    [Fact]
    public void ReadsMembersAndTreatsMissingDictionaryKeysAsNull()
    {
        var dictionary = new Dictionary<string, object?> { ["x"] = 10.0 };
        _target.Opacity = .5;

        Assert.Equal(.5, Presto.ResolveAccessor(_target, "Opacity").GetValue(_target));
        Assert.Equal(10.0, Presto.ResolveAccessor(dictionary, "x").GetValue(dictionary));
        Assert.Null(Presto.ResolveAccessor(dictionary, "y").GetValue(dictionary));
    }

    [Fact]
    public void WritesUnitValuesToStringMembers()
    {
        var size = CreateTween("Size", ValueKind.Unit);
        size._unit = "px";

        size.Accessor.SetValue(_target, 42, size);

        Assert.Equal("42px", _target.Size);
    }

    [Fact]
    public void WritesColorChannelsToColorAndStringMembers()
    {
        var tint = CreateTween("Tint", ValueKind.Color);
        var fill = CreateTween("Fill", ValueKind.Color);
        tint._numbers = [255, 0, 0, 1];
        fill._numbers = [0, 128, 255, .5];

        tint.Accessor.SetValue(_target, double.NaN, tint);
        fill.Accessor.SetValue(_target, double.NaN, fill);

        Assert.Equal(new PrestoColor(255, 0, 0, 1), _target.Tint);
        Assert.Equal("rgba(0,128,255,0.5)", _target.Fill);
    }

    [Fact]
    public void WritesComplexValuesToStringMembers()
    {
        var filter = CreateTween("Filter", ValueKind.Complex);
        filter._numbers = [10, 200];
        filter._strings = ["blur(", "px) contrast(", ")"];

        filter.Accessor.SetValue(_target, double.NaN, filter);

        Assert.Equal("blur(10px) contrast(200)", _target.Filter);
    }

    [Fact]
    public void ObjectMembersHoldNumbersAndComposedStrings()
    {
        var number = CreateTween("Value");
        var unit = CreateTween("Value", ValueKind.Unit);
        unit._unit = "%";

        number.Accessor.SetValue(_target, 12.5, number);
        Assert.Equal(12.5, _target.Value);

        unit.Accessor.SetValue(_target, 40, unit);
        Assert.Equal("40%", _target.Value);
    }

    [Fact]
    public void DictionaryEntriesHoldNumbersAndComposedStrings()
    {
        var dictionary = new Dictionary<string, object?>();
        var x = CreateTween("x", target: dictionary);
        var filter = CreateTween("filter", ValueKind.Complex, dictionary);
        filter._numbers = [10];
        filter._strings = ["blur(", "px)"];

        x.Accessor.SetValue(dictionary, 3, x);
        filter.Accessor.SetValue(dictionary, double.NaN, filter);

        Assert.Equal(3.0, dictionary["x"]);
        Assert.Equal("blur(10px)", dictionary["filter"]);
    }

    [Fact]
    public void RevertRestoresTheValueReadBeforeTheAnimation()
    {
        var dictionary = new Dictionary<string, object?> { ["x"] = 10.0 };
        _target.Opacity = .25;
        _target.Size = "10px";
        var opacity = CreateTween("Opacity");
        var size = CreateTween("Size", ValueKind.Unit);
        var x = CreateTween("x", target: dictionary);
        size._unit = "px";
        var originalOpacity = opacity.Accessor.GetValue(_target);
        var originalSize = size.Accessor.GetValue(_target);
        var originalX = x.Accessor.GetValue(dictionary);

        opacity.Accessor.SetValue(_target, 1, opacity);
        size.Accessor.SetValue(_target, 40, size);
        x.Accessor.SetValue(dictionary, 40, x);
        opacity.Accessor.Revert(_target, originalOpacity);
        size.Accessor.Revert(_target, originalSize);
        x.Accessor.Revert(dictionary, originalX);

        Assert.Equal(.25, _target.Opacity);
        Assert.Equal("10px", _target.Size);
        Assert.Equal(10.0, dictionary["x"]);
    }

    [Fact]
    public void UnknownMemberThrows() => Assert.Throws<ArgumentException>(() => Presto.ResolveAccessor(_target, "Nope"));

    [Fact]
    public void MemberTypeWithoutCodecIsDiscrete() => Assert.IsType<DiscreteCodec>(Presto.ResolveAccessor(_target, "Elapsed").Codec);

    [Fact]
    public void RegisteredCodecDecodesAndEncodesItsType()
    {
        Presto.Codecs[typeof(Meters)] = new MetersCodec();
        _target.Distance = new Meters(3);
        var distance = CreateTween("Distance");
        var decomposed = new DecomposedValue();

        ValueParser.Decompose(distance.Accessor.GetValue(_target), decomposed, distance.Accessor.Codec);
        distance.Accessor.SetValue(_target, 7, distance);

        Assert.Equal(3, decomposed.Number);
        Assert.Equal(new Meters(7), _target.Distance);
    }

    [Fact]
    public void AccessorsAreCachedPerTypeAndProperty()
    {
        Assert.Same(Presto.ResolveAccessor(_target, "Opacity"), Presto.ResolveAccessor(new Poco(), "Opacity"));
        Assert.NotSame(Presto.ResolveAccessor(_target, "Opacity"), Presto.ResolveAccessor(_target, "Count"));
    }

    [Fact]
    public void RegisteredAdaptersResolveBeforeTheObjectFallback()
    {
        var adapter = new OpacityAdapter();
        Presto.Adapters.Add(adapter);
        try
        {
            Assert.Same(adapter, Presto.ResolveAccessor(_target, "Opacity"));
            Assert.NotSame(adapter, Presto.ResolveAccessor(_target, "Count"));
        }
        finally
        {
            Presto.Adapters.Remove(adapter);
        }
    }

    private Tween CreateTween(string property, ValueKind kind = ValueKind.Number, object? target = null)
    {
        target ??= _target;
        return new Tween(_parent, property, target, Presto.ResolveAccessor(target, property)) { _valueType = kind };
    }

    private sealed class Poco
    {
        public double X = 1;
        public double Opacity { get; set; }
        public float Scale { get; set; }
        public int Count { get; set; }
        public string Size { get; set; } = "";
        public string Fill { get; set; } = "";
        public string Filter { get; set; } = "";
        public PrestoColor Tint { get; set; }
        public object? Value { get; set; }
        public TimeSpan Elapsed { get; set; }
        public Meters Distance { get; set; }
    }

    private readonly record struct Meters(double Value);

    private sealed class MetersCodec : IValueCodec
    {
        public bool TryDecode(object value, DecomposedValue into)
        {
            if (value is not Meters meters) return false;
            into.Number = meters.Value;
            return true;
        }

        public object Encode(double number, Tween tween) => new Meters(number);
    }

    private sealed class OpacityAdapter : ITargetAdapter, IPropertyAccessor
    {
        public IValueCodec? Codec => null;

        public IPropertyAccessor? Resolve(object target, string property) => target is Poco && property == "Opacity" ? this : null;

        public object? GetValue(object target) => ((Poco)target).Opacity;

        public void SetValue(object target, double number, Tween tween) => ((Poco)target).Opacity = number;

        public void Revert(object target, object? originalValue) => ((Poco)target).Opacity = (double)originalValue!;
    }
}
