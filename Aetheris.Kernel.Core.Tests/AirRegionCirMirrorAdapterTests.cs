using Aetheris.Kernel.Core.Air.Regions;

namespace Aetheris.Kernel.Core.Tests;

public sealed class AirRegionCirMirrorAdapterTests
{
    [Fact]
    public void SideHoleRegionCirMirror_RejectsForbiddenTopologyRequestsAsLossy()
    {
        var trace = AirRegionTraceFactory.ForFaceAttachedSideHoleDeferred();
        var side = trace.Regions.Single(r => r.RegionKind == AirRegionKind.FaceAttachedRegion);

        var result = AirSideHoleRegionCirMirrorAdapter.Admit(side, ["occupancy", "face-identity", "topology-parity", "entry-loop-identity", "boundary-patch-identity"]);

        Assert.False(result.Succeeded);
        Assert.Equal("mirror-rejected-lossy-for-request", result.Summary.Status);
        Assert.Empty(result.Summary.Capabilities);
        Assert.Contains("air-region-x3-face-identity-request-rejected-lossy", result.Summary.Diagnostics);
        Assert.Contains("air-region-x3-topology-parity-request-rejected-lossy", result.Summary.Diagnostics);
        Assert.Contains("air-region-x3-entry-loop-identity-request-rejected-lossy", result.Summary.Diagnostics);
        Assert.Contains("air-region-x3-boundary-patch-identity-request-rejected-lossy", result.Summary.Diagnostics);
        Assert.Contains("no-topology-authority", result.Summary.KnownLosses);
        Assert.Contains("no-face-identity", result.Summary.KnownLosses);
    }
}
