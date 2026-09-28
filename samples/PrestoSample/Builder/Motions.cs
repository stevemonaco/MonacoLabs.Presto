using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Builder;

/// <summary>Named recipes for <c>{x:Static}</c>, each with the source text that built it.</summary>
public static class Motions
{
    public static readonly AnimationBuilder FadeUp;
    public static readonly string FadeUpCode;
    public static readonly AnimationBuilder Float;
    public static readonly string FloatCode;
    public static readonly AnimationBuilder Shake;
    public static readonly string ShakeCode;

    static Motions()
    {
        (FadeUp, FadeUpCode) = Snippet.Of(Presto.Build()
            .Opacity(from: 0, to: 1)
            .Y(from: 32, to: 0)
            .Duration(700)
            .Ease(Eases.OutQuad));
        (Float, FloatCode) = Snippet.Of(Presto.Build()
            .Y(-10)
            .Duration(1200)
            .Ease(Eases.InOutSine)
            .Loop()
            .Alternate());
        (Shake, ShakeCode) = Snippet.Of(Presto.Build()
            .X(k => k.To(-12).To(12).To(-8).To(8).To(0))
            .Rotate(k => k.To(-8).To(8).To(-4).To(4).To(0))
            .Duration(500)
            .Loop()
            .LoopDelay(700));
    }
}
