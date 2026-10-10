using MonacoLabs.Presto;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>The Derived values page's model: a sweep the view feeds from the pointer, and a header opacity derived from it.</summary>
public sealed class DerivedValuesViewModel
{
    public DerivedValuesViewModel()
    {
        #region snippet DerivedValuesHeaderOpacity
        HeaderOpacity = Sweep.Map([0, .5, 1], [.25, 1, .25]);
        #endregion
    }

    public MotionValue Sweep { get; } = Presto.CreateMotionValue();

    public MotionValue HeaderOpacity { get; }

    public string HeaderCode => Snippets.DerivedValuesHeaderOpacity;
}
