using System.Windows.Input;

namespace MonacoLabs.Presto.Tests;

internal sealed class TestCommand(Action<object?> execute) : ICommand
{
    public bool Enabled
    {
        get;
        set
        {
            field = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => Enabled;

    public void Execute(object? parameter) => execute(parameter);
}
