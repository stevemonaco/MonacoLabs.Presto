using System.ComponentModel;
using Avalonia.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary><c>{presto:StateTokens}</c> composing <see cref="Motion.StateProperty"/> from compiled, reflection and empty-path bindings.</summary>
public sealed partial class StateTokensXamlView : UserControl
{
    public static readonly AnimationBuilder Dim = Presto.Build().Opacity(0.4).Duration(100).Ease(Eases.None);

    public static readonly AnimationBuilder Nudge = Presto.Build().X(10).Duration(100).Ease(Eases.None);

    public StateTokensXamlView() => InitializeComponent();
}

public enum TokenMode
{
    Focus,
    Break,
}

public sealed class TokensViewModel : INotifyPropertyChanged
{
    private TokenMode _mode;
    private bool _isRunning;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TokenMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            PropertyChanged?.Invoke(this, new(nameof(Mode)));
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            PropertyChanged?.Invoke(this, new(nameof(IsRunning)));
        }
    }

    public string? Label => null;

    public bool Focus => true;

    public TokensViewModel Inner => this;
}
