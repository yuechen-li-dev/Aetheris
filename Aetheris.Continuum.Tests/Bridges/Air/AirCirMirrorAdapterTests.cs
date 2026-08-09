using Aetheris.Kernel.Core.Air;
using Aetheris.Continuum.Bridges.Air;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep.Prismatic;
using Aetheris.Continuum.Mirrors;

namespace Aetheris.Continuum.Tests.Bridges.Air;

public sealed class AirCirMirrorAdapterTests
{
    [Fact]
    public void AirCirMirrorAdapter_PrismaticSectionTransition_AdmitsConvexMirror()
    {
        var result = AirCirMirrorAdapter.AdmitCanonicalPrismaticSectionTransition();

        Assert.True(result.Succeeded);
        Assert.Equal(AirNodeKind.PrismaticSectionTransition, result.Summary.SourceNodeKind);
        Assert.Equal("cir-convex-polyhedron", result.Summary.MirrorBackend);
        Assert.Equal(CirMirrorStatus.MirrorAdmittedExact, result.Summary.Status);
        Assert.True(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.Occupancy));
        Assert.True(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.Map));
        Assert.True(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.Containment));
        Assert.True(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.Bounds));
        Assert.True(result.Summary.KnownLosses.HasFlag(AirCirMirrorKnownLoss.FaceIdentity));
        Assert.True(result.Summary.KnownLosses.HasFlag(AirCirMirrorKnownLoss.TopologyParity));
        Assert.Contains("air-x5-cir-evaluation-side-channel-only", result.Summary.Diagnostics);
        Assert.Contains("air-x5-no-topology-authority", result.Summary.Diagnostics);
        Assert.Contains("air-x5-convex-polyhedron-mirror-admitted", result.Summary.Diagnostics);
    }

    [Fact]
    public void AirCirMirrorAdapter_TopFaceLoopChamfer_AdmitsConvexMirrorWithClassBProvenance()
    {
        var result = AirCirMirrorAdapter.AdmitCanonicalTopFaceLoopChamfer();

        Assert.True(result.Succeeded);
        Assert.Equal(AirNodeKind.TopFaceLoopChamfer, result.Summary.SourceNodeKind);
        Assert.Equal(AirRouteKind.TopFaceLoopChamferPrismatic, result.Summary.RouteKind);
        Assert.Equal(AirSelectionClass.FaceBoundaryLoop, result.Summary.SelectionClass);
        Assert.Equal(AirRuleKind.UniformChamfer, result.Summary.RuleKind);
        Assert.Equal("cir-convex-polyhedron", result.Summary.MirrorBackend);
        Assert.True(result.Summary.KnownLosses.HasFlag(AirCirMirrorKnownLoss.ChamferFaceIdentity));
        Assert.True(result.Summary.KnownLosses.HasFlag(AirCirMirrorKnownLoss.BRepPlanRoleParity));
        Assert.Contains("air-x5-class-b-provenance-preserved", result.Summary.Diagnostics);
        Assert.Contains("air-x5-cir-does-not-claim-chamfer-face-identity", result.Summary.Diagnostics);
    }

    [Fact]
    public void AirCirMirrorAdapter_TopFaceLoopChamfer_DoesNotClaimTopologyOrRoleParity()
    {
        var result = AirCirMirrorAdapter.AdmitCanonicalTopFaceLoopChamfer();
        Assert.False(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.FaceIdentity));
        Assert.False(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.LoopIdentity));
        Assert.False(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.TopologyParity));
        Assert.False(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.ChamferFaceIdentity));
        Assert.False(result.Summary.Capabilities.HasFlag(AirCirMirrorCapability.BRepPlanRoleParity));

        var lossy = AirCirMirrorAdapter.AdmitCanonicalTopFaceLoopChamfer(new AirCirMirrorRequest(
            AirNodeKind.TopFaceLoopChamfer,
            AirRouteKind.TopFaceLoopChamferPrismatic,
            AirCirMirrorSourceKind.GeneratedNativeAir,
            "air-x5-lossy-request",
            AirCirMirrorCapability.FaceIdentity | AirCirMirrorCapability.TopologyParity | AirCirMirrorCapability.ChamferFaceIdentity | AirCirMirrorCapability.BRepPlanRoleParity,
            AirSelectionClass.FaceBoundaryLoop,
            AirRuleKind.UniformChamfer,
            "generated/history-known"));

        Assert.False(lossy.Succeeded);
        Assert.Equal(CirMirrorStatus.MirrorRejectedLossyForRequest, lossy.Summary.Status);
        Assert.Contains("air-x5-face-identity-request-rejected-lossy", lossy.Summary.Diagnostics);
        Assert.Contains("air-x5-topology-parity-request-rejected-lossy", lossy.Summary.Diagnostics);
        Assert.Contains("air-x5-chamfer-face-identity-request-rejected-lossy", lossy.Summary.Diagnostics);
    }

    [Fact]
    public void AirCirMirrorAdapter_ImportedOrBRepOnlySource_DoesNotInferMirror()
    {
        var imported = AirCirMirrorAdapter.AdmitCanonicalPrismaticSectionTransition(new AirCirMirrorRequest(AirNodeKind.PrismaticSectionTransition, AirRouteKind.PrismaticSectionTransitionEmitter, AirCirMirrorSourceKind.ImportedOrRecovered, "imported-step"));
        var brepOnly = AirCirMirrorAdapter.AdmitCanonicalPrismaticSectionTransition(new AirCirMirrorRequest(AirNodeKind.PrismaticSectionTransition, AirRouteKind.PrismaticSectionTransitionEmitter, AirCirMirrorSourceKind.BRepOnly, "brep-only"));

        Assert.False(imported.Succeeded);
        Assert.False(brepOnly.Succeeded);
        Assert.Equal(CirMirrorStatus.MirrorUnavailable, imported.Summary.Status);
        Assert.Equal(CirMirrorStatus.MirrorUnavailable, brepOnly.Summary.Status);
        Assert.Contains("air-x5-imported-source-mirror-inference-rejected", imported.Summary.Diagnostics);
        Assert.Contains("air-x5-brep-only-source-mirror-unavailable", brepOnly.Summary.Diagnostics);
    }

    [Fact]
    public void AirCirMirrorAdapter_UnsupportedAirNode_ReturnsUnavailable()
    {
        var result = AirCirMirrorAdapter.AdmitCanonicalPrismaticSectionTransition(new AirCirMirrorRequest(AirNodeKind.Unsupported, AirRouteKind.Unsupported, AirCirMirrorSourceKind.Unsupported, "unsupported"));

        Assert.False(result.Succeeded);
        Assert.Equal(CirMirrorStatus.MirrorUnavailable, result.Summary.Status);
        Assert.Contains("air-x5-unsupported-air-node-mirror-unavailable", result.Summary.Diagnostics);
    }

    [Fact]
    public void AirCirMirrorAdapter_DeterministicSummaries()
    {
        var a = AirCirMirrorAdapter.AdmitCanonicalPrismaticSectionTransition().Summary;
        var b = AirCirMirrorAdapter.AdmitCanonicalPrismaticSectionTransition().Summary;
        var c = AirCirMirrorAdapter.AdmitCanonicalTopFaceLoopChamfer().Summary;
        var d = AirCirMirrorAdapter.AdmitCanonicalTopFaceLoopChamfer().Summary;

        Assert.Equal(Project(a), Project(b));
        Assert.Equal(Project(c), Project(d));
    }

    [Fact]
    public void AirCirMirrorAdapter_NonConvexPrismaticSectionTransition_ReturnsRejectedUnsupported()
    {
        var sections = new[]
        {
            new PrismaticSection(0, [(-5, -4), (5, -4), (5, 4), (-5, 4)]),
            new PrismaticSection(5, [(-5, -4), (5, -4), (5, 4), (-5, 4)]),
            new PrismaticSection(6, [(-4, -3), (0, -1), (4, -3), (4, 3), (-4, 3)]),
        };
        var result = AirCirMirrorAdapter.AdmitPrismaticSectionTransition(new PrismaticSectionTransitionRequest(sections, PrismaticCorrespondenceMap.Identity(5)));

        Assert.False(result.Succeeded);
        Assert.Equal(CirMirrorStatus.MirrorRejectedUnsupportedAtom, result.Summary.Status);
        Assert.Contains("air-x5-non-convex-prismatic-mirror-rejected", result.Summary.Diagnostics);
    }

    private static string Project(AirCirMirrorAdapterSummary s) => string.Join("|", s.StatusText, s.MirrorBackend, s.Capabilities, s.KnownLosses, string.Join(",", s.Diagnostics), string.Join(",", s.Provenance));
}
