using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>The form's state builders, which the page's <c>Motion.States</c> reference, and the source text that built them.</summary>
public static class FormMotions
{
    public static readonly AnimationBuilder CardValidating;
    public static readonly AnimationBuilder CardError;
    public static readonly AnimationBuilder CardSuccess;
    public static readonly AnimationBuilder BadgeIdle;
    public static readonly AnimationBuilder BadgeValidating;
    public static readonly AnimationBuilder BadgeError;
    public static readonly AnimationBuilder BadgeSuccess;
    public static readonly string Code;

    static FormMotions()
    {
        (CardValidating, var cardValidating) = Snippet.Of(Presto.Build()
            .Scale(.97)
            .Opacity(.7)
            .Duration(550)
            .Ease(Eases.InOutSine)
            .Loop()
            .Alternate());
        (CardError, var cardError) = Snippet.Of(Presto.Build()
            .X(k => k.To(-14).To(12).To(-8).To(6).To(-3).To(0))
            .Duration(520)
            .Ease(Eases.OutSine));
        (CardSuccess, var cardSuccess) = Snippet.Of(Presto.Build()
            .Scale(k => k.To(1.05).To(1))
            .Y(k => k.To(-8).To(0))
            .Duration(600)
            .Ease(Eases.OutBack()));
        (BadgeIdle, var badgeIdle) = Snippet.Of(Presto.Build()
            .Y(-4)
            .Duration(1400)
            .Ease(Eases.InOutSine)
            .Loop()
            .Alternate());
        (BadgeValidating, var badgeValidating) = Snippet.Of(Presto.Build()
            .Rotate(from: 0, to: 360)
            .Duration(900)
            .Ease(Eases.InOutQuad)
            .Loop());
        (BadgeError, var badgeError) = Snippet.Of(Presto.Build()
            .Rotate(k => k.To(-25).To(20).To(-12).To(0))
            .Duration(500));
        (BadgeSuccess, var badgeSuccess) = Snippet.Of(Presto.Build()
            .Scale(from: 0, to: 1)
            .Rotate(from: -90, to: 0)
            .Ease(Eases.Spring(bounce: .5, duration: 700)));
        Code = string.Join("\n\n",
            Declare(nameof(CardValidating), cardValidating),
            Declare(nameof(CardError), cardError),
            Declare(nameof(CardSuccess), cardSuccess),
            Declare(nameof(BadgeIdle), badgeIdle),
            Declare(nameof(BadgeValidating), badgeValidating),
            Declare(nameof(BadgeError), badgeError),
            Declare(nameof(BadgeSuccess), badgeSuccess));
    }

    private static string Declare(string name, string code) => Snippet.Declare($"public static readonly AnimationBuilder {name}", code);
}
