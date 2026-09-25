using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

public sealed record BrepHelicalRibResult(
    HelicalRibGeometry Authority,
    BrepBody Body,
    IReadOnlyDictionary<FaceId, string> FaceRoles,
    IReadOnlyDictionary<EdgeId, string> EdgeRoles,
    IReadOnlyDictionary<FaceId, QualifiedHelicalRibSurface> QualifiedSideFaces,
    int SeamSplits);

/// <summary>
/// Direct known-topology construction for complete-turn, single-start ribs.
/// The cylinder skin is partitioned into N+1 exposed bands; no support face
/// remains beneath the rib. This does not invoke a sweep or Boolean operator.
/// </summary>
public static class BrepHelicalRib
{
    private readonly record struct Use(EdgeId Edge, bool Reverse, PcurveGeometry? Pcurve = null);

    public static KernelResult<BrepHelicalRibResult> Create(HelicalRibGeometry rib)
    {
        ArgumentNullException.ThrowIfNull(rib);
        var turnsRounded = double.Round(rib.Turns);
        if (turnsRounded < 1d || turnsRounded > 40d || double.Abs(rib.Turns - turnsRounded) > 1e-9d)
            return Failure("complete-turns-required", "X1 admits one to forty complete turns; the rib axial span must be an integer multiple of pitch.");
        var p = rib.Parameters;
        if (p.AxialStartMm - p.RootWidthMm / 2d - p.SupportAxialMinMm <= 1e-6d
            || p.SupportAxialMaxMm - p.AxialEndMm - p.RootWidthMm / 2d <= 1e-6d)
            return Failure("support-end-contact-unsupported", "X1 requires exposed support-cylinder margins before and after the rib footprint.");
        try { return Build(rib, (int)turnsRounded); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Failure("construction-failed", ex.Message);
        }
    }

    private static KernelResult<BrepHelicalRibResult> Build(HelicalRibGeometry rib, int turns)
    {
        var p = rib.Parameters;
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var faceRoles = new Dictionary<FaceId, string>();
        var edgeRoles = new Dictionary<EdgeId, string>();
        var qualifiedSideFaces = new Dictionary<FaceId, QualifiedHelicalRibSurface>();
        var faces = new List<FaceId>();
        var nextCurve = 1;
        var nextSurface = 1;
        var cylinderId = new SurfaceGeometryId(nextSurface++);
        var seamRadial = Direction3D.Create(
            p.StartRadial.ToVector() * double.Cos(p.StartAngleRadians)
            + p.Axis.ToVector().Cross(p.StartRadial.ToVector()) * double.Sin(p.StartAngleRadians));
        var cylinder = new CylinderSurface(p.AxisOrigin, p.Axis, p.RootRadiusMm, seamRadial);
        geometry.AddSurface(cylinderId, SurfaceGeometry.FromCylinder(cylinder));

        VertexId Vertex(Point3D point)
        {
            var id = builder.AddVertex();
            points.Add(id, point);
            return id;
        }

        EdgeId Edge(VertexId from, VertexId to, CurveGeometry curve, ParameterInterval trim, string role)
        {
            var id = builder.AddEdge(from, to);
            var curveId = new CurveGeometryId(nextCurve++);
            geometry.AddCurve(curveId, curve);
            bindings.AddEdgeBinding(new(id, curveId, trim));
            edgeRoles.Add(id, role);
            return id;
        }

        EdgeId Straight(VertexId from, VertexId to, string role)
        {
            var a = points[from];
            var b = points[to];
            var length = (b - a).Length;
            return Edge(from, to, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(b - a))),
                new ParameterInterval(0d, length), role);
        }

        FaceId Face(string role, SurfaceGeometryId surfaceId, params Use[] uses)
        {
            var loop = builder.AllocateLoopId();
            var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < uses.Length; i++)
                builder.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop,
                    coedges[(i + 1) % uses.Length], coedges[(i + uses.Length - 1) % uses.Length], uses[i].Reverse));
            builder.AddLoop(new Loop(loop, coedges));
            var face = builder.AddFace([loop]);
            bindings.AddFaceBinding(new(face, surfaceId));
            bindings.AddFaceBoundaryRoleBinding(new(face, loop, FaceBoundaryRole.Outer));
            for (var i = 0; i < uses.Length; i++)
                if (uses[i].Pcurve is { } uv)
                    bindings.AddPcurveBinding(new(coedges[i], face, surfaceId, uv));
            faces.Add(face);
            faceRoles.Add(face, role);
            return face;
        }

        static Use[] Reversed(params Use[] uses) => uses.Reverse()
            .Select(use => use with { Reverse = !use.Reverse }).ToArray();

        PcurveGeometry CylinderVertical(EdgeId edge, double u)
        {
            var e = builder.Model.GetEdge(edge);
            var start = (points[e.StartVertexId] - p.AxisOrigin).Dot(p.Axis.ToVector());
            var end = (points[e.EndVertexId] - p.AxisOrigin).Dot(p.Axis.ToVector());
            var trim = bindings.EdgeBindings.Single(binding => binding.EdgeId == edge).TrimInterval!.Value;
            return PcurveGeometry.Line(trim, new(u, start), new(u, end));
        }

        PcurveGeometry CylinderHelix(EdgeId edge, int turn, bool trailing)
        {
            var trim = bindings.EdgeBindings.Single(binding => binding.EdgeId == edge).TrimInterval!.Value;
            var offset = trailing ? p.RootWidthMm / 2d : -p.RootWidthMm / 2d;
            var z0 = p.AxialStartMm + turn * p.PitchMm + offset;
            return PcurveGeometry.Line(trim, new(0d, z0), new(2d * double.Pi, z0 + p.PitchMm));
        }

        var section = new VertexId[turns + 1, 4];
        for (var k = 0; k <= turns; k++)
        {
            var angle = p.StartAngleRadians + 2d * double.Pi * k;
            for (var role = 0; role < 4; role++)
                section[k, role] = Vertex(rib.Boundary((HelicalRibBoundaryRole)role).Evaluate(angle));
        }
        var bottom = Vertex(cylinder.Evaluate(0d, p.SupportAxialMinMm));
        var top = Vertex(cylinder.Evaluate(0d, p.SupportAxialMaxMm));

        var sideSurfaces = new SurfaceGeometryId[turns, 3];
        var sideRealizations = new QualifiedHelicalRibSurface[turns, 3];
        var helical = new EdgeId[turns, 4];
        for (var k = 0; k < turns; k++)
        {
            var sub = HelicalRibGeometry.Create(p with
            {
                AxialStartMm = p.AxialStartMm + k * p.PitchMm,
                AxialEndMm = p.AxialStartMm + (k + 1) * p.PitchMm,
                StartAngleRadians = p.StartAngleRadians + 2d * double.Pi * k
            });
            if (!sub.IsSuccess) return KernelResult<BrepHelicalRibResult>.Failure(sub.Diagnostics);
            var realized = new QualifiedHelicalRibSurface[3];
            for (var role = 0; role < 3; role++)
            {
                var result = HelicalRibSurfaceRealizer.Realize(sub.Value, (HelicalRibSideRole)role);
                if (!result.IsSuccess) return KernelResult<BrepHelicalRibResult>.Failure(result.Diagnostics);
                realized[role] = result.Value;
                sideRealizations[k, role] = result.Value;
                var surfaceId = new SurfaceGeometryId(nextSurface++);
                geometry.AddSurface(surfaceId, SurfaceGeometry.FromBSplineSurfaceWithKnots(result.Value.Surface));
                sideSurfaces[k, role] = surfaceId;
            }
            for (var role = 0; role < 4; role++)
            {
                var source = role switch { 0 => realized[0].Surface, 1 => realized[0].Surface,
                    2 => realized[1].Surface, _ => realized[2].Surface };
                var column = role is 0 ? 0 : 1;
                var curve = new BSpline3Curve(source.DegreeU,
                    source.ControlPoints.Select(row => row[column]).ToArray(),
                    source.KnotMultiplicitiesU, source.KnotValuesU, "UNSPECIFIED", false, false, "UNSPECIFIED");
                helical[k, role] = Edge(section[k, role], section[k + 1, role],
                    CurveGeometry.FromBSpline(curve), new(curve.DomainStart, curve.DomainEnd),
                    $"{(HelicalRibBoundaryRole)role}.Segment[{k}]");
            }
        }

        var cross = new EdgeId[turns + 1, 3];
        for (var k = 0; k <= turns; k++)
            for (var role = 0; role < 3; role++)
                cross[k, role] = Straight(section[k, role], section[k, role + 1], $"Section[{k}].{(HelicalRibSideRole)role}");

        var startRoot = Straight(section[0, 0], section[0, 3], "Start.Root");
        var endRoot = Straight(section[turns, 0], section[turns, 3], "End.Root");
        var gaps = new EdgeId[turns + 1];
        for (var k = 1; k <= turns; k++)
            gaps[k] = Straight(section[k - 1, 3], section[k, 0], $"RootGap.Seam[{k}]");
        var lowerSeam = Straight(bottom, section[0, 0], "RootLower.Seam");
        var upperSeam = Straight(section[turns, 3], top, "RootUpper.Seam");
        var bottomCircle = Edge(bottom, bottom,
            CurveGeometry.FromCircle(new Circle3Curve(p.AxisOrigin + p.Axis.ToVector() * p.SupportAxialMinMm,
                p.Axis, p.RootRadiusMm, seamRadial)), new(0d, 2d * double.Pi), "Support.BottomRim");
        var topCircle = Edge(top, top,
            CurveGeometry.FromCircle(new Circle3Curve(p.AxisOrigin + p.Axis.ToVector() * p.SupportAxialMaxMm,
                p.Axis, p.RootRadiusMm, seamRadial)), new(0d, 2d * double.Pi), "Support.TopRim");
        var bottomUv = PcurveGeometry.Line(new(0d, 2d * double.Pi),
            new(0d, p.SupportAxialMinMm), new(2d * double.Pi, p.SupportAxialMinMm));
        var topUv = PcurveGeometry.Line(new(0d, 2d * double.Pi),
            new(0d, p.SupportAxialMaxMm), new(2d * double.Pi, p.SupportAxialMaxMm));

        Face("RootCylinderSkin.Lower", cylinderId, Reversed(
            new Use(lowerSeam, false, CylinderVertical(lowerSeam, 0d)),
            new Use(helical[0, 0], false, CylinderHelix(helical[0, 0], 0, false)),
            new Use(gaps[1], true, CylinderVertical(gaps[1], 2d * double.Pi)),
            new Use(startRoot, true, CylinderVertical(startRoot, 2d * double.Pi)),
            new Use(lowerSeam, true, CylinderVertical(lowerSeam, 2d * double.Pi)),
            new Use(bottomCircle, true, bottomUv)));

        for (var k = 1; k < turns; k++)
            Face($"RootCylinderSkin.Gap[{k}]", cylinderId, Reversed(
                new Use(gaps[k], false, CylinderVertical(gaps[k], 0d)),
                new Use(helical[k, 0], false, CylinderHelix(helical[k, 0], k, false)),
                new Use(gaps[k + 1], true, CylinderVertical(gaps[k + 1], 2d * double.Pi)),
                new Use(helical[k - 1, 3], true, CylinderHelix(helical[k - 1, 3], k - 1, true))));

        Face("RootCylinderSkin.Upper", cylinderId, Reversed(
            new Use(gaps[turns], false, CylinderVertical(gaps[turns], 0d)),
            new Use(endRoot, false, CylinderVertical(endRoot, 0d)),
            new Use(upperSeam, false, CylinderVertical(upperSeam, 0d)),
            new Use(topCircle, false, topUv),
            new Use(upperSeam, true, CylinderVertical(upperSeam, 2d * double.Pi)),
            new Use(helical[turns - 1, 3], true, CylinderHelix(helical[turns - 1, 3], turns - 1, true))));

        for (var k = 0; k < turns; k++)
        {
            for (var role = 0; role < 3; role++)
            {
                var u0 = p.StartAngleRadians + 2d * double.Pi * k;
                var u1 = u0 + 2d * double.Pi;
                var boundary0 = helical[k, role];
                var boundary1 = helical[k, role + 1];
                var interval = bindings.EdgeBindings.Single(item => item.EdgeId == boundary0).TrimInterval!.Value;
                var startInterval = bindings.EdgeBindings.Single(item => item.EdgeId == cross[k, role]).TrimInterval!.Value;
                var endInterval = bindings.EdgeBindings.Single(item => item.EdgeId == cross[k + 1, role]).TrimInterval!.Value;
                var sideFace = Face($"{(HelicalRibSideRole)role}.Segment[{k}]", sideSurfaces[k, role],
                    new Use(boundary0, false, PcurveGeometry.Line(interval, new(u0, 0d), new(u1, 0d))),
                    new Use(cross[k + 1, role], false, PcurveGeometry.Line(endInterval, new(u1, 0d), new(u1, 1d))),
                    new Use(boundary1, true, PcurveGeometry.Line(interval, new(u0, 1d), new(u1, 1d))),
                    new Use(cross[k, role], true, PcurveGeometry.Line(startInterval, new(u0, 0d), new(u0, 1d))));
                qualifiedSideFaces.Add(sideFace, sideRealizations[k, role]);
            }
        }

        var radialNormal = Direction3D.Create(p.Axis.ToVector().Cross(seamRadial.ToVector()));
        var startPlaneId = new SurfaceGeometryId(nextSurface++);
        geometry.AddSurface(startPlaneId, SurfaceGeometry.FromPlane(new PlaneSurface(points[section[0, 0]],
            Direction3D.Create(-radialNormal.ToVector()), seamRadial)));
        Face("StartCap", startPlaneId,
            new Use(cross[0, 0], false), new Use(cross[0, 1], false),
            new Use(cross[0, 2], false), new Use(startRoot, true));
        var endPlaneId = new SurfaceGeometryId(nextSurface++);
        geometry.AddSurface(endPlaneId, SurfaceGeometry.FromPlane(new PlaneSurface(points[section[turns, 0]],
            radialNormal, seamRadial)));
        Face("EndCap", endPlaneId,
            new Use(endRoot, false), new Use(cross[turns, 2], true),
            new Use(cross[turns, 1], true), new Use(cross[turns, 0], true));
        var bottomPlaneId = new SurfaceGeometryId(nextSurface++);
        geometry.AddSurface(bottomPlaneId, SurfaceGeometry.FromPlane(new PlaneSurface(
            p.AxisOrigin + p.Axis.ToVector() * p.SupportAxialMinMm,
            Direction3D.Create(-p.Axis.ToVector()), seamRadial)));
        Face("Support.BottomCap", bottomPlaneId, new Use(bottomCircle, true));
        var topPlaneId = new SurfaceGeometryId(nextSurface++);
        geometry.AddSurface(topPlaneId, SurfaceGeometry.FromPlane(new PlaneSurface(
            p.AxisOrigin + p.Axis.ToVector() * p.SupportAxialMaxMm, p.Axis, seamRadial)));
        Face("Support.TopCap", topPlaneId, new Use(topCircle, false));

        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var validation = BrepBindingValidator.Validate(body, true);
        if (!validation.IsSuccess) return KernelResult<BrepHelicalRibResult>.Failure(validation.Diagnostics);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid)
            return Failure("preflight", string.Join("; ", preflight.Diagnostics
                .Where(d => d.Severity == BrepExportPreflightSeverity.Error)
                .Take(8).Select(d => d.Code + ": " + d.Message)));
        return KernelResult<BrepHelicalRibResult>.Success(new(rib, body, faceRoles, edgeRoles, qualifiedSideFaces, turns - 1));
    }

    private static KernelResult<BrepHelicalRibResult> Failure(string code, string message) =>
        KernelResult<BrepHelicalRibResult>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                $"brep-helical-rib-{code}: {message}", "Brep.HelicalRib")]);
}
