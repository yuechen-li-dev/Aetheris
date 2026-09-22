using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Surfacing;

public sealed record PlanarPlateauFaceIdentity(string Identity, int FaceId, int LoopCount, int EdgeCount);
public sealed record PlanarPlateauMaterializationResult(BrepBody? Body, BrepPcurveEvidence? Pcurves,
    IReadOnlyList<PlanarPlateauFaceIdentity> Faces, IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess => Body is not null && Diagnostics.Count == 0;
}

/// <summary>Local planar-face graft. Retains source topology IDs and adds one hole, the band, and the raised cap.</summary>
public static class PlanarPlateauMaterializer
{
    public static PlanarPlateauMaterializationResult Apply(BrepBody source, FaceId supportFace, PlanarPlateauContactPlan plan)
    {
        PlanarPlateauMaterializationResult Fail(string message) => new(null, null, [], ["plateau-" + message]);
        if (!source.Bindings.TryGetFaceBinding(supportFace, out var supportBinding) || !supportBinding.Orientation.IsAlignedWithSurface ||
            source.Geometry.GetSurface(supportBinding.SurfaceGeometryId).Plane is not { } plane ||
            (plane.Normal.ToVector() - plan.Frame.Normal.ToVector()).Length > 1e-10 ||
            Math.Abs((plan.Frame.Origin - plane.Origin).Dot(plane.Normal.ToVector())) > 1e-8)
            return Fail("support-plane-mismatch");
        var support = source.Topology.GetFace(supportFace);
        if (source.Topology.Shells.Count() != 1 || support.LoopIds.Count != 1) return Fail("support-topology-unsupported");
        // Retain the source shell's loop convention, including historical clockwise
        // outer loops. New face loops use that same convention; the hole opposes it.
        var signedArea = 0d;
        foreach (var use in source.Topology.GetLoop(support.LoopIds[0]).CoedgeIds.Select(source.Topology.GetCoedge))
        {
            var binding = source.Bindings.GetEdgeBinding(use.EdgeId); var geometryCurve = source.Geometry.GetCurve(binding.CurveGeometryId);
            if (binding.TrimInterval is not { } trim) return Fail("support-trim-missing");
            var sign = (use.IsReversed ? -1 : 1) * (binding.OrientedEdgeSense ? 1 : -1);
            (double X, double Y) Uv(Point3D point) { var d = point - plane.Origin; return (d.Dot(plane.UAxis.ToVector()), d.Dot(plane.VAxis.ToVector())); }
            if (geometryCurve.Line3 is { } line)
            {
                var a = Uv(line.Evaluate(trim.Start)); var b = Uv(line.Evaluate(trim.End)); signedArea += sign * (a.X * b.Y - b.X * a.Y) / 2;
            }
            else if (geometryCurve.Circle3 is { } arc)
            {
                var a = Uv(arc.Evaluate(trim.Start)); var b = Uv(arc.Evaluate(trim.End)); var c = Uv(arc.Center);
                signedArea += sign * (c.X * (b.Y - a.Y) - c.Y * (b.X - a.X) + arc.Radius * arc.Radius *
                    arc.Normal.ToVector().Dot(plane.Normal.ToVector()) * (trim.End - trim.Start)) / 2;
            }
            else return Fail("support-boundary-unsupported");
        }
        if (Math.Abs(signedArea) <= 1e-8) return Fail("support-winding-degenerate");
        var winding = Math.Sign(signedArea);
        var contacts = plan.Spans.SelectMany(s => s.BaseContact.ControlPoints).ToArray();
        foreach (var coedge in source.Topology.GetLoop(support.LoopIds[0]).CoedgeIds.Select(source.Topology.GetCoedge))
        {
            var edge = source.Bindings.GetEdgeBinding(coedge.EdgeId);
            var curve = source.Geometry.GetCurve(edge.CurveGeometryId);
            var sign = (coedge.IsReversed ? -1 : 1) * (edge.OrientedEdgeSense ? 1 : -1);
            if (curve.Line3 is { } line)
            {
                var normal = (line.Direction.ToVector() * (sign * winding)).Cross(plane.Normal.ToVector());
                if (contacts.Any(p => (p - line.Origin).Dot(normal) >= -1e-7)) return Fail("contact-outside-support");
            }
            else if (curve.Circle3 is { } circle && edge.TrimInterval is { } interval)
            {
                if (circle.Normal.ToVector().Dot(plane.Normal.ToVector()) * sign * winding <= 0) return Fail("reflex-support-arc-unsupported");
                foreach (var p in contacts)
                {
                    var delta = p - circle.Center; var x = delta.Dot(circle.XAxis.ToVector()); var y = delta.Dot(circle.YAxis.ToVector());
                    double Distance(double t) => x * Math.Cos(t) + y * Math.Sin(t);
                    var max = Math.Max(Distance(interval.Start), Distance(interval.End));
                    var angle = Math.Atan2(y, x);
                    var t = angle + Math.Ceiling((interval.Start - angle) / (2 * Math.PI)) * 2 * Math.PI;
                    if (t <= interval.End) max = Math.Max(max, Math.Sqrt(x * x + y * y));
                    if (max >= circle.Radius - 1e-7) return Fail("contact-outside-support");
                }
            }
            else return Fail("support-boundary-unsupported");
        }
        var topology = new TopologyModel(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        foreach (var v in source.Topology.Vertices) topology.AddVertex(v);
        foreach (var e in source.Topology.Edges) topology.AddEdge(e);
        foreach (var e in source.Topology.Coedges) topology.AddCoedge(e);
        foreach (var l in source.Topology.Loops) topology.AddLoop(l);
        foreach (var f in source.Topology.Faces.Where(f => f.Id != supportFace)) topology.AddFace(f);
        foreach (var b in source.Topology.Bodies) topology.AddBody(b);
        foreach (var c in source.Geometry.Curves) geometry.AddCurve(c.Key, c.Value);
        foreach (var s in source.Geometry.Surfaces) geometry.AddSurface(s.Key, s.Value);
        foreach (var e in source.Bindings.EdgeBindings) bindings.AddEdgeBinding(e);
        foreach (var f in source.Bindings.FaceBindings) bindings.AddFaceBinding(f);
        foreach (var pc in source.Bindings.PcurveBindings) bindings.AddPcurveBinding(pc);
        var points = source.Topology.Vertices.Where(v => source.TryGetVertexPoint(v.Id, out _)).ToDictionary(v => v.Id,
            v => { source.TryGetVertexPoint(v.Id, out var p); return p; });
        var vertexId = source.Topology.Vertices.Max(v => v.Id.Value) + 1;
        var edgeId = source.Topology.Edges.Max(e => e.Id.Value) + 1;
        var coedgeId = source.Topology.Coedges.Max(e => e.Id.Value) + 1;
        var loopId = source.Topology.Loops.Max(l => l.Id.Value) + 1;
        var faceId = source.Topology.Faces.Max(f => f.Id.Value) + 1;
        var curveId = source.Geometry.Curves.Max(c => c.Key.Value) + 1;
        var surfaceId = source.Geometry.Surfaces.Max(s => s.Key.Value) + 1;
        var count = plan.Spans.Count;
        var vertices = new VertexId[3][]; var rings = new EdgeId[3][];
        for (var ring = 0; ring < 3; ring++)
        {
            vertices[ring] = new VertexId[count]; rings[ring] = new EdgeId[count];
            for (var i = 0; i < count; i++)
            {
                var id = new VertexId(vertexId++); vertices[ring][i] = id; topology.AddVertex(new(id));
                points[id] = Contact(i, ring).Evaluate(0);
            }
            for (var i = 0; i < count; i++) rings[ring][i] = Edge(vertices[ring][i], vertices[ring][(i + 1) % count], Contact(i, ring));
        }
        var addedFaces = new List<FaceId>(); var identities = new List<PlanarPlateauFaceIdentity>();
        var hole = Loop(rings[0].Reverse().Select(e => (e, true)).ToArray());
        topology.AddFace(new(supportFace, support.LoopIds.Concat(new[] { hole }).ToArray()));
        identities.Add(new(plan.BaseSupportId, supportFace.Value, 2, source.Topology.GetLoop(support.LoopIds[0]).CoedgeIds.Count + count));
        for (var stage = 0; stage < 2; stage++)
        {
            var seams = new EdgeId[count];
            for (var i = 0; i < count; i++) seams[i] = Edge(vertices[stage][i], vertices[stage + 1][i],
                new BSpline3Curve(5, plan.Spans[i].Patches[stage].ControlPoints[0], [6, 6], [0, 1], "UNSPECIFIED", false, false, "UNSPECIFIED"));
            for (var i = 0; i < count; i++)
            {
                var loop = Loop([(rings[stage][i], false), (seams[(i + 1) % count], false), (rings[stage + 1][i], true), (seams[i], true)]);
                var face = new FaceId(faceId++); topology.AddFace(new(face, [loop])); addedFaces.Add(face);
                var id = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(id, SurfaceGeometry.FromBSplineSurfaceWithKnots(plan.Spans[i].Patches[stage])); bindings.AddFaceBinding(new(face, id));
                var uv = new[] { (new SurfaceParameterPoint(0, 0), new SurfaceParameterPoint(1, 0)),
                    (new SurfaceParameterPoint(1, 0), new SurfaceParameterPoint(1, 1)),
                    (new SurfaceParameterPoint(0, 1), new SurfaceParameterPoint(1, 1)),
                    (new SurfaceParameterPoint(0, 0), new SurfaceParameterPoint(0, 1)) };
                foreach (var coedge in topology.GetLoop(loop).CoedgeIds)
                {
                    var edge = topology.GetCoedge(coedge).EdgeId;
                    var k = edge == rings[stage][i] ? 0 : edge == seams[(i + 1) % count] ? 1 : edge == rings[stage + 1][i] ? 2 : 3;
                    bindings.AddPcurveBinding(new(coedge, face, id, PcurveGeometry.Line(new(0, 1), uv[k].Item1, uv[k].Item2)));
                }
                identities.Add(new(plan.StableId + ".Blend." + plan.Spans[i].SourceSpanId + "." + stage, face.Value, 1, 4));
            }
        }
        var topLoop = Loop(rings[2].Select(e => (e, false)).ToArray()); var topFace = new FaceId(faceId++);
        topology.AddFace(new(topFace, [topLoop])); addedFaces.Add(topFace);
        var topSurface = new SurfaceGeometryId(surfaceId++);
        geometry.AddSurface(topSurface, SurfaceGeometry.FromPlane(new PlaneSurface(plan.Frame.Origin + plan.Frame.Normal.ToVector() * plan.Height, plan.Frame.Normal, plan.Frame.XAxis)));
        bindings.AddFaceBinding(new(topFace, topSurface)); identities.Add(new(plan.TopSupportId, topFace.Value, 1, count));
        var shell = source.Topology.Shells.Single(); topology.AddShell(new(shell.Id, shell.FaceIds.Concat(addedFaces).ToArray()));
        var body = new BrepBody(topology, geometry, bindings, points);
        var populated = BoundedPcurveBuilder.Populate(topology, geometry, bindings, 1e-5);
        if (!populated.IsSuccess) return Fail("pcurve-build:" + string.Join("|", populated.Diagnostics));
        var evidence = BrepPcurveValidator.Validate(body, 1e-5, requireEveryCoedge: true);
        if (!evidence.IsValid) return Fail("pcurve-invalid:" + string.Join("|", evidence.Diagnostics));
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return Fail("brep-invalid:" + string.Join("|", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => d.Message)));
        return new(body, evidence, identities, []);

        BSpline3Curve Contact(int i, int ring) => ring == 0 ? plan.Spans[i].BaseContact : ring == 1 ? plan.Spans[i].MiddleContact : plan.Spans[i].TopContact;
        EdgeId Edge(VertexId a, VertexId b, BSpline3Curve curve)
        {
            var edge = new EdgeId(edgeId++); topology.AddEdge(new(edge, a, b));
            var id = new CurveGeometryId(curveId++); geometry.AddCurve(id, CurveGeometry.FromBSpline(curve)); bindings.AddEdgeBinding(new(edge, id, new(0, 1))); return edge;
        }
        LoopId Loop(IReadOnlyList<(EdgeId Edge, bool Reversed)> uses)
        {
            if (winding < 0) uses = uses.Reverse().Select(use => (use.Edge, !use.Reversed)).ToArray();
            var id = new LoopId(loopId++); var ids = uses.Select(_ => new CoedgeId(coedgeId++)).ToArray();
            for (var i = 0; i < ids.Length; i++) topology.AddCoedge(new(ids[i], uses[i].Edge, id, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reversed));
            topology.AddLoop(new(id, ids)); return id;
        }
    }
}
