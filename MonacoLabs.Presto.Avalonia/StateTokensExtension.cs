using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace MonacoLabs.Presto;

/// <summary>
/// Composes <see cref="Motion.StateProperty"/> from several bindings, as in
/// <c>presto:Motion.State="{presto:StateTokens {Binding Mode}, {Binding IsRunning}}"</c>: a true <see cref="bool"/> holds its
/// binding path's last member name, false or <see langword="null"/> holds nothing, and any other value holds its own tokens.
/// </summary>
public sealed class StateTokensExtension : MarkupExtension
{
    private readonly BindingBase[] _bindings;

    /// <summary>Composes the tokens of two bindings.</summary>
    public StateTokensExtension(BindingBase first, BindingBase second) => _bindings = [first, second];

    /// <summary>Composes the tokens of three bindings.</summary>
    public StateTokensExtension(BindingBase first, BindingBase second, BindingBase third) => _bindings = [first, second, third];

    /// <summary>Composes the tokens of four bindings.</summary>
    public StateTokensExtension(BindingBase first, BindingBase second, BindingBase third, BindingBase fourth) =>
        _bindings = [first, second, third, fourth];

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">It is set on anything but <see cref="Motion.StateProperty"/>.</exception>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if ((serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget)?.TargetProperty as AvaloniaProperty != Motion.StateProperty)
            throw new InvalidOperationException("{presto:StateTokens} must be set on Motion.State.");
        return new MultiBinding
        {
            Mode = BindingMode.OneWay,
            Bindings = [.. _bindings],
            Converter = new Union(Array.ConvertAll(_bindings, MemberName)),
        };
    }

    private static string MemberName(BindingBase binding)
    {
        var path = binding switch
        {
            ReflectionBinding reflection => reflection.Path,
            CompiledBinding compiled => compiled.Path?.ToString(),
            _ => null,
        } ?? "";
        var end = path.Length;
        while (end > 0 && path[end - 1] == ']') end = Math.Max(0, path.LastIndexOf('[', end - 1));
        var start = end;
        while (start > 0 && (char.IsLetterOrDigit(path[start - 1]) || path[start - 1] == '_')) start--;
        return path[start..end];
    }

    private sealed class Union(string[] names) : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var tokens = new List<string>();
            for (var i = 0; i < values.Count; i++)
            {
                switch (values[i])
                {
                    case null or BindingNotification or UnsetValueType:
                        break;
                    case bool flag when names[i].Length > 0:
                        if (flag && !tokens.Contains(names[i])) tokens.Add(names[i]);
                        break;
                    case var value:
                        foreach (var token in value.ToString()?.Split(MotionTracker.Separators, StringSplitOptions.RemoveEmptyEntries) ?? [])
                        {
                            if (!tokens.Contains(token)) tokens.Add(token);
                        }
                        break;
                }
            }
            return tokens.Count == 0 ? null : string.Join(' ', tokens);
        }
    }
}
