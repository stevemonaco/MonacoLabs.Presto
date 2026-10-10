using MonacoLabs.Presto;

namespace PrestoSample.Builder;

/// <summary>The form's state builders, which the page's <c>Motion.States</c> reference.</summary>
public static class FormMotions
{
    #region snippet FormMotions
    public static readonly AnimationBuilder CardValidating = Presto.Build()
        .Scale(.97)
        .Opacity(.7)
        .Duration(550)
        .Ease(Eases.InOutSine)
        .Loop()
        .Alternate()
        .WhenReduced(reduced => reduced.Opacity(.7).Loop(false).Alternate(false));

    public static readonly AnimationBuilder CardError = Presto.Build()
        .X(k => k.To(-14).To(12).To(-8).To(6).To(-3).To(0))
        .Duration(520)
        .Ease(Eases.OutSine)
        .WhenReduced(reduced => reduced.Opacity(k => k.To(.6).To(1)));

    public static readonly AnimationBuilder CardSuccess = Presto.Build()
        .Scale(k => k.To(1.05).To(1))
        .Y(k => k.To(-8).To(0))
        .Duration(600)
        .Ease(Eases.OutBack())
        .WhenReduced(_ => { });

    public static readonly AnimationBuilder BadgeIdle = Presto.Build()
        .Y(-4)
        .Duration(1400)
        .Ease(Eases.InOutSine)
        .Loop()
        .Alternate()
        .WhenReduced(reduced => reduced.Loop(false).Alternate(false));

    public static readonly AnimationBuilder BadgeValidating = Presto.Build()
        .Rotate(from: 0, to: 360)
        .Duration(900)
        .Ease(Eases.InOutQuad)
        .Loop()
        .WhenReduced(reduced => reduced.Loop(false));

    public static readonly AnimationBuilder BadgeError = Presto.Build()
        .Rotate(k => k.To(-25).To(20).To(-12).To(0))
        .Duration(500)
        .WhenReduced(_ => { });

    public static readonly AnimationBuilder BadgeSuccess = Presto.Build()
        .Scale(from: 0, to: 1)
        .Rotate(from: -90, to: 0)
        .Ease(Eases.Spring(bounce: .5, duration: 700))
        .WhenReduced(reduced => reduced.Opacity(1, from: 0).Duration(300).Ease(Eases.OutQuad));
    #endregion
}
