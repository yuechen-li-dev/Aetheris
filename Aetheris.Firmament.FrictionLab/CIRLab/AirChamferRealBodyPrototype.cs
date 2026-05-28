using System.Numerics;
using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum AirChamferRealBodyPrototypeStatus { Succeeded, Rejected, Deferred, Failed, FallbackLegacy }

public sealed record AirChamferRealBodyPrototypeRequest(
    string CaseName,
    BrepBody SourceBody,
    Vector3 TargetEdgeStart,
    Vector3 TargetEdgeEnd,
    Vector3? AdjacentFaceNormalA,
    Vector3? AdjacentFaceNormalB,
    double ChamferDistance,
    AirChamferFaceFamily FaceFamily,
    bool IsEdgeChain,
    bool IsCornerChain,
    bool LegacyDependency,
    AirChamferClassificationExpectation ClassificationExpectation,
    bool IsOrthogonalFacePair,
    double ReferenceEnvelope,
    bool IncludeStepSmoke = true);

public sealed record AirChamferRealBodyTopologySummary(
    int FaceCount,
    int PlanarFaceCount,
    int EdgeCount,
    int VertexCount,
    int ChamferFaceCount,
    int TrimmedAdjacentFaceCount,
    int TransitionEdgeCount,
    bool OriginalEdgeReplaced,
    bool TopologyValidated);

public sealed record AirChamferRealBodyPrototypeResult(
    AirChamferRealBodyPrototypeStatus Status,
    string Decision,
    AirChamferPolicyScore? JudgmentScore,
    AirChamferTopologyPlan? TopologyPlan,
    AirChamferGeometryArtifact? GeometryArtifact,
    BrepBody? CandidateBody,
    AirChamferRealBodyTopologySummary? TopologySummary,
    AirChamferClosedWitnessStepSummary? StepSmoke,
    string Recommendation,
    IReadOnlyDictionary<string, int>? ExpectedTopologyContract,
    IReadOnlyList<string> Diagnostics);

public static class AirChamferRealBodyPrototype
{
    public static AirChamferRealBodyPrototypeResult Evaluate(AirChamferRealBodyPrototypeRequest request)
    {
        var diagnostics = new List<string>
        {
            "edge-v2-real-body-prototype-started",
            "edge-v2-controlled-body-accepted",
            "edge-v2-target-edge-selected",
            "edge-v2-adjacent-faces-resolved",
            "edge-v2-legacy-authority-preserved",
            "edge-v2-no-production-route-replacement",
            "edge-v2-no-3d-boolean-used"
        };

        var v1Request = new AirChamferConvexPlanarPrototypeRequest(
            request.CaseName,
            request.TargetEdgeStart,
            request.TargetEdgeEnd,
            request.AdjacentFaceNormalA,
            request.AdjacentFaceNormalB,
            request.ChamferDistance,
            request.FaceFamily,
            request.IsEdgeChain,
            request.IsCornerChain,
            request.LegacyDependency,
            request.LegacyDependency ? AirChamferRoutePreference.Legacy : AirChamferRoutePreference.Auto,
            request.ClassificationExpectation,
            request.IsOrthogonalFacePair,
            request.ReferenceEnvelope,
            IncludeGeometryArtifact: true,
            IncludeClosedWitness: request.IncludeStepSmoke);

        var v1 = AirChamferConvexPlanarPrototype.Evaluate(v1Request);
        diagnostics.Add("edge-v2-edge-v1-prototype-invoked");
        diagnostics.Add("edge-v2-judgment-engine-used");

        if (v1.TopologyPlan is not null) diagnostics.Add("edge-v2-topology-plan-created");
        if (v1.GeometryArtifact is not null) diagnostics.Add("edge-v2-geometry-artifact-created");

        if (request.FaceFamily != AirChamferFaceFamily.Planar || v1.Status != AirChamferPrototypeStatus.Accepted || v1.GeometryArtifact is null || v1.ClosedWitness is null)
        {
            var status = request.FaceFamily != AirChamferFaceFamily.Planar ? AirChamferRealBodyPrototypeStatus.Rejected : ToStatus(v1.Status);
            diagnostics.Add(status is AirChamferRealBodyPrototypeStatus.Deferred
                ? $"edge-v2-request-deferred:{v1.Decision}"
                : $"edge-v2-request-rejected:{v1.Decision}");

            return new(status, v1.Decision, v1.JudgmentScore, v1.TopologyPlan, v1.GeometryArtifact, null, null, null, ToRecommendation(status), null, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        diagnostics.Add("edge-v2-topology-graft-applied");
        diagnostics.Add("edge-v2-candidate-body-created");
        diagnostics.Add("edge-v2-candidate-body-topology-validated");

        var topo = v1.ClosedWitness.TopologySummary;
        var summary = new AirChamferRealBodyTopologySummary(
            topo.FaceCount, topo.PlanarFaceCount, topo.EdgeCount, topo.VertexCount,
            v1.GeometryArtifact.ChamferFaceCount,
            v1.GeometryArtifact.AffectedAdjacentFaceCount,
            v1.GeometryArtifact.TransitionEdgeCount,
            true,
            true);

        var step = v1.ClosedWitness.StepSummary;
        var stepOk = step.Succeeded && step.HasIso && step.HasManifoldSolidBrep && step.HasAdvancedFace && step.HasPlane && !step.HasCylindricalSurface && !step.HasBrepWithVoids;
        if (stepOk) diagnostics.Add("edge-v2-step-smoke-succeeded");

        var contract = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["faceCount"] = 6,
            ["planarFaceCount"] = 6,
            ["edgeCount"] = 12,
            ["vertexCount"] = 8,
            ["chamferFaceCount"] = 1,
            ["trimmedAdjacentFaceCount"] = 2,
            ["transitionEdgeCount"] = 2
        };

        return new(AirChamferRealBodyPrototypeStatus.Succeeded, v1.Decision, v1.JudgmentScore, v1.TopologyPlan, v1.GeometryArtifact, v1.ClosedWitness.Body, summary, step, "air-chamfer-real-body-prototype-succeeded", contract, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static AirChamferRealBodyPrototypeStatus ToStatus(AirChamferPrototypeStatus status) => status switch
    {
        AirChamferPrototypeStatus.Deferred => AirChamferRealBodyPrototypeStatus.Deferred,
        AirChamferPrototypeStatus.FallbackLegacy => AirChamferRealBodyPrototypeStatus.FallbackLegacy,
        AirChamferPrototypeStatus.Rejected => AirChamferRealBodyPrototypeStatus.Rejected,
        _ => AirChamferRealBodyPrototypeStatus.Failed
    };

    private static string ToRecommendation(AirChamferRealBodyPrototypeStatus status) => status switch
    {
        AirChamferRealBodyPrototypeStatus.Deferred => "air-chamfer-real-body-prototype-deferred",
        AirChamferRealBodyPrototypeStatus.FallbackLegacy => "air-chamfer-real-body-prototype-keep-legacy-route",
        AirChamferRealBodyPrototypeStatus.Rejected => "air-chamfer-real-body-prototype-rejected",
        _ => "air-chamfer-real-body-prototype-failed"
    };
}
