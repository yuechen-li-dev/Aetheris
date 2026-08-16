using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.FEA.Geometry;

/// <summary>
/// Production analysis-domain adapter for an imported closed BRep. The STEP importer remains the
/// sole owner of STEP syntax; this type only exposes kernel spatial and face-domain queries as CIR.
/// Imported coordinates are transformed to SI before they reach mechanics.
/// </summary>
public sealed class ImportedBrepAnalysisRegion : IContinuumRegion, IBoundsClassificationCapability, IPlanarBoundaryDomainCapability
{
    private readonly BrepBody _body;
    private readonly Transform3D _brepToWorld;
    private readonly Transform3D _worldToBrep;
    private readonly double _lengthScale;
    private readonly BrepSpatialQueries.PointContainmentQueryContext _queryContext;

    public ImportedBrepAnalysisRegion(RegionId id, BrepBody body, double lengthScaleToMeters = .001)
    {
        if (!double.IsFinite(lengthScaleToMeters) || lengthScaleToMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(lengthScaleToMeters));
        Id = id;
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _lengthScale = lengthScaleToMeters;
        _brepToWorld = Transform3D.CreateScale(lengthScaleToMeters);
        _worldToBrep = _brepToWorld.Inverse();
        _queryContext = BrepSpatialQueries.CreatePointContainmentQueryContext(body);
        var bodyId=body.Topology.Bodies.Single().Id;var shellId=body.Topology.Shells.Single().Id;
        // Whole-shell bounds include deterministic samples of curved edge trims; vertex-only bounds
        // collapse periodic faces such as an imported cylindrical rod to their seam endpoints.
        Bounds=new WholeShellBoundaryQuery(body,new(id,bodyId.Value.ToString(),shellId.Value.ToString()),_brepToWorld).Bounds;
    }

    public RegionId Id { get; }
    public BoundingBox3D Bounds { get; }

    public ContinuumPointClassification Classify(Point3D point, double tolerance = 1e-9)
    {
        return ClassifyKernel(point,tolerance) switch
        {
            PointContainment.Inside => ContinuumPointClassification.Inside,
            PointContainment.Boundary => ContinuumPointClassification.Boundary,
            _ => ContinuumPointClassification.Outside,
        };
    }

    /// <summary>Rejects a body when the kernel cannot establish any occupied interior evidence.</summary>
    public bool TryValidateOccupancySupport(out string evidence)
    {
        var inside=0;var outside=0;var unknown=0;
        for(var k=0;k<4;k++)for(var j=0;j<4;j++)for(var i=0;i<4;i++)
        {
            var point=new Point3D(Bounds.Min.X+(Bounds.Max.X-Bounds.Min.X)*(i+.5)/4,
                Bounds.Min.Y+(Bounds.Max.Y-Bounds.Min.Y)*(j+.5)/4,
                Bounds.Min.Z+(Bounds.Max.Z-Bounds.Min.Z)*(k+.5)/4);
            switch(ClassifyKernel(point,1e-9)){case PointContainment.Inside:inside++;break;case PointContainment.Outside:outside++;break;case PointContainment.Boundary:inside++;break;default:unknown++;break;}
        }
        evidence=$"4x4x4 deterministic imported occupancy probes: inside/boundary={inside}, outside={outside}, unknown={unknown}.";
        return inside>0;
    }

    private PointContainment ClassifyKernel(Point3D point,double tolerance)
    {
        var kernelTolerance = new ToleranceContext(double.Max(tolerance / _lengthScale, 1e-9), ToleranceContext.Default.Angular);
        return BrepSpatialQueries.ClassifyPoint(_body, _worldToBrep.Apply(point), kernelTolerance, _queryContext).Value;
    }

    // Arbitrary closed BReps may contain holes or re-entrant material, so only disjoint cells can
    // be classified without sampling. Intersecting cells deliberately use deterministic cut-cell sampling.
    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds, double tolerance = 1e-9) =>
        bounds.Max.X < Bounds.Min.X - tolerance || bounds.Min.X > Bounds.Max.X + tolerance
        || bounds.Max.Y < Bounds.Min.Y - tolerance || bounds.Min.Y > Bounds.Max.Y + tolerance
        || bounds.Max.Z < Bounds.Min.Z - tolerance || bounds.Min.Z > Bounds.Max.Z + tolerance
            ? ContinuumBoundsClassification.Outside : ContinuumBoundsClassification.Cut;

    public bool TryResolvePlanarBoundary(string semanticPath, string? exactBrepFaceId, out PlanarBoundaryDomain domain)
    {
        domain = null!;
        var faceId = ResolveFace(semanticPath, exactBrepFaceId);
        if (faceId is null || !_body.TryGetFaceSurface(faceId.Value, out var geometry)
            || geometry?.Kind != SurfaceGeometryKind.Plane || geometry.Plane is not PlaneSurface plane)
            return false;
        if (!TryLoops(faceId.Value, plane, out var outer, out var inner)) return false;
        var origin = _brepToWorld.Apply(plane.Origin);
        var u = _brepToWorld.Apply(plane.UAxis.ToVector());
        var v = _brepToWorld.Apply(plane.VAxis.ToVector());
        var n = plane.Normal.ToVector();
        u.TryNormalize(out u); v.TryNormalize(out v); n.TryNormalize(out n);
        var reference = new BoundaryReference("ImportedBRep", $"face:{faceId.Value.Value}", faceId.Value.Value.ToString(), semanticPath,
            Id.Value, _body.Topology.Bodies.Single().Id.Value.ToString(), _body.Topology.Shells.Single().Id.Value.ToString());
        domain = new(reference, origin, u, v, n,
            outer.Select(p => (p.U * _lengthScale, p.V * _lengthScale)).ToArray(),
            inner.Select(loop => (IReadOnlyList<(double U, double V)>)loop.Select(p => (p.U * _lengthScale, p.V * _lengthScale)).ToArray()).ToArray(),
            "Imported BRep planar trim; outward orientation resolved by CIR two-sided probe");
        return true;
    }

    private FaceId? ResolveFace(string path, string? exactId)
    {
        if (int.TryParse(exactId, out var id) && _body.Topology.TryGetFace(new(id), out _)) return new(id);
        var explicitMatch = System.Text.RegularExpressions.Regex.Match(path, @"\.face\(\#?(?<id>[0-9]+)\)$");
        if (explicitMatch.Success && int.TryParse(explicitMatch.Groups["id"].Value, out id) && _body.Topology.TryGetFace(new(id), out _)) return new(id);
        var token = new[] { "-X", "+X", "-Y", "+Y", "-Z", "+Z" }.FirstOrDefault(path.Contains);
        if (token is null) return null;
        var axis = token[1] switch { 'X' => new Vector3D(1, 0, 0), 'Y' => new Vector3D(0, 1, 0), _ => new Vector3D(0, 0, 1) };
        var candidates = _body.Topology.Faces.Select(face => (face.Id, Surface: _body.GetFaceSurface(face.Id)))
            .Where(item => item.Surface.Kind == SurfaceGeometryKind.Plane && item.Surface.Plane is not null
                && double.Abs(item.Surface.Plane.Value.Normal.ToVector().Dot(axis)) >= .999)
            .Select(item => (item.Id, Position: new Vector3D(item.Surface.Plane!.Value.Origin.X, item.Surface.Plane.Value.Origin.Y, item.Surface.Plane.Value.Origin.Z).Dot(axis)))
            .OrderBy(item => item.Id.Value).ToArray();
        if (candidates.Length == 0) return null;
        var extreme = token[0] == '-' ? candidates.Min(item => item.Position) : candidates.Max(item => item.Position);
        return candidates.First(item => double.Abs(item.Position - extreme) <= 1e-8).Id;
    }

    private bool TryLoops(FaceId faceId, PlaneSurface plane, out IReadOnlyList<(double U, double V)> outer, out IReadOnlyList<IReadOnlyList<(double U, double V)>> inner)
    {
        var loops = new List<IReadOnlyList<(double U, double V)>>();
        foreach (var loopId in _body.GetLoopIds(faceId))
        {
            var points = new List<Point3D>();
            foreach (var coedgeId in _body.GetCoedgeIds(loopId))
            {
                var coedge = _body.Topology.GetCoedge(coedgeId);
                if (!_body.TryGetEdgeCurve(coedge.EdgeId, out var curve) || curve is null || !_body.Bindings.TryGetEdgeBinding(coedge.EdgeId, out var binding))
                    continue;
                var interval = binding.TrimInterval ?? new ParameterInterval(0, 1);
                var count = curve.Kind == CurveGeometryKind.Line3 ? 2 : 25;
                for (var i = 0; i < count; i++)
                {
                    var fraction = i / (count - 1d);
                    if (coedge.IsReversed) fraction = 1 - fraction;
                    var t = interval.Start + (interval.End - interval.Start) * fraction;
                    var point = Evaluate(curve, t);
                    if (points.Count == 0 || (point - points[^1]).Length > 1e-9) points.Add(point);
                }
            }
            if (points.Count > 1 && (points[0] - points[^1]).Length <= 1e-9) points.RemoveAt(points.Count - 1);
            var projected = points.Select(point =>
            {
                var delta = point - plane.Origin;
                return (delta.Dot(plane.UAxis.ToVector()), delta.Dot(plane.VAxis.ToVector()));
            }).ToArray();
            if (projected.Length >= 3) loops.Add(projected);
        }
        var ordered = loops.OrderByDescending(loop => double.Abs(SignedArea(loop))).ToArray();
        outer = ordered.FirstOrDefault() ?? [];
        inner = ordered.Skip(1).ToArray();
        return outer.Count >= 3 && double.Abs(SignedArea(outer)) > 1e-12;
    }

    private static Point3D Evaluate(CurveGeometry curve, double t) => curve.Kind switch
    {
        CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(t),
        CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(t),
        CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(t),
        CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(t),
        CurveGeometryKind.Hyperbola3 => curve.Hyperbola3!.Value.Evaluate(t),
        _ => throw new NotSupportedException($"Imported boundary curve '{curve.Kind}' is unsupported."),
    };

    private static double SignedArea(IReadOnlyList<(double U, double V)> points)
    {
        var area = 0d;
        for (var i = 0; i < points.Count; i++) area += points[i].U * points[(i + 1) % points.Count].V - points[(i + 1) % points.Count].U * points[i].V;
        return area * .5;
    }
}
