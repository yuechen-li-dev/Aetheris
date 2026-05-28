using System.Numerics;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum AirChamferPrototypeStatus
{
    Accepted,
    Rejected,
    Deferred,
    FallbackLegacy
}

public sealed record AirChamferConvexPlanarPrototypeRequest(
    string CaseName,
    Vector3 EdgeStart,
    Vector3 EdgeEnd,
    Vector3? FaceANormal,
    Vector3? FaceBNormal,
    double ChamferDistance,
    AirChamferFaceFamily FaceFamily,
    bool IsEdgeChain,
    bool IsCornerChain,
    bool LegacyDependency,
    AirChamferRoutePreference RoutePreference,
    AirChamferClassificationExpectation ClassificationExpectation,
    bool IsOrthogonalPlanarPair,
    double? LocalFeatureEnvelope,
    bool IncludeGeometryArtifact,
    bool IncludeClosedWitness);

public sealed record AirChamferConvexPlanarPrototypeResult(
    AirChamferPrototypeStatus Status,
    string Decision,
    AirChamferPolicyScore? JudgmentScore,
    IReadOnlyDictionary<string, double> Considerations,
    AirChamferTopologyPlan? TopologyPlan,
    AirChamferGeometryArtifact? GeometryArtifact,
    AirChamferClosedWitnessBody? ClosedWitness,
    IReadOnlyList<string> Diagnostics);

public static class AirChamferConvexPlanarPrototype
{
    public static AirChamferConvexPlanarPrototypeResult Evaluate(AirChamferConvexPlanarPrototypeRequest request)
    {
        var diagnostics = new List<string>
        {
            "edge-v1-air-chamfer-prototype-started",
            "edge-v1-judgment-engine-used",
            "edge-v1-legacy-authority-preserved",
            "edge-v1-no-production-route-replacement",
            "edge-v1-no-3d-boolean-used"
        };

        var topologyCase = new AirChamferTopologyPlanCase(
            request.CaseName,
            new AirChamferPolicyRequest(
                request.EdgeStart,
                request.EdgeEnd,
                request.FaceANormal,
                request.FaceBNormal,
                request.ChamferDistance,
                request.FaceFamily,
                request.IsEdgeChain,
                request.IsCornerChain,
                request.LegacyDependency,
                request.RoutePreference,
                request.ClassificationExpectation,
                request.IsOrthogonalPlanarPair,
                request.LocalFeatureEnvelope),
            string.Empty);

        var witnessResult = AirChamferClosedWitnessLab.Evaluate(topologyCase);
        var artifactResult = witnessResult.ArtifactResult;
        var policyResult = artifactResult.TopologyPlan.Policy;

        diagnostics.AddRange(policyResult.Diagnostics.Where(d => d.Contains("judgment", StringComparison.Ordinal)));

        var decision = witnessResult.Decision;
        diagnostics.Add($"edge-v1-policy-decision:{decision}");

        if (artifactResult.TopologyPlan.Plan is not null)
        {
            diagnostics.Add("edge-v1-topology-plan-created");
        }

        if (request.IncludeGeometryArtifact && artifactResult.Artifact is not null)
        {
            diagnostics.Add("edge-v1-geometry-artifact-created");
        }

        var witness = request.IncludeClosedWitness ? witnessResult.Witness : null;
        if (witness is not null)
        {
            diagnostics.Add("edge-v1-closed-witness-created");
            if (witness.StepSummary.Succeeded && witness.StepSummary.HasIso && witness.StepSummary.HasManifoldSolidBrep && witness.StepSummary.HasAdvancedFace && witness.StepSummary.HasPlane && !witness.StepSummary.HasCylindricalSurface && !witness.StepSummary.HasBrepWithVoids)
            {
                diagnostics.Add("edge-v1-closed-witness-step-smoke-succeeded");
            }
        }

        var status = ToStatus(decision, diagnostics);
        var considerations = BuildConsiderations(policyResult.Candidate.Score);

        return new(
            status,
            decision,
            policyResult.Candidate.Score,
            considerations,
            artifactResult.TopologyPlan.Plan,
            request.IncludeGeometryArtifact ? artifactResult.Artifact : null,
            witness,
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyDictionary<string, double> BuildConsiderations(AirChamferPolicyScore score) =>
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["geometrySupport"] = score.GeometrySupportScore,
            ["topologyRisk"] = score.TopologyRiskScore,
            ["offsetStability"] = score.OffsetStabilityScore,
            ["cornerPolicy"] = score.CornerPolicyScore,
            ["legacyDependency"] = score.LegacyDependencyScore,
            ["overallUtility"] = score.OverallUtility
        };

    private static AirChamferPrototypeStatus ToStatus(string decision, List<string> diagnostics)
    {
        if (decision == "create-convex-closed-witness" || decision == "create-convex-replacement-geometry-artifact" || decision == "plan-convex-replacement-topology") return AirChamferPrototypeStatus.Accepted;
        if (decision.StartsWith("reject-", StringComparison.Ordinal))
        {
            diagnostics.Add($"edge-v1-request-rejected:{decision}");
            return AirChamferPrototypeStatus.Rejected;
        }

        if (decision.StartsWith("fallback-", StringComparison.Ordinal)) return AirChamferPrototypeStatus.FallbackLegacy;

        diagnostics.Add($"edge-v1-request-deferred:{decision}");
        return AirChamferPrototypeStatus.Deferred;
    }
}
