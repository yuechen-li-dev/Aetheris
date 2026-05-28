using System.Numerics;
using Aetheris.Firmament.FrictionLab.CIRLab;
using Aetheris.Kernel.Core.Brep;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferRealBodyPrototypeTests
{
    [Theory]
    [InlineData("canonical", false)]
    [InlineData("nonorth", true)]
    public void Evaluate_AcceptedCases_ProduceCandidate(string name, bool nonOrth)
    {
        var result = AirChamferRealBodyPrototype.Evaluate(Request(name, 1d, false, false, AirChamferFaceFamily.Planar, false, false, false, nonOrth));
        Assert.Equal(AirChamferRealBodyPrototypeStatus.Succeeded, result.Status);
        Assert.NotNull(result.CandidateBody);
        Assert.NotNull(result.TopologyPlan);
        Assert.NotNull(result.GeometryArtifact);
        Assert.NotNull(result.StepSmoke);
        Assert.Contains("edge-v2-edge-v1-prototype-invoked", result.Diagnostics);
        Assert.Contains("edge-v2-judgment-engine-used", result.Diagnostics);
        Assert.Contains("edge-v2-topology-graft-applied", result.Diagnostics);
        Assert.Contains("edge-v2-step-smoke-succeeded", result.Diagnostics);
        Assert.Contains("edge-v2-legacy-authority-preserved", result.Diagnostics);
        Assert.Contains("edge-v2-no-production-route-replacement", result.Diagnostics);
        Assert.Contains("edge-v2-no-3d-boolean-used", result.Diagnostics);

        Assert.Equal(6, result.TopologySummary!.FaceCount);
        Assert.Equal(6, result.TopologySummary.PlanarFaceCount);
        Assert.Equal(12, result.TopologySummary.EdgeCount);
        Assert.Equal(8, result.TopologySummary.VertexCount);
        Assert.Equal(1, result.TopologySummary.ChamferFaceCount);
        Assert.Equal(2, result.TopologySummary.TrimmedAdjacentFaceCount);
        Assert.Equal(2, result.TopologySummary.TransitionEdgeCount);
    }

    [Theory]
    [InlineData(0d, false, false, AirChamferFaceFamily.Planar, false, false, false, AirChamferRealBodyPrototypeStatus.Rejected)]
    [InlineData(1d, true, false, AirChamferFaceFamily.Planar, false, false, false, AirChamferRealBodyPrototypeStatus.Rejected)]
    [InlineData(1d, false, true, AirChamferFaceFamily.Planar, false, false, false, AirChamferRealBodyPrototypeStatus.Rejected)]
    [InlineData(1d, false, false, AirChamferFaceFamily.Cylindrical, false, false, false, AirChamferRealBodyPrototypeStatus.Rejected)]
    [InlineData(1d, false, false, AirChamferFaceFamily.Planar, true, false, false, AirChamferRealBodyPrototypeStatus.Deferred)]
    [InlineData(1d, false, false, AirChamferFaceFamily.Planar, false, true, false, AirChamferRealBodyPrototypeStatus.Deferred)]
    [InlineData(1d, false, false, AirChamferFaceFamily.Planar, false, false, true, AirChamferRealBodyPrototypeStatus.FallbackLegacy)]
    public void Evaluate_RejectedDeferredCases_NoCandidate(double distance, bool invalidEdge, bool missingFace, AirChamferFaceFamily family, bool edgeChain, bool cornerChain, bool legacyDep, AirChamferRealBodyPrototypeStatus expected)
    {
        var result = AirChamferRealBodyPrototype.Evaluate(Request("case", distance, invalidEdge, missingFace, family, edgeChain, cornerChain, legacyDep, false));
        Assert.Equal(expected, result.Status);
        Assert.Null(result.CandidateBody);
        Assert.Null(result.TopologySummary);
    }

    private static AirChamferRealBodyPrototypeRequest Request(string name, double distance, bool invalidEdge, bool missingFace, AirChamferFaceFamily family, bool edgeChain, bool cornerChain, bool legacyDep, bool nonOrth)
    {
        var body = BrepPrimitives.CreateBox(10d, 8d, 6d).Value;
        var edgeStart = new Vector3(5f, 4f, -3f);
        var edgeEnd = invalidEdge ? edgeStart : new Vector3(5f, 4f, 3f);
        Vector3? faceA = new(1f, 0f, 0f);
        Vector3? faceB = missingFace ? null : (nonOrth ? Vector3.Normalize(new Vector3(1f, 1f, 0f)) : new Vector3(0f, 1f, 0f));
        var cls = edgeChain || cornerChain || legacyDep ? AirChamferClassificationExpectation.Concave : AirChamferClassificationExpectation.Convex;
        return new(name, body, edgeStart, edgeEnd, faceA, faceB, distance, family, edgeChain, cornerChain, legacyDep, cls, !nonOrth, 10d);
    }
}
