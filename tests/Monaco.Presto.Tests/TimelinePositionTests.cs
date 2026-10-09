namespace Monaco.Presto.Tests;

public sealed class TimelinePositionTests : EngineTestBase
{
    private static readonly TimelineLabel Label = new("L");
    private static readonly TimelineLabel Missing = new("missing");

    private static TimelinePosition Typed(string form) => form switch
    {
        "label" => Label,
        "label + 25" => Label + 25,
        "label - 25" => Label - 25,
        "(label + 100) - 30" => (Label + 100) - 30,
        "End" => TimelinePosition.End,
        "End + 30" => TimelinePosition.End + 30,
        "End - 30" => TimelinePosition.End - 30,
        "PreviousEnd" => TimelinePosition.PreviousEnd,
        "PreviousEnd + 20" => TimelinePosition.PreviousEnd + 20,
        "PreviousEnd - 20" => TimelinePosition.PreviousEnd - 20,
        "PreviousStart" => TimelinePosition.PreviousStart,
        "PreviousStart + 20" => TimelinePosition.PreviousStart + 20,
        "PreviousStart - 20" => TimelinePosition.PreviousStart - 20,
        "50 + 25" => (TimelinePosition)50 + 25,
        "(50 - 25) + 5" => ((TimelinePosition)50 - 25) + 5,
        "\"L\" + 10" => (TimelinePosition)"L" + 10,
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static double ProbeOffset(TimelinePosition position)
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 }, Autoplay = false })
            .Add(target, new() { ["TranslateX"] = 100 })
            .Label("L", 40)
            .Add(target, new() { ["TranslateY"] = 100, Duration = 50 }, 100)
            .Add(target, new() { ["Rotate"] = 100 }, position);
        return TestHelpers.OffsetOf(tl, 2);
    }

    [Theory]
    [InlineData("label", "L")]
    [InlineData("label + 25", "L+=25")]
    [InlineData("label - 25", "L-=25")]
    [InlineData("(label + 100) - 30", "L+=70")]
    [InlineData("End", "+=0")]
    [InlineData("End + 30", "+=30")]
    [InlineData("End - 30", "-=30")]
    [InlineData("PreviousEnd", "<")]
    [InlineData("PreviousEnd + 20", "<+=20")]
    [InlineData("PreviousEnd - 20", "<-=20")]
    [InlineData("PreviousStart", "<<")]
    [InlineData("PreviousStart + 20", "<<+=20")]
    [InlineData("PreviousStart - 20", "<<-=20")]
    [InlineData("50 + 25", "75")]
    [InlineData("(50 - 25) + 5", "30")]
    [InlineData("\"L\" + 10", "L+=10")]
    public void TypedPositionsResolveAsTheirStringTwins(string typed, string twin) =>
        Assert.Equal(ProbeOffset(twin), ProbeOffset(Typed(typed)));

    [Fact]
    public void UnknownLabelsResolveToTheTimelineEnd()
    {
        Assert.Equal(150, ProbeOffset(Missing));
        Assert.Equal(160, ProbeOffset(Missing + 10));
    }
}
