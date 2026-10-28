using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

public sealed record PlanarAttachmentResult(BrepBody Body, IReadOnlyDictionary<FaceId, FaceId> AddedFaceMap);

/// <summary>Joins a construction-owned, Z-monotone additive section boundary to
/// a proved planar support. Auxiliary section-stack base material at/below the
/// contact plane is discarded. Only actual upper boundary and its exact contact
/// cycles survive. This is a shared planar interface, not a general solid Boolean.</summary>
public static class BrepPlanarAdditiveAttachment
{
    public static KernelResult<PlanarAttachmentResult> Attach(BrepBody host, BrepBody addition, double contactZ, FaceId supportFace)
    {
        const double tol = 1e-7;
        KernelResult<PlanarAttachmentResult> Fail(string message) => KernelResult<PlanarAttachmentResult>.Failure([
            new(Diagnostics.KernelDiagnosticCode.ValidationFailed, Diagnostics.KernelDiagnosticSeverity.Error,
                "rib-attachment-invalid: " + message, "Brep.PlanarAttachment")]);
        var hostPoints = Points(host); var addedPoints = Points(addition);
        if (!host.Topology.TryGetFace(supportFace, out var support) || support is null
            || host.Geometry.GetSurface(host.Bindings.GetFaceBinding(supportFace).SurfaceGeometryId).Plane is not { } plane
            || plane.Normal.Z < .999 || double.Abs(plane.Origin.Z - contactZ) > tol)
            return Fail("The selected support must be an outward +Z plane at the contact station.");
        var kept = addition.Topology.Faces.Where(f => f.LoopIds.SelectMany(l => addition.Topology.GetLoop(l).CoedgeIds)
            .SelectMany(c => { var e = addition.Topology.GetEdge(addition.Topology.GetCoedge(c).EdgeId); return new[] { e.StartVertexId, e.EndVertexId }; })
            .Any(v => addedPoints[v].Z > contactZ + tol)).ToArray();
        if (kept.Length == 0) return Fail("No positive-height additive boundary was constructed.");
        var coedges = kept.SelectMany(f => f.LoopIds).SelectMany(l => addition.Topology.GetLoop(l).CoedgeIds)
            .Select(addition.Topology.GetCoedge).ToArray();
        var uses = coedges.GroupBy(c => c.EdgeId).ToDictionary(g => g.Key, g => g.ToArray());
        if (uses.Any(g => g.Value.Length > 2)) return Fail("Additive boundary is non-manifold.");
        var contacts = uses.Where(g => g.Value.Length == 1).Select(g => g.Value[0] with { IsReversed = !g.Value[0].IsReversed }).ToArray();
        if (contacts.Length == 0) return Fail("No finite planar contact boundary was proved.");
        foreach (var c in contacts)
        {
            var e = addition.Topology.GetEdge(c.EdgeId);
            if (double.Abs(addedPoints[e.StartVertexId].Z - contactZ) > tol || double.Abs(addedPoints[e.EndVertexId].Z - contactZ) > tol)
                return Fail("An unclosed additive edge lies away from the attachment plane.");
        }
        var outgoing = contacts.GroupBy(c => DirectedEdgeUse.Resolve(addition.Topology.GetEdge(c.EdgeId), c).StartVertexId).ToArray();
        if (outgoing.Any(g => g.Count() != 1)) return Fail("Contact cycles branch or touch at a point; positive-area junctions are required.");
        var byStart = outgoing.ToDictionary(g => g.Key, g => g.Single()); var consumed = new HashSet<EdgeId>();
        var cycles = new List<Coedge[]>();
        foreach (var first in contacts.OrderBy(c => c.EdgeId.Value))
        {
            if (consumed.Contains(first.EdgeId)) continue;
            var cycle = new List<Coedge>(); var next = first;
            while (consumed.Add(next.EdgeId))
            {
                cycle.Add(next);
                if (!byStart.TryGetValue(DirectedEdgeUse.Resolve(addition.Topology.GetEdge(next.EdgeId), next).EndVertexId, out next!))
                    return Fail("Contact boundary is open.");
            }
            if (next.EdgeId != first.EdgeId) return Fail("Contact boundary merges ambiguously.");
            cycles.Add(cycle.ToArray());
        }
        var model = host.Topology.Clone(); var geometry = host.Geometry.Clone();
        // Rebuild only the host's face container later; immutable source entities remain unchanged.
        var bindings = new BrepBindingModel();
        foreach (var e in host.Bindings.EdgeBindings) bindings.AddEdgeBinding(e);
        foreach (var f in host.Bindings.FaceBindings) bindings.AddFaceBinding(f);
        foreach (var r in host.Bindings.FaceBoundaryRoleBindings) bindings.AddFaceBoundaryRoleBinding(r);
        // Adding an inner boundary makes role evidence explicit for this face.
        // Complete the original authored-order evidence before extending it.
        for (var i = 0; i < support.LoopIds.Count; i++)
            if (!bindings.TryGetFaceBoundaryRoleBinding(support.LoopIds[i], out _))
                bindings.AddFaceBoundaryRoleBinding(new(supportFace, support.LoopIds[i],
                    i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
        var vOffset = host.Topology.Vertices.Max(v => v.Id.Value); var eOffset = host.Topology.Edges.Max(e => e.Id.Value);
        var cOffset = host.Topology.Coedges.Max(c => c.Id.Value); var lOffset = host.Topology.Loops.Max(l => l.Id.Value);
        var fOffset = host.Topology.Faces.Max(f => f.Id.Value);
        var curveOffset = host.Geometry.Curves.Max(c => c.Key.Value); var surfaceOffset = host.Geometry.Surfaces.Max(s => s.Key.Value);
        VertexId V(VertexId id) => new(id.Value + vOffset); EdgeId E(EdgeId id) => new(id.Value + eOffset);
        CoedgeId C(CoedgeId id) => new(id.Value + cOffset); LoopId L(LoopId id) => new(id.Value + lOffset); FaceId F(FaceId id) => new(id.Value + fOffset);
        var usedVertices = uses.Keys.Select(addition.Topology.GetEdge).SelectMany(e => new[] { e.StartVertexId, e.EndVertexId }).Distinct().ToArray();
        foreach (var v in usedVertices) { model.AddVertex(new(V(v))); hostPoints[V(v)] = addedPoints[v]; }
        foreach (var id in uses.Keys.OrderBy(e => e.Value))
        {
            var e = addition.Topology.GetEdge(id); model.AddEdge(new(E(id), V(e.StartVertexId), V(e.EndVertexId)));
            var b = addition.Bindings.GetEdgeBinding(id); var cid = new CurveGeometryId(b.CurveGeometryId.Value + curveOffset);
            if (!geometry.TryGetCurve(cid, out _)) geometry.AddCurve(cid, addition.Geometry.GetCurve(b.CurveGeometryId));
            bindings.AddEdgeBinding(b with { EdgeId = E(id), CurveGeometryId = cid });
        }
        foreach (var c in coedges) model.AddCoedge(new(C(c.Id), E(c.EdgeId), L(c.LoopId), C(c.NextCoedgeId), C(c.PrevCoedgeId), c.IsReversed));
        foreach (var id in kept.SelectMany(f => f.LoopIds).Distinct())
            model.AddLoop(new(L(id), addition.Topology.GetLoop(id).CoedgeIds.Select(C).ToArray()));
        var addedFaces = new List<Face>(); var faceMap = new Dictionary<FaceId, FaceId>();
        foreach (var f in kept)
        {
            var mapped = new Face(F(f.Id), f.LoopIds.Select(L).ToArray()); addedFaces.Add(mapped); faceMap[f.Id] = mapped.Id;
            var b = addition.Bindings.GetFaceBinding(f.Id); var sid = new SurfaceGeometryId(b.SurfaceGeometryId.Value + surfaceOffset);
            if (!geometry.TryGetSurface(sid, out _)) geometry.AddSurface(sid, addition.Geometry.GetSurface(b.SurfaceGeometryId));
            bindings.AddFaceBinding(new(mapped.Id, sid, b.Orientation));
            for (var i = 0; i < f.LoopIds.Count; i++)
            {
                var role = addition.Bindings.TryGetFaceBoundaryRoleBinding(f.LoopIds[i], out var r) ? r.Role : i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner;
                bindings.AddFaceBoundaryRoleBinding(new(mapped.Id, L(f.LoopIds[i]), role));
            }
        }
        var nextLoop = System.Math.Max(lOffset, model.Loops.Max(l => l.Id.Value));
        var nextCoedge = System.Math.Max(cOffset, model.Coedges.Max(c => c.Id.Value));
        var nextFace = System.Math.Max(fOffset, addedFaces.Select(f => f.Id.Value).DefaultIfEmpty(fOffset).Max());
        var supportLoops = support.LoopIds.ToList();
        foreach (var cycle in cycles)
        {
            var area = cycle.Sum(c => Area(addition, c, addedPoints));
            if (double.Abs(area) < 1e-9) return Fail("Contact has zero signed area.");
            // Section construction stores hole source loops CCW and material
            // orientation separately. Recover the interface's explicit cap role;
            // a cycle's traversal alone is not evidence of material ownership.
            var boundaryEdges = cycle.Select(c => c.EdgeId).ToHashSet();
            var capRoles = addition.Topology.Faces.Except(kept).SelectMany(f =>
            {
                var binding = addition.Bindings.GetFaceBinding(f.Id);
                var cap = addition.Geometry.GetSurface(binding.SurfaceGeometryId).Plane;
                if (cap is not { } p || double.Abs(p.Origin.Z - contactZ) > tol || double.Abs(p.Normal.Z) < .999)
                    return Array.Empty<FaceBoundaryRole>();
                return f.LoopIds.Select((id, i) => (id, i)).Where(l => addition.Topology.GetLoop(l.id).CoedgeIds
                    .Select(id => addition.Topology.GetCoedge(id).EdgeId).ToHashSet().SetEquals(boundaryEdges))
                    .Select(l =>
                    {
                        var role = addition.Bindings.TryGetFaceBoundaryRoleBinding(l.id, out var r) ? r.Role
                            : l.i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner;
                        return p.Normal.Z * (binding.Orientation.IsAlignedWithSurface ? 1 : -1) > 0 ? role
                            : role == FaceBoundaryRole.Outer ? FaceBoundaryRole.Inner : FaceBoundaryRole.Outer;
                    }).ToArray();
            }).Distinct().ToArray();
            if (capRoles.Length != 1) return Fail("No unique construction-owned cap role was proved for a contact cycle.");
            var lid = new LoopId(++nextLoop); var ids = cycle.Select(_ => new CoedgeId(++nextCoedge)).ToArray();
            for (var i = 0; i < ids.Length; i++) model.AddCoedge(new(ids[i], E(cycle[i].EdgeId), lid, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], cycle[i].IsReversed));
            model.AddLoop(new(lid, ids));
            if (capRoles[0] == FaceBoundaryRole.Inner)
            { supportLoops.Add(lid); bindings.AddFaceBoundaryRoleBinding(new(supportFace, lid, FaceBoundaryRole.Inner)); }
            else
            {
                var island = new Face(new(++nextFace), [lid]); addedFaces.Add(island);
                bindings.AddFaceBinding(new(island.Id, host.Bindings.GetFaceBinding(supportFace).SurfaceGeometryId));
                bindings.AddFaceBoundaryRoleBinding(new(island.Id, lid, FaceBoundaryRole.Outer));
            }
        }
        var completed = new TopologyModel();
        foreach (var v in model.Vertices) completed.AddVertex(v);
        foreach (var e in model.Edges) completed.AddEdge(e);
        foreach (var c in model.Coedges) completed.AddCoedge(c);
        foreach (var l in model.Loops) completed.AddLoop(l);
        foreach (var f in model.Faces) completed.AddFace(f.Id == supportFace ? f with { LoopIds = supportLoops } : f);
        foreach (var f in addedFaces) completed.AddFace(f);
        foreach (var s in model.Shells) completed.AddShell(s with { FaceIds = s.FaceIds.Concat(addedFaces.Select(f => f.Id)).ToArray() });
        foreach (var b in model.Bodies) completed.AddBody(b);
        var body = new BrepBody(completed, geometry, bindings, hostPoints);
        var validation = BrepBindingValidator.Validate(body, true);
        if (!validation.IsSuccess) return KernelResult<PlanarAttachmentResult>.Failure(validation.Diagnostics);
        if (completed.Edges.Any(e => completed.Coedges.Count(c => c.EdgeId == e.Id) != 2)) return Fail("Final boundary has an open or overused edge.");
        body.PcurveRecoveryReport = BrepPcurveRecovery.Populate(completed, geometry, bindings);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return Fail(string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => d.Message)));
        return KernelResult<PlanarAttachmentResult>.Success(new(body, faceMap));
    }

    private static Dictionary<VertexId, Point3D> Points(BrepBody body)
    {
        var result = body.Topology.Vertices.Where(v => body.TryGetVertexPoint(v.Id, out _)).ToDictionary(v => v.Id, v => { body.TryGetVertexPoint(v.Id, out var p); return p; });
        foreach (var e in body.Topology.Edges)
        {
            var b = body.Bindings.GetEdgeBinding(e.Id); var c = body.Geometry.GetCurve(b.CurveGeometryId); var t = b.TrimInterval!.Value;
            result.TryAdd(e.StartVertexId, BrepPcurveValidator.Evaluate(c, b.OrientedEdgeSense ? t.Start : t.End)!.Value);
            result.TryAdd(e.EndVertexId, BrepPcurveValidator.Evaluate(c, b.OrientedEdgeSense ? t.End : t.Start)!.Value);
        }
        return result;
    }
    private static double Area(BrepBody body, Coedge use, IReadOnlyDictionary<VertexId, Point3D> points)
    {
        var edge = body.Topology.GetEdge(use.EdgeId); var direction = DirectedEdgeUse.Resolve(edge, use);
        var a = points[direction.StartVertexId]; var b = points[direction.EndVertexId];
        var binding = body.Bindings.GetEdgeBinding(edge.Id); var curve = body.Geometry.GetCurve(binding.CurveGeometryId);
        if (curve.Circle3 is not { } circle) return (a.X * b.Y - b.X * a.Y) / 2;
        var interval = binding.TrimInterval!.Value; var forward = binding.OrientedEdgeSense != use.IsReversed;
        var from = forward ? interval.Start : interval.End; var to = forward ? interval.End : interval.Start;
        var orientation = circle.XAxis.X * circle.YAxis.Y - circle.XAxis.Y * circle.YAxis.X;
        return (circle.Center.X * (b.Y - a.Y) - circle.Center.Y * (b.X - a.X)
            + circle.Radius * circle.Radius * orientation * (to - from)) / 2;
    }
}
