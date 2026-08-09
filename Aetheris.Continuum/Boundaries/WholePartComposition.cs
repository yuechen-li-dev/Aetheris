using System.Diagnostics;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Boundaries;

public sealed record CirBrepAssociation(RegionId ContinuumRegionId, string BrepBodyId, string OuterShellId, string? SemanticModelId = null);

public enum CutCellCompositionKind
{
    SingleFace,
    TwoFaceEdge,
    ThreeFaceCorner,
    MultiFaceTrimJunction,
    FilletContact,
    GeneralBoundedMultiFace,
}

public enum MaterialSideStatus { Resolved, Ambiguous, Inconsistent }

public sealed record MaterialSideEvidence(
    FaceId FaceId,
    Point3D BoundaryPoint,
    Vector3D BrepOrientedNormal,
    double ProbeDistance,
    ContinuumPointClassification PlusClassification,
    ContinuumPointClassification MinusClassification,
    double? PlusSignedDistance,
    double? MinusSignedDistance,
    Vector3D? MaterialSideNormal,
    MaterialSideStatus Status,
    string Basis);

public sealed record CutCellBoundaryContributor(
    BoundaryReference Boundary,
    SurfaceGeometryKind SupportKind,
    IReadOnlyList<EdgeId> EdgeIds,
    IReadOnlyList<VertexId> VertexIds,
    IReadOnlyList<FaceId> AdjacentFaces,
    MaterialSideEvidence MaterialSide,
    IBoundaryOffsetMap? LocalMap = null);

public sealed record BoundaryCompositionJudgmentTrace(
    string Reason,
    IReadOnlyList<string> CandidateFaces,
    IReadOnlyList<string> Evidence,
    string SelectedComposition,
    IReadOnlyList<JudgmentRejection> RejectedAlternatives,
    double RuntimeMilliseconds);

public sealed record LocalBoundaryIntegration(
    double OccupancyFraction,
    double BoundaryArea,
    IReadOnlyDictionary<string, double> BoundaryAreaByFace,
    string Method,
    int CirQueries);

public sealed record CutCellBoundarySet(
    CellIndex CellIndex,
    RegionId ContinuumRegionId,
    IReadOnlyList<CutCellBoundaryContributor> Contributors,
    CutCellCompositionKind CompositionKind,
    ContinuumPointClassification LocalMaterialClassification,
    BoundaryCompositionJudgmentTrace? Judgment,
    LocalBoundaryIntegration Integration)
{
    public IReadOnlyList<IBoundaryOffsetMap> CompositeBoundaryMaps => Contributors.Where(c => c.LocalMap is not null).Select(c => c.LocalMap!).ToArray();
}

public sealed record BrepCirConsistencyProbe(string Kind, string EntityId, Point3D Point, ContinuumPointClassification Classification, bool Passed);
public sealed record BrepCirConsistencyResult(bool Passed, IReadOnlyList<BrepCirConsistencyProbe> Probes, string Summary);

public static class BrepCirConsistencyChecker
{
    public static BrepCirConsistencyResult Check(IContinuumRegion region, WholeShellBoundaryQuery shell, double tolerance = 1e-5d)
    {
        var probes = new List<BrepCirConsistencyProbe>();
        foreach (var face in shell.Faces)
        {
            var point = Centroid(face.VertexIds.Select(shell.TransformPoint));
            var c = region.Classify(point, tolerance);
            probes.Add(new("face", face.FaceId.Value.ToString(), point, c, c == ContinuumPointClassification.Boundary));
        }
        foreach (var edge in shell.Faces.SelectMany(f => f.EdgeIds).Distinct().OrderBy(e => e.Value))
        {
            var topology = shell.Body.Topology.GetEdge(edge);
            var point = Midpoint(shell.TransformPoint(topology.StartVertexId), shell.TransformPoint(topology.EndVertexId));
            var c = region.Classify(point, tolerance);
            probes.Add(new("edge", edge.Value.ToString(), point, c, c == ContinuumPointClassification.Boundary));
        }
        foreach (var vertex in shell.Faces.SelectMany(f => f.VertexIds).Distinct().OrderBy(v => v.Value))
        {
            var point = shell.TransformPoint(vertex); var c = region.Classify(point, tolerance);
            probes.Add(new("vertex", vertex.Value.ToString(), point, c, c == ContinuumPointClassification.Boundary));
        }
        var center = new Point3D((region.Bounds.Min.X + region.Bounds.Max.X) * .5d, (region.Bounds.Min.Y + region.Bounds.Max.Y) * .5d, (region.Bounds.Min.Z + region.Bounds.Max.Z) * .5d);
        var inside = region.Classify(center, tolerance); probes.Add(new("known-interior", "region-center", center, inside, inside != ContinuumPointClassification.Outside));
        var diagonal = region.Bounds.Max - region.Bounds.Min;
        var outsidePoint = region.Bounds.Max + diagonal; var outside = region.Classify(outsidePoint, tolerance);
        probes.Add(new("known-exterior", "expanded-max", outsidePoint, outside, outside == ContinuumPointClassification.Outside));
        var passed = probes.All(p => p.Passed);
        return new(passed, probes, passed ? $"BRep/CIR agreement passed for {probes.Count} deterministic probes."
            : $"BRep/CIR disagreement in {probes.Count(p => !p.Passed)} of {probes.Count} deterministic probes.");
    }

    private static Point3D Centroid(IEnumerable<Point3D> points) { var a = points.ToArray(); return new(a.Average(p => p.X), a.Average(p => p.Y), a.Average(p => p.Z)); }
    private static Point3D Midpoint(Point3D a, Point3D b) => new((a.X + b.X) * .5d, (a.Y + b.Y) * .5d, (a.Z + b.Z) * .5d);
}

public sealed class WholePartCutCellComposer
{
    private readonly IContinuumRegion _region;
    private readonly WholeShellBoundaryQuery _shell;
    private readonly IReadOnlyDictionary<FaceId, MaterialSideEvidence> _materialSides;
    private readonly JudgmentEngine<CompositionContext> _engine = new();
    private int _judgmentCalls;
    private double _judgmentMilliseconds;

    public WholePartCutCellComposer(IContinuumRegion region, WholeShellBoundaryQuery shell)
    {
        _region = region ?? throw new ArgumentNullException(nameof(region)); _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        if (region.Id != shell.Association.ContinuumRegionId) throw new ArgumentException("CIR/BRep association does not name the supplied region.", nameof(shell));
        var consistency = BrepCirConsistencyChecker.Check(region, shell);
        if (!consistency.Passed) throw new InvalidOperationException(consistency.Summary);
        Consistency = consistency;
        _materialSides = shell.Faces.ToDictionary(face => face.FaceId, ResolveMaterialSide);
        if (_materialSides.Values.Any(e => e.Status == MaterialSideStatus.Inconsistent))
            throw new InvalidOperationException("CIR could not resolve the material side for one or more exact BRep faces.");
    }

    public BrepCirConsistencyResult Consistency { get; }
    public int JudgmentCallCount => _judgmentCalls;
    public double JudgmentRuntimeMilliseconds => _judgmentMilliseconds;

    public CutCellBoundarySet Compose(CellIndex index, BoundingBox3D bounds)
    {
        var candidates = _shell.Query(bounds);
        if (candidates.Count == 0) throw new InvalidOperationException($"Cut cell {index} has no bounded exact BRep face candidate.");
        var kind = Classify(candidates);
        BoundaryCompositionJudgmentTrace? trace = null;
        var ambiguous = candidates.Any(c => _materialSides[c.FaceId].Status != MaterialSideStatus.Resolved)
            || kind is CutCellCompositionKind.MultiFaceTrimJunction or CutCellCompositionKind.GeneralBoundedMultiFace;
        if (ambiguous) (kind, trace) = Judge(candidates, kind);
        var contributors = candidates.Select(c => new CutCellBoundaryContributor(c.Reference, c.SupportKind, c.EdgeIds, c.VertexIds,
            c.AdjacentFaceIds, _materialSides[c.FaceId])).ToArray();
        var integration = candidates.All(c => c.SupportKind == SurfaceGeometryKind.Plane)
            ? ConvexPlanarCellIntegrator.Integrate(bounds, _shell, _materialSides)
            : SampleFallback(bounds, 12);
        var center = new Point3D((bounds.Min.X + bounds.Max.X) * .5d, (bounds.Min.Y + bounds.Max.Y) * .5d, (bounds.Min.Z + bounds.Max.Z) * .5d);
        return new(index, _region.Id, contributors, kind, _region.Classify(center), trace, integration);
    }

    private MaterialSideEvidence ResolveMaterialSide(WholeShellBoundaryCandidate face)
    {
        if (_shell.Body.GetFaceSurface(face.FaceId).Plane is not PlaneSurface plane)
            return new(face.FaceId, Centroid(face.VertexIds.Select(_shell.TransformPoint)), default, 0d,
                ContinuumPointClassification.Boundary, ContinuumPointClassification.Boundary, null, null, null,
                MaterialSideStatus.Ambiguous, "Non-planar support requires local exact projection; deferred to its BoundaryOffsetMap support.");
        var point = Centroid(face.VertexIds.Select(_shell.TransformPoint));
        var normal = _shell.Transform.Apply(plane.Normal).ToVector(); normal.TryNormalize(out normal);
        if (!face.SameSense) normal = -normal;
        var scale = double.Max(1d, (_region.Bounds.Max - _region.Bounds.Min).Length);
        var epsilon = double.Clamp(scale * 1e-6d, 1e-8d, scale * 1e-3d);
        var plus = _region.Classify(point + normal * epsilon, epsilon * .1d);
        var minus = _region.Classify(point - normal * epsilon, epsilon * .1d);
        var plusInside = plus == ContinuumPointClassification.Inside; var minusInside = minus == ContinuumPointClassification.Inside;
        var status = plusInside ^ minusInside ? MaterialSideStatus.Resolved : MaterialSideStatus.Inconsistent;
        Vector3D? material = plusInside ^ minusInside ? (plusInside ? normal : -normal) : null;
        double? plusSdf = _region is ISignedDistanceCapability sdf ? sdf.SignedDistance(point + normal * epsilon) : null;
        double? minusSdf = _region is ISignedDistanceCapability sdf2 ? sdf2.SignedDistance(point - normal * epsilon) : null;
        return new(face.FaceId, point, normal, epsilon, plus, minus, plusSdf, minusSdf, material, status,
            "Occupied side selected exclusively from CIR probes; SameSense only orients the probe axis.");
    }

    private CutCellCompositionKind Classify(IReadOnlyList<WholeShellBoundaryCandidate> faces)
    {
        if (faces.Count == 1) return CutCellCompositionKind.SingleFace;
        if (faces.Any(f => f.SupportKind == SurfaceGeometryKind.Torus) && faces.Any(f => f.SupportKind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder)) return CutCellCompositionKind.FilletContact;
        if (faces.Count == 2 && faces[0].EdgeIds.Intersect(faces[1].EdgeIds).Any()) return CutCellCompositionKind.TwoFaceEdge;
        if (faces.Count == 3 && faces.Select(f => f.VertexIds.AsEnumerable()).Aggregate((a,b) => a.Intersect(b)).Any()) return CutCellCompositionKind.ThreeFaceCorner;
        if (faces.SelectMany(f => f.EdgeIds).GroupBy(e => e).Any(g => g.Count() > 1)) return CutCellCompositionKind.MultiFaceTrimJunction;
        return CutCellCompositionKind.GeneralBoundedMultiFace;
    }

    private (CutCellCompositionKind, BoundaryCompositionJudgmentTrace) Judge(IReadOnlyList<WholeShellBoundaryCandidate> faces, CutCellCompositionKind direct)
    {
        var context = new CompositionContext(faces, direct);
        JudgmentCandidate<CompositionContext>[] choices =
        [
            new("topology-supported", c => c.Direct is CutCellCompositionKind.MultiFaceTrimJunction, _ => 100d, _ => "No shared BRep edge supports this interpretation.", 0),
            new("bounded-general", _ => true, _ => 10d, _ => "Always admissible bounded fallback.", 1),
        ];
        var start = Stopwatch.GetTimestamp(); var decision = _engine.Evaluate(context, choices); var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        _judgmentCalls++; _judgmentMilliseconds += elapsed;
        var selected = decision.Selection!.Value.Candidate.Name == "topology-supported" ? CutCellCompositionKind.MultiFaceTrimJunction : CutCellCompositionKind.GeneralBoundedMultiFace;
        var evidence = faces.Select(f => $"face={f.FaceId.Value};support={f.SupportKind};edges={string.Join(',', f.EdgeIds.Select(e => e.Value))};cir={_materialSides[f.FaceId].Status}").ToArray();
        return (selected, new("Multiple bounded local interpretations remained after direct adjacency rules.", faces.Select(f => f.FaceId.Value.ToString()).ToArray(), evidence,
            selected.ToString(), decision.Rejections, elapsed));
    }

    private LocalBoundaryIntegration SampleFallback(BoundingBox3D bounds, int n)
    {
        var inside = 0; for (var k=0;k<n;k++) for (var j=0;j<n;j++) for (var i=0;i<n;i++)
        { var p = new Point3D(Lerp(bounds.Min.X,bounds.Max.X,(i+.5)/n),Lerp(bounds.Min.Y,bounds.Max.Y,(j+.5)/n),Lerp(bounds.Min.Z,bounds.Max.Z,(k+.5)/n)); if (_region.Classify(p) != ContinuumPointClassification.Outside) inside++; }
        return new(inside/(double)(n*n*n), 0d, new Dictionary<string,double>(), "bounded-CIR-MSAA-fallback", n*n*n);
    }
    private static double Lerp(double a,double b,double t)=>a+(b-a)*t;
    private static Point3D Centroid(IEnumerable<Point3D> points) { var a=points.ToArray(); return new(a.Average(p=>p.X),a.Average(p=>p.Y),a.Average(p=>p.Z)); }
    private sealed record CompositionContext(IReadOnlyList<WholeShellBoundaryCandidate> Faces, CutCellCompositionKind Direct);
}
