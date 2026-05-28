using System.Numerics;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirChamferPatchCase(string CaseName, Vector3 EdgeStart, Vector3 EdgeEnd, Vector3 FaceANormal, Vector3 FaceBNormal, double ChamferDistance);
public sealed record AirChamferPatchArtifact(IReadOnlyList<Vector3> Vertices, IReadOnlyList<(int A, int B)> Edges, Vector3 PlaneNormal, double Area);
public sealed record AirChamferPatchTopologySummary(bool PatchProduced, int VertexCount, int EdgeCount, int FaceCount, int PlanarFaceCount, int BoundaryLoopCount, int CoedgeCount);
public sealed record AirChamferPatchRow(string CaseName, LabProfileStatus Status, bool OffsetCurveAConstructed, bool OffsetCurveBConstructed, AirChamferPatchTopologySummary Topology, AirChamferPatchArtifact? Artifact, IReadOnlyList<string> Diagnostics, string Recommendation);

public static class AirChamferPatchLab
{
    private const double Tol = 1e-9;
    public static readonly IReadOnlySet<string> AllowedRecommendations = new HashSet<string>(StringComparer.Ordinal)
    {
        "air-chamfer-patch-constructive-proof-succeeded",
        "air-chamfer-patch-needs-brep-open-shell-support",
        "air-chamfer-patch-invalid-rejected",
        "air-chamfer-patch-deferred-topology"
    };

    public static AirChamferPatchCase Canonical(double edgeLength = 10d, double distance = 1d)
    {
        var hz = (float)(edgeLength / 2d);
        return new(
            $"canonical-l{edgeLength:0.###}-d{distance:0.###}",
            new Vector3(0f, 0f, -hz),
            new Vector3(0f, 0f, hz),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            distance);
    }

    public static IReadOnlyList<AirChamferPatchRow> RunAll() =>
    [
        Run(Canonical(10d, 1d)),
        Run(Canonical(10d, 2d)),
        Run(Canonical(7.5d, 1d)),
        Run(Canonical(10d, 0d) with { CaseName = "invalid-distance-zero" }),
        Run(Canonical(10d, double.NaN) with { CaseName = "invalid-distance-nan" }),
        Run(Canonical(0d, 1d) with { CaseName = "invalid-edge-length-zero" }),
        Run(Canonical(10d, 1d) with { CaseName = "invalid-edge-non-finite", EdgeStart = new Vector3(float.NaN, 0f, 0f) }),
        Run(Canonical(10d, 1d) with { CaseName = "invalid-face-adjacency-parallel", FaceBNormal = new Vector3(1f, 0f, 0f) })
    ];

    public static AirChamferPatchRow Run(AirChamferPatchCase c)
    {
        var d = new List<string> { "edge-x2-air-chamfer-patch-lab-started" };
        d.Add("edge-x2-no-3d-boolean-used");

        if (!Finite(c.ChamferDistance) || c.ChamferDistance <= Tol)
            return Reject(c.CaseName, d, "edge-x2-invalid-distance-rejected");
        if (!Finite(c.EdgeStart) || !Finite(c.EdgeEnd))
            return Reject(c.CaseName, d, "edge-x2-invalid-edge-rejected");

        var edge = c.EdgeEnd - c.EdgeStart;
        var edgeLen = edge.Length();
        if (!Finite(edgeLen) || edgeLen <= Tol)
            return Reject(c.CaseName, d, "edge-x2-invalid-edge-rejected");

        if (!TryNormalize(c.FaceANormal, out var nA) || !TryNormalize(c.FaceBNormal, out var nB))
            return Reject(c.CaseName, d, "edge-x2-invalid-face-adjacency-rejected");

        if (!TryNormalize(edge, out var eDir))
            return Reject(c.CaseName, d, "edge-x2-invalid-edge-rejected");

        var offA = Vector3.Cross(eDir, nA);
        var offB = Vector3.Cross(nB, eDir);
        if (!TryNormalize(offA, out offA) || !TryNormalize(offB, out offB) || Math.Abs(Vector3.Dot(offA, offB)) >= 1d - 1e-8)
            return Reject(c.CaseName, d, "edge-x2-invalid-face-adjacency-rejected");

        d.Add("edge-x2-concave-planar-edge-accepted");

        var a0 = c.EdgeStart + offA * (float)c.ChamferDistance;
        var a1 = c.EdgeEnd + offA * (float)c.ChamferDistance;
        d.Add("edge-x2-offset-curve-a-constructed");
        var b0 = c.EdgeStart + offB * (float)c.ChamferDistance;
        var b1 = c.EdgeEnd + offB * (float)c.ChamferDistance;
        d.Add("edge-x2-offset-curve-b-constructed");

        var verts = new[] { a0, a1, b1, b0 };
        var normal = Vector3.Cross(a1 - a0, b0 - a0);
        if (!TryNormalize(normal, out normal))
            return Reject(c.CaseName, d, "edge-x2-invalid-face-adjacency-rejected");

        var area = 0.5d * Vector3.Cross(a1 - a0, b1 - a0).Length() + 0.5d * Vector3.Cross(b1 - a0, b0 - a0).Length();
        var artifact = new AirChamferPatchArtifact(verts, [(0, 1), (1, 2), (2, 3), (3, 0)], normal, area);
        d.Add("edge-x2-ruled-chamfer-patch-constructed");
        d.Add("edge-x2-patch-topology-captured");
        d.Add("edge-x2-step-smoke-deferred:open-patch-export-unsupported");

        return new(c.CaseName, LabProfileStatus.Succeeded, true, true, new(true, 4, 4, 1, 1, 1, 4), artifact,
            d.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(), "air-chamfer-patch-constructive-proof-succeeded");
    }

    private static AirChamferPatchRow Reject(string caseName, List<string> d, string why)
    {
        d.Add(why);
        return new(caseName, LabProfileStatus.Failed, false, false, new(false, 0, 0, 0, 0, 0, 0), null,
            d.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(), "air-chamfer-patch-invalid-rejected");
    }

    private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static bool TryNormalize(Vector3 v, out Vector3 normalized)
    {
        var len = v.Length();
        if (!float.IsFinite(len) || len <= Tol) { normalized = default; return false; }
        normalized = v / len;
        return Finite(normalized);
    }
}
