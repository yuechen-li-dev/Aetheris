using System.Numerics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirChamferControlledBodyProbeCase(
    string CaseName,
    double ChamferDistance,
    bool InvalidTargetEdge,
    bool MissingAdjacentFace,
    AirChamferFaceFamily FaceFamily,
    bool IsEdgeChain,
    bool IsCornerChain,
    bool LegacyDependency,
    bool IncludeNonOrthogonalFacePair);

public sealed record AirChamferControlledBodyProbeResult(
    string CaseName,
    bool ControlledBodyCreated,
    string Decision,
    string Recommendation,
    bool PrototypeInvoked,
    bool CandidateReplacementBodyCreated,
    string? CandidateReplacementBlocker,
    AirChamferConvexPlanarPrototypeResult? PrototypeResult,
    AirChamferClosedWitnessTopologySummary? CandidateTopology,
    AirChamferClosedWitnessStepSummary? CandidateStep,
    IReadOnlyDictionary<string, int>? ExpectedTopologyContract,
    IReadOnlyList<string> Diagnostics);

public static class AirChamferControlledBodyProbeLab
{
    public static IReadOnlyList<AirChamferControlledBodyProbeCase> Cases() =>
    [
        new("controlled-box-convex-planar-single-edge", 1d, false, false, AirChamferFaceFamily.Planar, false, false, false, false),
        new("controlled-wedge-nonorthogonal-convex-planar-single-edge", 1d, false, false, AirChamferFaceFamily.Planar, false, false, false, true),
        new("controlled-body-invalid-distance", 0d, false, false, AirChamferFaceFamily.Planar, false, false, false, false),
        new("controlled-body-invalid-target-edge", 1d, true, false, AirChamferFaceFamily.Planar, false, false, false, false),
        new("controlled-body-missing-adjacent-face", 1d, false, true, AirChamferFaceFamily.Planar, false, false, false, false),
        new("controlled-body-non-planar-adjacent-marker", 1d, false, false, AirChamferFaceFamily.Cylindrical, false, false, false, false),
        new("controlled-body-edge-chain", 1d, false, false, AirChamferFaceFamily.Planar, true, false, false, false),
        new("controlled-body-corner-chain", 1d, false, false, AirChamferFaceFamily.Planar, false, true, false, false),
        new("controlled-body-triangle-legacy-dependent", 1d, false, false, AirChamferFaceFamily.Planar, false, false, true, false)
    ];

    public static AirChamferControlledBodyProbeResult Evaluate(AirChamferControlledBodyProbeCase c)
    {
        var diagnostics = new List<string>
        {
            "edge-x7-controlled-body-probe-started",
            "edge-x7-legacy-authority-preserved",
            "edge-x7-no-production-route-replacement",
            "edge-x7-no-3d-boolean-used"
        };

        var body = BrepPrimitives.CreateBox(10d, 8d, 6d).Value;
        diagnostics.Add("edge-x7-controlled-body-created");

        var edgeStart = new Vector3(5f, 4f, -3f);
        var edgeEnd = c.InvalidTargetEdge ? edgeStart : new Vector3(5f, 4f, 3f);
        diagnostics.Add("edge-x7-target-edge-selected");

        Vector3? faceA = new(1f, 0f, 0f);
        Vector3? faceB = c.MissingAdjacentFace ? null : (c.IncludeNonOrthogonalFacePair ? Vector3.Normalize(new Vector3(1f, 1f, 0f)) : new Vector3(0f, 1f, 0f));
        diagnostics.Add("edge-x7-adjacent-faces-resolved");

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
        diagnostics.Add("edge-x7-air-chamfer-request-created");

        var prototype = AirChamferConvexPlanarPrototype.Evaluate(request);
        diagnostics.Add("edge-x7-edge-v1-prototype-invoked");
        diagnostics.Add("edge-x7-judgment-engine-used");

        if (prototype.TopologyPlan is not null) diagnostics.Add("edge-x7-topology-plan-created");
        if (prototype.GeometryArtifact is not null) diagnostics.Add("edge-x7-geometry-artifact-created");

        var blocker = "body-mutation-not-implemented;using-closed-witness-artifact";
        var decision = prototype.Decision;
        var hasUnsupportedFaceFamily = c.FaceFamily != AirChamferFaceFamily.Planar;
        var recommendation = hasUnsupportedFaceFamily
            ? "air-chamfer-controlled-body-rejected-invalid"
            : prototype.Status switch
        {
            AirChamferPrototypeStatus.Accepted => "air-chamfer-controlled-body-needs-body-mutation-hardening",
            AirChamferPrototypeStatus.Deferred when c.IsEdgeChain || c.IsCornerChain => "air-chamfer-controlled-body-deferred-chain-or-corner",
            AirChamferPrototypeStatus.Rejected => "air-chamfer-controlled-body-rejected-invalid",
            _ => "air-chamfer-controlled-body-keep-legacy-route"
        };

        AirChamferClosedWitnessTopologySummary? topology = null;
        AirChamferClosedWitnessStepSummary? step = null;
        IReadOnlyDictionary<string, int>? contract = null;

        if (!hasUnsupportedFaceFamily && prototype.ClosedWitness is not null)
        {
            topology = prototype.ClosedWitness.TopologySummary;
            step = prototype.ClosedWitness.StepSummary;
            contract = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["faceCount"] = topology.FaceCount,
                ["planarFaceCount"] = topology.PlanarFaceCount,
                ["edgeCount"] = topology.EdgeCount,
                ["vertexCount"] = topology.VertexCount
            };
            diagnostics.Add($"edge-x7-candidate-replacement-body-deferred:{blocker}");
            diagnostics.Add(step.Succeeded && step.HasIso && step.HasManifoldSolidBrep && step.HasAdvancedFace && step.HasPlane && !step.HasCylindricalSurface && !step.HasBrepWithVoids
                ? "edge-x7-step-smoke-succeeded"
                : "edge-x7-step-smoke-failed:closed-witness-step-markers");
        }

        return new(c.CaseName, true, decision, recommendation, true, false, blocker, prototype, topology, step, contract, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}
