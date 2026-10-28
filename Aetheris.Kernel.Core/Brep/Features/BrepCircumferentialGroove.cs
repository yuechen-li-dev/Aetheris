using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

public sealed record CircumferentialGroove(string Name, PathSectionKind Section, double Width,
    double Depth, double Station, bool Inner = false);

/// <summary>Exact stock-owned rotational boundary. A groove replaces a finite
/// cylindrical band with shoulders/floor or a toroidal meridian. No Boolean tool
/// bodies or general intersections are involved. Coordinates are centered on Z.</summary>
public static class BrepCircumferentialGroove
{
    public static KernelResult<PathFeatureBody> BuildCylinder(double radius, double height,
        IReadOnlyList<CircumferentialGroove> grooves, double? wallThickness = null)
    {
        if (!double.IsFinite(radius + height) || radius <= 0 || height <= 0
            || wallThickness is { } wall && (!double.IsFinite(wall) || wall <= 0 || wall >= radius || wall >= height))
            return BrepPlanarPathFeature.Failure("dimensions", "Cylinder dimensions and optional top-open vessel wall must be finite and positive.");
        var innerRadius = wallThickness is { } w ? radius - w : 0;
        var bottom = -height / 2; var top = height / 2; var floor = bottom + (wallThickness ?? 0);
        var names = new HashSet<string>();
        foreach (var g in grooves)
        {
            var half = g.Width / 2;
            if (!names.Add(g.Name)) return BrepPlanarPathFeature.Failure("duplicate", $"Duplicate groove '{g.Name}'.");
            if (!Enum.IsDefined(g.Section) || !double.IsFinite(g.Width + g.Depth + g.Station) || g.Width <= 0 || g.Depth <= 0
                || g.Section == PathSectionKind.Semicircular && double.Abs(g.Width - 2 * g.Depth) > 1e-9)
                return BrepPlanarPathFeature.Failure("dimensions", $"{g.Name}: positive dimensions are required; a semicircle has width = 2 × depth.");
            if (g.Inner && wallThickness is null) return BrepPlanarPathFeature.Failure("support", $"{g.Name}: InnerWall requires a hollow cylindrical host.");
            if (g.Station - half <= (g.Inner ? floor : bottom) + 1e-8 || g.Station + half >= top - 1e-8
                || g.Depth >= (wallThickness ?? radius) - 1e-8)
                return BrepPlanarPathFeature.Failure("floor", $"{g.Name}: leave positive material at both axial ends and beneath the groove.");
        }
        var sorted = grooves.OrderBy(g => g.Station).ToArray();
        for (var i = 1; i < sorted.Length; i++)
            if (sorted[i].Station - sorted[i].Width / 2 <= sorted[i - 1].Station + sorted[i - 1].Width / 2 + 1e-8)
                return BrepPlanarPathFeature.Failure("junction-unqualified", "Groove axial envelopes overlap; intersecting inner/outer or repeated grooves require a junction plan.");

        var b = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>(); var rings = new Dictionary<(double Radius, double Z), (VertexId V, EdgeId E)>();
        var faces = new List<FaceId>(); var roles = new Dictionary<string, IReadOnlyList<FaceId>>();
        int nextCurve = 0, nextSurface = 0;
        var zAxis = BrepPlanarPathFeature.Z; var xAxis = BrepPlanarPathFeature.X;
        void Curve(EdgeId edge, CurveGeometry c, ParameterInterval interval)
        { var id = new CurveGeometryId(++nextCurve); geometry.AddCurve(id, c); bindings.AddEdgeBinding(new(edge, id, interval)); }
        (VertexId V, EdgeId E) Ring(double r, double z)
        {
            if (rings.TryGetValue((r, z), out var existing)) return existing;
            var v = b.AddVertex(); points[v] = new(r, 0, z); var e = b.AddEdge(v, v);
            Curve(e, CurveGeometry.FromCircle(new(new(0, 0, z), zAxis, r, xAxis)), new(0, 2 * double.Pi));
            return rings[(r, z)] = (v, e);
        }
        LoopId Loop(params (EdgeId Edge, bool Reverse)[] uses)
        {
            var loop = b.AllocateLoopId(); var ids = uses.Select(_ => b.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < ids.Length; i++) b.AddCoedge(new(ids[i], uses[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse));
            b.AddLoop(new Loop(loop, ids)); return loop;
        }
        FaceId Face(SurfaceGeometry support, bool aligned, params LoopId[] loops)
        {
            var f = b.AddFace(loops); faces.Add(f); var id = new SurfaceGeometryId(++nextSurface);
            geometry.AddSurface(id, support); bindings.AddFaceBinding(new(f, id, aligned));
            for (var i = 0; i < loops.Length; i++) bindings.AddFaceBoundaryRoleBinding(new(f, loops[i], i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
            return f;
        }
        FaceId Band(double ra, double za, double rb, double zb, bool inner, CircumferentialGroove? round = null)
        {
            var a = Ring(ra, za); var c = Ring(rb, zb);
            if (za == zb)
            {
                var normal = BrepPlanarPathFeature.D(new(0, 0, rb < ra ? 1 : -1));
                var outer = ra > rb ? Loop((a.E, false)) : Loop((c.E, true));
                var innerLoop = ra > rb ? Loop((c.E, true)) : Loop((a.E, false));
                return Face(SurfaceGeometry.FromPlane(new(new(0, 0, za), normal, xAxis)), true, outer, innerLoop);
            }
            var seam = b.AddEdge(a.V, c.V);
            SurfaceGeometry support; bool aligned;
            if (round is not null)
            {
                var circle = new Circle3Curve(new(ra, 0, round.Station), BrepPlanarPathFeature.D(new(0, 1, 0)), round.Depth,
                    BrepPlanarPathFeature.D(new(0, 0, inner ? 1 : -1)));
                Curve(seam, CurveGeometry.FromCircle(circle), new(0, double.Pi));
                support = SurfaceGeometry.FromTorus(new(new(0, 0, round.Station), zAxis, ra, round.Depth, xAxis)); aligned = false;
            }
            else
            {
                var delta = points[c.V] - points[a.V];
                Curve(seam, CurveGeometry.FromLine(new(points[a.V], BrepPlanarPathFeature.D(delta))), new(0, delta.Length));
                if (double.Abs(ra - rb) < 1e-12)
                { support = SurfaceGeometry.FromCylinder(new(new(0, 0, za), zAxis, ra, xAxis)); aligned = !inner; }
                else
                {
                    var normal = BrepPlanarPathFeature.D(new(0, 0, rb < ra ? 1 : -1));
                    support = SurfaceGeometry.FromPlane(new(new(0, 0, za), normal, xAxis)); aligned = true;
                }
            }
            return Face(support, aligned, Loop((a.E, false), (seam, false), (c.E, true), (seam, true)));
        }
        void Wall(double r, double from, double to, bool inner)
        {
            var position = from;
            foreach (var g in grooves.Where(g => g.Inner == inner).OrderBy(g => inner ? -g.Station : g.Station))
            {
                var start = g.Station + (inner ? 1 : -1) * g.Width / 2;
                var end = g.Station - (inner ? 1 : -1) * g.Width / 2;
                Band(r, position, r, start, inner);
                var ids = new List<FaceId>();
                if (g.Section == PathSectionKind.Semicircular) ids.Add(Band(r, start, r, end, inner, g));
                else
                {
                    var reduced = r + (inner ? g.Depth : -g.Depth);
                    ids.Add(Band(r, start, reduced, start, inner));
                    ids.Add(Band(reduced, start, reduced, end, inner));
                    ids.Add(Band(reduced, end, r, end, inner));
                }
                roles[g.Name] = ids; position = end;
            }
            Band(r, position, r, to, inner);
        }
        Wall(radius, bottom, top, false);
        Face(SurfaceGeometry.FromPlane(new(new(0, 0, bottom), BrepPlanarPathFeature.D(new(0, 0, -1)), xAxis)), true, Loop((Ring(radius, bottom).E, true)));
        if (wallThickness is null)
            Face(SurfaceGeometry.FromPlane(new(new(0, 0, top), zAxis, xAxis)), true, Loop((Ring(radius, top).E, false)));
        else
        {
            Wall(innerRadius, top, floor, true);
            Face(SurfaceGeometry.FromPlane(new(new(0, 0, top), zAxis, xAxis)), true, Loop((Ring(radius, top).E, false)), Loop((Ring(innerRadius, top).E, true)));
            Face(SurfaceGeometry.FromPlane(new(new(0, 0, floor), zAxis, xAxis)), true, Loop((Ring(innerRadius, floor).E, false)));
        }
        b.AddBody([b.AddShell(faces)]);
        var body = new BrepBody(b.Model, geometry, bindings, points);
        body.PcurveRecoveryReport = BrepPcurveRecovery.Populate(body.Topology, geometry, bindings);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!validation.IsSuccess) return KernelResult<PathFeatureBody>.Failure(validation.Diagnostics);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return BrepPlanarPathFeature.Failure("boundary", string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => d.Message)));
        var removed = grooves.Sum(g => g.Section == PathSectionKind.Rectangular
            ? 2 * double.Pi * g.Width * ((g.Inner ? innerRadius : radius) * g.Depth + (g.Inner ? 1 : -1) * g.Depth * g.Depth / 2)
            : double.Pi * double.Pi * (g.Inner ? innerRadius : radius) * g.Depth * g.Depth + (g.Inner ? 1 : -1) * 4 * double.Pi * double.Pow(g.Depth, 3) / 3);
        return KernelResult<PathFeatureBody>.Success(new(body, roles, -removed));
    }
}
