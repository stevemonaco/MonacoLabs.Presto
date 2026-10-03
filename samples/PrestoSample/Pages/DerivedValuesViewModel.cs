using Monaco.Presto;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>The Derived values page's model: a sweep the view feeds from the pointer, and a header opacity derived from it.</summary>
public sealed class DerivedValuesViewModel
{
    public DerivedValuesViewModel()
    {
        var (opacity, code) = Snippet.Of(Sweep.Map([0, .5, 1], [.25, 1, .25]));
        HeaderOpacity = opacity;
        HeaderCode = Snippet.Declare("HeaderOpacity", code);
    }

    public MotionValue Sweep { get; } = Presto.CreateMotionValue();

    public MotionValue HeaderOpacity { get; }

    public string HeaderCode { get; }
}
