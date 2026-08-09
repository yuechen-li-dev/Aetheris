using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Boundaries;

public sealed record WholeShellBoundaryCandidate(
    FaceId FaceId,
    SurfaceGeometryKind SupportKind,
    BoundingBox3D Bounds,
    IReadOnlyList<Point3D> ExactBoundarySamples,
    IReadOnlyList<EdgeId> EdgeIds,
    IReadOnlyList<VertexId> VertexIds,
    IReadOnlyList<Point3D> OuterTrimVertices,
    IReadOnlyList<FaceId> AdjacentFaceIds,
    bool SameSense,
    string? SemanticIdentity,
    BoundaryReference Reference);

/// <summary>
/// Deterministic, body-local exact boundary index. The current exact bounds path admits faces whose
/// trim vertices bound the support patch; no mesh is consulted and candidates remain BRep entities.
/// </summary>
public sealed class WholeShellBoundaryQuery
{
    private readonly IReadOnlyList<WholeShellBoundaryCandidate> _faces;

    public WholeShellBoundaryQuery(BrepBody body, CirBrepAssociation association, Transform3D transform,
        IReadOnlyDictionary<FaceId, string>? semanticIdentities = null)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Association = association;
        Transform = transform;
        var faceEdges = body.Topology.Faces.OrderBy(face => face.Id.Value)
            .ToDictionary(face => face.Id, face => body.GetEdges(face.Id).OrderBy(id => id.Value).ToArray());
        var edgeFaces = faceEdges.SelectMany(pair => pair.Value.Select(edge => (edge, pair.Key)))
            .GroupBy(pair => pair.edge).ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).OrderBy(id => id.Value).ToArray());
        var rows = new List<WholeShellBoundaryCandidate>();
        foreach (var face in body.Topology.Faces.OrderBy(face => face.Id.Value))
        {
            var edges = faceEdges[face.Id];
            var vertices = edges.SelectMany(body.GetVertices).Distinct().OrderBy(id => id.Value).ToArray();
            var points = ExactTrimSamples(body, face.Id).Select(transform.Apply).ToArray();
            if (points.Length == 0) throw new InvalidOperationException($"BRep face {face.Id.Value} has no bounded trim vertices.");
            var bounds = BoundsOf(points);
            var adjacent = edges.SelectMany(edge => edgeFaces[edge]).Where(id => id != face.Id).Distinct().OrderBy(id => id.Value).ToArray();
            var surface = body.GetFaceSurface(face.Id);
            var trim = OrderedOuterTrim(body, face.Id).Select(transform.Apply).ToArray();
            var binding = body.Bindings.GetFaceBinding(face.Id);
            string? semantic = null;
            semanticIdentities?.TryGetValue(face.Id, out semantic);
            var reference = new BoundaryReference("BRep", $"face:{face.Id.Value}", face.Id.Value.ToString(), semantic,
                association.ContinuumRegionId.Value, association.BrepBodyId, association.OuterShellId);
            var candidates=new List<Point3D>(points);
            var centroid=new Point3D(points.Average(p=>p.X),points.Average(p=>p.Y),points.Average(p=>p.Z));
            candidates.Add(ExactSupportBoundaryQuery.ProjectToSupport(body,face.Id,centroid,transform));
            for(var i=0;i<points.Length;i+=int.Max(1,points.Length/8))
                candidates.Add(ExactSupportBoundaryQuery.ProjectToSupport(body,face.Id,new Point3D((points[i].X+centroid.X)*.5d,(points[i].Y+centroid.Y)*.5d,(points[i].Z+centroid.Z)*.5d),transform));
            rows.Add(new(face.Id, surface.Kind, bounds, candidates, edges, vertices, trim, adjacent, binding.SameSense, semantic, reference));
        }
        _faces = rows;
        Bounds = BoundsOf(rows.SelectMany(row=>new[]{row.Bounds.Min,row.Bounds.Max}).ToArray());
    }

    public BrepBody Body { get; }
    public CirBrepAssociation Association { get; }
    public Transform3D Transform { get; }
    public IReadOnlyList<WholeShellBoundaryCandidate> Faces => _faces;
    public BoundingBox3D Bounds { get; }

    public IReadOnlyList<WholeShellBoundaryCandidate> Query(BoundingBox3D cellBounds, double tolerance = 1e-9d) =>
        _faces.Where(face => Intersects(face.Bounds, cellBounds, tolerance)
            && (face.SupportKind != SurfaceGeometryKind.Plane || ClippedPlanarTrimIsNonEmpty(face.OuterTrimVertices, cellBounds, tolerance))).ToArray();

    public Point3D TransformPoint(VertexId id) => Transform.Apply(ResolveVertex(Body, id));
    public Point3D TransformEdgeMidpoint(EdgeId edgeId)
    {
        if(!Body.TryGetEdgeCurveGeometry(edgeId,out var curve)||curve is null||!Body.Bindings.TryGetEdgeBinding(edgeId,out var binding))
        {var edge=Body.Topology.GetEdge(edgeId);return new Point3D((TransformPoint(edge.StartVertexId).X+TransformPoint(edge.EndVertexId).X)*.5d,(TransformPoint(edge.StartVertexId).Y+TransformPoint(edge.EndVertexId).Y)*.5d,(TransformPoint(edge.StartVertexId).Z+TransformPoint(edge.EndVertexId).Z)*.5d);}
        var interval=binding.TrimInterval??new ParameterInterval(0d,1d);var t=(interval.Start+interval.End)*.5d;
        var p=curve.Kind switch{CurveGeometryKind.Line3=>curve.Line3!.Value.Evaluate(t),CurveGeometryKind.Circle3=>curve.Circle3!.Value.Evaluate(t),CurveGeometryKind.BSpline3=>curve.BSpline3!.Value.Evaluate(t),CurveGeometryKind.Ellipse3=>curve.Ellipse3!.Value.Evaluate(t),CurveGeometryKind.Hyperbola3=>curve.Hyperbola3!.Value.Evaluate(t),_=>throw new NotSupportedException($"Exact edge midpoint does not support {curve.Kind}.")};
        return Transform.Apply(p);
    }

    private static BoundingBox3D BoundsOf(IReadOnlyList<Point3D> points) => new(
        new(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
        new(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));

    private static bool Intersects(BoundingBox3D a, BoundingBox3D b, double tolerance) =>
        a.Max.X >= b.Min.X - tolerance && a.Min.X <= b.Max.X + tolerance
        && a.Max.Y >= b.Min.Y - tolerance && a.Min.Y <= b.Max.Y + tolerance
        && a.Max.Z >= b.Min.Z - tolerance && a.Min.Z <= b.Max.Z + tolerance;

    private static IReadOnlyList<Point3D> OrderedOuterTrim(BrepBody body, FaceId faceId)
    {
        var loopId = body.GetLoopIds(faceId).First(); var loop = body.Topology.GetLoop(loopId); var points = new List<Point3D>();
        foreach (var coedgeId in loop.CoedgeIds)
        {
            var coedge = body.Topology.GetCoedge(coedgeId); var edge = body.Topology.GetEdge(coedge.EdgeId);
            var vertex = coedge.IsReversed ? edge.EndVertexId : edge.StartVertexId;
            points.Add(ResolveVertex(body, vertex));
        }
        return points;
    }

    private static IReadOnlyList<Point3D> ExactTrimSamples(BrepBody body, FaceId faceId)
    {
        var values=new List<Point3D>();
        foreach(var edgeId in body.GetEdges(faceId).Distinct())
        {
            if(!body.TryGetEdgeCurveGeometry(edgeId,out var curve)||curve is null||!body.Bindings.TryGetEdgeBinding(edgeId,out var binding)) continue;
            var interval=binding.TrimInterval??new ParameterInterval(0d,1d);
            var count=curve.Kind==CurveGeometryKind.Line3?2:33;
            for(var i=0;i<count;i++)
            {
                var t=interval.Start+((interval.End-interval.Start)*i/(count-1d));
                values.Add(curve.Kind switch
                {
                    CurveGeometryKind.Line3=>curve.Line3!.Value.Evaluate(t),
                    CurveGeometryKind.Circle3=>curve.Circle3!.Value.Evaluate(t),
                    CurveGeometryKind.BSpline3=>curve.BSpline3!.Value.Evaluate(t),
                    CurveGeometryKind.Ellipse3=>curve.Ellipse3!.Value.Evaluate(t),
                    CurveGeometryKind.Hyperbola3=>curve.Hyperbola3!.Value.Evaluate(t),
                    _=>throw new NotSupportedException($"Exact trim sampling does not support {curve.Kind}.")
                });
            }
        }
        return values.Count>0?values:OrderedOuterTrim(body,faceId);
    }

    private static bool ClippedPlanarTrimIsNonEmpty(IReadOnlyList<Point3D> trim, BoundingBox3D box, double tolerance)
    {
        var polygon = trim.ToList();
        polygon = Clip(polygon, p => p.X - box.Min.X + tolerance, (a,b,da,db) => a + (b-a)*(da/(da-db)));
        polygon = Clip(polygon, p => box.Max.X - p.X + tolerance, (a,b,da,db) => a + (b-a)*(da/(da-db)));
        polygon = Clip(polygon, p => p.Y - box.Min.Y + tolerance, (a,b,da,db) => a + (b-a)*(da/(da-db)));
        polygon = Clip(polygon, p => box.Max.Y - p.Y + tolerance, (a,b,da,db) => a + (b-a)*(da/(da-db)));
        polygon = Clip(polygon, p => p.Z - box.Min.Z + tolerance, (a,b,da,db) => a + (b-a)*(da/(da-db)));
        polygon = Clip(polygon, p => box.Max.Z - p.Z + tolerance, (a,b,da,db) => a + (b-a)*(da/(da-db)));
        return polygon.Count >= 3;
    }

    private static List<Point3D> Clip(List<Point3D> input, Func<Point3D,double> distance, Func<Point3D,Point3D,double,double,Point3D> intersect)
    {
        if (input.Count == 0) return input; var output = new List<Point3D>();
        for(var i=0;i<input.Count;i++) { var a=input[i];var b=input[(i+1)%input.Count];var da=distance(a);var db=distance(b);var ai=da>=0d;var bi=db>=0d;
            if(ai) output.Add(a); if(ai!=bi) output.Add(intersect(a,b,da,db)); }
        return output;
    }

    private static Point3D ResolveVertex(BrepBody body, VertexId vertexId)
    {
        if (body.TryGetVertexPoint(vertexId, out var point)) return point;
        foreach (var edge in body.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            var useStart = edge.StartVertexId == vertexId; if (!useStart && edge.EndVertexId != vertexId) continue;
            if (!body.TryGetEdgeCurveGeometry(edge.Id, out var curve) || curve is null || !body.Bindings.TryGetEdgeBinding(edge.Id, out var binding)) continue;
            var interval = binding.TrimInterval ?? new ParameterInterval(0d, 1d); var parameter = useStart ? interval.Start : interval.End;
            return curve.Kind switch
            {
                CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(parameter),
                CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(parameter),
                CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(parameter),
                CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(parameter),
                CurveGeometryKind.Hyperbola3 => curve.Hyperbola3!.Value.Evaluate(parameter),
                _ => throw new NotSupportedException($"Vertex {vertexId.Value} endpoint curve kind {curve.Kind} is unsupported by whole-shell indexing.")
            };
        }
        throw new InvalidOperationException($"BRep vertex {vertexId.Value} cannot be resolved from point or exact edge trim geometry.");
    }
}
