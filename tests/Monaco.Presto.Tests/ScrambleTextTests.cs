namespace Monaco.Presto.Tests;

/// <summary>Covers <see cref="Presto.ScrambleText"/>: the <c>scrambleText</c> half of tests/suites/text.test.js, over a plain object's <c>Text</c>.</summary>
public sealed class ScrambleTextTests : EngineTestBase
{
    private const string Sentence = "Hello world";

    private static TweenParams Scramble(ScrambleTextParams? parameters = null) =>
        Assert.IsType<TweenParams>(Presto.ScrambleText(parameters)(new Label(), 0, 1, null).Raw);

    [Fact]
    public void IsAFunctionBasedValue()
    {
        var parameters = new AnimationParams { ["Text"] = Presto.ScrambleText() };

        Assert.IsType<ChainedFunctionValue>(parameters["Text"].Raw);
    }

    [Fact]
    public void ReturnsATweenWithFromToEaseDurationAndModifier()
    {
        var tween = Scramble();

        Assert.Equal(0d, tween.From!.Value.AsDouble);
        Assert.Equal(1d, tween.To!.Value.AsDouble);
        Assert.Same(Eases.None, tween.Ease!.Value.Raw);
        Assert.True(tween.Duration!.Value.AsDouble > 0);
        Assert.NotNull(tween.TextModifier);
    }

    [Fact]
    public void DefaultOverrideScramblesTheTextAtTheStart()
    {
        var tween = Scramble(new() { Seed = 1 });

        Assert.NotEqual(Sentence, tween.TextModifier!(0));
    }

    [Fact]
    public void OverrideFalsePreservesTheOriginalTextAtTheStart()
    {
        var tween = Scramble(new() { Override = false });

        Assert.Equal(Sentence, tween.TextModifier!(0));
    }

    [Fact]
    public void ModifierReturnsTheTargetTextAtOne()
    {
        var tween = Scramble();

        Assert.Equal(Sentence, tween.TextModifier!(1));
    }

    [Fact]
    public void ModifierReturnsScrambledTextInBetween()
    {
        var tween = Scramble(new() { Seed = 1 });

        var scrambled = tween.TextModifier!(.5);

        Assert.Equal(Sentence.Length, scrambled.Length);
        Assert.NotEqual(Sentence, scrambled);
    }

    [Fact]
    public void ModifierPreservesWhitespace()
    {
        var tween = Scramble(new() { Seed = 1 });

        Assert.Equal(' ', tween.TextModifier!(.125)[Sentence.IndexOf(' ')]);
    }

    [Fact]
    public void ModifierCachesTheValueOfTheSameInput()
    {
        var tween = Scramble(new() { Seed = 1 });

        Assert.Same(tween.TextModifier!(.25), tween.TextModifier(.25));
    }

    [Fact]
    public void ScramblesWithTheCustomCharacters()
    {
        var tween = Scramble(new() { Chars = "XY", Seed = 1 });

        var scrambled = tween.TextModifier!(.5);

        for (var i = 0; i < scrambled.Length; i++)
        {
            if (scrambled[i] != ' ' && scrambled[i] != Sentence[i]) Assert.Contains(scrambled[i], "XY");
        }
    }

    [Fact]
    public void RevealsMoreCharactersAsItProgresses()
    {
        var tween = Scramble(new() { Seed = 1 });

        Assert.True(Revealed(tween.TextModifier!(.75)) > Revealed(tween.TextModifier(.25)));

        static int Revealed(string scrambled)
        {
            var revealed = 0;
            for (var i = 0; i < Sentence.Length; i++)
            {
                if (scrambled[i] == Sentence[i] && Sentence[i] != ' ') revealed++;
            }
            return revealed;
        }
    }

    [Fact]
    public void SettlesOnTheCustomText()
    {
        var tween = Scramble(new() { Text = "Goodbye" });

        Assert.Equal("Goodbye", tween.TextModifier!(1));
    }

    [Fact]
    public void AnimateEndsOnTheTargetText()
    {
        var label = new Label();

        var animation = Presto.Animate(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Text = "Goodbye", Duration = 100, Seed = 1 }) });
        Frames.Advance(50, 16);
        var midway = label.Text;
        RunUntil(() => animation.Completed);

        Assert.NotEqual("Goodbye", midway);
        Assert.Equal("Goodbye", label.Text);
    }

    [Fact]
    public void SettlesOnThePreviousScramblesTextWhenChained()
    {
        var label = new Label();

        var timeline = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Text = "Goodbye", Duration = 100 }) })
            .Add(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Duration = 100 }) });

        timeline.Seek(timeline.Duration);

        Assert.Equal("Goodbye", label.Text);
    }

    /// <summary>Upstream "scrambleText keyframe array reads prevTween value between keyframes".</summary>
    [Fact]
    public void KeyframesStartFromThePreviousFramesText()
    {
        var label = new Label();

        var animation = Presto.Animate(label, new AnimationParams
        {
            ["Text"] = TweenValue.Keyframes(
                new TweenParams { To = Presto.ScrambleText(new() { Text = "First", Duration = 100 }) },
                new TweenParams { To = Presto.ScrambleText(new() { Text = "Second", Duration = 100 }) }),
            Autoplay = false,
        });

        animation.Seek(100);
        Assert.Equal("First", label.Text);

        animation.Seek(animation.Duration);
        Assert.Equal("Second", label.Text);
    }

    [Fact]
    public void RevertRestoresTheOriginalText()
    {
        var label = new Label();

        var animation = Presto.Animate(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Text = "Goodbye", Duration = 200, Seed = 1 }) });
        Frames.Advance(100, 16);
        Assert.NotEqual(Sentence, label.Text);

        animation.Revert();

        Assert.Equal(Sentence, label.Text);
    }

    private sealed class Label
    {
        public string Text { get; set; } = Sentence;
    }
}
