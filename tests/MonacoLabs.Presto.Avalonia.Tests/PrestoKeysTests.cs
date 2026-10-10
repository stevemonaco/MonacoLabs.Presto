using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto.Effects;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Text;
using MonacoLabs.Presto.Transforms;

namespace MonacoLabs.Presto.Tests;

public sealed class PrestoKeysTests : BridgeTestBase
{
    private static readonly AvaloniaObjectTargetAdapter Adapter = new();

    private static Dictionary<string, PropertyKey> Keys() =>
        typeof(PrestoKeys).GetFields(BindingFlags.Public | BindingFlags.Static).ToDictionary(f => f.Name, f => (PropertyKey)f.GetValue(null)!);

    private static IEnumerable<(string Name, Func<AvaloniaObject> Target)> Shorthands() =>
        Enum.GetNames<TransformShorthand>().Concat(Enum.GetNames<EffectShorthand>()).Concat(Enum.GetNames<ClipShorthand>())
            .Concat(Enum.GetNames<MaskShorthand>()).Select(n => (n, (Func<AvaloniaObject>)(() => new Border())))
            .Concat(Enum.GetNames<WarpShorthand>().Select(n => (n, (Func<AvaloniaObject>)(() => new TextBlock()))))
            .Append(("Draw", () => new Rectangle()))
            .Append(("ScrollX", () => new ScrollViewer()))
            .Append(("ScrollY", () => new ScrollViewer()));

    [AvaloniaFact]
    public void HoldsOneKeyPerShorthandAndPseudoPropertyAndNothingElse()
    {
        var expected = Shorthands().Select(s => s.Name).Concat(["Text", "Data"]).Order();

        Assert.Equal(expected, Keys().Keys.Order());
    }

    [AvaloniaFact]
    public void EachShorthandKeyResolvesOnItsTargetAndCanonicalizesToItsOwnName()
    {
        var keys = Keys();
        foreach (var (name, target) in Shorthands())
        {
            Assert.Equal(name, keys[name].Name);
            Assert.NotNull(Adapter.Resolve(target(), name));
            Assert.Equal(name, Adapter.Canonicalize(target(), name));
        }
    }

    [AvaloniaFact]
    public void TextAndDataResolveOnATextBlockAndAPath()
    {
        Assert.NotNull(Adapter.Resolve(new TextBlock(), PrestoKeys.Text.Name));
        Assert.NotNull(Adapter.Resolve(new Avalonia.Controls.Shapes.Path(), PrestoKeys.Data.Name));
    }

    [AvaloniaFact]
    public void OfReturnsTheEncodedName()
    {
        Assert.Equal(AvaloniaObjectTargetAdapter.Encode(Visual.OpacityProperty), PrestoKeys.Of(Visual.OpacityProperty).Name);
        Assert.Equal(new PropertyKey("Canvas.Left"), PrestoKeys.Of(Canvas.LeftProperty));
    }

    [AvaloniaFact]
    public void AccessingTheKeysDoesNotInstallTheBridge()
    {
        PrestoAvalonia.Initialized = false;

        _ = Keys();
        _ = PrestoKeys.Of(Visual.OpacityProperty);

        Assert.False(PrestoAvalonia.Initialized);
        PrestoAvalonia.EnsureInitialized();
    }
}
