using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Builder;

/// <summary>The form's recipes keyed by state, each table with the source text that built it; a state without an entry plays nothing.</summary>
public static class FormMotions
{
    public static readonly IReadOnlyDictionary<FormState, AnimationBuilder> Card;
    public static readonly string CardCode;
    public static readonly IReadOnlyDictionary<FormState, AnimationBuilder> Badge;
    public static readonly string BadgeCode;

    static FormMotions()
    {
        (Card, CardCode) = Snippet.Of(new Dictionary<FormState, AnimationBuilder>
        {
            [FormState.Validating] = Presto.Build()
                .Scale(.97)
                .Opacity(.7)
                .Duration(550)
                .Ease(Eases.InOutSine)
                .Loop()
                .Alternate(),
            [FormState.Error] = Presto.Build()
                .X(k => k.To(-14).To(12).To(-8).To(6).To(-3).To(0))
                .Duration(520)
                .Ease(Eases.OutSine),
            [FormState.Success] = Presto.Build()
                .Scale(k => k.To(1.05).To(1))
                .Y(k => k.To(-8).To(0))
                .Duration(600)
                .Ease(Eases.OutBack()),
        });
        (Badge, BadgeCode) = Snippet.Of(new Dictionary<FormState, AnimationBuilder>
        {
            [FormState.Idle] = Presto.Build()
                .Y(-4)
                .Duration(1400)
                .Ease(Eases.InOutSine)
                .Loop()
                .Alternate(),
            [FormState.Validating] = Presto.Build()
                .Rotate(from: 0, to: 360)
                .Duration(900)
                .Ease(Eases.InOutQuad)
                .Loop(),
            [FormState.Error] = Presto.Build()
                .Rotate(k => k.To(-25).To(20).To(-12).To(0))
                .Duration(500),
            [FormState.Success] = Presto.Build()
                .Scale(from: 0, to: 1)
                .Rotate(from: -90, to: 0)
                .Ease(Eases.Spring(bounce: .5, duration: 700)),
        });
    }
}
