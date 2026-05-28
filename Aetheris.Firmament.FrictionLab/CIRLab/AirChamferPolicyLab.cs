using System.Numerics;

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
    bool IsOrthogonalPlanarPair = true);

public sealed record AirChamferPolicyCase(string CaseName, AirChamferPolicyRequest Request, string ExpectedDecision);
public sealed record AirChamferPolicyScore(int GeometrySupportScore, int TopologyRiskScore, int OffsetStabilityScore, int CornerPolicyScore, int LegacyDependencyScore, int OverallUtility);
public sealed record AirChamferPolicyCandidate(string CandidateName, bool Admissible, AirChamferPolicyScore Score, IReadOnlyList<string> Diagnostics, string Decision);
public sealed record AirChamferPolicyDecision(string Decision, string Recommendation, bool AllowPatchConstruction);
public sealed record AirChamferPolicyResult(AirChamferPolicyCase Case, AirChamferPolicyCandidate Candidate, AirChamferPolicyDecision Decision, AirChamferPatchRow? PatchRow, IReadOnlyList<string> Diagnostics);
public sealed record AirChamferPolicyRow(string CaseName, string Decision, AirChamferPolicyScore Score, string Recommendation, bool PatchConstructed, IReadOnlyList<string> Diagnostics);

public static class AirChamferPolicyLab
{
    private const double Tol = 1e-9;
    private const double MinStablePlanarAngleDeg = 2d;
    public static readonly IReadOnlySet<string> AllowedDecisions = new HashSet<string>(StringComparer.Ordinal)
    {
        "accept-air-chamfer-patch","fallback-legacy-chamfer","defer-nonorthogonal-policy","defer-convex-replacement-policy","defer-edge-chain-policy","defer-corner-policy","defer-unsupported-face-family","defer-legacy-dependent-topology","reject-invalid-distance","reject-invalid-edge","reject-invalid-face-adjacency","reject-ambiguous-classification"
    };

    public static IReadOnlyList<AirChamferPolicyCase> Cases() =>
    [
        new("canonical-concave-planar", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "accept-air-chamfer-patch"),
        new("nonorthogonal-concave-planar-safe", new(new(0,0,-5),new(0,0,5),new(1,0,0),Vector3.Normalize(new Vector3(1,1,0)),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,false), "accept-air-chamfer-patch"),
        new("nonorthogonal-concave-planar-shallow", new(new(0,0,-5),new(0,0,5),new(1,0,0),Vector3.Normalize(new Vector3(0.99f,0.1f,0f)),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,false), "defer-nonorthogonal-policy"),
        new("nonorthogonal-concave-planar-near-parallel", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(1,0,0.000001f),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,false), "reject-invalid-face-adjacency"),
        new("convex-planar", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Convex,true), "defer-convex-replacement-policy"),
        new("edge-chain", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,true,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "defer-edge-chain-policy"),
        new("corner-chain", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,true,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "defer-corner-policy"),
        new("cylindrical-face", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Cylindrical,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "defer-unsupported-face-family"),
        new("triangle-legacy-dependent", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,true,AirChamferRoutePreference.Legacy,AirChamferClassificationExpectation.Concave,true), "fallback-legacy-chamfer"),
        new("invalid-distance-zero", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),0d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "reject-invalid-distance"),
        new("invalid-distance-nan", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),double.NaN,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "reject-invalid-distance"),
        new("invalid-edge-zero-length", new(new(0,0,0),new(0,0,0),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "reject-invalid-edge"),
        new("invalid-edge-nonfinite", new(new(float.NaN,0,0),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "reject-invalid-edge"),
        new("invalid-face-adjacency-parallel", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(1,0,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "reject-invalid-face-adjacency"),
        new("invalid-face-adjacency-missing", new(new(0,0,-5),new(0,0,5),new(1,0,0),null,1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Concave,true), "reject-invalid-face-adjacency"),
        new("ambiguous-classification", new(new(0,0,-5),new(0,0,5),new(1,0,0),new(0,1,0),1d,AirChamferFaceFamily.Planar,false,false,false,AirChamferRoutePreference.Auto,AirChamferClassificationExpectation.Ambiguous,true), "reject-ambiguous-classification")
    ];

    public static IReadOnlyList<AirChamferPolicyRow> RunAll() => Cases().Select(Evaluate).Select(ToRow).ToArray();

    public static AirChamferPolicyResult Evaluate(AirChamferPolicyCase c)
    {
        var d = new List<string> { "edge-x2-1-policy-lab-started", "edge-x2-1-no-production-behavior-changed", "edge-x2-1-no-3d-boolean-used" };
        var r = c.Request;
        string decision;
        if (!Finite(r.ChamferDistance) || r.ChamferDistance <= Tol) decision = "reject-invalid-distance";
        else if (!Finite(r.EdgeStart) || !Finite(r.EdgeEnd) || !Finite((r.EdgeEnd-r.EdgeStart).Length()) || (r.EdgeEnd-r.EdgeStart).Length() <= Tol) decision = "reject-invalid-edge";
        else if (r.FaceANormal is null || r.FaceBNormal is null || !TryNormalize(r.FaceANormal.Value, out var na) || !TryNormalize(r.FaceBNormal.Value, out var nb) || Math.Abs(Vector3.Dot(na, nb)) >= 1d-1e-8) decision = "reject-invalid-face-adjacency";
        else if (r.ClassificationExpectation == AirChamferClassificationExpectation.Ambiguous) decision = "reject-ambiguous-classification";
        else if (r.FaceFamily != AirChamferFaceFamily.Planar) decision = "defer-unsupported-face-family";
        else if (r.IsCornerChain) decision = "defer-corner-policy";
        else if (r.IsEdgeChain) decision = "defer-edge-chain-policy";
        else if (r.ClassificationExpectation == AirChamferClassificationExpectation.Convex) decision = "defer-convex-replacement-policy";
        else if (r.LegacyDependency && r.RoutePreference == AirChamferRoutePreference.Legacy) decision = "fallback-legacy-chamfer";
        else if (r.LegacyDependency) decision = "defer-legacy-dependent-topology";
        else if (!r.IsOrthogonalPlanarPair && !HasStableNonOrthogonalAdmissibility(r, out var nonOrthReason))
        {
            decision = nonOrthReason == "near-parallel" ? "reject-invalid-face-adjacency" : "defer-nonorthogonal-policy";
            d.Add($"edge-x2-2-policy-nonorthogonal-concave-deferred:{nonOrthReason}");
        }
        else decision = "accept-air-chamfer-patch";

        var score = Score(r, decision);
        d.Add($"edge-x2-1-score-geometry-support:{score.GeometrySupportScore}");
        d.Add($"edge-x2-1-score-topology-risk:{score.TopologyRiskScore}");
        d.Add($"edge-x2-1-score-offset-stability:{score.OffsetStabilityScore}");
        d.Add($"edge-x2-1-score-corner-policy:{score.CornerPolicyScore}");
        d.Add($"edge-x2-1-score-legacy-dependency:{score.LegacyDependencyScore}");
        d.Add($"edge-x2-1-decision:{decision}");
        d.Add("edge-x2-1-policy-case-evaluated");

        var recommendation = decision switch
        {
            "accept-air-chamfer-patch" => "air-chamfer-policy-ready-for-canonical-patch",
            "fallback-legacy-chamfer" => "air-chamfer-policy-keep-legacy-route",
            "reject-invalid-distance" or "reject-invalid-edge" or "reject-invalid-face-adjacency" or "reject-ambiguous-classification" => "air-chamfer-policy-invalid-rejected",
            "defer-nonorthogonal-policy" => "air-chamfer-policy-needs-nonorthogonal-lab",
            "defer-convex-replacement-policy" => "air-chamfer-policy-needs-convex-replacement-lab",
            _ => "air-chamfer-policy-needs-chain-corner-policy"
        };

        AirChamferPatchRow? patch = null;
        if (decision == "accept-air-chamfer-patch")
        {
            d.Add("edge-x2-1-air-chamfer-patch-admitted");
            if (!r.IsOrthogonalPlanarPair) d.Add("edge-x2-2-policy-nonorthogonal-concave-admitted");
            var patchCase = new AirChamferPatchCase(c.CaseName, r.EdgeStart, r.EdgeEnd, r.FaceANormal!.Value, r.FaceBNormal!.Value, r.ChamferDistance);
            patch = AirChamferPatchLab.Run(patchCase);
        }
        else if (decision.StartsWith("reject-", StringComparison.Ordinal)) d.Add($"edge-x2-1-air-chamfer-patch-rejected:{decision}");
        else if (decision == "fallback-legacy-chamfer") d.Add("edge-x2-1-legacy-route-preferred:legacy-dependent-topology");
        else d.Add($"edge-x2-1-air-chamfer-patch-deferred:{decision}");

        var candidate = new AirChamferPolicyCandidate("air-chamfer-policy-candidate", decision == "accept-air-chamfer-patch", score, d.ToArray(), decision);
        return new(c, candidate, new(decision, recommendation, decision == "accept-air-chamfer-patch"), patch, d.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static AirChamferPolicyRow ToRow(AirChamferPolicyResult r) => new(r.Case.CaseName, r.Decision.Decision, r.Candidate.Score, r.Decision.Recommendation, r.PatchRow?.Topology.PatchProduced == true, r.Diagnostics);
    private static AirChamferPolicyScore Score(AirChamferPolicyRequest r, string decision)
    {
        var gs = r.FaceFamily == AirChamferFaceFamily.Planar ? 100 : 35;
        if (!r.IsOrthogonalPlanarPair) gs -= 25;
        if (r.ClassificationExpectation == AirChamferClassificationExpectation.Convex) gs -= 30;
        var tr = r.LegacyDependency ? 90 : 10;
        var os = (Finite(r.ChamferDistance) && r.ChamferDistance > 0d) ? (r.IsOrthogonalPlanarPair ? 100 : 60) : 0;
        var cp = r.IsCornerChain ? 0 : r.IsEdgeChain ? 20 : 100;
        var ld = r.LegacyDependency ? 0 : 100;
        var overall = (int)Math.Round(gs * 0.30 + (100 - tr) * 0.20 + os * 0.20 + cp * 0.15 + ld * 0.15);
        if (decision.StartsWith("reject-", StringComparison.Ordinal)) overall = Math.Min(overall, 10);
        return new(gs, tr, os, cp, ld, overall);
    }
    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static bool TryNormalize(Vector3 v, out Vector3 normalized)
    {
        var len = v.Length(); if (!float.IsFinite(len) || len <= Tol) { normalized = default; return false; }
        normalized = v / len; return Finite(normalized);
    }

    private static bool HasStableNonOrthogonalAdmissibility(AirChamferPolicyRequest r, out string reason)
    {
        reason = "unsupported";
        if (r.FaceANormal is null || r.FaceBNormal is null) { reason = "missing-face-normal"; return false; }
        if (!TryNormalize(r.FaceANormal.Value, out var a) || !TryNormalize(r.FaceBNormal.Value, out var b)) { reason = "non-finite-normal"; return false; }
        var angle = Math.Acos(Math.Clamp(Vector3.Dot(a, b), -1f, 1f)) * (180d / Math.PI);
        if (!Finite(angle)) { reason = "non-finite-angle"; return false; }
        if (angle < MinStablePlanarAngleDeg || (180d - angle) < MinStablePlanarAngleDeg) { reason = "near-parallel"; return false; }
        if (angle < 20d) { reason = "shallow-angle-unstable"; return false; }
        return true;
    }
}
