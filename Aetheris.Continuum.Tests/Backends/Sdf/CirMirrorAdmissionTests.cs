using Aetheris.Continuum.Mirrors;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class CirMirrorAdmissionTests
{
    [Fact]
    public void AirCirX1_BoxPrimitiveMirror_IsAdmittedExactForFieldUses() =>
        AssertPrimitiveAdmission("BrepPrimitives.CreateBox", CirMirrorAtomKind.BoxPrimitive);

    [Fact]
    public void AirCirX1_CylinderPrimitiveMirror_IsAdmittedExactForFieldUses() =>
        AssertPrimitiveAdmission("BrepPrimitives.CreateCylinder", CirMirrorAtomKind.CylinderPrimitive);

    [Fact]
    public void AirCirX1_SpherePrimitiveMirror_IsAdmittedExactForFieldUses() =>
        AssertPrimitiveAdmission("BrepPrimitives.CreateSphere", CirMirrorAtomKind.SpherePrimitive);

    private static void AssertPrimitiveAdmission(string sourceRoute, CirMirrorAtomKind atomKind)
    {
        var result = CirMirrorAdmissionService.Admit(new CirMirrorAdmission(
            CirMirrorSourceRepresentationKind.Brep,
            sourceRoute,
            atomKind,
            CirMirrorCapability.PointContainment | CirMirrorCapability.ApproximateVolume | CirMirrorCapability.MapOccupancy));

        Assert.Equal(CirMirrorStatus.MirrorAdmittedExact, result.Status);
        Assert.Equal("mirror-admitted-exact", result.StatusText);
        Assert.True(result.Supports(CirMirrorCapability.PointContainment));
        Assert.True(result.Supports(CirMirrorCapability.ApproximateVolume));
        Assert.True(result.Supports(CirMirrorCapability.MapOccupancy));
        Assert.True(result.Supports(CirMirrorCapability.SectionSampling));
        Assert.False(result.Supports(CirMirrorCapability.FaceIdentity));
        Assert.False(result.Supports(CirMirrorCapability.TopologyParity));
        Assert.True(result.HasLoss(CirMirrorLossFlags.FaceIdentityLost));
        Assert.True(result.HasLoss(CirMirrorLossFlags.ExactTopologyUnavailable));
        Assert.Equal(sourceRoute, result.Provenance.SourceRoute);
        Assert.Equal(CirMirrorSourceRepresentationKind.Brep, result.Provenance.SourceRepresentationKind);
        Assert.Equal(CirMirrorRegistry.PrototypeVersion, result.Provenance.EmitterOrMirrorVersion);
        Assert.Contains($"air-cir-x1-mirror-admitted-exact:{sourceRoute}", result.Diagnostics);
        Assert.Contains("air-cir-x1-capability-map-occupancy", result.Diagnostics);
        Assert.Contains("air-cir-x1-capability-point-containment", result.Diagnostics);
        Assert.Contains("air-cir-x1-capability-approximate-volume", result.Diagnostics);
        Assert.Contains("air-cir-x1-no-production-analyzer-behavior-changed", result.Diagnostics);
        Assert.Contains("air-cir-x1-no-prismatic-mirror-created", result.Diagnostics);
        Assert.Contains("air-cir-x1-no-cir-to-brep-extraction", result.Diagnostics);
    }

    [Fact]
    public void AirCirX1_PrismaticSectionTransition_IsRejectedAsUnsupportedAtom()
    {
        var result = CirMirrorAdmissionService.Admit(new CirMirrorAdmission(
            CirMirrorSourceRepresentationKind.Air,
            "PrismaticSectionTransitionEmitter",
            CirMirrorAtomKind.PrismaticSectionTransition,
            CirMirrorCapability.MapOccupancy));

        Assert.Equal(CirMirrorStatus.MirrorRejectedUnsupportedAtom, result.Status);
        Assert.Equal("mirror-rejected-unsupported-atom", result.StatusText);
        Assert.Equal(CirMirrorCapability.None, result.AllowedCapabilities);
        Assert.Contains("air-cir-x1-mirror-rejected-unsupported-atom:PrismaticSectionTransitionEmitter", result.Diagnostics);
        Assert.Contains("air-cir-x1-mirror-unavailable:PrismaticSectionTransitionEmitter", result.Diagnostics);
        Assert.Contains("air-cir-x1-no-prismatic-mirror-created", result.Diagnostics);
    }

    [Fact]
    public void AirCirX1_ProfileAuthoredVerticalChamfer_RemainsMirrorUnavailable()
    {
        var result = CirMirrorAdmissionService.Admit(new CirMirrorAdmission(
            CirMirrorSourceRepresentationKind.Air,
            "ProfileVertexChamferExtrudeEmitter",
            CirMirrorAtomKind.ProfileAuthoredVerticalChamfer,
            CirMirrorCapability.MapOccupancy));

        Assert.Equal(CirMirrorStatus.MirrorUnavailable, result.Status);
        Assert.Equal("mirror-unavailable", result.StatusText);
        Assert.Equal(CirMirrorCapability.None, result.AllowedCapabilities);
        Assert.Contains("air-cir-x1-mirror-unavailable:ProfileVertexChamferExtrudeEmitter", result.Diagnostics);
        Assert.Contains("air-cir-x1-profile-chamfer-mirror-deferred", result.Diagnostics);
        Assert.Contains("air-cir-x1-no-prismatic-mirror-created", result.Diagnostics);
    }

    [Fact]
    public void AirCirX1_FaceIdentityRequestAgainstPrimitive_IsRejectedAsLossyForRequest()
    {
        var result = CirMirrorAdmissionService.Admit(new CirMirrorAdmission(
            CirMirrorSourceRepresentationKind.Brep,
            "BrepPrimitives.CreateBox",
            CirMirrorAtomKind.BoxPrimitive,
            CirMirrorCapability.MapOccupancy | CirMirrorCapability.FaceIdentity | CirMirrorCapability.TopologyParity));

        Assert.Equal(CirMirrorStatus.MirrorRejectedLossyForRequest, result.Status);
        Assert.Equal("mirror-rejected-lossy-for-request", result.StatusText);
        Assert.Equal(CirMirrorCapability.None, result.AllowedCapabilities);
        Assert.True(result.HasLoss(CirMirrorLossFlags.FaceIdentityLost));
        Assert.True(result.HasLoss(CirMirrorLossFlags.ExactTopologyUnavailable));
        Assert.Contains("air-cir-x1-mirror-rejected-lossy-for-request:face-identity+topology-parity+map-occupancy", result.Diagnostics);
        Assert.Contains("air-cir-x1-loss-face-identity", result.Diagnostics);
        Assert.Contains("air-cir-x1-loss-topology-parity", result.Diagnostics);
    }

    [Fact]
    public void AirCirX1_StaleOrMismatchedRequest_IsRejectedBeforeAdmission()
    {
        var result = CirMirrorAdmissionService.Admit(new CirMirrorAdmission(
            CirMirrorSourceRepresentationKind.Brep,
            "BrepPrimitives.CreateSphere",
            CirMirrorAtomKind.SpherePrimitive,
            CirMirrorCapability.MapOccupancy,
            ExpectedTopologySummary: "hash-a",
            ActualTopologySummary: "hash-b"));

        Assert.Equal(CirMirrorStatus.MirrorRejectedStaleOrMismatched, result.Status);
        Assert.Equal("mirror-rejected-stale-or-mismatched", result.StatusText);
        Assert.Equal(CirMirrorCapability.None, result.AllowedCapabilities);
        Assert.Contains("air-cir-x1-mirror-rejected-stale-or-mismatched:BrepPrimitives.CreateSphere", result.Diagnostics);
    }

    [Fact]
    public void AirCirX1_RepeatedRequests_ProduceStableStatusAndDiagnostics()
    {
        var request = new CirMirrorAdmission(
            CirMirrorSourceRepresentationKind.Brep,
            "BrepPrimitives.CreateCylinder",
            CirMirrorAtomKind.CylinderPrimitive,
            CirMirrorCapability.PointContainment | CirMirrorCapability.MapOccupancy,
            SourceIdOrLabel: "cylinder-case");

        var first = CirMirrorAdmissionService.Admit(request);
        var second = CirMirrorAdmissionService.Admit(request);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.StatusText, second.StatusText);
        Assert.Equal(first.AllowedCapabilities, second.AllowedCapabilities);
        Assert.Equal(first.KnownLosses, second.KnownLosses);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(first.Provenance.SourceRepresentationKind, second.Provenance.SourceRepresentationKind);
        Assert.Equal(first.Provenance.SourceRoute, second.Provenance.SourceRoute);
        Assert.Equal(first.Provenance.SourceIdOrLabel, second.Provenance.SourceIdOrLabel);
        Assert.Equal(first.Provenance.EmitterOrMirrorVersion, second.Provenance.EmitterOrMirrorVersion);
        Assert.Equal(first.Provenance.TopologySummary, second.Provenance.TopologySummary);
        Assert.Equal(first.Provenance.ToleranceContext, second.Provenance.ToleranceContext);
        Assert.Equal(first.Provenance.Diagnostics, second.Provenance.Diagnostics);
    }
}
