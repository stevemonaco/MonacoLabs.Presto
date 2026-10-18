using Avalonia;
using Avalonia.Markup.Xaml;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto;

/// <summary>
/// <c>{presto:ShowToast}</c>: a <see cref="CueAction"/> that calls <see cref="ToastHost.Show"/> on its <see cref="ToastHost"/>
/// with <see cref="Content"/>, or the value when that is unset, for each value except a first watched one.
/// </summary>
public sealed class ShowToastExtension : MarkupExtension
{
    /// <summary>How long each toast shows, in ms.</summary>
    public double Duration { get; set; } = 4000;

    /// <summary>The toast's content, or the raised or watched value when unset.</summary>
    public object? Content { get; set; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => new ShowToastAction(Duration, Content);

    private sealed class ShowToastAction(double duration, object? content) : CueAction
    {
        protected internal override void Invoke(Visual target, object? value, bool first)
        {
            var host = Require<ToastHost>(target, "{presto:ShowToast}");
            if (first) return;
            host.Show(content ?? value ?? throw new InvalidOperationException("{presto:ShowToast} has nothing to show: set Content or raise a value."), duration);
        }
    }
}
