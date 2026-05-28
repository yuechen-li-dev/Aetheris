using System.Numerics;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirChamferGeometryArtifactFace(
    string Name,
    string FaceFamily,
    Vector3 Normal,
    double Area,
    IReadOnlyList<Vector3> BoundaryVertices);

public sealed record AirChamferGeometryArtifactEdge(
    string Name,
    Vector3 Start,
    Vector3 End,
    bool IsOffsetCurve,
    bool IsTransitionEdge);

public sealed record AirChamferGeometryArtifact(
    string ReplacementMode,
    AirChamferGeometryArtifactFace TrimmedFacePatchA,
    AirChamferGeometryArtifactFace TrimmedFacePatchB,
    AirChamferGeometryArtifactFace ChamferFace,
    IReadOnlyList<AirChamferGeometryArtifactEdge> OffsetCurves,
    IReadOnlyList<AirChamferGeometryArtifactEdge> TransitionEdges,
    bool OriginalEdgeMarkedForReplacement,
    bool CornerPatchesDeferred,
    int CornerPatchCount,
    bool IsOpenLocalWitness,
    int FaceCount,
    int PlanarFaceCount,
    int ChamferFaceCount,
    int AffectedAdjacentFaceCount,
    int OffsetCurveCount,
    int TransitionEdgeCount);

public sealed record AirChamferGeometryArtifactResult(
    AirChamferTopologyPlanCase Case,
    AirChamferTopologyPlanResult TopologyPlan,
    AirChamferGeometryArtifact? Artifact,
    string Decision,
    string Recommendation,
    IReadOnlyList<string> Diagnostics);

public sealed record AirChamferGeometryArtifactRow(
    string CaseName,
    string Decision,
    string Recommendation,
    bool ArtifactProduced,
    int FaceCount,
    int PlanarFaceCount,
    int ChamferFaceCount,
    int OffsetCurveCount,
    int TransitionEdgeCount,
    int CornerPatchCount,
    IReadOnlyList<string> Diagnostics);

public static class AirChamferGeometryArtifactLab
{
    private const float Tol = 1e-6f;

    public static readonly IReadOnlySet<string> AllowedRecommendations = new HashSet<string>(StringComparer.Ordinal)
    {
        "air-chamfer-geometry-artifact-ready-for-closed-witness-lab",
        "air-chamfer-geometry-artifact-needs-topology-plan-hardening",
        "air-chamfer-geometry-artifact-rejected-invalid",
        "air-chamfer-geometry-artifact-deferred-chain-or-corner",
        "air-chamfer-geometry-artifact-keep-legacy-route"
    };

    public static IReadOnlyList<AirChamferTopologyPlanCase> Cases() => AirChamferTopologyPlanLab.Cases();
    public static IReadOnlyList<AirChamferGeometryArtifactRow> RunAll() => Cases().Select(Evaluate).Select(ToRow).ToArray();

    public static AirChamferGeometryArtifactResult Evaluate(AirChamferTopologyPlanCase c)
    {
        var diagnostics = new List<string>
        {
            "edge-x5-geometry-artifact-lab-started",
            "edge-x5-judgment-engine-used",
            "edge-x5-no-production-behavior-changed",
            "edge-x5-no-3d-boolean-used"
        };

        var topology = AirChamferTopologyPlanLab.Evaluate(c);
        diagnostics.AddRange(topology.Diagnostics.Where(d => d.Contains("judgment", StringComparison.Ordinal)));

        AirChamferGeometryArtifact? artifact = null;
        var decision = topology.Decision;
        if (topology.Plan is { } plan && decision == "plan-convex-replacement-topology")
        {
            diagnostics.Add("edge-x5-topology-plan-created");
            artifact = BuildArtifact(plan, diagnostics);
            decision = "create-convex-replacement-geometry-artifact";
            diagnostics.Add("edge-x5-geometry-artifact-created");
            diagnostics.Add("edge-x5-step-smoke-deferred:open-local-artifact-export-unsupported");
        }

        var recommendation = decision switch
        {
            "create-convex-replacement-geometry-artifact" => "air-chamfer-geometry-artifact-ready-for-closed-witness-lab",
            "defer-edge-chain-policy" or "defer-corner-policy" => "air-chamfer-geometry-artifact-deferred-chain-or-corner",
            "fallback-legacy-chamfer" or "defer-legacy-dependent-topology" => "air-chamfer-geometry-artifact-keep-legacy-route",
            var d when d.StartsWith("reject-", StringComparison.Ordinal) => "air-chamfer-geometry-artifact-rejected-invalid",
            _ => "air-chamfer-geometry-artifact-needs-topology-plan-hardening"
        };

        return new(c, topology, artifact, decision, recommendation, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static AirChamferGeometryArtifact BuildArtifact(AirChamferTopologyPlan plan, List<string> diagnostics)
    {
        var edgeDirection = Vector3.Normalize(plan.TargetEdgeEnd - plan.TargetEdgeStart);
        var faceAOffset = Vector3.Normalize(plan.FaceANormal) * (float)plan.ChamferDistance;
        var faceBOffset = Vector3.Normalize(plan.FaceBNormal) * (float)plan.ChamferDistance;

        var a0 = plan.TargetEdgeStart + faceAOffset;
        var a1 = plan.TargetEdgeEnd + faceAOffset;
        var b0 = plan.TargetEdgeStart + faceBOffset;
        var b1 = plan.TargetEdgeEnd + faceBOffset;

        diagnostics.Add("edge-x5-offset-curves-materialized");

        var offsetA = new AirChamferGeometryArtifactEdge("offset-curve-a", a0, a1, true, false);
        var offsetB = new AirChamferGeometryArtifactEdge("offset-curve-b", b0, b1, true, false);
        var transition0 = new AirChamferGeometryArtifactEdge("transition-edge-start", a0, b0, false, true);
        var transition1 = new AirChamferGeometryArtifactEdge("transition-edge-end", a1, b1, false, true);
        diagnostics.Add("edge-x5-transition-edges-materialized");

        var chamferNormal = Vector3.Normalize(Vector3.Cross(a1 - a0, b0 - a0));
        var chamferArea = TriangleArea(a0, a1, b1) + TriangleArea(a0, b1, b0);

        var trimmedFaceA = new AirChamferGeometryArtifactFace("trimmed-face-a", "planar", Vector3.Normalize(plan.FaceANormal), SegmentLength(a0, a1) * plan.ChamferDistance, [a0, a1]);
        var trimmedFaceB = new AirChamferGeometryArtifactFace("trimmed-face-b", "planar", Vector3.Normalize(plan.FaceBNormal), SegmentLength(b0, b1) * plan.ChamferDistance, [b0, b1]);
        var chamferFace = new AirChamferGeometryArtifactFace("chamfer-face", "planar", chamferNormal, chamferArea, [a0, a1, b1, b0]);

        diagnostics.Add("edge-x5-trimmed-face-a-artifact-created");
        diagnostics.Add("edge-x5-trimmed-face-b-artifact-created");
        diagnostics.Add("edge-x5-chamfer-face-artifact-created");
        diagnostics.Add("edge-x5-original-edge-marked-for-replacement");
        diagnostics.Add("edge-x5-corner-patches-deferred");

        ValidateArtifact(edgeDirection, offsetA, offsetB, transition0, transition1, trimmedFaceA, trimmedFaceB, chamferFace, diagnostics);

        return new(
            plan.ReplacementMode,
            trimmedFaceA,
            trimmedFaceB,
            chamferFace,
            [offsetA, offsetB],
            [transition0, transition1],
            OriginalEdgeMarkedForReplacement: true,
            CornerPatchesDeferred: true,
            CornerPatchCount: 0,
            IsOpenLocalWitness: true,
            FaceCount: 3,
            PlanarFaceCount: 3,
            ChamferFaceCount: 1,
            AffectedAdjacentFaceCount: 2,
            OffsetCurveCount: 2,
            TransitionEdgeCount: 2);
    }

    private static void ValidateArtifact(Vector3 edgeDirection, AirChamferGeometryArtifactEdge offsetA, AirChamferGeometryArtifactEdge offsetB, AirChamferGeometryArtifactEdge transition0, AirChamferGeometryArtifactEdge transition1, AirChamferGeometryArtifactFace faceA, AirChamferGeometryArtifactFace faceB, AirChamferGeometryArtifactFace chamfer, List<string> diagnostics)
    {
        if (!Finite(faceA.Normal) || !Finite(faceB.Normal) || !Finite(chamfer.Normal)) throw new InvalidOperationException("Non-finite normals in artifact.");
        if (!(faceA.Area > 0d && faceB.Area > 0d && chamfer.Area > 0d)) throw new InvalidOperationException("Non-positive area in artifact.");
        if (!IsParallel(offsetA.End - offsetA.Start, edgeDirection) || !IsParallel(offsetB.End - offsetB.Start, edgeDirection)) throw new InvalidOperationException("Offset curves are not parallel to edge.");
        if (!AlmostEqual(transition0.Start, offsetA.Start) || !AlmostEqual(transition0.End, offsetB.Start) || !AlmostEqual(transition1.Start, offsetA.End) || !AlmostEqual(transition1.End, offsetB.End)) throw new InvalidOperationException("Transition edges do not connect offset endpoints.");

        diagnostics.Add("edge-x5-artifact-orientation-validated");
        diagnostics.Add("edge-x5-artifact-area-validated");
    }

    private static AirChamferGeometryArtifactRow ToRow(AirChamferGeometryArtifactResult r)
        => new(r.Case.CaseName, r.Decision, r.Recommendation, r.Artifact is not null, r.Artifact?.FaceCount ?? 0, r.Artifact?.PlanarFaceCount ?? 0, r.Artifact?.ChamferFaceCount ?? 0, r.Artifact?.OffsetCurveCount ?? 0, r.Artifact?.TransitionEdgeCount ?? 0, r.Artifact?.CornerPatchCount ?? 0, r.Diagnostics);

    private static double TriangleArea(Vector3 a, Vector3 b, Vector3 c) => 0.5d * Vector3.Cross(b - a, c - a).Length();
    private static double SegmentLength(Vector3 a, Vector3 b) => (b - a).Length();
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static bool IsParallel(Vector3 a, Vector3 b) => Vector3.Cross(a, b).Length() <= Tol;
    private static bool AlmostEqual(Vector3 a, Vector3 b) => (a - b).Length() <= Tol;
}
