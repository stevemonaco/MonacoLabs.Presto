namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>
/// A <see cref="Sheet"/> from the <see cref="Edge.Left"/> (the default) or <see cref="Edge.Right"/> edge of its host that a
/// swipe in from that edge opens, from a 20-DIP strip along it that takes presses while the drawer is closed.
/// </summary>
public sealed class Drawer : Sheet
{
    static Drawer() => EdgeProperty.OverrideDefaultValue<Drawer>(Edge.Left);

    private protected override double EdgeSwipe => 20;
}
