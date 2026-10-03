using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// A sign-up form whose card and badge bind <c>Motion.State</c> to the view model's state and list a <c>MotionState</c> per
/// value.
/// </summary>
public partial class FormStatesPage : ShowcasePage
{
    public FormStatesPage()
    {
        InitializeComponent();
        StatesCode.Xaml = $"{Snippets.FormStatesCard}\n    …\n\n{Snippets.FormStatesBadge}\n    …";
        StatesCode.CSharp = $"// FormMotions.cs\n{FormMotions.Code}";
    }

    public FormViewModel ViewModel => (FormViewModel)DataContext!;
}
