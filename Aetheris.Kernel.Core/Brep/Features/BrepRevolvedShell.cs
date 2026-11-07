using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Planar;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>The axis a meridian turns about: meridian x is the radius along <see cref="Radial"/>, y the position along <see cref="Axis"/>.</summary>
public sealed record RevolutionFrame(Point3D Origin, Direction3D Axis, Direction3D Radial)
{
    public static RevolutionFrame WorldZ { get; } = new(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)));
}

/// <summary>The face a meridian span became, by its index in the loop.</summary>
public sealed record BrepRevolvedShellResult(BrepBody Body, IReadOnlyDictionary<int, FaceId> SpanFaces);

/// <summary>
/// A full turn of one closed line/arc meridian loop. Each span becomes one face of revolution, bounded by the full
/// circles its endpoints sweep: a radial line a planar disk or annulus, an axial line a cylinder, a sloped line a
/// cone, an arc a torus (or a sphere when centred on the axis). Spans lying on the axis make no face. Only a radial
/// span may meet the axis, so no face has an apex or pole. No seam edges are needed: every face is bounded by circles.
/// </summary>
public static class BrepRevolvedShell
{
    private const double Tol = 1e-9;

    public static KernelResult<BrepRevolvedShellResult> Build(IReadOnlyList<PlanarSpan> meridian, RevolutionFrame frame)
    {
        if (meridian.Count < 2 || meridian.Any(s => s is not (PlanarLine or PlanarArc)))
            return Failure("revolve-shell-meridian-invalid", "A revolved shell needs a closed loop of at least two lines and arcs.");
        var loop = LineArcInset.SignedArea(meridian) > 0d ? meridian : LineArcInset.Reverse(meridian);
        var points = loop.Select(LineArcInset.StartPoint).ToArray();
        for (var i = 0; i < loop.Count; i++)
        {
            var end = LineArcInset.EndPoint(loop[i]);
            var next = points[(i + 1) % loop.Count];
            if (double.Hypot(end.X - next.X, end.Y - next.Y) > 1e-7)
                return Failure("revolve-shell-meridian-open", $"Meridian span {i} does not end where span {(i + 1) % loop.Count} starts.");
        }
        if (points.Any(p => p.X < -Tol)) return Failure("revolve-shell-crosses-axis", "The meridian crosses the axis.");

        var axis = frame.Axis.ToVector();
        var radial = frame.Radial.ToVector();
        // Adding +0 clears negative zeros, so points match those built from literal coordinates.
        Point3D At(double r, double z) { var p = frame.Origin + axis * z + radial * r; return new Point3D(p.X + 0d, p.Y + 0d, p.Z + 0d); }
        Direction3D Along(double sign) { var v = axis * sign; return Direction3D.Create(new Vector3D(v.X + 0d, v.Y + 0d, v.Z + 0d)); }
        bool OnAxis((double X, double Y) p) => double.Abs(p.X) <= 1e-7;

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, Point3D>();
        var circles = new EdgeId?[loop.Count];
        var nextCurve = 1;
        var nextSurface = 1;
        for (var i = 0; i < loop.Count; i++)
        {
            if (OnAxis(points[i])) continue;
            var vertex = builder.AddVertex();
            vertexPoints[vertex] = At(points[i].X, points[i].Y);
            var edge = builder.AddEdge(vertex, vertex);
            var curveId = new CurveGeometryId(nextCurve++);
            geometry.AddCurve(curveId, CurveGeometry.FromCircle(new Circle3Curve(frame.Origin + axis * points[i].Y, frame.Axis, points[i].X, frame.Radial)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, curveId, new ParameterInterval(0d, 2d * double.Pi)));
            circles[i] = edge;
        }

        var faces = new List<FaceId>();
        var spanFaces = new Dictionary<int, FaceId>();
        for (var i = 0; i < loop.Count; i++)
        {
            var p = points[i];
            var q = points[(i + 1) % loop.Count];
            if (OnAxis(p) && OnAxis(q) && loop[i] is PlanarLine) continue;
            // Outward in the meridian is right of travel for a counter-clockwise loop.
            var (dr, dz) = MidTangent(loop[i]);
            var (nr, nz) = (dz, -dr);
            SurfaceGeometry surface;
            bool sameSense;
            switch (loop[i])
            {
                case PlanarLine when double.Abs(q.Y - p.Y) <= Tol:
                    surface = SurfaceGeometry.FromPlane(new PlaneSurface(At(0, p.Y), Along(double.Sign(nz)), frame.Radial));
                    sameSense = true;
                    break;
                case PlanarLine when OnAxis(p) || OnAxis(q):
                    return Failure("revolve-shell-apex-unsupported", $"Meridian span {i} meets the axis at a point; only a flat (radial) span may meet the axis.");
                case PlanarLine when double.Abs(q.X - p.X) <= Tol:
                    surface = SurfaceGeometry.FromCylinder(new CylinderSurface(At(0, p.Y), frame.Axis, p.X, frame.Radial));
                    sameSense = nr > 0d;
                    break;
                case PlanarLine:
                {
                    var k = (q.X - p.X) / (q.Y - p.Y);
                    var apexZ = p.Y - p.X / k;
                    var coneAxis = Along(double.Sign(k));
                    var cone = new ConeSurface(At(0, apexZ), coneAxis, double.Atan(double.Abs(k)), frame.Radial);
                    surface = SurfaceGeometry.FromCone(cone);
                    sameSense = cone.Normal(0d).ToVector().Dot(radial * nr + axis * nz) > 0d;
                    break;
                }
                case PlanarArc arc when OnAxis(p) || OnAxis(q):
                    return Failure("revolve-shell-pole-unsupported", $"Meridian arc {i} meets the axis; only a flat (radial) span may meet the axis.");
                case PlanarArc arc when double.Abs(arc.Center.X) > 1e-7 && arc.Center.X <= arc.Radius + Tol:
                    return Failure("revolve-shell-spindle-unsupported", $"Meridian arc {i} is centred closer to the axis than its radius.");
                case PlanarArc arc:
                {
                    var mid = arc.StartAngle + arc.Sweep / 2d;
                    var (mr, mz) = (double.Cos(mid), double.Sin(mid));
                    // A sphere's or torus' own normal points away from the arc's centre.
                    sameSense = mr * nr + mz * nz > 0d;
                    surface = double.Abs(arc.Center.X) <= 1e-7
                        ? SurfaceGeometry.FromSphere(new SphereSurface(At(0, arc.Center.Y), frame.Axis, arc.Radius, frame.Radial))
                        : SurfaceGeometry.FromTorus(new TorusSurface(At(0, arc.Center.Y), frame.Axis, arc.Center.X, arc.Radius, frame.Radial));
                    break;
                }
                default:
                    return Failure("revolve-shell-meridian-invalid", $"Meridian span {i} is not a line or arc.");
            }
            // The circle at the span's start bounds the face counter-clockwise about its outward normal, the circle at
            // its end clockwise. Loops follow the face's outward normal, whatever the surface's own normal, so each
            // circle is walked once each way by the two faces it joins.
            var loops = new List<LoopId>();
            void AddLoop(EdgeId edge, bool reversedAboutOutward)
            {
                var loopId = builder.AllocateLoopId();
                var coedge = builder.AllocateCoedgeId();
                builder.AddCoedge(new Coedge(coedge, edge, loopId, coedge, coedge, reversedAboutOutward));
                builder.AddLoop(new Loop(loopId, [coedge]));
                loops.Add(loopId);
            }
            if (circles[i] is { } startCircle) AddLoop(startCircle, false);
            if (circles[(i + 1) % loop.Count] is { } endCircle) AddLoop(endCircle, true);
            if (loops.Count == 0) return Failure("revolve-shell-meridian-invalid", $"Meridian span {i} sweeps no area.");
            var face = builder.AddFace(loops);
            // An annulus's outer boundary is its larger circle, whichever end of the span that is.
            if (surface.Kind == SurfaceGeometryKind.Plane && loops.Count == 2)
            {
                var startOuter = p.X > q.X;
                bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(face, loops[0], startOuter ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
                bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(face, loops[1], startOuter ? FaceBoundaryRole.Inner : FaceBoundaryRole.Outer));
            }
            var surfaceId = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(surfaceId, surface);
            bindings.AddFaceBinding(new FaceGeometryBinding(face, surfaceId, sameSense));
            faces.Add(face);
            spanFaces[i] = face;
        }
        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);
        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid)
            return Failure("revolve-shell-preflight", string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Take(6).Select(d => d.Code + ": " + d.Message)));
        return KernelResult<BrepRevolvedShellResult>.Success(new(body, spanFaces));
    }

    private static (double R, double Z) MidTangent(PlanarSpan span)
    {
        switch (span)
        {
            case PlanarLine line:
            {
                var (dx, dy) = (line.End.X - line.Start.X, line.End.Y - line.Start.Y);
                var l = double.Hypot(dx, dy);
                return (dx / l, dy / l);
            }
            case PlanarArc arc:
            {
                var mid = arc.StartAngle + arc.Sweep / 2d;
                var s = double.Sign(arc.Sweep);
                return (-double.Sin(mid) * s, double.Cos(mid) * s);
            }
            default: throw new NotSupportedException();
        }
    }

    private static KernelResult<BrepRevolvedShellResult> Failure(string code, string message) =>
        KernelResult<BrepRevolvedShellResult>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"{code}: {message}", "BrepRevolvedShell")]);
}
