using MonacoLabs.Presto.DevTools;
using MonacoLabs.Presto.Lottie;

namespace MonacoLabs.Presto.Tests;

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
