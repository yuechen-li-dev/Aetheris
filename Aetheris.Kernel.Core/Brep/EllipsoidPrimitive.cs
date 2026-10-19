using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>Exact native ellipsoid stock with explicit shared octant boundaries.</summary>
public static class EllipsoidPrimitive
{
    public static KernelResult<BrepBody> Create(double radiusX, double radiusY, double radiusZ)
    {
        if (new[] { radiusX, radiusY, radiusZ }.Any(r => !double.IsFinite(r) || r <= 1e-7 || r > 1e100))
            return KernelResult<BrepBody>.Failure([new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                "EllipsoidRadiiInvalid: radii must be finite positive X/Y/Z semiaxes above the kernel construction tolerance and within finite engineering range.", "Ellipsoid.Admission")]);

        // Eight native quadric octants project exactly to rational quadratic
        // STEP patches. Shared equator/meridian edges close the engineering shell.
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var positions = new Dictionary<VertexId, Point3D>();
        (double X, double Y)[] longitude = [(1, 0), (0, 1), (-1, 0), (0, -1)];
        var equator = longitude.Select(p => AddVertex(new(radiusX * p.X, radiusY * p.Y, 0))).ToArray();
        var north = AddVertex(new(0, 0, radiusZ)); var south = AddVertex(new(0, 0, -radiusZ));
        var ring = Enumerable.Range(0, 4).Select(i => Arc(equator[i], equator[(i + 1) % 4])).ToArray();
        var upper = equator.Select(v => Arc(v, north)).ToArray(); var lower = equator.Select(v => Arc(v, south)).ToArray();
        var faces = new List<FaceId>();
        for (var i = 0; i < 4; i++) foreach (var top in new[] { true, false })
        {
            var a = longitude[i];
            var surface = new EllipsoidSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)),
                Direction3D.Create(new Vector3D(a.X, a.Y, 0)), i % 2 == 0 ? radiusX : radiusY,
                i % 2 == 0 ? radiusY : radiusX, radiusZ, top);
            (EdgeId Edge, bool Reverse)[] uses = top
                ? [(ring[i], false), (upper[(i + 1) % 4], false), (upper[i], true)]
                : [(ring[i], true), (lower[i], false), (lower[(i + 1) % 4], true)];
            var loop = builder.AllocateLoopId(); var ids = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var k = 0; k < 3; k++) builder.AddCoedge(new(ids[k], uses[k].Edge, loop, ids[(k + 1) % 3], ids[(k + 2) % 3], uses[k].Reverse));
            builder.AddLoop(new Loop(loop, ids)); var face = builder.AddFace([loop]); faces.Add(face);
            var surfaceId = new SurfaceGeometryId(geometry.Surfaces.Count() + 1);
            geometry.AddSurface(surfaceId, SurfaceGeometry.FromEllipsoid(surface)); bindings.AddFaceBinding(new(face, surfaceId));
        }
        builder.AddBody([builder.AddShell(faces)]);
        return KernelResult<BrepBody>.Success(new(builder.Model, geometry, bindings, positions));

        VertexId AddVertex(Point3D point) { var id = builder.AddVertex(); positions.Add(id, point); return id; }
        EdgeId Arc(VertexId start, VertexId end)
        {
            var a = positions[start] - Point3D.Origin; var b = positions[end] - Point3D.Origin;
            var major = a.Length >= b.Length ? a : b; var minor = a.Length >= b.Length ? b : a * -1;
            var angle = a.Length >= b.Length ? 0d : 1.5 * double.Pi;
            var ellipse = new Ellipse3Curve(Point3D.Origin, Direction3D.Create(Direction3D.Create(major).ToVector().Cross(Direction3D.Create(minor).ToVector())), major.Length, minor.Length, Direction3D.Create(major));
            var id = new CurveGeometryId(geometry.Curves.Count() + 1); geometry.AddCurve(id, CurveGeometry.FromEllipse(ellipse));
            var edge = builder.AddEdge(start, end); bindings.AddEdgeBinding(new(edge, id, new(angle, angle + double.Pi / 2)));
            return edge;
        }
    }
}
