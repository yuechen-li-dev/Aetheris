using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record PrismProfileParityCase(string Name, PrismKind Kind, double SizeA, double SizeB, double Height, bool IsValidExpected);
public enum PrismKind { Triangle, Hex }
public sealed record PrismProfileTopologySummary(bool BodyProduced, int VertexCount, int EdgeCount, int FaceCount, int PlanarFaceCount, int CylindricalFaceCount, int LoopCount, int CoedgeCount, (double X,double Y,double Z) Min, (double X,double Y,double Z) Max);
public sealed record PrismProfileStepSmokeSummary(bool Exported, IReadOnlyList<string> PresentMarkers, IReadOnlyList<string> MissingMarkers, bool ContainsBrepWithVoids, bool ContainsCylindricalSurface);
public sealed record PrismProfileParityRow(string CaseName, PrismKind Kind, bool IsValidInput, bool BaselineSucceeded, bool CandidateSucceeded, PrismProfileTopologySummary BaselineTopology, PrismProfileTopologySummary CandidateTopology, bool TopologyParityWithBaseline, PrismProfileStepSmokeSummary StepSmoke, IReadOnlyList<string> Diagnostics, string Recommendation);

public static class TriangleHexPrismProfileParityLab
{
    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];

    public static IReadOnlyList<PrismProfileParityRow> RunAll() =>
    [
        Run(new("triangle-basic", PrismKind.Triangle, 12, 8, 10, true)),
        Run(new("triangle-alt", PrismKind.Triangle, 7.5, 6.25, 4, true)),
        Run(new("hex-basic", PrismKind.Hex, 10, 0, 12, true)),
        Run(new("hex-alt", PrismKind.Hex, 6.5, 0, 3.5, true)),
        Run(new("triangle-invalid-height", PrismKind.Triangle, 10, 8, 0, false)),
        Run(new("hex-invalid-size", PrismKind.Hex, -2, 0, 4, false)),
        Run(new("triangle-invalid-nan", PrismKind.Triangle, double.NaN, 8, 4, false))
    ];

    public static PrismProfileParityRow Run(PrismProfileParityCase c)
    {
        var d = new List<string> { "v2-x8-prism-profile-parity-lab-started", "v2-x8-no-3d-boolean-used" };
        var baseline = BuildBaseline(c, d);
        var candidate = BuildCandidate(c, d);
        if (!baseline.IsSuccess || !candidate.IsSuccess)
        {
            d.Add("v2-x8-invalid-input-rejected");
            return new(c.Name, c.Kind, false, baseline.IsSuccess, candidate.IsSuccess,
                baseline.Topology, candidate.Topology, false, EmptyStep(), d.Distinct().OrderBy(x => x).ToArray(), "prism-profile-invalid-rejected");
        }

        var topologyParity = baseline.Topology == candidate.Topology;
        if (topologyParity) d.Add("v2-x8-topology-parity-succeeded");
        else d.Add($"v2-x8-topology-parity-mismatch:{c.Name}");

        var step = SummarizeStep(candidate.Body!);
        if (step.Exported && step.MissingMarkers.Count == 0 && !step.ContainsBrepWithVoids && !step.ContainsCylindricalSurface) d.Add("v2-x8-step-smoke-succeeded");
        else d.Add($"v2-x8-step-smoke-failed:{c.Name}");

        var recommendation = topologyParity && step.Exported && step.MissingMarkers.Count == 0 && !step.ContainsBrepWithVoids && !step.ContainsCylindricalSurface
            ? "prism-profile-ready-for-production-migration"
            : "prism-profile-needs-emitter-parity-work";

        return new(c.Name, c.Kind, true, true, true, baseline.Topology, candidate.Topology, topologyParity, step, d.Distinct().OrderBy(x => x).ToArray(), recommendation);
    }

    private static (bool IsSuccess, BrepBody? Body, PrismProfileTopologySummary Topology) BuildBaseline(PrismProfileParityCase c, List<string> d)
    {
        var result = c.Kind == PrismKind.Triangle ? BrepPrimitives.CreateTriangularPrism(c.SizeA, c.SizeB, c.Height) : BrepPrimitives.CreateHexagonalPrism(c.SizeA, c.Height);
        d.Add(c.Kind == PrismKind.Triangle ? "v2-x8-baseline-triangle-created" : "v2-x8-baseline-hex-created");
        return !result.IsSuccess || result.Value is null ? (false, null, EmptyTopology()) : (true, result.Value, SummarizeTopology(result.Value));
    }

    private static (bool IsSuccess, BrepBody? Body, PrismProfileTopologySummary Topology) BuildCandidate(PrismProfileParityCase c, List<string> d)
    {
        var req = new LineArcProfileExtrudeRequest([ToLoop(c)], c.Height);
        d.Add("v2-x8-line-profile-adapted");
        var result = LineArcProfileExtrudeEmitter.TryEmit(req);
        d.Add(c.Kind == PrismKind.Triangle ? "v2-x8-candidate-triangle-created" : "v2-x8-candidate-hex-created");
        return result.Status != LineArcProfileExtrudeStatus.Succeeded || result.Body is null ? (false, null, EmptyTopology()) : (true, result.Body, SummarizeTopology(result.Body));
    }

    private static LineArcProfileLoop2D ToLoop(PrismProfileParityCase c)
    {
        if (c.Kind == PrismKind.Triangle)
        {
            var hw = c.SizeA / 2d; var hd = c.SizeB / 2d;
            return new([
                new LineArcLineSegment2D((-hw, -hd), (hw, -hd)),
                new LineArcLineSegment2D((hw, -hd), (0, hd)),
                new LineArcLineSegment2D((0, hd), (-hw, -hd))
            ], false);
        }

        var r = c.SizeA / Math.Sqrt(3d);
        var pts = Enumerable.Range(0, 6).Select(i => (r * Math.Cos(Math.PI * i / 3d), r * Math.Sin(Math.PI * i / 3d))).ToArray();
        return new([
            new LineArcLineSegment2D(pts[0], pts[1]),
            new LineArcLineSegment2D(pts[1], pts[2]),
            new LineArcLineSegment2D(pts[2], pts[3]),
            new LineArcLineSegment2D(pts[3], pts[4]),
            new LineArcLineSegment2D(pts[4], pts[5]),
            new LineArcLineSegment2D(pts[5], pts[0])
        ], false);
    }

    private static PrismProfileTopologySummary SummarizeTopology(BrepBody body)
    {
        var verts = body.Topology.Vertices.Count(); var edges = body.Topology.Edges.Count(); var faces = body.Topology.Faces.Count();
        var planar = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane);
        var cyl = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder);
        var loops = body.Topology.Loops.Count(); var coedges = body.Topology.Coedges.Count();
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : default).ToArray();
        var minX = points.Min(p => p.X); var minY = points.Min(p => p.Y); var minZ = points.Min(p => p.Z);
        var maxX = points.Max(p => p.X); var maxY = points.Max(p => p.Y); var maxZ = points.Max(p => p.Z);
        return new(true, verts, edges, faces, planar, cyl, loops, coedges, (minX,minY,minZ), (maxX,maxY,maxZ));
    }

    private static PrismProfileStepSmokeSummary SummarizeStep(BrepBody body)
    {
        var ex = Step242Exporter.ExportBody(body);
        if (!ex.IsSuccess || ex.Value is null) return new(false, [], RequiredStepMarkers, false, false);
        var txt = ex.Value;
        var present = RequiredStepMarkers.Where(m => txt.Contains(m, StringComparison.Ordinal)).OrderBy(x => x).ToArray();
        return new(true, present, RequiredStepMarkers.Except(present).OrderBy(x=>x).ToArray(), txt.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal), txt.Contains("CYLINDRICAL_SURFACE", StringComparison.Ordinal));
    }

    private static PrismProfileTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, (0,0,0), (0,0,0));
    private static PrismProfileStepSmokeSummary EmptyStep() => new(false, [], [], false, false);
}
