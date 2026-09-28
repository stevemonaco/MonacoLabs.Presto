using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// A sign-up form whose card and badge bind <c>Animate.OnAttached</c> to builders the view model picks per state. The
/// XAML snippets are constants pinned to the markup by a test.
/// </summary>
public partial class FormStatesPage : ShowcasePage
{
    public const string CardXaml = """
        <Border Classes="form" presto:Animate.OnAttached="{Binding CardMotion}">
        """;

    public const string BadgeXaml = """
        <Border Classes="badge" Background="{Binding Tint}" presto:Animate.OnAttached="{Binding BadgeMotion}">
        """;

    public FormStatesPage()
    {
        InitializeComponent();
        BindingCode.Text = $"{CardXaml}\n    …\n        {BadgeXaml}";
        const string field = "public static readonly IReadOnlyDictionary<FormState, AnimationBuilder>";
        TablesCode.Text = $"// FormMotions.cs\n{Snippet.Declare($"{field} Card", FormMotions.CardCode)}\n\n{Snippet.Declare($"{field} Badge", FormMotions.BadgeCode)}";
    }

    public FormViewModel ViewModel => (FormViewModel)DataContext!;
}
