using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

public enum PathSectionKind { Rectangular, Semicircular }
public enum PlanarGuideKind { Line, Arc, Circle }
public sealed record PlanarFeatureGuide(PlanarGuideKind Kind, ProfilePoint2D Start, ProfilePoint2D End,
    ProfilePoint2D Center, double Radius = 0, double StartAngle = 0, double Sweep = 0);
public sealed record PlanarPathFeature(string Name, bool Add, PathSectionKind Section, PlanarFeatureGuide Guide,
    double Width, double Extent, double DraftDegrees = 0, bool RoundEnds = false);
public sealed record PathFeatureBody(BrepBody Body, IReadOnlyDictionary<string, IReadOnlyList<FaceId>> Faces,
    double MaterialVolumeDelta);

/// <summary>Analytic local bands on a Box top. Footprints are integrated into the
/// support face, not emitted as intersecting tool solids. Disjoint bounds are a
/// conservative admission proof; crossing or adjoining features require a junction plan.</summary>
public static class BrepPlanarPathFeature
{
    public static KernelResult<PathFeatureBody> BuildBox(double x, double y, double z, IReadOnlyList<PlanarPathFeature> features)
    {
        if (!double.IsFinite(x + y + z) || x <= 0 || y <= 0 || z <= 0) return Failure("dimensions", "Box dimensions must be finite and positive.");
        var source = BrepPrimitives.CreateBox(x, y, z);
        if (!source.IsSuccess) return KernelResult<PathFeatureBody>.Failure(source.Diagnostics);
        var p = new AnalyticPatchBuilder(source.Value);
        var support = source.Value.Topology.Faces.Single(f => source.Value.Geometry.GetSurface(source.Value.Bindings.GetFaceBinding(f.Id).SurfaceGeometryId).Plane is { } plane
            && plane.Normal.Z > .9);
        var supportLoops = support.LoopIds.ToList();
        var footprints = new List<(double MinX, double MinY, double MaxX, double MaxY)>();
        var faces = new Dictionary<string, IReadOnlyList<FaceId>>(StringComparer.Ordinal);
        double volume = 0;
        foreach (var f in features)
        {
            if (!double.IsFinite(f.Width + f.Extent + f.DraftDegrees) || f.Width <= 0 || f.Extent <= 0 || f.DraftDegrees < 0 || f.DraftDegrees >= 89)
                return Failure("dimensions", $"{f.Name}: width/height/depth must be positive; draft must be in [0deg,89deg).");
            if (!Enum.IsDefined(f.Guide.Kind) || !Enum.IsDefined(f.Section)
                || !double.IsFinite(f.Guide.Start.X + f.Guide.Start.Y + f.Guide.End.X + f.Guide.End.Y + f.Guide.Center.X + f.Guide.Center.Y))
                return Failure("guide", $"{f.Name}: guide kind and coordinates must be finite and admitted.");
            if (faces.ContainsKey(f.Name)) return Failure("duplicate", $"Duplicate path feature '{f.Name}'.");
            if (f.Section == PathSectionKind.Semicircular && (f.Add || double.Abs(f.Extent - f.Width / 2) > 1e-9 || f.DraftDegrees != 0))
                return Failure("section", $"{f.Name}: semicircular removal requires depth = radius = width/2 and no draft.");
            if (!f.Add && (f.Extent >= z - 1e-8 || f.DraftDegrees != 0)) return Failure("floor", $"{f.Name}: groove must leave a positive floor; groove draft is unsupported.");
            if (f.Guide.Kind != PlanarGuideKind.Line && (f.Guide.Radius <= f.Width / 2 + 1e-8
                || !double.IsFinite(f.Guide.Radius + f.Guide.StartAngle + f.Guide.Sweep)))
                return Failure("offset-collapse", $"{f.Name}: guide radius must exceed half the section width.");
            if (f.Guide.Kind == PlanarGuideKind.Arc && (f.RoundEnds || f.Guide.Sweep <= 0 || f.Guide.Sweep > double.Pi))
                return Failure("arc", $"{f.Name}: open arcs currently require Flat ends and a positive sweep no greater than 180deg.");
            if (f.Guide.Kind == PlanarGuideKind.Circle && f.RoundEnds) return Failure("ends", $"{f.Name}: closed guides have no end caps.");
            var widthTop = f.Width - 2 * f.Extent * double.Tan(f.DraftDegrees * double.Pi / 180);
            if (widthTop <= 1e-8) return Failure("draft-collapse", $"{f.Name}: draft collapses the rib top; reduce draft/height or increase thickness.");
            var baseOutline = Outline(f.Guide, f.Width / 2, f.RoundEnds);
            if (baseOutline is null) return Failure("guide", $"{f.Name}: line guide must have nonzero finite length.");
            var bounds = Bounds(baseOutline);
            if (bounds.MinX <= -x / 2 + 1e-8 || bounds.MaxX >= x / 2 - 1e-8 || bounds.MinY <= -y / 2 + 1e-8 || bounds.MaxY >= y / 2 - 1e-8)
                return Failure("support", $"{f.Name}: complete footprint must remain strictly inside the Box top.");
            if (footprints.Any(b => bounds.MinX <= b.MaxX + 1e-8 && bounds.MaxX >= b.MinX - 1e-8 && bounds.MinY <= b.MaxY + 1e-8 && bounds.MaxY >= b.MinY - 1e-8))
                return Failure("junction-unqualified", $"{f.Name}: feature bounds overlap another path feature. Crossing, touching and nested footprint arrangements require a qualified junction plan.");
            footprints.Add(bounds);
            var firstFace = p.AddedFaces.Count;
            var baseEdges = baseOutline.Select(loop => p.Edges(loop, z / 2)).ToArray();
            supportLoops.Add(p.Loop(baseEdges[0].Select(e => (e, true)).Reverse().ToArray()));
            if (baseEdges.Length == 2) p.Face([p.Loop(baseEdges[1].Select(e => (e, true)).Reverse().ToArray())],
                SurfaceGeometry.FromPlane(new(new(f.Guide.Center.X, f.Guide.Center.Y, z / 2), Z, X)), true, f.Name + ".SupportIsland");
            var level = z / 2 + (f.Add ? f.Extent : -f.Extent);
            if (f.Section == PathSectionKind.Rectangular)
            {
                var topOutline = Outline(f.Guide, widthTop / 2, f.RoundEnds)!;
                var topEdges = topOutline.Select(loop => p.Edges(loop, level)).ToArray();
                for (var loop = 0; loop < baseOutline.Count; loop++)
                {
                    var connectors = baseOutline[loop].Select((edge, i) => p.Line(p.Start(baseEdges[loop][i]), p.Start(topEdges[loop][i]))).ToArray();
                    for (var i = 0; i < baseOutline[loop].Count; i++)
                    {
                        var j = (i + 1) % baseOutline[loop].Count;
                        var edge = baseOutline[loop][i]; var top = topOutline[loop][i];
                        SurfaceGeometry surface; bool aligned = true;
                        if (edge.Radius == 0)
                        {
                            var a = p.Point(p.Start(baseEdges[loop][i])); var b = p.Point(p.End(baseEdges[loop][i]));
                            var c = p.Point(p.End(connectors[i]));
                            surface = SurfaceGeometry.FromPlane(new(a, D((b - a).Cross(c - a)), D(b - a)));
                        }
                        else
                        {
                            var center = new Point3D(edge.Cx, edge.Cy, z / 2);
                            var slope = (top.Radius - edge.Radius) / (level - z / 2);
                            surface = double.Abs(slope) < 1e-12
                                ? SurfaceGeometry.FromCylinder(new(center, Z, edge.Radius, X))
                                : SurfaceGeometry.FromCone(new(center + new Vector3D(0, 0, -edge.Radius / slope), slope > 0 ? Z : D(new(0, 0, -1)), double.Atan(double.Abs(slope)), X));
                            aligned = (edge.Sweep > 0) == f.Add;
                        }
                        p.Face([p.Loop([(baseEdges[loop][i], false), (connectors[j], false), (topEdges[loop][i], true), (connectors[i], true)])], surface, aligned, f.Name + ".Side" + loop + "_" + i);
                    }
                }
                p.Face(topEdges.Select(edges => p.Loop(edges.Select(e => (e, false)).ToArray())).ToArray(),
                    SurfaceGeometry.FromPlane(new(new(0, 0, level), Z, X)), true, f.Name + (f.Add ? ".Top" : ".Floor"));
                var length = Length(f.Guide);
                volume += (f.Add ? 1 : -1) * f.Extent * (length * (f.Width + widthTop) / 2
                    + (f.RoundEnds ? double.Pi * (f.Width * f.Width + f.Width * widthTop + widthTop * widthTop) / 12 : 0));
            }
            else
            {
                BuildSemicircle(p, f, baseEdges, z / 2);
                volume -= Length(f.Guide) * double.Pi * f.Width * f.Width / 8
                    + (f.RoundEnds ? 2 * double.Pi * double.Pow(f.Width / 2, 3) / 3 : 0);
            }
            faces.Add(f.Name, p.AddedFaces.Skip(firstFace).ToArray());
        }
        p.ReplaceFace(support with { LoopIds = supportLoops });
        var body = p.Build();
        body.PcurveRecoveryReport = BrepPcurveRecovery.Populate(body.Topology, body.Geometry, body.Bindings);
        var binding = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!binding.IsSuccess) return KernelResult<PathFeatureBody>.Failure(binding.Diagnostics);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return Failure("boundary", string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => d.Code + ": " + d.Message)));
        return KernelResult<PathFeatureBody>.Success(new(body, faces, volume));
    }

    private static void BuildSemicircle(AnalyticPatchBuilder p, PlanarPathFeature f, EdgeId[][] baseEdges, double z)
    {
        var g = f.Guide; var r = f.Width / 2;
        if (g.Kind == PlanarGuideKind.Circle)
        {
            var outer = baseEdges[0][0]; var inner = baseEdges[1][0];
            var seam = p.Circle(new(g.Center.X + g.Radius, g.Center.Y, z), D(new(0, 1, 0)), r, X, 0, double.Pi);
            p.Face([p.Loop([(outer, false), (seam, false), (inner, false), (seam, true)])],
                SurfaceGeometry.FromTorus(new(new(g.Center.X, g.Center.Y, z), Z, g.Radius, r, X)), false, f.Name + ".Bottom");
            return;
        }
        var outline = baseEdges[0];
        var a = p.Point(p.Start(outline[0])); var b = p.Point(p.End(outline[0]));
        var c = p.Point(p.Start(outline[2])); var d = p.Point(p.End(outline[2]));
        var startCenter = Mid(a, b); var endCenter = Mid(c, d);
        // Round end boundaries have their endpoints on the same diameter.
        var across = D(b - a); var tangent = D(endCenter - startCenter);
        EdgeId startArc, endArc;
        var startTangent = tangent; var endTangent = tangent;
        SurfaceGeometry bottom;
        if (g.Kind == PlanarGuideKind.Line)
        {
            startArc = p.Circle(startCenter, D(-tangent.ToVector()), r, D(a - startCenter), 0, double.Pi);
            endArc = p.Circle(endCenter, D(-tangent.ToVector()), r, D(d - endCenter), 0, double.Pi);
            bottom = SurfaceGeometry.FromCylinder(new(startCenter, tangent, r, D(a - startCenter)));
        }
        else
        {
            var radialA = D(new(double.Cos(g.StartAngle), double.Sin(g.StartAngle), 0));
            var radialB = D(new(double.Cos(g.StartAngle + g.Sweep), double.Sin(g.StartAngle + g.Sweep), 0));
            var ta = D(Z.ToVector().Cross(radialA.ToVector())); var tb = D(Z.ToVector().Cross(radialB.ToVector()));
            startTangent = ta; endTangent = tb;
            startArc = p.Circle(startCenter, D(-ta.ToVector()), r, D(a - startCenter), 0, double.Pi);
            endArc = p.Circle(endCenter, D(-tb.ToVector()), r, D(d - endCenter), 0, double.Pi);
            bottom = SurfaceGeometry.FromTorus(new(new(g.Center.X, g.Center.Y, z), Z, g.Radius, r, X));
        }
        p.Face([p.Loop([(outline[1], false), (endArc, true), (outline[3], false), (startArc, false)])], bottom, false, f.Name + ".Bottom");
        var startSurface = f.RoundEnds ? SurfaceGeometry.FromSphere(new(startCenter, Z, r, X))
            : SurfaceGeometry.FromPlane(new(startCenter, startTangent, across));
        var endSurface = f.RoundEnds ? SurfaceGeometry.FromSphere(new(endCenter, Z, r, X))
            : SurfaceGeometry.FromPlane(new(endCenter, D(-endTangent.ToVector()), D(c - endCenter)));
        p.Face([p.Loop([(outline[0], false), (startArc, true)])], startSurface, !f.RoundEnds, f.Name + ".Start");
        p.Face([p.Loop([(outline[2], false), (endArc, false)])], endSurface, !f.RoundEnds, f.Name + ".End");
    }

    private static Point3D Mid(Point3D a, Point3D b) => a + (b - a) * .5;
    internal static Direction3D D(Vector3D v) => Direction3D.Create(v);
    internal static readonly Direction3D Z = D(new(0, 0, 1)), X = D(new(1, 0, 0));
    internal static KernelResult<PathFeatureBody> Failure(string code, string message) => KernelResult<PathFeatureBody>.Failure([
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "path-feature-" + code + ": " + message, "Brep.PathFeature")]);
    private static double Length(PlanarFeatureGuide g) => g.Kind == PlanarGuideKind.Line
        ? double.Sqrt(double.Pow(g.End.X - g.Start.X, 2) + double.Pow(g.End.Y - g.Start.Y, 2)) : g.Radius * (g.Kind == PlanarGuideKind.Circle ? 2 * double.Pi : g.Sweep);

    // CCW outer footprint, optional clockwise inner footprint. Offsets are exact;
    // no sampling is used to decide topology or intersections.
    private static List<List<BandEdge>>? Outline(PlanarFeatureGuide g, double half, bool round)
    {
        if (g.Kind == PlanarGuideKind.Line)
        {
            var l = Length(g); if (!double.IsFinite(l) || l <= 1e-8) return null;
            var dx = (g.End.X - g.Start.X) / l; var dy = (g.End.Y - g.Start.Y) / l;
            var a = new ProfilePoint2D(g.Start.X - dy * half, g.Start.Y + dx * half);
            var b = new ProfilePoint2D(g.Start.X + dy * half, g.Start.Y - dx * half);
            var c = new ProfilePoint2D(g.End.X + dy * half, g.End.Y - dx * half);
            var d = new ProfilePoint2D(g.End.X - dy * half, g.End.Y + dx * half);
            return [[round ? BandEdge.Arc(g.Start.X, g.Start.Y, half, double.Atan2(dx, -dy), double.Pi) : new(a, b), new(b, c),
                round ? BandEdge.Arc(g.End.X, g.End.Y, half, double.Atan2(-dx, dy), double.Pi) : new(c, d), new(d, a)]];
        }
        if (g.Kind == PlanarGuideKind.Circle)
            return [[BandEdge.Arc(g.Center.X, g.Center.Y, g.Radius + half, 0, 2 * double.Pi)], [BandEdge.Arc(g.Center.X, g.Center.Y, g.Radius - half, 0, -2 * double.Pi)]];
        var outer = BandEdge.Arc(g.Center.X, g.Center.Y, g.Radius + half, g.StartAngle, g.Sweep);
        var inner = BandEdge.Arc(g.Center.X, g.Center.Y, g.Radius - half, g.StartAngle + g.Sweep, -g.Sweep);
        return [[new(inner.End, outer.Start), outer, new(outer.End, inner.Start), inner]];
    }
    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(List<List<BandEdge>> outline)
    {
        var points = outline.SelectMany(l => l).SelectMany(e => e.Extrema()).ToArray();
        return (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    }
    internal sealed record BandEdge(ProfilePoint2D Start, ProfilePoint2D End, double Cx = 0, double Cy = 0, double Radius = 0, double Angle = 0, double Sweep = 0)
    {
        internal static BandEdge Arc(double x, double y, double r, double a, double s)
        {
            a = double.Ieee754Remainder(a, 2 * double.Pi);
            return new(new(x + r * double.Cos(a), y + r * double.Sin(a)), new(x + r * double.Cos(a + s), y + r * double.Sin(a + s)), x, y, r, a, s);
        }
        internal IEnumerable<ProfilePoint2D> Extrema()
        {
            yield return Start; yield return End;
            if (Radius > 0)
            {
                var lo = double.Min(Angle, Angle + Sweep); var hi = double.Max(Angle, Angle + Sweep);
                for (var i = double.Ceiling(lo / (double.Pi / 2)); i <= double.Floor(hi / (double.Pi / 2)); i++)
                { var angle = i * double.Pi / 2; yield return new(Cx + Radius * double.Cos(angle), Cy + Radius * double.Sin(angle)); }
            }
        }
    }
}

/// <summary>Local analytic patch assembly preserving untouched source IDs.
/// Shared vertices/edges are reused; caps replace support regions.</summary>
internal sealed class AnalyticPatchBuilder
{
    private readonly BrepBody source;
    private readonly TopologyModel additions = new();
    private readonly BrepGeometryStore geometry;
    private readonly BrepBindingModel bindings;
    private readonly Dictionary<VertexId, Point3D> points;
    private readonly Dictionary<FaceId, Face> replacements = [];
    private int vertex, edge, coedge, loop, face, curve, surface;
    internal List<FaceId> AddedFaces { get; } = [];
    internal Dictionary<string, List<FaceId>> Roles { get; } = [];
    internal AnalyticPatchBuilder(BrepBody source)
    {
        this.source = source; geometry = source.Geometry.Clone(); bindings = source.Bindings.Clone();
        points = source.Topology.Vertices.Where(v => source.TryGetVertexPoint(v.Id, out _))
            .ToDictionary(v => v.Id, v => { source.TryGetVertexPoint(v.Id, out var p); return p; });
        foreach (var e in source.Topology.Edges)
        {
            var binding = source.Bindings.GetEdgeBinding(e.Id);
            var c = source.Geometry.GetCurve(binding.CurveGeometryId); var trim = binding.TrimInterval!.Value;
            points.TryAdd(e.StartVertexId, BrepPcurveValidator.Evaluate(c, binding.OrientedEdgeSense ? trim.Start : trim.End)!.Value);
            points.TryAdd(e.EndVertexId, BrepPcurveValidator.Evaluate(c, binding.OrientedEdgeSense ? trim.End : trim.Start)!.Value);
        }
        vertex = source.Topology.Vertices.Max(v => v.Id.Value); edge = source.Topology.Edges.Max(e => e.Id.Value);
        coedge = source.Topology.Coedges.Max(c => c.Id.Value); loop = source.Topology.Loops.Max(l => l.Id.Value); face = source.Topology.Faces.Max(f => f.Id.Value);
        curve = geometry.Curves.Max(c => c.Key.Value); surface = geometry.Surfaces.Max(s => s.Key.Value);
    }
    internal Point3D Point(VertexId id) => points[id];
    internal VertexId Start(EdgeId id) => additions.GetEdge(id).StartVertexId;
    internal VertexId End(EdgeId id) => additions.GetEdge(id).EndVertexId;
    private VertexId Vertex(Point3D p)
    {
        foreach (var item in points) if ((item.Value - p).Length < 1e-9) return item.Key;
        var id = new VertexId(++vertex); additions.AddVertex(new(id)); points[id] = p; return id;
    }
    internal EdgeId Line(VertexId a, VertexId b)
    {
        var delta = Point(b) - Point(a);
        return Edge(a, b, CurveGeometry.FromLine(new(Point(a), BrepPlanarPathFeature.D(delta))), new(0, delta.Length));
    }
    internal EdgeId Circle(Point3D center, Direction3D normal, double radius, Direction3D reference, double from, double sweep)
    {
        var c = new Circle3Curve(center, normal, radius, reference);
        var positive = sweep > 0;
        var lo = positive ? from : from + sweep; var hi = positive ? from + sweep : from;
        return Edge(Vertex(c.Evaluate(from)), Vertex(c.Evaluate(from + sweep)), CurveGeometry.FromCircle(c), new(lo, hi), positive);
    }
    private EdgeId Edge(VertexId a, VertexId b, CurveGeometry c, ParameterInterval trim, bool sense = true)
    {
        var id = new EdgeId(++edge); additions.AddEdge(new(id, a, b));
        var cid = new CurveGeometryId(++curve); geometry.AddCurve(cid, c); bindings.AddEdgeBinding(new(id, cid, trim, sense)); return id;
    }
    internal EdgeId[] Edges(List<BrepPlanarPathFeature.BandEdge> outline, double z) => outline.Select(e => e.Radius == 0
        ? Line(Vertex(new(e.Start.X, e.Start.Y, z)), Vertex(new(e.End.X, e.End.Y, z)))
        : Circle(new(e.Cx, e.Cy, z), BrepPlanarPathFeature.Z, e.Radius, BrepPlanarPathFeature.X, e.Angle, e.Sweep)).ToArray();
    internal LoopId Loop((EdgeId Edge, bool Reverse)[] uses)
    {
        var id = new LoopId(++loop); var ids = uses.Select(_ => new CoedgeId(++coedge)).ToArray();
        for (var i = 0; i < ids.Length; i++) additions.AddCoedge(new(ids[i], uses[i].Edge, id, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse));
        additions.AddLoop(new(id, ids)); return id;
    }
    internal FaceId Face(LoopId[] loops, SurfaceGeometry support, bool aligned, string role)
    {
        var id = new FaceId(++face); var sid = new SurfaceGeometryId(++surface);
        geometry.AddSurface(sid, support); additions.AddFace(new(id, loops)); bindings.AddFaceBinding(new(id, sid, aligned)); AddedFaces.Add(id);
        for (var i = 0; i < loops.Length; i++) bindings.AddFaceBoundaryRoleBinding(new(id, loops[i], i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
        if (!Roles.TryGetValue(role, out var list)) Roles[role] = list = []; list.Add(id); return id;
    }
    internal void ReplaceFace(Face f)
    {
        replacements[f.Id] = f;
        for (var i = 0; i < f.LoopIds.Count; i++)
            if (!bindings.TryGetFaceBoundaryRoleBinding(f.LoopIds[i], out _)) bindings.AddFaceBoundaryRoleBinding(new(f.Id, f.LoopIds[i], i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
    }
    internal BrepBody Build()
    {
        var topology = new TopologyModel();
        foreach (var v in source.Topology.Vertices.Concat(additions.Vertices)) topology.AddVertex(v);
        foreach (var e in source.Topology.Edges.Concat(additions.Edges)) topology.AddEdge(e);
        foreach (var c in source.Topology.Coedges.Concat(additions.Coedges)) topology.AddCoedge(c);
        foreach (var l in source.Topology.Loops.Concat(additions.Loops)) topology.AddLoop(l);
        foreach (var f in source.Topology.Faces) topology.AddFace(replacements.GetValueOrDefault(f.Id, f));
        foreach (var f in additions.Faces) topology.AddFace(f);
        foreach (var s in source.Topology.Shells) topology.AddShell(s with { FaceIds = s.FaceIds.Concat(AddedFaces).ToArray() });
        foreach (var b in source.Topology.Bodies) topology.AddBody(b);
        return new(topology, geometry, bindings, points);
    }
}
