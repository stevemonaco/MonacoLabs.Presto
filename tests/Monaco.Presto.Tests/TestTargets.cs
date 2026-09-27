using Monaco.Presto.Values;

namespace Monaco.Presto.Tests;

/// <summary>The plain object the upstream suites share through tests/setup.js.</summary>
internal sealed class TestObject
{
    public double PlainValue { get; set; } = 10;
    public string ValueWithUnit { get; set; } = "10px";
    public string MultiplePlainValues { get; set; } = "16 32 64 128";
    public string MultipleValuesWithUnits { get; set; } = "16px 32em 64% 128ch";
}

/// <summary>
/// Stands in for the suites' <c>#target-id</c> element: the string members carry the units a browser would supply as the
/// property's original value, which the core otherwise has no way to know.
/// </summary>
internal sealed class StyleTarget
{
    public string TranslateX { get; set; } = "0px";
    public string TranslateY { get; set; } = "0px";
    public string Rotate { get; set; } = "0deg";
    public string BackgroundColor { get; set; } = "rgb(255,255,255)";
    public double Opacity { get; set; }
    public double Scale { get; set; } = 1;
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>Stands in for the suites' <c>.target-class</c> elements, whose <c>data-index</c> attribute drives function based values.</summary>
internal sealed class IndexedTarget
{
    public int Index { get; set; }
    public string TranslateX { get; set; } = "0px";
}

/// <summary>Stands in for the stagger suite's <c>{ x, y, val }</c> objects, whose coordinates drive the auto grid.</summary>
internal sealed class GridTarget
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Val { get; set; }
}

/// <inheritdoc cref="GridTarget"/>
/// <remarks>The <c>z</c> carrying variant, which switches the auto grid to three dimensions.</remarks>
internal sealed class CubeTarget
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Val { get; set; }
}

/// <summary>Stands in for the suites' <c>#scramble-text</c> element, whose property receives whole strings from a text modifier.</summary>
internal sealed class TextTarget
{
    public string Text { get; set; } = "";
}

/// <summary>Stands in for the suites' DOM color properties with a typed member instead of a CSS color string.</summary>
internal sealed class ColorTarget
{
    public PrestoColor Background { get; set; }
}
