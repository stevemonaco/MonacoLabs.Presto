using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge.Text;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Covers <c>PrestoAvalonia.ScrambleText</c>: the <c>scrambleText</c> half of tests/suites/text.test.js, over a <see cref="TextBlock"/>'s <c>Text</c>.</summary>
public sealed class ScrambleTextTests : BridgeTestBase
{
    private const string Sentence = "Hello world";

    private static TextBlock Block() => new() { Text = Sentence };

    private static TweenParams Scramble(ScrambleTextParams? parameters = null) =>
        Assert.IsType<TweenParams>(PrestoAvalonia.ScrambleText(parameters)(Block(), 0, 1, null).Raw);

    [AvaloniaFact]
    public void IsAFunctionBasedValue()
    {
        var parameters = new AvaloniaAnimationParams { ["Text"] = PrestoAvalonia.ScrambleText() };

        Assert.IsType<ChainedFunctionValue>(parameters["Text"].Raw);
    }

    [AvaloniaFact]
    public void ReturnsATweenWithFromToEaseDurationAndModifier()
    {
        var tween = Scramble();

        Assert.Equal(0d, tween.From!.Value.AsDouble);
        Assert.Equal(1d, tween.To!.Value.AsDouble);
        Assert.Same(Eases.None, tween.Ease!.Value.Raw);
        Assert.True(tween.Duration!.Value.AsDouble > 0);
        Assert.NotNull(tween.TextModifier);
    }

    [AvaloniaFact]
    public void DefaultOverrideScramblesTheTextAtTheStart()
    {
        var tween = Scramble(new() { Seed = 1 });

        Assert.NotEqual(Sentence, tween.TextModifier!(0));
    }

    [AvaloniaFact]
    public void OverrideFalsePreservesTheOriginalTextAtTheStart()
    {
        var tween = Scramble(new() { Override = false });

        Assert.Equal(Sentence, tween.TextModifier!(0));
    }

    [AvaloniaFact]
    public void ModifierReturnsTheTargetTextAtOne()
    {
        var tween = Scramble();

        Assert.Equal(Sentence, tween.TextModifier!(1));
    }

    [AvaloniaFact]
    public void ModifierReturnsScrambledTextInBetween()
    {
        var tween = Scramble(new() { Seed = 1 });

        var scrambled = tween.TextModifier!(.5);

        Assert.Equal(Sentence.Length, scrambled.Length);
        Assert.NotEqual(Sentence, scrambled);
    }

    [AvaloniaFact]
    public void ModifierPreservesWhitespace()
    {
        var tween = Scramble(new() { Seed = 1 });

        Assert.Equal(' ', tween.TextModifier!(.125)[Sentence.IndexOf(' ')]);
    }

    [AvaloniaFact]
    public void ModifierCachesTheValueOfTheSameInput()
    {
        var tween = Scramble(new() { Seed = 1 });

        Assert.Same(tween.TextModifier!(.25), tween.TextModifier(.25));
    }

    [AvaloniaFact]
    public void ScramblesWithTheCustomCharacters()
    {
        var tween = Scramble(new() { Chars = "XY", Seed = 1 });

        var scrambled = tween.TextModifier!(.5);

        for (var i = 0; i < scrambled.Length; i++)
        {
            if (scrambled[i] != ' ' && scrambled[i] != Sentence[i]) Assert.Contains(scrambled[i], "XY");
        }
    }

    [AvaloniaFact]
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

    [AvaloniaFact]
    public void SettlesOnTheCustomText()
    {
        var tween = Scramble(new() { Text = "Goodbye" });

        Assert.Equal("Goodbye", tween.TextModifier!(1));
    }

    [AvaloniaFact]
    public void AnimateEndsOnTheTargetText()
    {
        var block = Block();

        var animation = block.Animate(new() { ["Text"] = PrestoAvalonia.ScrambleText(new() { Text = "Goodbye", Duration = 100, Seed = 1 }) });
        Frames.Advance(50, 16);
        var midway = block.Text;
        RunUntil(() => animation.Completed);

        Assert.NotEqual("Goodbye", midway);
        Assert.Equal("Goodbye", block.Text);
    }

    [AvaloniaFact]
    public void SettlesOnThePreviousScramblesTextWhenChained()
    {
        var block = Block();

        var timeline = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(block, new AvaloniaAnimationParams { ["Text"] = PrestoAvalonia.ScrambleText(new() { Text = "Goodbye", Duration = 100 }) })
            .Add(block, new AvaloniaAnimationParams { ["Text"] = PrestoAvalonia.ScrambleText(new() { Duration = 100 }) });

        timeline.Seek(timeline.Duration);

        Assert.Equal("Goodbye", block.Text);
    }

    /// <summary>Upstream "scrambleText keyframe array reads prevTween value between keyframes".</summary>
    [AvaloniaFact]
    public void KeyframesStartFromThePreviousFramesText()
    {
        var block = Block();

        var animation = block.Animate(new()
        {
            ["Text"] = TweenValue.Keyframes(
                new TweenParams { To = PrestoAvalonia.ScrambleText(new() { Text = "First", Duration = 100 }) },
                new TweenParams { To = PrestoAvalonia.ScrambleText(new() { Text = "Second", Duration = 100 }) }),
            Autoplay = false,
        });

        animation.Seek(100);
        Assert.Equal("First", block.Text);

        animation.Seek(animation.Duration);
        Assert.Equal("Second", block.Text);
    }

    [AvaloniaFact]
    public void RevertRestoresTheOriginalText()
    {
        var block = Block();

        var animation = block.Animate(new() { ["Text"] = PrestoAvalonia.ScrambleText(new() { Text = "Goodbye", Duration = 200, Seed = 1 }) });
        Frames.Advance(100, 16);
        Assert.NotEqual(Sentence, block.Text);

        animation.Revert();

        Assert.Equal(Sentence, block.Text);
    }
}
