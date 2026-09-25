using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using System.Diagnostics;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>Stable, feature-local identities; topology indices are deliberately not public identities.</summary>
public sealed record HollowRadialCutIdentity(string Hole, string OuterOpening, string InnerOpening, string Wall)
{
    public static HollowRadialCutIdentity Single { get; } = new("RadialHole", "RadialHole.OuterOpening", "RadialHole.InnerOpening", "RadialHole.Wall");
}

public sealed record HollowRadialCutCertificate(
    double OuterCurveBoundMm, double InnerCurveBoundMm,
    double OuterHostPcurveBoundMm, double OuterToolPcurveBoundMm,
    double InnerHostPcurveBoundMm, double InnerToolPcurveBoundMm,
    int OuterSegments, int InnerSegments);

public sealed record HollowRadialCutBuildTimings(double IntersectionsMs, double CurveRealizationMs,
    double PcurveRealizationMs, double TopologyMs, double ValidationMs);

/// <summary>Semantic names resolve to this particular realization's topology; numeric IDs are never the feature identity.</summary>
public sealed record HollowRadialCutTopologyMap(
    IReadOnlyDictionary<string, FaceId> Faces,
    IReadOnlyDictionary<string, LoopId> Loops,
    IReadOnlyDictionary<string, IReadOnlyList<EdgeId>> Edges);

public sealed record HollowRadialCutResult(BrepBody Body, HollowRadialCutIdentity Identity,
    HollowRadialCutCertificate Certificate, double InsideRadialCoordinate, double OutsideRadialCoordinate,
    HollowRadialCutBuildTimings Timings, HollowRadialCutTopologyMap TopologyMap);

/// <summary>
/// Constructive local through-wall hole on the positive radial side of a semantic
/// cylindrical Hollow vessel. This is one wall only, with no Boolean subtraction.
/// The host seam is placed a quarter turn from the opening. Each non-conic
/// intersection loop has two finite certified edges; one cutter seam joins them.
/// </summary>
public static class BrepHollowRadialCut
{
    public static KernelResult<HollowRadialCutResult> Build(
        ThinWalledBodyRealization hollow, double holeRadius, double axialPosition,
        double angleRadians, double? insideRadialCoordinate = null,
        double? outsideRadialCoordinate = null, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(hollow);
        var tol = tolerance ?? ToleranceContext.Default;
        if (hollow.Feature.PrimitiveKind != "Cylinder" || hollow.Feature.Openings.Count != 1 || hollow.Feature.Openings[0] != "Top")
            return Failure("Only a top-open Cylinder<Hollow> authority is admitted.", "Brep.HollowRadialCut.UnsupportedHost");
        var radius = hollow.Feature.PrimitiveParameters["Radius"];
        var height = hollow.Feature.PrimitiveParameters["Height"];
        var thickness = hollow.Feature.WallThickness;
        var innerRadius = radius - thickness;
        if (!double.IsFinite(holeRadius) || holeRadius <= 0 || !double.IsFinite(axialPosition) || !double.IsFinite(angleRadians))
            return Failure("Hole radius, axial position, and angle must be finite; radius must be positive.", "Brep.HollowRadialCut.InvalidDimension");
        angleRadians %= 2d * double.Pi;
        if (angleRadians < 0d) angleRadians += 2d * double.Pi;
        if (angleRadians <= tol.Angular || 2d * double.Pi - angleRadians <= tol.Angular) angleRadians = 0d;
        if (holeRadius >= innerRadius - tol.Linear)
            return Failure("The hole must clear the inner cylinder's tangent case.", "Brep.HollowRadialCut.TangentOrOversize");
        if (axialPosition - holeRadius <= thickness + tol.Linear || height - axialPosition - holeRadius <= tol.Linear)
            return Failure("The complete openings must clear the inner bottom and top rim.", "Brep.HollowRadialCut.EndCollision");
        var overshoot = double.Min(innerRadius / 2d, double.Max(thickness, 0.5d));
        var innerIntersectionNearSide = double.Sqrt(innerRadius * innerRadius - holeRadius * holeRadius);
        var inside = insideRadialCoordinate ?? double.Max(innerIntersectionNearSide / 2d, innerIntersectionNearSide - overshoot);
        var outside = outsideRadialCoordinate ?? (radius + overshoot);
        if (!double.IsFinite(inside) || !double.IsFinite(outside) || inside <= 0d || outside <= 0d || inside >= outside)
            return Failure("Finite cutter extent must stay on one radial side and run inward to outward.", "Brep.HollowRadialCut.InvalidCutterExtent");
        if (inside >= innerIntersectionNearSide - tol.Linear || outside <= radius + tol.Linear)
            return Failure("Finite cutter must overrun both local wall intersections.", "Brep.HollowRadialCut.IncompleteCutterExtent");

        var z = Direction3D.Create(new Vector3D(0, 0, 1));
        var x = Direction3D.Create(new Vector3D(double.Cos(angleRadians), double.Sin(angleRadians), 0));
        var seamDirection = Direction3D.Create(z.ToVector().Cross(x.ToVector()));
        var y = seamDirection.ToVector();
        var origin = new Point3D(0, 0, 0);
        var center = new Point3D(0, 0, axialPosition);
        var outer = new CylinderSurface(origin, z, radius, seamDirection);
        var inner = new CylinderSurface(new Point3D(0, 0, thickness), z, innerRadius, seamDirection);
        var tool = new CylinderSurface(center, x, holeRadius, seamDirection);
        var full = new ParameterInterval(0d, 2d * double.Pi);
        var first = new ParameterInterval(0d, double.Pi);
        var second = new ParameterInterval(double.Pi, 2d * double.Pi);
        var intersectionStart = Stopwatch.GetTimestamp();
        var outerAuthority = CylinderCylinderIntersectionCurve.Create(outer, tool, center, CylinderIntersectionBranch.PositiveCutterAxis, full, tolerance: tol);
        if (!outerAuthority.IsSuccess) return KernelResult<HollowRadialCutResult>.Failure(outerAuthority.Diagnostics);
        var innerAuthority = CylinderCylinderIntersectionCurve.Create(inner, tool, center, CylinderIntersectionBranch.PositiveCutterAxis, full, tolerance: tol);
        if (!innerAuthority.IsSuccess) return KernelResult<HollowRadialCutResult>.Failure(innerAuthority.Diagnostics);
        var intersectionEnd = Stopwatch.GetTimestamp();
        var outerCurve = CylinderIntersectionCurveRealizer.Realize(outerAuthority.Value, tol);
        if (!outerCurve.IsSuccess) return KernelResult<HollowRadialCutResult>.Failure(outerCurve.Diagnostics);
        var innerCurve = CylinderIntersectionCurveRealizer.Realize(innerAuthority.Value, tol);
        if (!innerCurve.IsSuccess) return KernelResult<HollowRadialCutResult>.Failure(innerCurve.Diagnostics);
        var curveEnd = Stopwatch.GetTimestamp();
        var outerUv = CylinderIntersectionPcurveRealizer.Realize(outerCurve.Value, tol);
        if (!outerUv.IsSuccess) return KernelResult<HollowRadialCutResult>.Failure(outerUv.Diagnostics);
        var innerUv = CylinderIntersectionPcurveRealizer.Realize(innerCurve.Value, tol);
        if (!innerUv.IsSuccess) return KernelResult<HollowRadialCutResult>.Failure(innerUv.Diagnostics);
        var pcurveEnd = Stopwatch.GetTimestamp();

        var b = new TopologyBuilder();
        var points = new Dictionary<VertexId, Point3D>();
        VertexId Vertex(Point3D p) { var id = b.AddVertex(); points.Add(id, p); return id; }
        var ob = Vertex(origin + y * radius); var ot = Vertex(new Point3D(0, 0, height) + y * radius);
        var ib = Vertex(new Point3D(0, 0, thickness) + y * innerRadius);
        var it = Vertex(new Point3D(0, 0, height) + y * innerRadius);
        var o0 = Vertex(outerAuthority.Value.Evaluate(0d)); var oPi = Vertex(outerAuthority.Value.Evaluate(double.Pi));
        var i0 = Vertex(innerAuthority.Value.Evaluate(0d)); var iPi = Vertex(innerAuthority.Value.Evaluate(double.Pi));
        var outerSeam = b.AddEdge(ob, ot); var innerSeam = b.AddEdge(ib, it);
        var outerBottom = b.AddEdge(ob, ob); var outerTop = b.AddEdge(ot, ot);
        var innerBottom = b.AddEdge(ib, ib); var innerTop = b.AddEdge(it, it);
        var outerFirst = b.AddEdge(o0, oPi); var outerSecond = b.AddEdge(oPi, o0);
        var innerFirst = b.AddEdge(i0, iPi); var innerSecond = b.AddEdge(iPi, i0);
        var toolSeam = b.AddEdge(o0, i0);

        var outerEnvelope = AddLoop(b, [new(outerSeam), new(outerTop), new(outerSeam, true), new(outerBottom, true)]);
        var outerOpening = AddLoop(b, [new(outerSecond, true), new(outerFirst, true)]);
        var innerEnvelope = AddLoop(b, [new(innerSeam), new(innerTop), new(innerSeam, true), new(innerBottom, true)]);
        var innerOpening = AddLoop(b, [new(innerFirst), new(innerSecond)]);
        var outerFace = b.AddFace([outerEnvelope.Id, outerOpening.Id]);
        var innerFace = b.AddFace([innerEnvelope.Id, innerOpening.Id]);
        var bottomOutside = AddLoop(b, [new(outerBottom)]); var outerBottomFace = b.AddFace([bottomOutside.Id]);
        var bottomInside = AddLoop(b, [new(innerBottom)]); var innerBottomFace = b.AddFace([bottomInside.Id]);
        var rimOuter = AddLoop(b, [new(outerTop, true)]);
        var rimInner = AddLoop(b, [new(innerTop, true)]);
        var rimFace = b.AddFace([rimOuter.Id, rimInner.Id]);
        var wall = AddLoop(b, [new(outerFirst), new(outerSecond), new(toolSeam),
            new(innerSecond, true), new(innerFirst, true), new(toolSeam, true)]);
        var wallFace = b.AddFace([wall.Id]);
        var shell = b.AddShell([outerFace, innerFace, outerBottomFace, innerBottomFace, rimFace, wallFace]);
        b.AddBody([shell]);

        var g = new BrepGeometryStore();
        g.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(points[ob], z)));
        g.AddCurve(new CurveGeometryId(2), CurveGeometry.FromLine(new Line3Curve(points[ib], z)));
        g.AddCurve(new CurveGeometryId(3), CurveGeometry.FromCircle(new Circle3Curve(origin, z, radius, seamDirection)));
        g.AddCurve(new CurveGeometryId(4), CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, height), z, radius, seamDirection)));
        g.AddCurve(new CurveGeometryId(5), CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, thickness), z, innerRadius, seamDirection)));
        g.AddCurve(new CurveGeometryId(6), CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, height), z, innerRadius, seamDirection)));
        g.AddCurve(new CurveGeometryId(7), CurveGeometry.FromCertifiedIntersection(outerCurve.Value));
        g.AddCurve(new CurveGeometryId(8), CurveGeometry.FromCertifiedIntersection(innerCurve.Value));
        var seamLength = (points[o0] - points[i0]).Length;
        g.AddCurve(new CurveGeometryId(9), CurveGeometry.FromLine(new Line3Curve(points[o0], Direction3D.Create(points[i0] - points[o0]))));
        g.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCylinder(outer));
        g.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromCylinder(inner));
        g.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(-z.ToVector()), seamDirection)));
        g.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, thickness), z, seamDirection)));
        g.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, height), z, seamDirection)));
        g.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromCylinder(tool));

        var bindings = new BrepBindingModel();
        void Edge(EdgeId edge, int curve, ParameterInterval interval) => bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, new CurveGeometryId(curve), interval));
        Edge(outerSeam, 1, new(0d, height)); Edge(innerSeam, 2, new(0d, height - thickness));
        Edge(outerBottom, 3, full); Edge(outerTop, 4, full); Edge(innerBottom, 5, full); Edge(innerTop, 6, full);
        Edge(outerFirst, 7, first); Edge(outerSecond, 7, second); Edge(innerFirst, 8, first); Edge(innerSecond, 8, second);
        Edge(toolSeam, 9, new(0d, seamLength));
        bindings.AddFaceBinding(new FaceGeometryBinding(outerFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(innerFace, new SurfaceGeometryId(2), IsAlignedWithSurface: false));
        bindings.AddFaceBinding(new FaceGeometryBinding(outerBottomFace, new SurfaceGeometryId(3)));
        bindings.AddFaceBinding(new FaceGeometryBinding(innerBottomFace, new SurfaceGeometryId(4)));
        bindings.AddFaceBinding(new FaceGeometryBinding(rimFace, new SurfaceGeometryId(5)));
        bindings.AddFaceBinding(new FaceGeometryBinding(wallFace, new SurfaceGeometryId(6), IsAlignedWithSurface: false));
        void Role(FaceId face, LoopId loop, FaceBoundaryRole role) => bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(face, loop, role));
        Role(outerFace, outerEnvelope.Id, FaceBoundaryRole.Outer); Role(outerFace, outerOpening.Id, FaceBoundaryRole.Inner);
        Role(innerFace, innerEnvelope.Id, FaceBoundaryRole.Outer); Role(innerFace, innerOpening.Id, FaceBoundaryRole.Inner);
        Role(outerBottomFace, bottomOutside.Id, FaceBoundaryRole.Outer); Role(innerBottomFace, bottomInside.Id, FaceBoundaryRole.Outer);
        Role(rimFace, rimOuter.Id, FaceBoundaryRole.Outer); Role(rimFace, rimInner.Id, FaceBoundaryRole.Inner);
        Role(wallFace, wall.Id, FaceBoundaryRole.Outer);

        void Bind(CoedgeId id, FaceId face, int surface, PcurveGeometry uv)
            => bindings.AddPcurveBinding(new CoedgePcurveBinding(id, face, new SurfaceGeometryId(surface), uv));
        void HostEnvelope((LoopId Id, CoedgeId[] Coedges) loop, FaceId face, int surface, double bottomZ)
        {
            var length = height - bottomZ;
            Bind(loop.Coedges[0], face, surface, PcurveGeometry.Line(new(0d, length), new(0d, 0d), new(0d, length)));
            Bind(loop.Coedges[1], face, surface, PcurveGeometry.Line(full, new(0d, length), new(2d * double.Pi, length)));
            Bind(loop.Coedges[2], face, surface, PcurveGeometry.Line(new(0d, length), new(2d * double.Pi, 0d), new(2d * double.Pi, length)));
            Bind(loop.Coedges[3], face, surface, PcurveGeometry.Line(full, new(0d, 0d), new(2d * double.Pi, 0d)));
        }
        HostEnvelope(outerEnvelope, outerFace, 1, 0d);
        HostEnvelope(innerEnvelope, innerFace, 2, thickness);
        Bind(outerOpening.Coedges[0], outerFace, 1, PcurveGeometry.Polynomial(second, outerUv.Value.Host));
        Bind(outerOpening.Coedges[1], outerFace, 1, PcurveGeometry.Polynomial(first, outerUv.Value.Host));
        Bind(innerOpening.Coedges[0], innerFace, 2, PcurveGeometry.Polynomial(first, innerUv.Value.Host));
        Bind(innerOpening.Coedges[1], innerFace, 2, PcurveGeometry.Polynomial(second, innerUv.Value.Host));
        Bind(bottomOutside.Coedges[0], outerBottomFace, 3, PcurveGeometry.Circle(full, new(0d, 0d), radius, -radius));
        Bind(bottomInside.Coedges[0], innerBottomFace, 4, PcurveGeometry.Circle(full, new(0d, 0d), innerRadius, innerRadius));
        Bind(rimOuter.Coedges[0], rimFace, 5, PcurveGeometry.Circle(full, new(0d, 0d), radius, radius));
        Bind(rimInner.Coedges[0], rimFace, 5, PcurveGeometry.Circle(full, new(0d, 0d), innerRadius, innerRadius));
        Bind(wall.Coedges[0], wallFace, 6, PcurveGeometry.Polynomial(first, outerUv.Value.Tool));
        Bind(wall.Coedges[1], wallFace, 6, PcurveGeometry.Polynomial(second, outerUv.Value.Tool));
        Bind(wall.Coedges[2], wallFace, 6, PcurveGeometry.Line(new(0d, seamLength), new(2d * double.Pi, (points[o0] - center).Dot(x.ToVector())), new(2d * double.Pi, (points[i0] - center).Dot(x.ToVector()))));
        Bind(wall.Coedges[3], wallFace, 6, PcurveGeometry.Polynomial(second, innerUv.Value.Tool));
        Bind(wall.Coedges[4], wallFace, 6, PcurveGeometry.Polynomial(first, innerUv.Value.Tool));
        Bind(wall.Coedges[5], wallFace, 6, PcurveGeometry.Line(new(0d, seamLength), new(0d, (points[o0] - center).Dot(x.ToVector())), new(0d, (points[i0] - center).Dot(x.ToVector()))));

        var body = new BrepBody(b.Model, g, bindings, points);
        var topologyEnd = Stopwatch.GetTimestamp();
        var bindingValidation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!bindingValidation.IsSuccess) return KernelResult<HollowRadialCutResult>.Failure(bindingValidation.Diagnostics);
        var pcurveValidation = BrepPcurveValidator.Validate(body, tol.Linear, requireEveryCoedge: true, samples: 129);
        if (!pcurveValidation.IsValid) return Failure(string.Join("; ", pcurveValidation.Diagnostics), "Brep.HollowRadialCut.PcurveValidation");
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return Failure(string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => $"{d.Code}: {d.Message}")), "Brep.HollowRadialCut.Preflight");
        var validationEnd = Stopwatch.GetTimestamp();
        var identity = HollowRadialCutIdentity.Single;
        var topologyMap = new HollowRadialCutTopologyMap(
            new Dictionary<string, FaceId> { [identity.Wall] = wallFace },
            new Dictionary<string, LoopId> { [identity.OuterOpening] = outerOpening.Id, [identity.InnerOpening] = innerOpening.Id },
            new Dictionary<string, IReadOnlyList<EdgeId>>
            {
                [identity.OuterOpening] = [outerFirst, outerSecond],
                [identity.InnerOpening] = [innerFirst, innerSecond]
            });
        return KernelResult<HollowRadialCutResult>.Success(new(body, identity,
            new(outerCurve.Value.CertifiedDeviationBoundMm, innerCurve.Value.CertifiedDeviationBoundMm,
                outerUv.Value.HostEdgeMismatchBoundMm, outerUv.Value.ToolEdgeMismatchBoundMm,
                innerUv.Value.HostEdgeMismatchBoundMm, innerUv.Value.ToolEdgeMismatchBoundMm,
                outerCurve.Value.SegmentCount, innerCurve.Value.SegmentCount), inside, outside,
            new(Stopwatch.GetElapsedTime(intersectionStart, intersectionEnd).TotalMilliseconds,
                Stopwatch.GetElapsedTime(intersectionEnd, curveEnd).TotalMilliseconds,
                Stopwatch.GetElapsedTime(curveEnd, pcurveEnd).TotalMilliseconds,
                Stopwatch.GetElapsedTime(pcurveEnd, topologyEnd).TotalMilliseconds,
                Stopwatch.GetElapsedTime(topologyEnd, validationEnd).TotalMilliseconds), topologyMap));
    }

    private static (LoopId Id, CoedgeId[] Coedges) AddLoop(TopologyBuilder builder, IReadOnlyList<Use> uses)
    {
        var loop = builder.AllocateLoopId();
        var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < uses.Count; i++)
            builder.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop,
                coedges[(i + 1) % uses.Count], coedges[(i + uses.Count - 1) % uses.Count], uses[i].Reversed));
        builder.AddLoop(new Loop(loop, coedges));
        return (loop, coedges);
    }

    private static KernelResult<HollowRadialCutResult> Failure(string message, string source)
        => KernelResult<HollowRadialCutResult>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);

    private readonly record struct Use(EdgeId Edge, bool Reversed = false);
}
