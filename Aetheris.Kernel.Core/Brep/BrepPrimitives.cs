using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Minimal M08 primitive solid constructors.
/// Current scope intentionally favors explicit, validator-accepted topology over advanced manifold semantics.
/// </summary>
public static class BrepPrimitives
{
    public static KernelResult<BrepBody> CreateBox(double width, double height, double depth)
    {
        var diagnostics = ValidatePositiveFinite((width, nameof(width)), (height, nameof(height)), (depth, nameof(depth)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var hx = width * 0.5d;
        var hy = height * 0.5d;
        var hz = depth * 0.5d;

        var builder = new TopologyBuilder();

        var vertices = new[]
        {
            builder.AddVertex(), // 1: (-x,-y,-z)
            builder.AddVertex(), // 2: (+x,-y,-z)
            builder.AddVertex(), // 3: (+x,+y,-z)
            builder.AddVertex(), // 4: (-x,+y,-z)
            builder.AddVertex(), // 5: (-x,-y,+z)
            builder.AddVertex(), // 6: (+x,-y,+z)
            builder.AddVertex(), // 7: (+x,+y,+z)
            builder.AddVertex(), // 8: (-x,+y,+z)
        };

        var edges = new[]
        {
            builder.AddEdge(vertices[0], vertices[1]),
            builder.AddEdge(vertices[1], vertices[2]),
            builder.AddEdge(vertices[2], vertices[3]),
            builder.AddEdge(vertices[3], vertices[0]),
            builder.AddEdge(vertices[4], vertices[5]),
            builder.AddEdge(vertices[5], vertices[6]),
            builder.AddEdge(vertices[6], vertices[7]),
            builder.AddEdge(vertices[7], vertices[4]),
            builder.AddEdge(vertices[0], vertices[4]),
            builder.AddEdge(vertices[1], vertices[5]),
            builder.AddEdge(vertices[2], vertices[6]),
            builder.AddEdge(vertices[3], vertices[7]),
        };

        var faces = new[]
        {
            AddFaceWithLoop(builder, [EdgeUse.Forward(edges[0]), EdgeUse.Forward(edges[1]), EdgeUse.Forward(edges[2]), EdgeUse.Forward(edges[3])]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(edges[4]), EdgeUse.Forward(edges[5]), EdgeUse.Forward(edges[6]), EdgeUse.Forward(edges[7])]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(edges[0]), EdgeUse.Forward(edges[9]), EdgeUse.Reversed(edges[4]), EdgeUse.Reversed(edges[8])]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(edges[1]), EdgeUse.Forward(edges[10]), EdgeUse.Reversed(edges[5]), EdgeUse.Reversed(edges[9])]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(edges[2]), EdgeUse.Forward(edges[11]), EdgeUse.Reversed(edges[6]), EdgeUse.Reversed(edges[10])]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(edges[3]), EdgeUse.Forward(edges[8]), EdgeUse.Reversed(edges[7]), EdgeUse.Reversed(edges[11])]),
        };

        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();

        var p1 = new Point3D(-hx, -hy, -hz);
        var p2 = new Point3D(hx, -hy, -hz);
        var p3 = new Point3D(hx, hy, -hz);
        var p4 = new Point3D(-hx, hy, -hz);
        var p5 = new Point3D(-hx, -hy, hz);
        var p6 = new Point3D(hx, -hy, hz);
        var p7 = new Point3D(hx, hy, hz);
        var p8 = new Point3D(-hx, hy, hz);

        var lineCurves = new[]
        {
            (p1, new Vector3D(width, 0d, 0d)),
            (p2, new Vector3D(0d, height, 0d)),
            (p3, new Vector3D(-width, 0d, 0d)),
            (p4, new Vector3D(0d, -height, 0d)),
            (p5, new Vector3D(width, 0d, 0d)),
            (p6, new Vector3D(0d, height, 0d)),
            (p7, new Vector3D(-width, 0d, 0d)),
            (p8, new Vector3D(0d, -height, 0d)),
            (p1, new Vector3D(0d, 0d, depth)),
            (p2, new Vector3D(0d, 0d, depth)),
            (p3, new Vector3D(0d, 0d, depth)),
            (p4, new Vector3D(0d, 0d, depth)),
        };

        for (var i = 0; i < lineCurves.Length; i++)
        {
            geometry.AddCurve(new CurveGeometryId(i + 1), CurveGeometry.FromLine(new Line3Curve(lineCurves[i].Item1, Direction3D.Create(lineCurves[i].Item2))));
        }

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, -hz), Direction3D.Create(new Vector3D(0d, 0d, -1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, hz), Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, -hy, 0d), Direction3D.Create(new Vector3D(0d, -1d, 0d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(hx, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)), Direction3D.Create(new Vector3D(0d, 1d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, hy, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)), Direction3D.Create(new Vector3D(-1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(-hx, 0d, 0d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)), Direction3D.Create(new Vector3D(0d, 1d, 0d)))));

        var bindings = new BrepBindingModel();
        for (var i = 0; i < edges.Length; i++)
        {
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edges[i], new CurveGeometryId(i + 1), new ParameterInterval(0d, 1d)));
        }

        for (var i = 0; i < faces.Length; i++)
        {
            bindings.AddFaceBinding(new FaceGeometryBinding(faces[i], new SurfaceGeometryId(i + 1)));
        }

        return ValidateAndReturn(new BrepBody(builder.Model, geometry, bindings));
    }

    public static KernelResult<BrepBody> CreateCylinder(double radius, double height)
    {
        var diagnostics = ValidatePositiveFinite((radius, nameof(radius)), (height, nameof(height)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var hz = height * 0.5d;
        var topCenter = new Point3D(0d, 0d, hz);
        var bottomCenter = new Point3D(0d, 0d, -hz);

        var builder = new TopologyBuilder();

        // Minimal seam strategy (M08): a single explicit seam edge on the side face plus one circular edge per cap.
        var seamStart = builder.AddVertex();
        var seamEnd = builder.AddVertex();
        var topVertex = builder.AddVertex();
        var bottomVertex = builder.AddVertex();

        var seamEdge = builder.AddEdge(seamStart, seamEnd);
        var topCircleEdge = builder.AddEdge(topVertex, topVertex);
        var bottomCircleEdge = builder.AddEdge(bottomVertex, bottomVertex);

        var sideFace = AddFaceWithLoop(
            builder,
            [
                EdgeUse.Forward(seamEdge),
                EdgeUse.Forward(topCircleEdge),
                EdgeUse.Reversed(seamEdge),
                EdgeUse.Reversed(bottomCircleEdge),
            ]);

        var topFace = AddFaceWithLoop(builder, [EdgeUse.Reversed(topCircleEdge)]);
        var bottomFace = AddFaceWithLoop(builder, [EdgeUse.Forward(bottomCircleEdge)]);

        var shell = builder.AddShell([sideFace, topFace, bottomFace]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var zAxis = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var xAxis = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var yAxis = Direction3D.Create(new Vector3D(0d, 1d, 0d));

        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(new Point3D(radius, 0d, -hz), zAxis)));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromCircle(new Circle3Curve(topCenter, zAxis, radius, xAxis)));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromCircle(new Circle3Curve(bottomCenter, zAxis, radius, xAxis)));

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCylinder(new CylinderSurface(bottomCenter, zAxis, radius, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(topCenter, zAxis, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(bottomCenter, Direction3D.Create(new Vector3D(0d, 0d, -1d)), yAxis)));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(seamEdge, new CurveGeometryId(1), new ParameterInterval(0d, height)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(topCircleEdge, new CurveGeometryId(2), new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(bottomCircleEdge, new CurveGeometryId(3), new ParameterInterval(0d, 2d * double.Pi)));

        bindings.AddFaceBinding(new FaceGeometryBinding(sideFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(topFace, new SurfaceGeometryId(2)));
        bindings.AddFaceBinding(new FaceGeometryBinding(bottomFace, new SurfaceGeometryId(3)));

        return ValidateAndReturn(new BrepBody(builder.Model, geometry, bindings));
    }

    public static KernelResult<BrepBody> CreateSphere(double radius)
    {
        var diagnostics = ValidatePositiveFinite((radius, nameof(radius)));
        if (diagnostics.Count > 0)
        {
            return KernelResult<BrepBody>.Failure(diagnostics);
        }

        var builder = new TopologyBuilder();

        // M08 simplification: represent the sphere as one closed periodic face with no boundary loops.
        var sphereFace = builder.AddFace([]);
        var shell = builder.AddShell([sphereFace]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        geometry.AddSurface(
            new SurfaceGeometryId(1),
            SurfaceGeometry.FromSphere(
                new SphereSurface(
                    Point3D.Origin,
                    Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                    radius,
                    Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        var bindings = new BrepBindingModel();
        bindings.AddFaceBinding(new FaceGeometryBinding(sphereFace, new SurfaceGeometryId(1)));

        return ValidateAndReturn(new BrepBody(builder.Model, geometry, bindings));
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
    {
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new CoedgeId[edgeUses.Count];

        for (var i = 0; i < edgeUses.Count; i++)
        {
            coedgeIds[i] = builder.AllocateCoedgeId();
        }

        for (var i = 0; i < edgeUses.Count; i++)
        {
            var next = coedgeIds[(i + 1) % edgeUses.Count];
            var prev = coedgeIds[(i + edgeUses.Count - 1) % edgeUses.Count];
            builder.AddCoedge(new Coedge(coedgeIds[i], edgeUses[i].EdgeId, loopId, next, prev, edgeUses[i].IsReversed));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return builder.AddFace([loopId]);
    }

    private static KernelResult<BrepBody> ValidateAndReturn(BrepBody body)
    {
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static List<KernelDiagnostic> ValidatePositiveFinite(params (double Value, string Name)[] values)
    {
        var diagnostics = new List<KernelDiagnostic>();

        foreach (var (value, name) in values)
        {
            if (!double.IsFinite(value) || value <= 0d)
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"{name} must be finite and greater than zero."));
            }
        }

        return diagnostics;
    }

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, IsReversed: false);

        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, IsReversed: true);
    }
}
