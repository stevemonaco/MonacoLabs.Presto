using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

/// <summary>
/// Value-driven animation: a bar, swatch, card and list rows bound through <c>{presto:Follow}</c> and a gauge through
/// <c>presto:Follow.Rotate</c>, all following <see cref="ValueDrivenViewModel"/> with no animation code.
/// </summary>
public partial class ValueDrivenPage : ShowcasePage
{
    public ValueDrivenPage() => InitializeComponent();

    protected override void OnAttached()
    {
        Presto.CreateTimer(new() { Duration = 1200, Loop = true, OnLoop = _ => ((ValueDrivenViewModel)DataContext!).Step() });
    }
}
