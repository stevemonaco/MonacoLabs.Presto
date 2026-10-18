namespace MonacoLabs.Presto.Tests;

/// <summary>Covers <see cref="Presto.ScrambleText"/>: the <c>scrambleText</c> half of tests/suites/text.test.js, over a plain object's <c>Text</c>.</summary>
public sealed class ScrambleTextTests : EngineTestBase
{
    private const string Sentence = "Hello world";

    private const string Lower = "abcdefghij";
    private const string Upper = "ABCDEFGHIJ";

    private static readonly ScrambleTextParams Ordered = new() { Text = Upper, Chars = "*", Duration = 1000, SettleDuration = 10, Seed = 1 };

    private static readonly ScrambleTextParams Windowed = new() { Text = Upper, Chars = "*", OverrideChars = "#", Duration = 1000, SettleDuration = 500, Seed = 1 };

    private static TweenParams Scramble(ScrambleTextParams? parameters = null) =>
        Assert.IsType<TweenParams>(Presto.ScrambleText(parameters)(new Label(), 0, 1, null).Raw);

    private static TweenParams Scramble(ScrambleTextParams parameters, string text, int index = 0, int total = 1) =>
        Assert.IsType<TweenParams>(Presto.ScrambleText(parameters)(new Label { Text = text }, index, total, null).Raw);

    private static double DurationOf(TweenParams tween) => tween.Duration!.Value.AsDouble!.Value;

    private static string SettledMask(TweenParams tween, double progress)
    {
        var scrambled = tween.TextModifier!(progress);
        var mask = new char[scrambled.Length];
        for (var i = 0; i < scrambled.Length; i++) mask[i] = scrambled[i] == Upper[i] ? 'S' : '.';
        return new string(mask);
    }

    [Fact]
    public void DurationFollowsTheRevealRateSettleDurationAndRevealDelay()
    {
        Assert.Equal(467, DurationOf(Scramble()));
        Assert.Equal(650, DurationOf(Scramble(new() { RevealRate = 20, SettleDuration = 100, RevealDelay = 50 })));
        Assert.Equal(250, DurationOf(Scramble(new() { Duration = 200, RevealDelay = 50 })));
        Assert.Equal(467, DurationOf(Scramble(new() { Duration = 0 })));
        Assert.Equal(467, DurationOf(Scramble(new() { Text = "Hi" })));
        Assert.Equal(633, DurationOf(Scramble(new() { Text = "Hello wonderful world" })));
        Assert.Equal(317, DurationOf(Scramble(new() { Text = "Hi", OverrideChars = "" })));
    }

    [Fact]
    public void DefaultRevealRateIsSixtyAndDefaultSettleDurationIsThreeHundredScaled()
    {
        Assert.Equal(167, DurationOf(Scramble(new() { SettleDuration = 0 })));
        Assert.Equal(300, DurationOf(Scramble(new() { RevealRate = 1e9 })));

        Presto.Engine.TimeUnit = TimeUnit.Seconds;

        Assert.Equal(.467, DurationOf(Scramble()), 1e-12);
        Assert.Equal(.3, DurationOf(Scramble(new() { RevealRate = 1e9 })), 1e-12);
    }

    [Fact]
    public void ScrambleCharactersAreRedrawnOnlyOnSettleRateSteps()
    {
        var parameters = new ScrambleTextParams { Text = "yyyyyyyyyy", Chars = "0-9", Duration = 1000, SettleDuration = 900, Seed = 1 };
        var byDefault = Scramble(parameters, "xxxxxxxxxx").TextModifier!;
        var atTen = Scramble(parameters with { SettleRate = 10 }, "xxxxxxxxxx").TextModifier!;

        var first = byDefault(.21);
        Assert.All(first, c => Assert.InRange(c, '0', '9'));
        Assert.Equal(first, byDefault(.23));
        Assert.NotEqual(first, byDefault(.24));

        var slow = atTen(.21);
        Assert.Equal(slow, atTen(.29));
        Assert.NotEqual(slow, atTen(.31));
    }

    [Fact]
    public void TheRevealRunsInTheOrderFromAndFromIndexGive()
    {
        string MaskAt(ScrambleTextParams parameters) => SettledMask(Scramble(parameters, Lower), .45);

        Assert.Equal("SSSSS.....", MaskAt(Ordered with { From = ScrambleFrom.Left }));
        Assert.Equal(".....SSSSS", MaskAt(Ordered with { From = ScrambleFrom.Right }));
        Assert.Equal("..SSSSS...", MaskAt(Ordered with { From = ScrambleFrom.Center }));
        Assert.Equal(".SSSSS....", MaskAt(Ordered with { FromIndex = 3, From = ScrambleFrom.Right }));
        Assert.Equal(".....SSSSS", MaskAt(Ordered with { From = ScrambleFrom.Left, Reversed = true }));
        Assert.Equal("SS.....SSS", MaskAt(Ordered with { From = ScrambleFrom.Center, Reversed = true }));

        var random = MaskAt(Ordered with { From = ScrambleFrom.Random });
        Assert.Equal(5, random.Count(m => m == 'S'));
        Assert.DoesNotContain(random, (string[])["SSSSS.....", ".....SSSSS", "..SSSSS..."]);
        Assert.Equal(random, MaskAt(Ordered with { From = ScrambleFrom.Random }));
    }

    [Fact]
    public void ByDefaultTheRevealRunsFromTheLeftOrFromTheRightWhenTheTextShrinks()
    {
        Assert.Equal("SSSSS.....", SettledMask(Scramble(Ordered, Lower), .45));

        var shrinking = Ordered with { Text = "ABCDE" };
        Assert.Equal("*****", Scramble(shrinking, Lower).TextModifier!(.45));
        Assert.Equal("*****", Scramble(shrinking with { From = ScrambleFrom.Right }, Lower).TextModifier!(.45));
        Assert.Equal("ABCDE*****", Scramble(shrinking with { From = ScrambleFrom.Left }, Lower).TextModifier!(.45));
    }

    [Fact]
    public void ShrinkingTextFromTheCenterOrAnIndexSettlesOnlyOnceTheExtraCharactersHaveGone()
    {
        AssertKeptSettleLast(Ordered with { Text = "ABCDE", From = ScrambleFrom.Center });
        AssertKeptSettleLast(Ordered with { Text = "ABCDE", FromIndex = 4 });

        static void AssertKeptSettleLast(ScrambleTextParams parameters)
        {
            var modifier = Scramble(parameters, Lower).TextModifier!;
            var sawExtra = false;
            for (var v = .01; v < 1; v += .01)
            {
                var scrambled = modifier(v);
                if (scrambled.Length <= 5) continue;
                sawExtra = true;
                for (var i = 0; i < 5; i++) Assert.NotEqual(Upper[i], scrambled[i]);
            }
            Assert.True(sawExtra);
            Assert.Equal("ABCDE", modifier(1));
        }
    }

    [Fact]
    public void PerturbationOffsetsEachWindowByUpToItsFractionOfTheSettleWindow()
    {
        var plain = FirstSettled(Windowed);
        var perturbed = Windowed with { Perturbation = .2 };
        var modifier = Scramble(perturbed, Lower).TextModifier!;
        var step = Math.Round(1 / 30d, 12);

        Assert.Equal(plain, FirstSettled(Windowed with { Perturbation = 0 }));
        Assert.Equal(plain.Order(), plain);
        for (var c = 0; c < 10; c++) Assert.Equal(Math.Ceiling((c * .05 + .5) / step) * step, plain[c], .002);
        Assert.NotEqual(plain, FirstSettled(perturbed));

        for (var v = .001; v < 1; v += .001)
        {
            var scrambled = modifier(v);
            for (var c = 0; c < 10; c++)
            {
                if (scrambled[c] == Upper[c]) Assert.True(v >= c * .05 + .4 - 1e-9);
                if (v >= c * .05 + .6 + step + 1e-9) Assert.Equal(Upper[c], scrambled[c]);
                if (scrambled[c] == '*') Assert.True(v >= c * .05 - .1 - 1e-9);
                if (scrambled[c] == '#') Assert.True(v < c * .05 + .1 + 1e-9);
            }
        }

        static double[] FirstSettled(ScrambleTextParams parameters)
        {
            var modifier = Scramble(parameters, Lower).TextModifier!;
            var first = new double[10];
            Array.Fill(first, double.NaN);
            for (var v = .001; v < 1; v += .001)
            {
                var scrambled = modifier(v);
                for (var c = 0; c < 10; c++)
                {
                    if (double.IsNaN(first[c]) && scrambled[c] == Upper[c]) first[c] = v;
                }
            }
            return first;
        }
    }

    [Fact]
    public void CursorCharactersRunAheadOfTheRevealLastOneLeading()
    {
        Assert.Equal("****12####", Scramble(Windowed with { Cursor = "12" }, Lower).TextModifier!(.27));
        Assert.Equal("******####", Scramble(Windowed, Lower).TextModifier!(.27));
    }

    [Fact]
    public void DelayShowsTheUnchangedTextAndRevealDelayScramblesWithoutRevealing()
    {
        var delayed = Scramble(new() { Delay = 100, Seed = 1 });
        Assert.Equal(100d, delayed.Delay!.Value.AsDouble);
        Assert.Equal(Sentence, delayed.TextModifier!(0));

        var label = new Label();
        Presto.Animate(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Delay = 100, Duration = 100, Seed = 1 }) });
        Frames.Advance(80, 16);
        Assert.Equal(Sentence, label.Text);
        Frames.Advance(60, 16);
        Assert.NotEqual(Sentence, label.Text);

        var revealDelayed = Scramble(new() { Chars = "0-9", RevealDelay = 200, Duration = 200, SettleDuration = 50, Seed = 1 });
        Assert.Equal(400, DurationOf(revealDelayed));
        var seen = new HashSet<string>();
        for (var v = .05; v <= .5; v += .05)
        {
            var scrambled = revealDelayed.TextModifier!(v);
            Assert.DoesNotContain(scrambled, char.IsLetter);
            seen.Add(scrambled);
        }
        Assert.True(seen.Count > 1);
        Assert.Contains(revealDelayed.TextModifier!(.9), char.IsLetter);
    }

    [Fact]
    public void DelayAndRevealDelayDefaultToZero()
    {
        var tween = Scramble(new() { Seed = 1 });

        Assert.Equal(0d, tween.Delay!.Value.AsDouble);
        Assert.Equal(467, DurationOf(tween));
        Assert.NotEqual(Sentence, tween.TextModifier!(0));
    }

    [Fact]
    public void EaseSpreadsTheRevealPerTargetWhileTheTweenStaysLinear()
    {
        var perTarget = Ordered with { Ease = new FunctionValue((_, i, _) => i == 0 ? (Ease)Eases.None : Eases.InQuad) };
        var linear = Scramble(perTarget, Lower, 0, 2);
        var eased = Scramble(perTarget, Lower, 1, 2);

        Assert.Equal("SSSSS.....", SettledMask(linear, .45));
        Assert.Equal("SS........", SettledMask(eased, .45));
        Assert.Equal("SS........", SettledMask(Scramble(Ordered with { Ease = Eases.InQuad }, Lower), .45));
        Assert.Equal("SSSSS.....", SettledMask(Scramble(Ordered, Lower), .45));
        Assert.Same(Eases.None, eased.Ease!.Value.Raw);
    }

    [Fact]
    public void DefaultCharactersAreLettersDigitsAndFourSymbols()
    {
        var expected = new HashSet<char>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!%#_");
        var seen = new HashSet<char>();
        var text = new string('x', 40);
        for (var seed = 0; seed < 300; seed++) seen.UnionWith(Scramble(new() { Seed = seed }, text).TextModifier!(0));

        Assert.True(expected.SetEquals(seen));
    }

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

    [Fact]
    public void AnUnchainedScrambleSettlesOnTextTheAppChangedSince()
    {
        var label = new Label();
        var first = Presto.Animate(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Duration = 100, Seed = 1 }) });
        RunUntil(() => first.Completed);

        label.Text = "Changed";
        var second = Presto.Animate(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Duration = 100, Seed = 1 }) });
        RunUntil(() => second.Completed);

        Assert.Equal("Changed", label.Text);
    }

    [Fact]
    public void AScrambleInterruptingARunningOneSettlesOnTheOriginalText()
    {
        var label = new Label();
        Presto.Animate(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Duration = 200, Seed = 1 }) });
        Frames.Advance(100, 16);
        Assert.NotEqual(Sentence, label.Text);

        var second = Presto.Animate(label, new AnimationParams { ["Text"] = Presto.ScrambleText(new() { Duration = 100, Seed = 2 }) });
        RunUntil(() => second.Completed);

        Assert.Equal(Sentence, label.Text);
    }

    private sealed class Label
    {
        public string Text { get; set; } = Sentence;
    }
}
