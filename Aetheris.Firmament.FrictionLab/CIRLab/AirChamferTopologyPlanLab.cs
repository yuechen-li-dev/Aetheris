using System.Numerics;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirChamferTopologyPlanCase(string CaseName, AirChamferPolicyRequest Request, string ExpectedDecision);
public sealed record AirChamferTopologyPlan(
    string ReplacementMode,
    Vector3 TargetEdgeStart,
    Vector3 TargetEdgeEnd,
    Vector3 EdgeDirection,
    Vector3 FaceANormal,
    Vector3 FaceBNormal,
    double ChamferDistance,
    string ConvexityClassification,
    string FaceADescriptor,
    string FaceBDescriptor,
    int OriginalTargetEdgeCount,
    int OffsetCurveCount,
    int NewChamferFaceCount,
    int NewTransitionEdgeCount,
    int AdjacentFaceAffectedCount,
    int CornerPatchCount,
    bool CornerPatchesDeferred,
    bool OriginalEdgeMarkedForReplacement,
    bool FaceATrimPlanned,
    bool FaceBTrimPlanned,
    bool GeometryEmissionPerformed);

public sealed record AirChamferTopologyPlanResult(
    AirChamferTopologyPlanCase Case,
    AirChamferPolicyResult Policy,
    AirChamferTopologyPlan? Plan,
    string Decision,
    string Recommendation,
    IReadOnlyList<string> Diagnostics);

public sealed record AirChamferTopologyPlanRow(
    string CaseName,
    string Decision,
    string Recommendation,
    bool PlanProduced,
    int OffsetCurveCount,
    int NewChamferFaceCount,
    int AdjacentFaceAffectedCount,
    int NewTransitionEdgeCount,
    int CornerPatchCount,
    bool GeometryEmissionPerformed,
    IReadOnlyList<string> Diagnostics);

public static class AirChamferTopologyPlanLab
{
    public static readonly IReadOnlySet<string> AllowedRecommendations = new HashSet<string>(StringComparer.Ordinal)
    {
        "air-chamfer-topology-plan-ready-for-geometry-lab",
        "air-chamfer-topology-plan-needs-policy-hardening",
        "air-chamfer-topology-plan-rejected-invalid",
        "air-chamfer-topology-plan-deferred-chain-or-corner",
        "air-chamfer-topology-plan-keep-legacy-route"
    };

    public static IReadOnlyList<AirChamferTopologyPlanCase> Cases() =>
    [
        new("canonical-orthogonal-convex-planar-single-edge", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,true,10d), "plan-convex-replacement-topology"),
        new("nonorthogonal-convex-planar-single-edge", new(new(0,0,-5),new(0,0,5),new(1,0,0),Vector3.Normalize(new Vector3(1,1,0)),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,false,10d), "plan-convex-replacement-topology"),
        new("convex-planar-unsafe-envelope", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),8d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,true,6d), "reject-unsafe-offset-envelope"),
        new("invalid-distance", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),0d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,true,10d), "reject-invalid-distance"),
        new("invalid-edge", new(new(0,0,0),new(0,0,0),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,true,10d), "reject-invalid-edge"),
        new("invalid-face-adjacency", new(new(0,0,-5),new(0,0,5),new(1,0,0),null,1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,true,10d), "reject-invalid-face-adjacency"),
        new("ambiguous-classification", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Ambiguous,true,10d), "reject-ambiguous-classification"),
        new("edge-chain", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,true,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true,10d), "defer-edge-chain-policy"),
        new("corner-chain", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,true,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true,10d), "defer-corner-policy"),
        new("triangle-legacy-dependent", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,true,AirChamferRoutePreference.Legacy,AirChamferClassificationExpectation.Concave,true,10d), "fallback-legacy-chamfer")
    ];

    public static IReadOnlyList<AirChamferTopologyPlanRow> RunAll() => Cases().Select(Evaluate).Select(ToRow).ToArray();

    public static AirChamferTopologyPlanResult Evaluate(AirChamferTopologyPlanCase c)
    {
        var diagnostics = new List<string> { "edge-x4-topology-plan-lab-started", "edge-x4-judgment-engine-used", "edge-x4-no-geometry-emission", "edge-x4-no-production-behavior-changed", "edge-x4-no-3d-boolean-used" };
        var policyCase = new AirChamferPolicyCase(c.CaseName, c.Request, c.ExpectedDecision);
        var policy = AirChamferPolicyLab.Evaluate(policyCase);
        diagnostics.AddRange(policy.Diagnostics.Where(d => d.Contains("judgment", StringComparison.Ordinal)));

        AirChamferTopologyPlan? plan = null;
        var decision = policy.Decision.Decision;
        if (decision == "defer-convex-replacement-geometry")
        {
            diagnostics.Add("edge-x4-policy-admitted-convex-plan");
            plan = CreatePlan(c.Request, diagnostics);
            decision = "plan-convex-replacement-topology";
            diagnostics.Add("edge-x4-topology-plan-created");
        }
        else if (decision.StartsWith("reject-", StringComparison.Ordinal))
        {
            diagnostics.Add($"edge-x4-policy-rejected-before-plan:{decision}");
        }
        else
        {
            diagnostics.Add($"edge-x4-policy-deferred-before-plan:{decision}");
        }

        var recommendation = decision switch
        {
            "plan-convex-replacement-topology" => "air-chamfer-topology-plan-ready-for-geometry-lab",
            "defer-edge-chain-policy" or "defer-corner-policy" => "air-chamfer-topology-plan-deferred-chain-or-corner",
            "fallback-legacy-chamfer" or "defer-legacy-dependent-topology" => "air-chamfer-topology-plan-keep-legacy-route",
            var d when d.StartsWith("reject-", StringComparison.Ordinal) => "air-chamfer-topology-plan-rejected-invalid",
            _ => "air-chamfer-topology-plan-needs-policy-hardening"
        };

        return new(c, policy, plan, decision, recommendation, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static AirChamferTopologyPlan CreatePlan(AirChamferPolicyRequest request, List<string> diagnostics)
    {
        var edgeDirection = Vector3.Normalize(request.EdgeEnd - request.EdgeStart);
        diagnostics.Add("edge-x4-offset-curve-a-planned");
        diagnostics.Add("edge-x4-offset-curve-b-planned");
        diagnostics.Add("edge-x4-original-edge-marked-for-replacement");
        diagnostics.Add("edge-x4-adjacent-face-a-trim-planned");
        diagnostics.Add("edge-x4-adjacent-face-b-trim-planned");
        diagnostics.Add("edge-x4-chamfer-face-planned");
        diagnostics.Add("edge-x4-transition-edges-planned");
        diagnostics.Add("edge-x4-corner-patches-deferred");

        return new(
            "single-edge-convex-planar",
            request.EdgeStart,
            request.EdgeEnd,
            edgeDirection,
            Vector3.Normalize(request.FaceANormal!.Value),
            Vector3.Normalize(request.FaceBNormal!.Value),
            request.ChamferDistance,
            "convex",
            "adjacent-face-a-planar",
            "adjacent-face-b-planar",
            1,
            2,
            1,
            2,
            2,
            0,
            true,
            true,
            true,
            true,
            false);
    }

    private static AirChamferTopologyPlanRow ToRow(AirChamferTopologyPlanResult r) =>
        new(r.Case.CaseName, r.Decision, r.Recommendation, r.Plan is not null, r.Plan?.OffsetCurveCount ?? 0, r.Plan?.NewChamferFaceCount ?? 0, r.Plan?.AdjacentFaceAffectedCount ?? 0, r.Plan?.NewTransitionEdgeCount ?? 0, r.Plan?.CornerPatchCount ?? 0, r.Plan?.GeometryEmissionPerformed ?? false, r.Diagnostics);
}
