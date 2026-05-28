using System.Numerics;
using Aetheris.Kernel.Core.Judgment;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum AirChamferFaceFamily { Planar, Cylindrical, Unsupported }
public enum AirChamferRoutePreference { Auto, AirChamfer, Legacy }
public enum AirChamferClassificationExpectation { Auto, Concave, Convex, Ambiguous }

public sealed record AirChamferPolicyRequest(
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
    bool IsOrthogonalPlanarPair = true,
    double? LocalFeatureEnvelope = null);

public sealed record AirChamferPolicyCase(string CaseName, AirChamferPolicyRequest Request, string ExpectedDecision);
public sealed record AirChamferPolicyScore(int GeometrySupportScore, int TopologyRiskScore, int OffsetStabilityScore, int CornerPolicyScore, int LegacyDependencyScore, int OverallUtility);
public sealed record AirChamferPolicyCandidate(string CandidateName, bool Admissible, AirChamferPolicyScore Score, IReadOnlyList<string> Diagnostics, string Decision);
public sealed record AirChamferPolicyDecision(string Decision, string Recommendation, bool AllowPatchConstruction);
public sealed record AirChamferPolicyResult(AirChamferPolicyCase Case, AirChamferPolicyCandidate Candidate, AirChamferPolicyDecision Decision, AirChamferPatchRow? PatchRow, IReadOnlyList<string> Diagnostics);
public sealed record AirChamferPolicyRow(string CaseName, string Decision, AirChamferPolicyScore Score, string Recommendation, bool PatchConstructed, IReadOnlyList<string> Diagnostics);

internal sealed record AirChamferJudgmentContext(AirChamferPolicyRequest Request, IReadOnlyDictionary<string, double> Considerations);

public static class AirChamferPolicyLab
{
    private const double Tol = 1e-9;
    private const double MinStablePlanarAngleDeg = 2d;
    private static readonly JudgmentEngine<AirChamferJudgmentContext> Engine = new();

    public static readonly IReadOnlySet<string> AllowedDecisions = new HashSet<string>(StringComparer.Ordinal)
    {
        "accept-air-chamfer-patch","fallback-legacy-chamfer","defer-convex-replacement-policy","defer-convex-replacement-geometry","defer-edge-chain-policy","defer-corner-policy","defer-unsupported-face-family","defer-legacy-dependent-topology","reject-invalid-distance","reject-invalid-edge","reject-invalid-face-adjacency","reject-ambiguous-classification","reject-unsafe-offset-envelope"
    };

    public static IReadOnlyList<AirChamferPolicyCase> Cases() =>
    [
        new("canonical-concave-planar", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true,10d), "accept-air-chamfer-patch"),
        new("nonorthogonal-concave-planar-safe", new(new(0,0,-5),new(0,0,5),new(1,0,0),Vector3.Normalize(new Vector3(1,1,0)),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,false,10d), "accept-air-chamfer-patch"),
        new("convex-planar", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,true,10d), "defer-convex-replacement-geometry"),
        new("convex-planar-unsafe-envelope", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),8d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,true,6d), "reject-unsafe-offset-envelope"),
        new("edge-chain", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,true,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true,10d), "defer-edge-chain-policy"),
        new("corner-chain", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,true,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true,10d), "defer-corner-policy"),
        new("triangle-legacy-dependent", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,true,AirChamferRoutePreference.Legacy,AirChamferClassificationExpectation.Concave,true,10d), "fallback-legacy-chamfer"),
        new("invalid-distance-zero", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),0d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true,10d), "reject-invalid-distance"),
        new("invalid-edge-zero-length", new(new(0,0,0),new(0,0,0),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true,10d), "reject-invalid-edge"),
        new("invalid-face-adjacency-missing", new(new(0,0,-5),new(0,0,5),new(1,0,0),null,1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true,10d), "reject-invalid-face-adjacency"),
        new("ambiguous-classification", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Ambiguous,true,10d), "reject-ambiguous-classification")
    ];

    public static IReadOnlyList<AirChamferPolicyRow> RunAll() => Cases().Select(Evaluate).Select(ToRow).ToArray();

    public static AirChamferPolicyResult Evaluate(AirChamferPolicyCase c)
    {
        var diagnostics = new List<string> { "edge-x3-policy-lab-started", "edge-x3-judgment-engine-used", "edge-x3-no-production-behavior-changed", "edge-x3-no-3d-boolean-used" };
        var considerations = BuildConsiderations(c.Request);
        foreach (var kvp in considerations.OrderBy(x => x.Key, StringComparer.Ordinal))
            diagnostics.Add($"edge-x3-judgment-consideration:{kvp.Key}:{kvp.Value:F4}");

        var context = new AirChamferJudgmentContext(c.Request, considerations);
        var candidates = BuildCandidates(c.Request);
        diagnostics.AddRange(candidates.Select(x => $"edge-x3-judgment-candidate-created:{x.Name}"));

        var result = Engine.Evaluate(context, candidates);
        string decision;
        if (result.Selection is { } selection)
        {
            decision = selection.Candidate.Name;
            diagnostics.Add($"edge-x3-judgment-score:{selection.Candidate.Name}:{selection.Score:F4}");
        }
        else
        {
            decision = "reject-invalid-face-adjacency";
            diagnostics.Add("edge-x3-judgment-score:<none>:0.0000");
        }

        if (decision == "defer-convex-replacement-geometry")
            diagnostics.Add("edge-x3-convex-replacement-deferred:no-topology-replacement-plan");
        if (decision == "reject-unsafe-offset-envelope")
            diagnostics.Add("edge-x3-convex-replacement-rejected:unsafe-offset-envelope");
        if (decision is "accept-air-chamfer-patch" && c.CaseName.Contains("concave", StringComparison.Ordinal))
            diagnostics.Add("edge-x3-concave-policy-regression-check-passed");
        if (decision is "fallback-legacy-chamfer" or "defer-legacy-dependent-topology")
            diagnostics.Add("edge-x3-legacy-route-preferred:legacy-dependent-topology");
        diagnostics.Add($"edge-x3-decision:{decision}");

        var score = ToScore(considerations, decision);
        var recommendation = decision switch
        {
            "accept-air-chamfer-patch" => "air-chamfer-policy-ready-for-patch",
            "defer-convex-replacement-geometry" or "defer-convex-replacement-policy" => "air-chamfer-policy-needs-convex-replacement-plan",
            "reject-unsafe-offset-envelope" => "air-chamfer-policy-unsafe-offset-rejected",
            _ => "air-chamfer-policy-deferred-or-rejected"
        };

        AirChamferPatchRow? patch = null;
        if (decision == "accept-air-chamfer-patch")
        {
            var r = c.Request;
            var patchCase = new AirChamferPatchCase(c.CaseName, r.EdgeStart, r.EdgeEnd, r.FaceANormal!.Value, r.FaceBNormal!.Value, r.ChamferDistance);
            patch = AirChamferPatchLab.Run(patchCase);
        }

        var candidate = new AirChamferPolicyCandidate(decision, decision == "accept-air-chamfer-patch", score, diagnostics, decision);
        return new(c, candidate, new(decision, recommendation, decision == "accept-air-chamfer-patch"), patch, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<JudgmentCandidate<AirChamferJudgmentContext>> BuildCandidates(AirChamferPolicyRequest request)
    {
        var decisions = new[]
        {
            "accept-air-chamfer-patch","defer-convex-replacement-geometry","defer-convex-replacement-policy","defer-edge-chain-policy","defer-corner-policy","defer-legacy-dependent-topology","fallback-legacy-chamfer","reject-invalid-distance","reject-invalid-edge","reject-invalid-face-adjacency","reject-ambiguous-classification","reject-unsafe-offset-envelope"
        };

        return decisions.Select((name, idx) => new JudgmentCandidate<AirChamferJudgmentContext>(
            name,
            ctx => IsAdmissible(name, ctx.Request),
            ctx => CandidateScore(name, ctx.Considerations),
            null,
            idx)).ToArray();
    }

    private static bool IsAdmissible(string decision, AirChamferPolicyRequest r)
    {
        return decision switch
        {
            "reject-invalid-distance" => !Finite(r.ChamferDistance) || r.ChamferDistance <= Tol,
            "reject-invalid-edge" => !Finite(r.EdgeStart) || !Finite(r.EdgeEnd) || !Finite((r.EdgeEnd-r.EdgeStart).Length()) || (r.EdgeEnd-r.EdgeStart).Length() <= Tol,
            "reject-invalid-face-adjacency" => r.FaceANormal is null || r.FaceBNormal is null || !TryNormalize(r.FaceANormal.Value, out var na) || !TryNormalize(r.FaceBNormal.Value, out var nb) || Math.Abs(Vector3.Dot(na, nb)) >= 1d-1e-8,
            "reject-ambiguous-classification" => r.ClassificationExpectation == AirChamferClassificationExpectation.Ambiguous,
            "defer-corner-policy" => r.IsCornerChain,
            "defer-edge-chain-policy" => r.IsEdgeChain,
            "fallback-legacy-chamfer" => r.LegacyDependency && r.RoutePreference == AirChamferRoutePreference.Legacy,
            "defer-legacy-dependent-topology" => r.LegacyDependency,
            "reject-unsafe-offset-envelope" => r.ClassificationExpectation == AirChamferClassificationExpectation.Convex && r.LocalFeatureEnvelope is > 0d && r.ChamferDistance > r.LocalFeatureEnvelope.Value,
            "defer-convex-replacement-geometry" => r.ClassificationExpectation == AirChamferClassificationExpectation.Convex,
            "defer-convex-replacement-policy" => r.ClassificationExpectation == AirChamferClassificationExpectation.Convex,
            "accept-air-chamfer-patch" => r.FaceFamily == AirChamferFaceFamily.Planar && !r.IsEdgeChain && !r.IsCornerChain && !r.LegacyDependency && r.ClassificationExpectation == AirChamferClassificationExpectation.Concave,
            _ => false
        };
    }

    private static double CandidateScore(string decision, IReadOnlyDictionary<string, double> c)
    {
        var g = c["geometry-support"];
        var o = c["offset-stability"];
        var cp = c["corner-policy"];
        var l = c["legacy-readiness"];
        return decision switch
        {
            "accept-air-chamfer-patch" => g * 0.4 + o * 0.3 + cp * 0.15 + l * 0.15,
            "defer-convex-replacement-geometry" => 88 + g * 0.1,
            "defer-convex-replacement-policy" => 70,
            "reject-unsafe-offset-envelope" => 95,
            "fallback-legacy-chamfer" => 85,
            "defer-edge-chain-policy" or "defer-corner-policy" => 82,
            "defer-legacy-dependent-topology" => 83,
            _ => 90
        };
    }

    private static IReadOnlyDictionary<string, double> BuildConsiderations(AirChamferPolicyRequest r)
    {
        var geometrySupport = r.FaceFamily == AirChamferFaceFamily.Planar ? 1d : 0d;
        var offsetStability = (Finite(r.ChamferDistance) && r.ChamferDistance > Tol) ? 1d : 0d;
        if (r.LocalFeatureEnvelope is > 0d && r.ChamferDistance > r.LocalFeatureEnvelope.Value) offsetStability = 0d;
        var cornerPolicy = (r.IsCornerChain || r.IsEdgeChain) ? 0d : 1d;
        var legacyReadiness = r.LegacyDependency ? 0d : 1d;
        return new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["geometry-support"] = geometrySupport,
            ["offset-stability"] = offsetStability,
            ["corner-policy"] = cornerPolicy,
            ["legacy-readiness"] = legacyReadiness
        };
    }

    private static AirChamferPolicyScore ToScore(IReadOnlyDictionary<string, double> c, string decision)
    {
        var gs = (int)Math.Round(c["geometry-support"] * 100d);
        var os = (int)Math.Round(c["offset-stability"] * 100d);
        var cp = (int)Math.Round(c["corner-policy"] * 100d);
        var ld = (int)Math.Round(c["legacy-readiness"] * 100d);
        var tr = decision.StartsWith("reject-", StringComparison.Ordinal) ? 100 : 20;
        var overall = (int)Math.Round(gs * 0.35 + os * 0.25 + cp * 0.20 + ld * 0.20);
        return new(gs, tr, os, cp, ld, overall);
    }

    private static AirChamferPolicyRow ToRow(AirChamferPolicyResult r) => new(r.Case.CaseName, r.Decision.Decision, r.Candidate.Score, r.Decision.Recommendation, r.PatchRow?.Topology.PatchProduced == true, r.Diagnostics);
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static bool TryNormalize(Vector3 v, out Vector3 normalized)
    {
        var len = v.Length(); if (!float.IsFinite(len) || len <= Tol) { normalized = default; return false; }
        normalized = v / len; return Finite(normalized);
    }
}
