using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Tests;

/// <summary>The object half of targets.test.js; the selector and node list cases have no core equivalent.</summary>
public sealed class TargetsTests : EngineTestBase
{
    private readonly object _first = new();
    private readonly object _second = new();
    private readonly object _third = new();

    [Fact]
    public void SingleObjectIsOneTarget() => Assert.Equal([_first], TargetParser.Parse(_first));

    [Fact]
    public void StringIsOneTarget() => Assert.Equal(["#target-id"], TargetParser.Parse("#target-id"));

    [Fact]
    public void DictionaryIsOneTarget()
    {
        var target = new Dictionary<string, object?> { ["x"] = 0 };

        Assert.Equal([target], TargetParser.Parse(target));
    }

    [Fact]
    public void NestedArraysAreFlattened() =>
        Assert.Equal([_first, _second, _third], TargetParser.Parse(new object[] { _first, new object[] { _second, new[] { _third } } }));

    [Fact]
    public void NullEntriesAreSkipped() => Assert.Equal([_first, _second], TargetParser.Parse(new[] { null, _first, null, _second }));

    [Fact]
    public void DuplicatesKeepTheirFirstPosition() =>
        Assert.Equal([_first, _second], TargetParser.Parse(new object[] { _first, _second, _first, new[] { _second } }));

    [Theory]
    [InlineData(5)]
    [InlineData(100)]
    public void DuplicatesKeepTheirFirstPositionAtAnySize(int count)
    {
        var targets = Enumerable.Range(0, count).Select(_ => new object()).ToArray();
        var input = targets.Select((target, i) => new[] { target, targets[i / 2] }).ToArray();

        Assert.Equal(targets, TargetParser.Parse(input));
    }

    [Fact]
    public void NullTargetsAreEmpty() => Assert.Empty(TargetParser.Parse(null));

    [Fact]
    public void AnimatingASingleObjectBuildsOneTween()
    {
        var target = new TestObject();
        var animation = Presto.Animate(target, new() { ["PlainValue"] = 200, Duration = 100, Autoplay = false });

        Assert.Equal(1, TestHelpers.GetTweenCount(animation));
        Assert.Equal([target], animation.Targets);
    }

    [Fact]
    public void AnimatingAnArrayBuildsOneTweenPerTarget()
    {
        var first = new TestObject();
        var second = new TestObject();
        var animation = Presto.Animate(new[] { first, second }, new() { ["PlainValue"] = 200, Duration = 100, Autoplay = false });

        Assert.Equal(2, TestHelpers.GetTweenCount(animation));
        Assert.Equal([first, second], animation.Targets);
    }

    [Fact]
    public void AnimatingNestedArraysFlattensThem()
    {
        var first = new TestObject();
        var second = new TestObject();
        var third = new TestObject();
        var animation = Presto.Animate(
            new object[] { first, new object[] { second, new[] { third } } },
            new() { ["PlainValue"] = 200, Duration = 100, Autoplay = false });

        Assert.Equal(3, TestHelpers.GetTweenCount(animation));
        Assert.Equal([first, second, third], animation.Targets);
    }

    [Fact]
    public void AnimatingNullHasNoTargets() => Assert.Empty(Presto.Animate(null, new() { Duration = 10 }).Targets);

    [Fact]
    public void AnimateRejectsNullParamsAndBuilder()
    {
        Assert.Equal("parameters", Assert.Throws<ArgumentNullException>(() => Presto.Animate(_first, (AnimationParams)null!)).ParamName);
        Assert.Equal("build", Assert.Throws<ArgumentNullException>(() => Presto.Animate(_first, (Action<AnimationBuilder>)null!)).ParamName);
    }

    [Fact]
    public void SetRejectsNullParamsAndBuilder()
    {
        Assert.Equal("parameters", Assert.Throws<ArgumentNullException>(() => Presto.Set(_first, (AnimationParams)null!)).ParamName);
        Assert.Equal("build", Assert.Throws<ArgumentNullException>(() => Presto.Set(_first, (Action<AnimationBuilder>)null!)).ParamName);
    }

    [Fact]
    public void CreateAnimatableRejectsNullParams() =>
        Assert.Equal("parameters", Assert.Throws<ArgumentNullException>(() => Presto.CreateAnimatable(_first, null!)).ParamName);

    [Fact]
    public void GetRejectsANullUnit() =>
        Assert.Equal("unit", Assert.Throws<ArgumentNullException>(() => Presto.Get(_first, "x", (string)null!)).ParamName);

    [Fact]
    public void GetAnimationsRejectsANullTarget() =>
        Assert.Equal("target", Assert.Throws<ArgumentNullException>(() => Presto.GetAnimations(null!)).ParamName);

    [Fact]
    public void TheAccessorIsResolvedByCanonicalNameOncePerTargetAndProperty()
    {
        var first = new StyleTarget();
        var second = new StyleTarget();
        var adapter = new ResolveCountingAdapter();
        Presto.Adapters.Add(adapter);
        try
        {
            var animation = Presto.Animate(new[] { first, second }, new() { ["Alias"] = new double[] { 10, 20, 30 }, Autoplay = false });

            Assert.Equal([(first, "X"), (second, "X")], adapter.Resolved);
            foreach (var tween in animation.Tweens) Assert.Equal("X", tween.Property);
        }
        finally
        {
            Presto.Adapters.Remove(adapter);
        }
    }

    [Fact]
    public void RemovingEverythingTellsTheAdapters()
    {
        var target = new TestObject();
        var adapter = new RecordingAdapter();
        Presto.Adapters.Add(adapter);
        try
        {
            Presto.Animate(target, new() { ["PlainValue"] = 200, Duration = 100 });

            Presto.Remove(target, property: "PlainValue");

            var (targets, property) = Assert.Single(adapter.Calls);
            Assert.Equal([target], targets);
            Assert.Equal("PlainValue", property);
        }
        finally
        {
            Presto.Adapters.Remove(adapter);
        }
    }

    [Fact]
    public void RemovingOneRenderableLeavesTheAdaptersAlone()
    {
        var target = new TestObject();
        var adapter = new RecordingAdapter();
        Presto.Adapters.Add(adapter);
        try
        {
            var animation = Presto.Animate(target, new() { ["PlainValue"] = 200, Duration = 100 });

            Presto.Remove(target, animation);

            Assert.Empty(adapter.Calls);
        }
        finally
        {
            Presto.Adapters.Remove(adapter);
        }
    }

    private sealed class RecordingAdapter : ITargetAdapter
    {
        public List<(IReadOnlyList<object> Targets, string? Property)> Calls { get; } = [];

        public IPropertyAccessor? Resolve(object target, string property) => null;

        public void Removed(IReadOnlyList<object> targets, string? property) => Calls.Add((targets, property));
    }

    private sealed class ResolveCountingAdapter : ITargetAdapter
    {
        public List<(object Target, string Property)> Resolved { get; } = [];

        public IPropertyAccessor? Resolve(object target, string property)
        {
            Resolved.Add((target, property));
            return null;
        }

        public string? Canonicalize(object target, string property) => property == "Alias" ? "X" : null;
    }
}
