using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// A sign-up form whose card and badge bind <c>Motion.State</c> to the view model's state and list a <c>MotionState</c> per
/// value. The XAML snippets are constants pinned to the markup by a test.
/// </summary>
public partial class FormStatesPage : ShowcasePage
{
    public const string CardXaml = """
        <Border Classes="form" presto:Motion.State="{Binding State}">
            <presto:Motion.States>
                <presto:MotionStates>
                    <presto:MotionState When="Validating" Motion="{x:Static local:FormMotions.CardValidating}" />
                    <presto:MotionState When="Error" Motion="{x:Static local:FormMotions.CardError}" />
                    <presto:MotionState When="Success" Motion="{x:Static local:FormMotions.CardSuccess}" />
                </presto:MotionStates>
            </presto:Motion.States>
        """;

    public const string BadgeXaml = """
        <Border Classes="badge" Background="{Binding Tint}" presto:Motion.State="{Binding State}">
            <presto:Motion.States>
                <presto:MotionStates>
                    <presto:MotionState When="Idle" Motion="{x:Static local:FormMotions.BadgeIdle}" />
                    <presto:MotionState When="Validating" Motion="{x:Static local:FormMotions.BadgeValidating}" />
                    <presto:MotionState When="Error" Motion="{x:Static local:FormMotions.BadgeError}" />
                    <presto:MotionState When="Success" Motion="{x:Static local:FormMotions.BadgeSuccess}" />
                </presto:MotionStates>
            </presto:Motion.States>
        """;

    public FormStatesPage()
    {
        InitializeComponent();
        StatesCode.Text = $"{CardXaml}\n    …\n\n{BadgeXaml}\n    …";
        BuildersCode.Text = $"// FormMotions.cs\n{FormMotions.Code}";
    }

    public FormViewModel ViewModel => (FormViewModel)DataContext!;
}
