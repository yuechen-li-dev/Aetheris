using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// Bounded materializer for the composition of the existing rectangular top-boundary
/// chamfer family and disjoint, world-Z through-hole cylinders.  It is deliberately
/// a final-plan consumer: callers must have already admitted the host, hole, and
/// semantic finish selections.  It performs no topology search or feature recovery.
/// </summary>
public sealed record CombinedTopBoundaryChamferThroughHole(
    string FeatureId,
    double CenterX,
    double CenterY,
    double Radius);

public sealed record CombinedTopBoundaryChamferThroughHolePlan(
    string PlanId,
    string? ParentHostPlanId,
    IReadOnlyList<string> AppliedFeatureIds,
    double Width,
    double Depth,
    double ZMin,
    double ZMax,
    double ChamferDistance,
    IReadOnlyList<CombinedTopBoundaryChamferThroughHole> Holes)
{
    public double AnalyticVolume
    {
        get
        {
            var height = ZMax - ZMin;
            var lowerHeight = height - ChamferDistance;
            var bottomArea = Width * Depth;
            var topArea = (Width - (2d * ChamferDistance)) * (Depth - (2d * ChamferDistance));
            var chamferedHost = (bottomArea * lowerHeight) + (ChamferDistance / 3d * (bottomArea + topArea + System.Math.Sqrt(bottomArea * topArea)));
            return chamferedHost - Holes.Sum(h => System.Math.PI * h.Radius * h.Radius * height);
        }
    }
}

public static class CombinedTopBoundaryChamferThroughHoleBuilder
{
    private const double Tol = 1e-9;

    public static KernelResult<BrepBody> Build(CombinedTopBoundaryChamferThroughHolePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var validation = Validate(plan);
        if (!validation.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(validation.Diagnostics);
        }

        var z0 = plan.ZMin;
        var z1 = plan.ZMax - plan.ChamferDistance;
        var z2 = plan.ZMax;
        var x0 = -plan.Width / 2d;
        var x1 = plan.Width / 2d;
        var y0 = -plan.Depth / 2d;
        var y1 = plan.Depth / 2d;
        var d = plan.ChamferDistance;
        var bottom = new[] { new Point3D(x0, y0, z0), new Point3D(x1, y0, z0), new Point3D(x1, y1, z0), new Point3D(x0, y1, z0) };
        var shoulder = new[] { new Point3D(x0, y0, z1), new Point3D(x1, y0, z1), new Point3D(x1, y1, z1), new Point3D(x0, y1, z1) };
        var top = new[] { new Point3D(x0 + d, y0 + d, z2), new Point3D(x1 - d, y0 + d, z2), new Point3D(x1 - d, y1 - d, z2), new Point3D(x0 + d, y1 - d, z2) };
        return BuildWithHoleTopology(plan, bottom, shoulder, top);
    }

    private static KernelResult<BrepBody> BuildWithHoleTopology(
        CombinedTopBoundaryChamferThroughHolePlan plan,
        Point3D[] bottom,
        Point3D[] shoulder,
        Point3D[] top)
    {
        var b = new TopologyBuilder();
        var vertices = Enumerable.Range(0, 12).Select(_ => b.AddVertex()).ToArray();
        var bottomEdges = Ring(b, vertices, 0);
        var shoulderEdges = Ring(b, vertices, 4);
        var topEdges = Ring(b, vertices, 8);
        var lowerEdges = Enumerable.Range(0, 4).Select(i => b.AddEdge(vertices[i], vertices[4 + i])).ToArray();
        var chamferEdges = Enumerable.Range(0, 4).Select(i => b.AddEdge(vertices[4 + i], vertices[8 + i])).ToArray();
        var holes = plan.Holes.Select(h =>
        {
            // The periodic circle and its longitudinal seam share endpoint identities.
            // Coincident but distinct seam/circle vertices produce invalid STEP wires.
            var seamTop = b.AddVertex(); var seamBottom = b.AddVertex();
            return new HoleTopology(h, seamTop, seamBottom,
                b.AddEdge(seamTop, seamTop), b.AddEdge(seamBottom, seamBottom), b.AddEdge(seamTop, seamBottom));
        }).ToArray();

        var bottomLoops = new List<LoopId> { AddLoop(b, bottomEdges.Select(Forward).ToArray()) };
        var topLoops = new List<LoopId> { AddLoop(b, topEdges.Select(Forward).ToArray()) };
        foreach (var hole in holes)
        {
            bottomLoops.Add(AddLoop(b, [Reversed(hole.BottomCircle)]));
            topLoops.Add(AddLoop(b, [Forward(hole.TopCircle)]));
        }

        var bottomFace = b.AddFace(bottomLoops);
        var topFace = b.AddFace(topLoops);
        var lowerFaces = new FaceId[4];
        var chamferFaces = new FaceId[4];
        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            lowerFaces[i] = b.AddFace([AddLoop(b, [Forward(bottomEdges[i]), Forward(lowerEdges[next]), Reversed(shoulderEdges[i]), Reversed(lowerEdges[i])])]);
            chamferFaces[i] = b.AddFace([AddLoop(b, [Forward(shoulderEdges[i]), Forward(chamferEdges[next]), Reversed(topEdges[i]), Reversed(chamferEdges[i])])]);
        }
        var holeFaces = holes.Select(h => b.AddFace([AddLoop(b, [Forward(h.Seam), Forward(h.BottomCircle), Reversed(h.Seam), Reversed(h.TopCircle)])])).ToArray();
        var shell = b.AddShell([bottomFace, topFace, .. lowerFaces, .. chamferFaces, .. holeFaces]);
        b.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = bottom.Concat(shoulder).Concat(top).ToArray();
        var vertexPoints = vertices.Select((id, index) => new KeyValuePair<VertexId, Point3D>(id, points[index])).ToDictionary(x => x.Key, x => x.Value);
        var edgeCurve = 1;
        foreach (var edge in bottomEdges.Concat(shoulderEdges).Concat(topEdges).Concat(lowerEdges).Concat(chamferEdges))
        {
            var start = vertexPoints[edge == default ? vertices[0] : b.Model.Edges.Single(e => e.Id == edge).StartVertexId];
            var end = vertexPoints[b.Model.Edges.Single(e => e.Id == edge).EndVertexId];
            var vector = end - start;
            var id = new CurveGeometryId(edgeCurve++);
            geometry.AddCurve(id, CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(vector))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, id, new ParameterInterval(0d, vector.Length)));
        }

        var zAxis = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var xAxis = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var surface = 1;
        BindPlane(bottomFace, bottom[0], new Vector3D(0d, 1d, 0d), new Vector3D(1d, 0d, 0d));
        BindPlane(topFace, top[0], new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d));
        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            BindPlane(lowerFaces[i], bottom[i], bottom[next] - bottom[i], shoulder[i] - bottom[i]);
            BindPlane(chamferFaces[i], shoulder[i], shoulder[next] - shoulder[i], top[i] - shoulder[i]);
        }
        foreach (var (hole, index) in holes.Select((h, i) => (h, i)))
        {
            var topCurve = new CurveGeometryId(edgeCurve++); var bottomCurve = new CurveGeometryId(edgeCurve++); var seamCurve = new CurveGeometryId(edgeCurve++);
            var centerTop = new Point3D(hole.Hole.CenterX, hole.Hole.CenterY, plan.ZMax);
            var centerBottom = new Point3D(hole.Hole.CenterX, hole.Hole.CenterY, plan.ZMin);
            geometry.AddCurve(topCurve, CurveGeometry.FromCircle(new Circle3Curve(centerTop, zAxis, hole.Hole.Radius, xAxis)));
            geometry.AddCurve(bottomCurve, CurveGeometry.FromCircle(new Circle3Curve(centerBottom, zAxis, hole.Hole.Radius, xAxis)));
            geometry.AddCurve(seamCurve, CurveGeometry.FromLine(new Line3Curve(new Point3D(hole.Hole.CenterX + hole.Hole.Radius, hole.Hole.CenterY, plan.ZMin), zAxis)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(hole.TopCircle, topCurve, new ParameterInterval(0d, 2d * System.Math.PI)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(hole.BottomCircle, bottomCurve, new ParameterInterval(0d, 2d * System.Math.PI)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(hole.Seam, seamCurve, new ParameterInterval(0d, plan.ZMax - plan.ZMin)));
            var sid = new SurfaceGeometryId(surface++);
            geometry.AddSurface(sid, SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(hole.Hole.CenterX, hole.Hole.CenterY, plan.ZMin), zAxis, hole.Hole.Radius, xAxis)));
            // Hole walls bound void material, so their analytic cylinder sense is
            // opposite the retained outer shell.
            bindings.AddFaceBinding(new FaceGeometryBinding(holeFaces[index], sid, SameSense: false));
            vertexPoints[hole.SeamTopVertex] = new Point3D(hole.Hole.CenterX + hole.Hole.Radius, hole.Hole.CenterY, plan.ZMax);
            vertexPoints[hole.SeamBottomVertex] = new Point3D(hole.Hole.CenterX + hole.Hole.Radius, hole.Hole.CenterY, plan.ZMin);
        }

        var body = new BrepBody(b.Model, geometry, bindings, vertexPoints);
        var brepValidation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return brepValidation.IsSuccess ? KernelResult<BrepBody>.Success(body, brepValidation.Diagnostics) : KernelResult<BrepBody>.Failure(brepValidation.Diagnostics);

        void BindPlane(FaceId face, Point3D origin, Vector3D u, Vector3D v)
        {
            var normal = Direction3D.Create(u.Cross(v));
            var uAxis = Direction3D.Create(u);
            var id = new SurfaceGeometryId(surface++);
            geometry.AddSurface(id, SurfaceGeometry.FromPlane(new PlaneSurface(origin, normal, uAxis)));
            bindings.AddFaceBinding(new FaceGeometryBinding(face, id));
        }
    }

    private static KernelResult<bool> Validate(CombinedTopBoundaryChamferThroughHolePlan plan)
    {
        if (!double.IsFinite(plan.Width) || !double.IsFinite(plan.Depth) || !double.IsFinite(plan.ZMin) || !double.IsFinite(plan.ZMax) || !double.IsFinite(plan.ChamferDistance)
            || plan.Width <= Tol || plan.Depth <= Tol || plan.ZMax - plan.ZMin <= Tol || plan.ChamferDistance <= Tol
            || 2d * plan.ChamferDistance >= plan.Width - Tol || 2d * plan.ChamferDistance >= plan.Depth - Tol || plan.ChamferDistance >= plan.ZMax - plan.ZMin - Tol)
            return Failure("CombinedFeaturePlanChainInvalid: bounded top-boundary chamfer dimensions are invalid.");
        foreach (var hole in plan.Holes)
        {
            if (!double.IsFinite(hole.CenterX) || !double.IsFinite(hole.CenterY) || !double.IsFinite(hole.Radius) || hole.Radius <= Tol)
                return Failure($"CombinedFeaturePlanChainInvalid: hole '{hole.FeatureId}' has invalid cylinder data.");
            if (System.Math.Abs(hole.CenterX) + hole.Radius >= plan.Width / 2d - plan.ChamferDistance - Tol || System.Math.Abs(hole.CenterY) + hole.Radius >= plan.Depth / 2d - plan.ChamferDistance - Tol)
                return Failure($"CombinedFeatureInteractionUnsupported: hole '{hole.FeatureId}' intersects or splits the selected outer top-boundary chamfer chain.");
        }
        for (var i = 0; i < plan.Holes.Count; i++)
        for (var j = i + 1; j < plan.Holes.Count; j++)
        {
            var a = plan.Holes[i]; var b = plan.Holes[j];
            if (System.Math.Sqrt(System.Math.Pow(a.CenterX - b.CenterX, 2d) + System.Math.Pow(a.CenterY - b.CenterY, 2d)) <= a.Radius + b.Radius + Tol)
                return Failure($"CombinedFeatureInteractionUnsupported: holes '{a.FeatureId}' and '{b.FeatureId}' overlap.");
        }
        return KernelResult<bool>.Success(true);

        static KernelResult<bool> Failure(string message) => KernelResult<bool>.Failure([new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, message, "CombinedTopBoundaryChamferThroughHole")]);
    }

    private static EdgeId[] Ring(TopologyBuilder builder, VertexId[] vertices, int offset) => Enumerable.Range(0, 4).Select(i => builder.AddEdge(vertices[offset + i], vertices[offset + ((i + 1) % 4)])).ToArray();
    private static LoopId AddLoop(TopologyBuilder builder, IReadOnlyList<Use> uses)
    {
        var loop = builder.AllocateLoopId(); var coedges = new CoedgeId[uses.Count];
        for (var i = 0; i < uses.Count; i++) coedges[i] = builder.AllocateCoedgeId();
        for (var i = 0; i < uses.Count; i++) builder.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop, coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].Reversed));
        builder.AddLoop(new Loop(loop, coedges)); return loop;
    }
    private static Use Forward(EdgeId edge) => new(edge, false);
    private static Use Reversed(EdgeId edge) => new(edge, true);
    private readonly record struct Use(EdgeId Edge, bool Reversed);
    private sealed record HoleTopology(CombinedTopBoundaryChamferThroughHole Hole, VertexId SeamTopVertex, VertexId SeamBottomVertex, EdgeId TopCircle, EdgeId BottomCircle, EdgeId Seam);
}
