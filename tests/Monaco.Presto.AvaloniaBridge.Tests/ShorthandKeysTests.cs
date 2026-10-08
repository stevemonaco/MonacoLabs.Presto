using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge.Effects;
using Monaco.Presto.AvaloniaBridge.Targets;
using Monaco.Presto.AvaloniaBridge.Transforms;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Every shorthand the bridge resolves has a <see cref="Shorthands"/> constant, and the constants are the string keys.</summary>
public sealed class ShorthandKeysTests : BridgeTestBase
{
    private static readonly string[] s_special = [Shorthands.Draw, Shorthands.ScrollX, Shorthands.ScrollY];

    private static string[] Constants() =>
        [.. typeof(Shorthands).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!)];

    [AvaloniaFact]
    public void EveryResolvedShorthandHasAConstant()
    {
        string[] resolved =
        [
            .. Enum.GetNames<TransformShorthand>(),
            .. Enum.GetNames<EffectShorthand>(),
            .. Enum.GetNames<ClipShorthand>(),
            .. Enum.GetNames<MaskShorthand>(),
            .. s_special,
        ];

        Assert.Equal(resolved.Order(), Constants().Order());
    }

    [AvaloniaFact]
    public void EveryConstantResolvesOnItsTarget()
    {
        var adapter = new AvaloniaObjectTargetAdapter();
        foreach (var name in Constants())
        {
            Visual target = name switch
            {
                Shorthands.Draw => new Rectangle(),
                Shorthands.ScrollX or Shorthands.ScrollY => new ScrollViewer(),
                _ => new Border(),
            };

            Assert.NotNull(adapter.Resolve(target, name));
            Assert.Equal(name, adapter.Canonicalize(target, name));
        }
    }
}
