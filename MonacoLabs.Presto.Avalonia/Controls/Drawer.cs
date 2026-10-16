namespace MonacoLabs.Presto.Controls;

/// <summary>
/// A <see cref="Sheet"/> from any edge of its host, <see cref="Edge.Left"/> by default, that a swipe in from that edge opens,
/// from a 20-DIP strip along it that takes presses while the drawer is closed.
/// </summary>
public sealed class Drawer : Sheet
{
    static Drawer() => EdgeProperty.OverrideDefaultValue<Drawer>(Edge.Left);

    private protected override double EdgeSwipe => 20;
}
