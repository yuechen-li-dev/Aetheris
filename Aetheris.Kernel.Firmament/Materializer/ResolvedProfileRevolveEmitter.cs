using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

public sealed record RevolveBRepPlan(
    string StableId, string ProfileStableId, string AxisStableId, ConstructionPlane Frame,
    Point3D AxisOrigin, Direction3D AxisDirection, Direction3D RadialDirection,
    double SweepRadians, bool IsFull, string DeterministicSignature,
    IReadOnlyList<string> SurfaceRoles, IReadOnlyList<string> Diagnostics);

public sealed record ResolvedProfileRevolveResult(
    BrepBody? Body, RevolveBRepPlan? Plan, IReadOnlyList<string> Diagnostics,
    IReadOnlyDictionary<string, FaceId>? SemanticFaces = null)
{
    public bool Succeeded => Body is not null && Plan is not null;
}

/// <summary>
/// Authoritative bounded revolution plan/materializer. The source Profile remains the only
/// boundary authority; periodic seams are representational and partial caps are semantic.
/// </summary>
public static class ResolvedProfileRevolveEmitter
{
    private const double Tol = 1e-7;

    public static ResolvedProfileRevolveResult TryEmit(ResolvedRevolve revolve)
    {
        var diagnostics = new List<string>();
        var validation = ResolvedProfile2DValidator.Validate(revolve.Profile);
        if (!validation.IsValid) diagnostics.AddRange(validation.Diagnostics);
        if (revolve.Profile.Loops.Count != 1) diagnostics.Add($"firmament-revolve-inner-loops-not-supported:{revolve.Name}");
        var sourceOrigin = new Point3D(revolve.Axis.Origin.X, revolve.Axis.Origin.Y, revolve.Axis.Origin.Z);
        var sourceDirection = new Vector3D(revolve.Axis.Direction.X, revolve.Axis.Direction.Y, revolve.Axis.Direction.Z);
        if (!Direction3D.TryCreate(sourceDirection, out var sourceAxis)) diagnostics.Add($"firmament-revolve-axis-zero-direction:{revolve.Name}");
        var sweepMagnitude = Math.Abs(revolve.SweepRadians);
        if (!double.IsFinite(sweepMagnitude) || sweepMagnitude <= Tol) diagnostics.Add($"firmament-revolve-zero-angle:{revolve.Name}");
        if (sweepMagnitude > 2d * Math.PI + Tol) diagnostics.Add($"firmament-revolve-angle-out-of-range:{revolve.Name}");
        if (diagnostics.Count != 0) return new(null, null, diagnostics.Distinct(StringComparer.Ordinal).ToArray());

        var frame = revolve.Profile.EffectiveConstructionPlane;
        var localAxisOrigin = frame.ToLocal(sourceOrigin);
        var localDx = sourceAxis.ToVector().Dot(frame.AxisX.ToVector());
        var localDy = sourceAxis.ToVector().Dot(frame.AxisY.ToVector());
        var localLength = Math.Sqrt(localDx * localDx + localDy * localDy);
        if (Math.Abs(localAxisOrigin.Z) > Tol || localLength <= Tol || Math.Abs(sourceAxis.ToVector().Dot(frame.AxisZ.ToVector())) > Tol)
            return new(null, null, [$"firmament-revolve-axis-not-in-profile-plane:{revolve.Name}"]);
        localDx /= localLength; localDy /= localLength;

        var firstNonAxis = revolve.Profile.Loops[0].Segments
            .SelectMany(segment => Endpoints(segment.Geometry))
            .FirstOrDefault(point => Math.Abs(Radius(point)) > Tol);
        var sideSign = Math.Sign(Radius(firstNonAxis));
        if (sideSign == 0) sideSign = 1;
        var radialVector = frame.ToWorldVector((-localDy * sideSign, localDx * sideSign));
        var radialDirection = Direction3D.Create(radialVector);
        var axisDirection = sourceAxis;
        var isFull = Math.Abs(sweepMagnitude - 2d * Math.PI) <= Tol;
        var roles = revolve.Profile.Loops[0].Segments.Select(segment => "RevolvedSpan:" + segment.Name)
            .Concat(isFull ? [] : ["Start", "End"]).ToArray();
        var signatureText = string.Join("|", revolve.StableId, revolve.Profile.Name, revolve.Axis.StableId,
            revolve.SweepRadians.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            string.Join(";", revolve.Profile.Loops[0].Segments.Select(s => s.Name + ":" + s.Geometry)));
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureText)));
        var plan = new RevolveBRepPlan(revolve.StableId, "profile:" + revolve.Profile.Name, revolve.Axis.StableId, frame,
            sourceOrigin, axisDirection, radialDirection, revolve.SweepRadians, isFull, signature, roles,
            ["revolve-profile-authoritative", "revolve-axis-explicit", isFull ? "revolve-periodic-seam" : "revolve-semantic-radial-caps"]);

        if (isFull && TryEmitSphere(revolve, plan, localAxisOrigin, localDx, localDy, out var sphere)) return sphere;
        return EmitGeneral(revolve, plan, localAxisOrigin, localDx, localDy, sideSign, sweepMagnitude);

        double Radius((double X, double Y) point) => localDx * (point.Y - localAxisOrigin.Y) - localDy * (point.X - localAxisOrigin.X);
    }

    private static ResolvedProfileRevolveResult EmitGeneral(ResolvedRevolve revolve, RevolveBRepPlan plan,
        (double X, double Y, double Z) localAxisOrigin, double localDx, double localDy, int sideSign, double sweep)
    {
        var segments = revolve.Profile.Loops[0].Segments;
        var rotationAxis = revolve.SweepRadians < 0d ? Direction3D.Create(-plan.AxisDirection.ToVector()) : plan.AxisDirection;
        if (segments.Any(segment => segment.Geometry is LineArcFullCircle2D or LineArcFullEllipse2D))
            return new(null, plan, [$"firmament-revolve-full-closed-curve-materialization-not-supported:{revolve.Name}"]);
        var starts = segments.Select(segment => Endpoints(segment.Geometry)[0]).ToArray();
        var radii = starts.Select(Radius).ToArray();
        if (radii.Any(radius => radius <= Tol))
            return new(null, plan, [$"firmament-revolve-axis-touch-materialization-not-supported:{revolve.Name}"]);

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var semanticFaces = new Dictionary<string, FaceId>(StringComparer.Ordinal);
        var startVertices = starts.Select(_ => builder.AddVertex()).ToArray();
        var endVertices = plan.IsFull ? startVertices : starts.Select(_ => builder.AddVertex()).ToArray();
        var startEdges = new EdgeId?[segments.Count];
        var endEdges = new EdgeId?[segments.Count];
        var angularEdges = new EdgeId[segments.Count];
        var vertexPoints = new Dictionary<VertexId, Point3D>();
        var curveId = 1;
        var surfaceId = 1;

        for (var i = 0; i < starts.Length; i++)
        {
            var start = World(starts[i], 0d);
            var end = World(starts[i], sweep);
            vertexPoints[startVertices[i]] = start;
            vertexPoints[endVertices[i]] = end;
            angularEdges[i] = builder.AddEdge(startVertices[i], endVertices[i]);
            var center = plan.AxisOrigin + plan.AxisDirection.ToVector() * Axial(starts[i]);
            var circle = new Circle3Curve(center, rotationAxis, radii[i], Direction3D.Create(start - center));
            var id = new CurveGeometryId(curveId++);
            geometry.AddCurve(id, CurveGeometry.FromCircle(circle));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(angularEdges[i], id, new ParameterInterval(0d, plan.IsFull ? 2d * Math.PI : sweep)));
        }

        for (var i = 0; i < segments.Count; i++)
        {
            var next = (i + 1) % segments.Count;
            var periodicPlanarAnnulus = plan.IsFull && segments[i].Geometry is LineArcLineSegment2D radial
                && Math.Abs(Axial(radial.Start) - Axial(radial.End)) <= Tol;
            if (!periodicPlanarAnnulus)
            {
                startEdges[i] = builder.AddEdge(startVertices[i], startVertices[next]);
                BindProfileCurve(startEdges[i]!.Value, segments[i].Geometry, 0d);
            }
            if (!plan.IsFull)
            {
                endEdges[i] = builder.AddEdge(endVertices[i], endVertices[next]);
                BindProfileCurve(endEdges[i]!.Value, segments[i].Geometry, sweep);
            }
            else endEdges[i] = startEdges[i];
        }

        var sideFaces = new List<FaceId>();
        for (var i = 0; i < segments.Count; i++)
        {
            var next = (i + 1) % segments.Count;
            var face = startEdges[i].HasValue
                ? AddFace(builder,
                [
                    Use.Forward(startEdges[i]!.Value), Use.Forward(angularEdges[next]),
                    Use.Reversed(endEdges[i]!.Value), Use.Reversed(angularEdges[i])
                ])
                : AddFaceWithLoops(builder,
                [
                    [Use.Forward(angularEdges[next])],
                    [Use.Reversed(angularEdges[i])]
                ]);
            sideFaces.Add(face);
            semanticFaces["RevolvedSpan:" + segments[i].Name] = face;
            var sid = new SurfaceGeometryId(surfaceId++);
            var surface = SideSurface(segments[i].Geometry);
            geometry.AddSurface(sid, surface);
            bindings.AddFaceBinding(new FaceGeometryBinding(face, sid, SideSameSense(segments[i].Geometry, surface)));
        }

        var faces = new List<FaceId>(sideFaces);
        if (!plan.IsFull)
        {
            var startFace = AddFace(builder, startEdges.Reverse().Select(edge => Use.Reversed(edge!.Value)).ToArray());
            var endFace = AddFace(builder, endEdges.Select(edge => Use.Forward(edge!.Value)).ToArray());
            faces.Add(startFace); faces.Add(endFace);
            semanticFaces["Start"] = startFace; semanticFaces["End"] = endFace;
            var startNormal = Direction3D.Create(-rotationAxis.ToVector().Cross(plan.RadialDirection.ToVector()));
            var endRadial = Direction3D.Create(Rotate(plan.RadialDirection.ToVector(), rotationAxis, sweep));
            var endNormal = Direction3D.Create(rotationAxis.ToVector().Cross(endRadial.ToVector()));
            var s0 = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(s0, SurfaceGeometry.FromPlane(new PlaneSurface(plan.AxisOrigin, startNormal, plan.AxisDirection))); bindings.AddFaceBinding(new(startFace, s0));
            var s1 = new SurfaceGeometryId(surfaceId); geometry.AddSurface(s1, SurfaceGeometry.FromPlane(new PlaneSurface(plan.AxisOrigin, endNormal, plan.AxisDirection))); bindings.AddFaceBinding(new(endFace, s1));
        }
        var shell = builder.AddShell(faces); builder.AddBody([shell]);
        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? new(body, plan, plan.Diagnostics.Concat(["revolve-brep-plan-consumed"]).ToArray(), semanticFaces)
            : new(null, plan, validation.Diagnostics.Select(d => d.Message).ToArray());

        double Radius((double X, double Y) point) => sideSign * (localDx * (point.Y - localAxisOrigin.Y) - localDy * (point.X - localAxisOrigin.X));
        double Axial((double X, double Y) point) => localDx * (point.X - localAxisOrigin.X) + localDy * (point.Y - localAxisOrigin.Y);
        Point3D World((double X, double Y) point, double angle) => plan.AxisOrigin + plan.AxisDirection.ToVector() * Axial(point) + Rotate(plan.RadialDirection.ToVector() * Radius(point), rotationAxis, angle);
        void BindProfileCurve(EdgeId edge, LineArcProfileCurve2D curve, double angle)
        {
            var (worldCurve, interval) = WorldCurve(curve, angle, World);
            var id = new CurveGeometryId(curveId++); geometry.AddCurve(id, worldCurve); bindings.AddEdgeBinding(new(edge, id, interval));
        }
        SurfaceGeometry SideSurface(LineArcProfileCurve2D curve)
        {
            if (curve is LineArcLineSegment2D line)
            {
                var r0 = Radius(line.Start); var r1 = Radius(line.End); var a0 = Axial(line.Start); var a1 = Axial(line.End);
                var center0 = plan.AxisOrigin + plan.AxisDirection.ToVector() * a0;
                if (Math.Abs(r0 - r1) <= Tol) return SurfaceGeometry.FromCylinder(new CylinderSurface(center0, plan.AxisDirection, r0, plan.RadialDirection));
                if (Math.Abs(a0 - a1) <= Tol) return SurfaceGeometry.FromPlane(new PlaneSurface(center0, plan.AxisDirection, plan.RadialDirection));
                var slope = (r1 - r0) / (a1 - a0); var apex = plan.AxisOrigin + plan.AxisDirection.ToVector() * (a0 - r0 / slope);
                var coneAxis = slope > 0d ? plan.AxisDirection : Direction3D.Create(-plan.AxisDirection.ToVector());
                return SurfaceGeometry.FromCone(new ConeSurface(apex, coneAxis, Math.Atan(Math.Abs(slope)), plan.RadialDirection));
            }
            var (directrix, _) = WorldCurve(curve, 0d, World);
            if (curve is LineArcCircularArc2D arc)
            {
                var centerRadius = Radius(arc.Center); var centerWorld = World(arc.Center, 0d);
                if (Math.Abs(centerRadius) <= Tol) return SurfaceGeometry.FromSphere(new SphereSurface(centerWorld, plan.AxisDirection, arc.Radius, plan.RadialDirection));
                var torusCenter = plan.AxisOrigin + plan.AxisDirection.ToVector() * Axial(arc.Center);
                return SurfaceGeometry.FromTorus(new TorusSurface(torusCenter, plan.AxisDirection, Math.Abs(centerRadius), arc.Radius, plan.RadialDirection));
            }
            return SurfaceGeometry.FromSurfaceOfRevolution(new SurfaceOfRevolutionSurface(directrix, plan.AxisOrigin, plan.AxisDirection));
        }
        bool SideSameSense(LineArcProfileCurve2D curve, SurfaceGeometry surface)
        {
            if (curve is not LineArcLineSegment2D line) return true;
            var dr = Radius(line.End) - Radius(line.Start); var da = Axial(line.End) - Axial(line.Start);
            var desired = plan.RadialDirection.ToVector() * da - plan.AxisDirection.ToVector() * dr;
            var carrier = surface.Kind switch
            {
                SurfaceGeometryKind.Cylinder => plan.RadialDirection.ToVector(),
                SurfaceGeometryKind.Plane => surface.Plane!.Value.Normal.ToVector(),
                SurfaceGeometryKind.Cone => surface.Cone!.Value.Normal(0d).ToVector(),
                _ => desired
            };
            return desired.Dot(carrier) >= 0d;
        }
    }

    private static bool TryEmitSphere(ResolvedRevolve revolve, RevolveBRepPlan plan,
        (double X, double Y, double Z) localAxisOrigin, double localDx, double localDy,
        out ResolvedProfileRevolveResult result)
    {
        result = default!;
        var segments = revolve.Profile.Loops[0].Segments;
        var arcSegments = segments.Where(segment => segment.Geometry is LineArcCircularArc2D).ToArray();
        if (segments.Count != 2 || arcSegments.Length != 1 || arcSegments[0].Geometry is not LineArcCircularArc2D arc || Math.Abs(Math.Abs(arc.SweepAngleRadians) - Math.PI) > Tol) return false;
        var arcSegment = arcSegments[0];
        var line = segments.Single(segment => segment != arcSegment).Geometry as LineArcLineSegment2D;
        if (line is null) return false;
        double Radius((double X, double Y) p) => localDx * (p.Y - localAxisOrigin.Y) - localDy * (p.X - localAxisOrigin.X);
        if (Math.Abs(Radius(arc.Center)) > Tol || Math.Abs(Radius(line.Start)) > Tol || Math.Abs(Radius(line.End)) > Tol) return false;
        var center = revolve.Profile.EffectiveConstructionPlane.ToWorld(arc.Center);
        var builder = new TopologyBuilder(); var face = builder.AddFace([]); var shell = builder.AddShell([face]); builder.AddBody([shell]);
        var geometry = new BrepGeometryStore(); geometry.AddSurface(new(1), SurfaceGeometry.FromSphere(new SphereSurface(center, plan.AxisDirection, arc.Radius, plan.RadialDirection)));
        var bindings = new BrepBindingModel(); bindings.AddFaceBinding(new(face, new(1)));
        var body = new BrepBody(builder.Model, geometry, bindings);
        result = new(body, plan, plan.Diagnostics.Concat(["revolve-sphere-analytic-carrier"]).ToArray(), new Dictionary<string, FaceId> { ["RevolvedSpan:" + arcSegment.Name] = face });
        return true;
    }

    private static ((double X, double Y)[] Points, bool Closed) CurveEndpoints(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => ([line.Start, line.End], false),
        LineArcCircularArc2D arc => ([(arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)), (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians))], false),
        _ => ([], true)
    };
    private static (double X, double Y)[] Endpoints(LineArcProfileCurve2D curve) => CurveEndpoints(curve).Points;

    private static (CurveGeometry Curve, ParameterInterval Interval) WorldCurve(LineArcProfileCurve2D curve, double rotation,
        Func<(double X, double Y), double, Point3D> world) => curve switch
    {
        LineArcLineSegment2D line => Line(world(line.Start, rotation), world(line.End, rotation)),
        LineArcCircularArc2D arc => Arc(arc, rotation, world),
        _ => throw new NotSupportedException("The bounded revolve curve family is line/arc in X1.")
    };

    private static (CurveGeometry, ParameterInterval) Line(Point3D start, Point3D end)
    {
        var delta = end - start; return (CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(delta))), new(0d, delta.Length));
    }
    private static (CurveGeometry, ParameterInterval) Arc(LineArcCircularArc2D arc, double rotation, Func<(double X, double Y), double, Point3D> world)
    {
        var start2 = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
        var mid2 = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + Math.Sign(arc.SweepAngleRadians) * Math.PI / 2d), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + Math.Sign(arc.SweepAngleRadians) * Math.PI / 2d));
        var center = world(arc.Center, rotation); var start = world(start2, rotation); var mid = world(mid2, rotation);
        var normal = Direction3D.Create((start - center).Cross(mid - center) * Math.Sign(arc.SweepAngleRadians));
        var circle = new Circle3Curve(center, normal, arc.Radius, Direction3D.Create(start - center));
        return (CurveGeometry.FromCircle(circle), new(0d, Math.Abs(arc.SweepAngleRadians)));
    }

    private static Vector3D Rotate(Vector3D vector, Direction3D axis, double angle)
    {
        var k = axis.ToVector(); return vector * Math.Cos(angle) + k.Cross(vector) * Math.Sin(angle) + k * k.Dot(vector) * (1d - Math.Cos(angle));
    }

    private static FaceId AddFace(TopologyBuilder builder, IReadOnlyList<Use> uses)
    {
        var loop = builder.AllocateLoopId(); var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < uses.Count; i++) builder.AddCoedge(new(coedges[i], uses[i].Edge, loop, coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].IsReversed));
        builder.AddLoop(new Loop(loop, coedges)); return builder.AddFace([loop]);
    }
    private static FaceId AddFaceWithLoops(TopologyBuilder builder, IReadOnlyList<IReadOnlyList<Use>> boundaries)
    {
        var loops = new List<LoopId>();
        foreach (var uses in boundaries)
        {
            var loop = builder.AllocateLoopId(); var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < uses.Count; i++) builder.AddCoedge(new(coedges[i], uses[i].Edge, loop, coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].IsReversed));
            builder.AddLoop(new Loop(loop, coedges)); loops.Add(loop);
        }
        return builder.AddFace(loops);
    }
    private readonly record struct Use(EdgeId Edge, bool IsReversed)
    {
        public static Use Forward(EdgeId edge) => new(edge, false);
        public static Use Reversed(EdgeId edge) => new(edge, true);
    }
}
