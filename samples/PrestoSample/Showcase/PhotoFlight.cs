using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Showcase;

/// <summary>The connected pages' photo flight: X and the scale ease out fast while Y eases in and out, bending the path into an arc.</summary>
public static class PhotoFlight
{
    /// <summary>A fresh builder on every read, so a caller can retime it.</summary>
    #region snippet ConnectedXamlArc
    public static AnimationBuilder Arc => Presto.Build().Duration(620).Ease(Eases.OutQuint).Y(ease: Eases.InOutCubic);
    #endregion
}
