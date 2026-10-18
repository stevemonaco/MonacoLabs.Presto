namespace MonacoLabs.Presto.Controls;

/// <summary>
/// A <see cref="Sheet"/> from any edge of its host, <see cref="Edge.Left"/> by default, that a swipe in from that edge opens:
/// its <see cref="Sheet.EdgeSwipe"/> defaults to a 20-DIP strip that takes presses while the drawer is closed.
/// </summary>
public sealed class Drawer : Sheet
{
    static Drawer()
    {
        EdgeProperty.OverrideDefaultValue<Drawer>(Edge.Left);
        EdgeSwipeProperty.OverrideDefaultValue<Drawer>(20);
    }
}
