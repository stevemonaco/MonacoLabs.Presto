using Tempo.ViewModels;

namespace Tempo.Tests;

internal sealed class ManualTicks : ITickSource
{
    public event EventHandler? Tick;

    public void Raise() => Tick?.Invoke(this, EventArgs.Empty);
}
