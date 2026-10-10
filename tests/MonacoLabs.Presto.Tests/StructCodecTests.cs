using System.Numerics;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>A struct tweened one channel per component through <see cref="StructCodec{T}"/>.</summary>
public sealed class StructCodecTests : EngineTestBase
{
    private static readonly StructCodec<Vector2> Codec = new(v => [v.X, v.Y], c => new Vector2((float)c[0], (float)c[1]));

    [Fact]
    public void InterpolatesEachComponent()
    {
        Presto.Codecs[typeof(Vector2)] = Codec;
        try
        {
            var target = new VectorTarget();
            var animation = Presto.Animate(target, new()
            {
                ["Position"] = TweenValue.Of(new Vector2(10, 20)),
                Duration = 100,
                Ease = Eases.Linear(),
                Autoplay = false,
            });

            animation.Seek(50);
            Assert.Equal(new Vector2(5, 10), target.Position);

            animation.Seek(100);
            Assert.Equal(new Vector2(10, 20), target.Position);
        }
        finally
        {
            Presto.Codecs.Remove(typeof(Vector2));
        }
    }

    [Fact]
    public void DoesNotClaimAnotherType() => Assert.False(Codec.TryDecode(new Vector3(1, 2, 3), new DecomposedValue()));

    private sealed class VectorTarget
    {
        public Vector2 Position { get; set; }
    }
}
