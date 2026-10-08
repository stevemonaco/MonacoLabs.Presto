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
        StatesCode.Xaml = $"{Snippets.FormStatesCard}\n\n{Snippets.FormStatesBadge}";
        StatesCode.CSharp = $"// FormMotions.cs\n{Snippets.FormMotions}";
    }

    public FormViewModel ViewModel => (FormViewModel)DataContext!;
}
