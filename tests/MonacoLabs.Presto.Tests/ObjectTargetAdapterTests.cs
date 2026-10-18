using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

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
    public void RevertRemovesADictionaryKeyTheAnimationAdded()
    {
        var dictionary = new Dictionary<string, object?>();
        var animation = Presto.Animate(dictionary, new() { ["x"] = 100, Duration = 100, Autoplay = false });

        animation.Seek(100);
        Assert.Equal(100.0, dictionary["x"]);
        animation.Revert();

        Assert.False(dictionary.ContainsKey("x"));
    }

    [Fact]
    public void RevertRestoresANullMember()
    {
        _target.Size = null!;
        var animation = Presto.Animate(_target, new() { ["Size"] = "40px", Duration = 100, Autoplay = false });

        animation.Seek(100);
        Assert.Equal("40px", _target.Size);
        animation.Revert();

        Assert.Null(_target.Size);
    }

    [Fact]
    public void UnknownMemberThrows()
    {
        var thrown = Assert.Throws<ArgumentException>(() => Presto.ResolveAccessor(_target, "Nope"));

        Assert.Contains("trimmed or NativeAOT", thrown.Message);
        Assert.Contains("Presto.RegisterTarget<T>()", thrown.Message);
    }

    [Fact]
    public void RegisteringATargetChangesNoResolution()
    {
        var before = Presto.ResolveAccessor(_target, "Opacity");

        Presto.RegisterTarget<Poco>();

        Assert.Same(before, Presto.ResolveAccessor(_target, "Opacity"));
        Assert.Throws<ArgumentException>(() => Presto.ResolveAccessor(_target, "Nope"));
    }

#if DEBUG
    [Fact]
    public void ReflectingOnALibraryTypeFailsInDebug()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Presto.ResolveAccessor(new TimerParams(), nameof(TimerParams.Duration)));

        Assert.Contains(nameof(IMemberTarget), thrown.Message);
    }
#endif

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
    public void ACodecRegisteredAfterAResolutionAppliesToLaterAnimations()
    {
        Assert.IsType<DiscreteCodec>(Presto.ResolveAccessor(_target, "Height").Codec);
        Presto.Codecs[typeof(Feet)] = new StructCodec<Feet>(feet => [feet.Value], channels => new Feet(channels[0]));
        try
        {
            var animation = Presto.Animate(_target, new()
            {
                ["Height"] = TweenValue.Of(new Feet(10)),
                Duration = 100,
                Ease = Eases.Linear(),
                Autoplay = false,
            });

            animation.Seek(50);

            Assert.Equal(new Feet(5), _target.Height);
        }
        finally
        {
            Presto.Codecs.Remove(typeof(Feet));
        }
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

    [Fact]
    public void TypedMembersWriteLikeReflectedOnes()
    {
        Presto.Codecs[typeof(Meters)] = new MetersCodec();
        try
        {
            _target.Size = "10px";
            var typed = new TypedPoco { Size = "10px" };

            AnimatesAlike(typed, "Opacity", 1);
            AnimatesAlike(typed, "Count", 7.5);
            AnimatesAlike(typed, "Size", "40px");
            AnimatesAlike(typed, "Tint", "#ff0000");
            AnimatesAlike(typed, "Value", 10);
            AnimatesAlike(typed, "Distance", TweenValue.Of(new Meters(10)));
            AnimatesAlike(typed, "Elapsed", TweenValue.Of(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            Presto.Codecs.Remove(typeof(Meters));
        }
    }

    [Fact]
    public void ACodecRegisteredAfterAMemberTableIsBuiltAppliesToItsEntries()
    {
        var typed = new TypedPoco();
        Assert.IsType<DiscreteCodec>(Presto.ResolveAccessor(typed, "Height").Codec);
        var codec = new StructCodec<Feet>(feet => [feet.Value], channels => new Feet(channels[0]));
        Presto.Codecs[typeof(Feet)] = codec;
        try
        {
            Assert.Same(codec, Presto.ResolveAccessor(typed, "Height").Codec);
        }
        finally
        {
            Presto.Codecs.Remove(typeof(Feet));
        }
    }

    [Theory]
    [InlineData("CurrentTime", 50, 50)]
    [InlineData("IterationCurrentTime", 25, 25)]
    [InlineData("Progress", .75, 150)]
    [InlineData("IterationProgress", .5, 50)]
    [InlineData("CurrentIteration", 1.4, 100)]
    public void TimerMembersResolveWithoutReflection(string member, double value, double currentTime)
    {
        var timer = Presto.CreateTimer(new() { Duration = 100, Loop = 1, Autoplay = false });
        var accessor = Presto.ResolveAccessor(timer, member);

        accessor.SetValue(timer, value, new Tween(_parent, member, timer, accessor));

        Assert.Equal(currentTime, timer.CurrentTime);
    }

    [Fact]
    public void UnknownTimerMemberThrows()
    {
        var timer = Presto.CreateTimer(new() { Autoplay = false });

        Assert.Throws<ArgumentException>(() => Presto.ResolveAccessor(timer, "Reversed"));
    }

    private void AnimatesAlike(TypedPoco typed, string property, TweenValue to)
    {
        var reflected = Animate(_target, property, to);
        var tabled = Animate(typed, property, to);

        reflected.Seek(60);
        tabled.Seek(60);
        Assert.Equal(Presto.ResolveAccessor(_target, property).GetValue(_target), Presto.ResolveAccessor(typed, property).GetValue(typed));

        reflected.Revert();
        tabled.Revert();
        Assert.Equal(Presto.ResolveAccessor(_target, property).GetValue(_target), Presto.ResolveAccessor(typed, property).GetValue(typed));
    }

    private static Animation Animate(object target, string property, TweenValue to) =>
        Presto.Animate(target, new() { [property] = to, Duration = 100, Ease = Eases.Linear(), Autoplay = false });
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
        public Feet Height { get; set; }
    }

    private sealed class TypedPoco : IMemberTarget
    {
        private static readonly Dictionary<string, IPropertyAccessor> s_members = new()
        {
            ["Opacity"] = ObjectTargetAdapter.MemberAccessor.Of<TypedPoco, double>(t => t.Opacity, (t, v) => t.Opacity = v),
            ["Count"] = ObjectTargetAdapter.MemberAccessor.Of<TypedPoco, int>(t => t.Count, (t, v) => t.Count = v),
            ["Size"] = ObjectTargetAdapter.MemberAccessor.Of<TypedPoco, string>(t => t.Size, (t, v) => t.Size = v),
            ["Tint"] = ObjectTargetAdapter.MemberAccessor.Of<TypedPoco, PrestoColor>(t => t.Tint, (t, v) => t.Tint = v),
            ["Value"] = ObjectTargetAdapter.MemberAccessor.Of<TypedPoco, object?>(t => t.Value, (t, v) => t.Value = v),
            ["Elapsed"] = ObjectTargetAdapter.MemberAccessor.Of<TypedPoco, TimeSpan>(t => t.Elapsed, (t, v) => t.Elapsed = v),
            ["Distance"] = ObjectTargetAdapter.MemberAccessor.Of<TypedPoco, Meters>(t => t.Distance, (t, v) => t.Distance = v),
            ["Height"] = ObjectTargetAdapter.MemberAccessor.Of<TypedPoco, Feet>(t => t.Height, (t, v) => t.Height = v),
        };

        public double Opacity { get; set; }
        public int Count { get; set; }
        public string Size { get; set; } = "";
        public PrestoColor Tint { get; set; }
        public object? Value { get; set; }
        public TimeSpan Elapsed { get; set; }
        public Meters Distance { get; set; }
        public Feet Height { get; set; }

        IReadOnlyDictionary<string, IPropertyAccessor> IMemberTarget.Members => s_members;
    }
    private readonly record struct Meters(double Value);

    private readonly record struct Feet(double Value);

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
