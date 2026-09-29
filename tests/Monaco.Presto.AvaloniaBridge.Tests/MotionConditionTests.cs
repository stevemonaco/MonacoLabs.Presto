namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class MotionConditionTests
{
    [Fact]
    public void AConditionSplitsIntoPseudoClassesClassesAndALeadingStateName()
    {
        var condition = MotionCondition.Parse("Error.selected:checked");

        Assert.Equal(["selected", ":checked"], condition.Classes);
        Assert.Equal("Error", condition.State);
    }

    [Fact]
    public void PointeroverIsTheSynthesisedHoverRatherThanAClass()
    {
        var condition = MotionCondition.Parse(".card:pointerover");

        Assert.Equal(["card"], condition.Classes);
        Assert.True(condition.Matches(["card"], [], hovered: true));
        Assert.False(condition.Matches(["card", ":pointerover"], []));
    }

    [Fact]
    public void EveryTokenMustHoldForAMatch()
    {
        var condition = MotionCondition.Parse("Error.selected:checked");
        string[] states = ["Error"];

        Assert.True(condition.Matches(["selected", ":checked", "card"], states));
        Assert.False(condition.Matches(["selected"], states));
        Assert.False(condition.Matches(["selected", ":checked"], []));
    }

    [Fact]
    public void ABareNameMatchesOnlyStateTokens()
    {
        var condition = MotionCondition.Parse("Error");

        Assert.Empty(condition.Classes);
        Assert.False(condition.Matches(["Error"], []));
        Assert.True(condition.Matches([], ["Busy", "Error"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Border.card:pointerover Path.arrow")]
    [InlineData(".card > .arrow")]
    [InlineData(":nth-child(2)")]
    [InlineData(".card:")]
    public void SelectorPartsAreRejectedWithTheStateSetterEscapeHatch(string when)
    {
        var error = Assert.Throws<ArgumentException>(() => MotionCondition.Parse(when));

        Assert.Contains("Motion.State", error.Message);
    }
}
