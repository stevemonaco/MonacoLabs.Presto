using Monaco.Presto.AvaloniaBridge.DevTools;
using Monaco.Presto.AvaloniaBridge.Lottie;
using Monaco.Presto.Tests;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class SpecCoverageTests
{
    [Fact]
    public void EveryPublicBridgeTypeIsOwnedByExactlyOneSpec()
    {
        SpecCatalog.AssertNone(SpecCatalog.OwnershipErrors(typeof(PrestoAvalonia).Assembly, "avalonia-bridge"));
    }

    [Fact]
    public void EveryPublicLottieTypeIsOwnedByExactlyOneSpec()
    {
        SpecCatalog.AssertNone(SpecCatalog.OwnershipErrors(typeof(LottieView).Assembly, "lottie"));
    }

    [Fact]
    public void EveryPublicDevToolsTypeIsOwnedByExactlyOneSpec()
    {
        SpecCatalog.AssertNone(SpecCatalog.OwnershipErrors(typeof(PrestoDevTools).Assembly, "devtools"));
    }
}
