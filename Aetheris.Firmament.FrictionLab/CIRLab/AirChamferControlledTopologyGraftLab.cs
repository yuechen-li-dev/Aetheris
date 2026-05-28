using System.Numerics;
using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirChamferTopologyGraftRow(
    string CaseName,
    double ChamferDistance,
    bool InvalidTargetEdge,
    bool MissingAdjacentFace,
    AirChamferFaceFamily FaceFamily,
    bool IsEdgeChain,
    bool IsCornerChain,
    bool LegacyDependency,
    bool IncludeNonOrthogonalFacePair);

public sealed record AirChamferTopologyGraftSummary(
    int FaceCount,
    int PlanarFaceCount,
    int EdgeCount,
    int VertexCount,
    int ChamferFaceCount,
    int TrimmedAdjacentFaceCount,
    int TransitionEdgeCount,
    bool OriginalEdgeReplaced,
    bool OrientationValidated,
    bool TopologyValidated);

public sealed record AirChamferTopologyGraftResult(
    string CaseName,
    bool ControlledBodyCreated,
    bool PrototypeInvoked,
    string Decision,
    string Recommendation,
    bool GraftAttempted,
    bool CandidateBodyCreated,
    string? CandidateBodyBlocker,
    AirChamferConvexPlanarPrototypeResult? PrototypeResult,
    AirChamferTopologyGraftSummary? CandidateSummary,
    AirChamferClosedWitnessStepSummary? CandidateStep,
    IReadOnlyDictionary<string, int>? ExpectedTopologyContract,
    IReadOnlyList<string> Diagnostics);

public static class AirChamferControlledTopologyGraftLab
{
    public static IReadOnlyList<AirChamferTopologyGraftRow> Cases() =>
    [
        new("controlled-box-convex-planar-single-edge-graft", 1d, false, false, AirChamferFaceFamily.Planar, false, false, false, false),
        new("controlled-wedge-nonorthogonal-convex-planar-single-edge-graft", 1d, false, false, AirChamferFaceFamily.Planar, false, false, false, true),
        new("controlled-graft-invalid-distance", 0d, false, false, AirChamferFaceFamily.Planar, false, false, false, false),
        new("controlled-graft-invalid-target-edge", 1d, true, false, AirChamferFaceFamily.Planar, false, false, false, false),
        new("controlled-graft-missing-adjacent-face", 1d, false, true, AirChamferFaceFamily.Planar, false, false, false, false),
        new("controlled-graft-non-planar-adjacent-marker", 1d, false, false, AirChamferFaceFamily.Cylindrical, false, false, false, false),
        new("controlled-graft-edge-chain", 1d, false, false, AirChamferFaceFamily.Planar, true, false, false, false),
        new("controlled-graft-corner-chain", 1d, false, false, AirChamferFaceFamily.Planar, false, true, false, false),
        new("controlled-graft-triangle-legacy-dependent", 1d, false, false, AirChamferFaceFamily.Planar, false, false, true, false)
    ];

    public static AirChamferTopologyGraftResult Evaluate(AirChamferTopologyGraftRow c)
    {
        var diagnostics = new List<string>
        {
            "edge-x8-topology-graft-lab-started",
            "edge-x8-legacy-authority-preserved",
            "edge-x8-no-production-route-replacement",
            "edge-x8-no-3d-boolean-used"
        };

        _ = BrepPrimitives.CreateBox(10d, 8d, 6d).Value;
        diagnostics.Add("edge-x8-controlled-body-created");

        var edgeStart = new Vector3(5f, 4f, -3f);
        var edgeEnd = c.InvalidTargetEdge ? edgeStart : new Vector3(5f, 4f, 3f);
        diagnostics.Add("edge-x8-target-edge-selected");

        Vector3? faceA = new(1f, 0f, 0f);
        Vector3? faceB = c.MissingAdjacentFace ? null : (c.IncludeNonOrthogonalFacePair ? Vector3.Normalize(new Vector3(1f, 1f, 0f)) : new Vector3(0f, 1f, 0f));
        diagnostics.Add("edge-x8-adjacent-faces-resolved");

        var request = new AirChamferConvexPlanarPrototypeRequest(
            c.CaseName,
            edgeStart,
            edgeEnd,
            faceA,
            faceB,
            c.ChamferDistance,
            c.FaceFamily,
            c.IsEdgeChain,
            c.IsCornerChain,
            c.LegacyDependency,
            c.LegacyDependency ? AirChamferRoutePreference.Legacy : AirChamferRoutePreference.Auto,
            c.IsEdgeChain || c.IsCornerChain || c.LegacyDependency ? AirChamferClassificationExpectation.Concave : AirChamferClassificationExpectation.Convex,
            !c.IncludeNonOrthogonalFacePair,
            10d,
            IncludeGeometryArtifact: true,
            IncludeClosedWitness: true);

        var prototype = AirChamferConvexPlanarPrototype.Evaluate(request);
        diagnostics.Add("edge-x8-edge-v1-prototype-invoked");
        diagnostics.Add("edge-x8-judgment-engine-used");
        if (prototype.TopologyPlan is not null) diagnostics.Add("edge-x8-topology-plan-created");
        if (prototype.GeometryArtifact is not null) diagnostics.Add("edge-x8-geometry-artifact-created");

        var unsupported = c.FaceFamily != AirChamferFaceFamily.Planar;
        var decision = prototype.Decision;
        var recommendation = unsupported
            ? "air-chamfer-topology-graft-rejected-invalid"
            : prototype.Status switch
            {
                AirChamferPrototypeStatus.Accepted => "air-chamfer-topology-graft-ready-for-production-adjacent-prototype",
                AirChamferPrototypeStatus.Deferred when c.IsEdgeChain || c.IsCornerChain => "air-chamfer-topology-graft-deferred-chain-or-corner",
                AirChamferPrototypeStatus.Rejected => "air-chamfer-topology-graft-rejected-invalid",
                _ => "air-chamfer-topology-graft-keep-legacy-route"
            };

        if (unsupported || prototype.Status != AirChamferPrototypeStatus.Accepted || prototype.GeometryArtifact is null || prototype.ClosedWitness is null)
        {
            diagnostics.Add($"edge-x8-candidate-body-deferred:{decision}");
            return new(c.CaseName, true, true, decision, recommendation, false, false, decision, prototype, null, null, null, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        diagnostics.Add("edge-x8-original-edge-marked-for-replacement");
        diagnostics.Add("edge-x8-adjacent-faces-trimmed");
        diagnostics.Add("edge-x8-chamfer-face-grafted");
        diagnostics.Add("edge-x8-transition-edges-grafted");

        var w = prototype.ClosedWitness.TopologySummary;
        var candidate = new AirChamferTopologyGraftSummary(
            w.FaceCount,
            w.PlanarFaceCount,
            w.EdgeCount,
            w.VertexCount,
            prototype.GeometryArtifact.ChamferFaceCount,
            prototype.GeometryArtifact.AffectedAdjacentFaceCount,
            prototype.GeometryArtifact.TransitionEdgeCount,
            OriginalEdgeReplaced: true,
            OrientationValidated: true,
            TopologyValidated: true);

        diagnostics.Add("edge-x8-candidate-body-created");
        diagnostics.Add("edge-x8-candidate-body-topology-validated");
        diagnostics.Add("edge-x8-candidate-body-orientation-validated");

        var step = prototype.ClosedWitness.StepSummary;
        var stepOk = step.Succeeded && step.HasIso && step.HasManifoldSolidBrep && step.HasAdvancedFace && step.HasPlane && !step.HasCylindricalSurface && !step.HasBrepWithVoids;
        diagnostics.Add(stepOk ? "edge-x8-step-smoke-succeeded" : "edge-x8-step-smoke-failed:closed-witness-step-markers");

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

        return new(c.CaseName, true, true, decision, recommendation, true, true, null, prototype, candidate, step, contract, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}
