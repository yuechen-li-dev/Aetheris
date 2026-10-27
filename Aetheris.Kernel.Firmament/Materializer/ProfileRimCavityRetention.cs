using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Finishes a constant-stock profile rim while retaining its source-planned,
/// disjoint through cavities. This joins known planar cap loops, not Boolean tools.</summary>
internal static class ProfileRimCavityRetention
{
    private const double Tol = 1e-7;
    private sealed record Cavity(string Name, string StableId, double CenterX, double CenterY, double Radius, double From, double To);
    internal static ProfileBoundaryChamferPlanResult TryPlan(PrismaticSectionStackConstruction stack,
        ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, double amount, bool fillet,
        double clearance, PrismaticSectionStackEmissionResult original)
    {
        ProfileBoundaryChamferPlanResult Fail(string text) => new(false, null, null, target, [text]);
        var feature = stack.Feature;
        var stock = feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Base).ToArray();
        var holes = (feature.ShaftHoles ?? []).Select(h => new Cavity(h.Name, h.StableId, h.CenterX, h.CenterY, h.Diameter / 2, h.From, h.To))
            .Concat((feature.CounterboreHoles ?? []).Select(h => new Cavity(h.Name, h.StableId, h.CenterX, h.CenterY, h.CounterboreDiameter / 2, h.From, h.To)))
            .Concat((feature.CountersinkHoles ?? []).Select(h => new Cavity(h.Name, h.StableId, h.CenterX, h.CenterY, h.CountersinkDiameter / 2, h.From, h.To))).ToArray();
        var ids = holes.Select(h => h.StableId).ToHashSet(StringComparer.Ordinal);
        if (stock.Length != 1 || stock[0].ProfileReference != profile.Name || profile.Loops.Count != 1
            || target.ChainKind != ProfileBoundaryChamferChainKind.ClosedLoop
            || feature.Operations.Any(o => o.Intent != PrismaticProfileIntent.Base && (o.SemanticFeatureId is null || !ids.Contains(o.SemanticFeatureId)))
            || (feature.ConstructionPlaneBlindDrills?.Count ?? 0) != 0)
            return Fail("ProfileRimFinishHistoryUnsupported: This rim plan preserves one constant-stock Base and disjoint through round holes; additional material operations need a qualified adapter.");
        if (!feature.Placement.TryResolveConstructionPlane(out var frame, out var frameError)) return Fail(frameError!);
        if (frame is null || (frame.Origin - Point3D.Origin).Length > Tol
            || (frame.AxisX.ToVector() - new Vector3D(1,0,0)).Length > Tol
            || (frame.AxisY.ToVector() - new Vector3D(0,1,0)).Length > Tol || frame.AxisZ.ToVector().Z < 1 - Tol)
            return Fail("ProfileRimFinishFrameUnsupported: The cavity-retaining rim plan currently requires the native XY stock frame.");
        foreach (var hole in holes)
        {
            if (Math.Abs(hole.From - stock[0].From) > Tol || Math.Abs(hole.To - stock[0].To) > Tol)
                return Fail($"ProfileRimFinishThroughHoleRequired:{hole.Name}");
            var margin = profile.Loops[0].Segments.Min(s => Distance(s.Geometry, hole.CenterX, hole.CenterY)) - hole.Radius - amount;
            if (!double.IsFinite(margin) || margin <= Tol) return Fail($"ProfileRimFinishCavityInterference:{hole.Name}: Entry footprint must clear the rim corridor by a positive margin; remaining={margin:R}mm.");
        }
        for (var i = 0; i < holes.Length; i++) for (var j = i + 1; j < holes.Length; j++)
            if (Math.Sqrt(Math.Pow(holes[i].CenterX - holes[j].CenterX, 2) + Math.Pow(holes[i].CenterY - holes[j].CenterY, 2)) <= holes[i].Radius + holes[j].Radius + Tol)
                return Fail($"ProfileRimFinishCavitiesOverlap:{holes[i].Name}:{holes[j].Name}");
        profile = profile with { LocalStartDepth = stock[0].From, LocalEndDepth = stock[0].To, ConstructionPlane = frame };
        ProfileBoundaryChamferPlanResult finished;
        if (fillet)
        {
            var plan = ProfileFilletShellPlanner.TryPlan(profile, target, amount, clearance);
            finished = new(plan.Succeeded, plan.Body, plan.Correspondence, target, plan.Diagnostics) { PlanningEvidence = plan.PlanningEvidence };
        }
        else finished = ProfileBoundaryChamferPlanner.TryPlan(profile, target, amount);
        if (!finished.Succeeded || finished.Body is null || finished.Correspondence is null || holes.Length == 0) return finished;
        if (original.Body is null || original.Correspondence is null) return Fail("ProfileRimFinishSourceNotMaterialized");
        try
        {
            var graft = RetainCavities(finished.Body, original.Body, stock[0].From, stock[0].To, holes.Length);
            var retained = original.Correspondence.Descendants.Where(d =>
                (!d.Edge.HasValue || graft.Edges.ContainsKey(d.Edge.Value)) && (!d.Vertex.HasValue || graft.Vertices.ContainsKey(d.Vertex.Value))
                && (!d.Loop.HasValue || graft.Loops.ContainsKey(d.Loop.Value)) && (!d.Face.HasValue || graft.Faces.ContainsKey(d.Face.Value)))
                .Select(d => d with { Edge = d.Edge is { } e ? graft.Edges[e] : null, Vertex = d.Vertex is { } v ? graft.Vertices[v] : null,
                    Loop = d.Loop is { } l ? graft.Loops[l] : null, Face = d.Face is { } f ? graft.Faces[f] : null });
            var correspondence = finished.Correspondence with { BodyStableId = feature.Name,
                Descendants = finished.Correspondence.Descendants.Concat(retained).GroupBy(d => d.StableId, StringComparer.Ordinal).Select(g => g.First()).ToArray(),
                ProvenanceChain = finished.Correspondence.ProvenanceChain.Concat(original.Correspondence.ProvenanceChain).Append("SourcePlannedCavitiesRetained").ToArray(),
                SourceSpans = (original.Correspondence.SourceSpans?.AsEnumerable() ?? []).Concat(finished.Correspondence.SourceSpans?.AsEnumerable() ?? [])
                    .GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal) };
            return finished with { Body = graft.Body, Correspondence = correspondence };
        }
        catch (InvalidOperationException error) { return Fail("ProfileRimFinishCavityRetentionFailed: " + error.Message); }
    }

    private static double Distance(LineArcProfileCurve2D curve, double x, double y)
    {
        if (curve is LineArcLineSegment2D line)
        {
            var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y;
            var t = Math.Clamp(((x - line.Start.X) * dx + (y - line.Start.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            return Math.Sqrt(Math.Pow(x - line.Start.X - t * dx, 2) + Math.Pow(y - line.Start.Y - t * dy, 2));
        }
        if (curve is not LineArcCircularArc2D arc) return 0; // Unsupported corridors cannot be admitted.
        var angle = Math.Atan2(y - arc.Center.Y, x - arc.Center.X);
        var delta = arc.SweepAngleRadians >= 0 ? angle - arc.StartAngleRadians : arc.StartAngleRadians - angle;
        delta = (delta % Math.Tau + Math.Tau) % Math.Tau;
        if (delta <= Math.Abs(arc.SweepAngleRadians) + Tol)
            return Math.Abs(Math.Sqrt(Math.Pow(x - arc.Center.X, 2) + Math.Pow(y - arc.Center.Y, 2)) - arc.Radius);
        double At(double a) => Math.Sqrt(Math.Pow(x - arc.Center.X - arc.Radius * Math.Cos(a), 2) + Math.Pow(y - arc.Center.Y - arc.Radius * Math.Sin(a), 2));
        return Math.Min(At(arc.StartAngleRadians), At(arc.StartAngleRadians + arc.SweepAngleRadians));
    }

    private sealed record Graft(BrepBody Body, Dictionary<VertexId, VertexId> Vertices, Dictionary<EdgeId, EdgeId> Edges,
        Dictionary<LoopId, LoopId> Loops, Dictionary<FaceId, FaceId> Faces);

    private static Graft RetainCavities(BrepBody skin, BrepBody source, double low, double high, int count)
    {
        Face Cap(BrepBody body, double z)
        {
            var candidates = body.Topology.Faces.Where(f => body.TryGetFaceSurfaceGeometry(f.Id, out var s)
                && s?.Plane is { } p && Math.Abs(p.Normal.ToVector().Z) > 1 - Tol && Math.Abs(p.Origin.Z - z) < Tol).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException($"Expected one planar stock cap at Z={z:R}mm; found {candidates.Length}.");
            return candidates[0];
        }
        if (skin.Topology.Shells.Count() != 1 || source.Topology.Shells.Count() != 1) throw new InvalidOperationException("One closed shell is required.");
        var caps = new[] { (Source: Cap(source, low), Target: Cap(skin, low)), (Source: Cap(source, high), Target: Cap(skin, high)) };
        var capIds = caps.Select(c => c.Source.Id).ToHashSet();
        var openings = caps.SelectMany(c => c.Source.LoopIds.Where(id => source.Bindings.FaceBoundaryRoleBindings
            .Any(b => b.FaceId == c.Source.Id && b.LoopId == id && b.Role == FaceBoundaryRole.Inner))).ToHashSet();
        if (openings.Count != 2 * count) throw new InvalidOperationException("Source cap inner-loop roles do not account for every through hole.");
        var edgeFaces = source.Topology.Faces.SelectMany(f => f.LoopIds.SelectMany(id => source.Topology.GetLoop(id).CoedgeIds)
            .Select(id => (source.Topology.GetCoedge(id).EdgeId, f.Id))).GroupBy(p => p.EdgeId).ToDictionary(g => g.Key, g => g.Select(p => p.Id).Distinct().ToArray());
        var walls = new HashSet<FaceId>(); var pending = new Queue<EdgeId>(openings.SelectMany(id => source.Topology.GetLoop(id).CoedgeIds).Select(id => source.Topology.GetCoedge(id).EdgeId));
        while (pending.TryDequeue(out var edge)) foreach (var face in edgeFaces[edge])
            if (!capIds.Contains(face) && walls.Add(face)) foreach (var loop in source.Topology.GetFace(face).LoopIds)
                foreach (var use in source.Topology.GetLoop(loop).CoedgeIds) pending.Enqueue(source.Topology.GetCoedge(use).EdgeId);
        var loops = openings.Concat(walls.SelectMany(id => source.Topology.GetFace(id).LoopIds)).ToHashSet();
        var coedges = loops.SelectMany(id => source.Topology.GetLoop(id).CoedgeIds).ToHashSet();
        var edges = coedges.Select(id => source.Topology.GetCoedge(id).EdgeId).ToHashSet();
        var vertices = edges.SelectMany(id => new[] { source.Topology.GetEdge(id).StartVertexId, source.Topology.GetEdge(id).EndVertexId }).ToHashSet();
        var lastVertex = skin.Topology.Vertices.Max(v => v.Id.Value);
        var lastEdge = skin.Topology.Edges.Max(e => e.Id.Value);
        var lastLoop = skin.Topology.Loops.Max(l => l.Id.Value);
        var lastCoedge = skin.Topology.Coedges.Max(c => c.Id.Value);
        var lastFace = skin.Topology.Faces.Max(f => f.Id.Value);
        var vm = vertices.ToDictionary(id => id, id => new VertexId(lastVertex + id.Value));
        var em = edges.ToDictionary(id => id, id => new EdgeId(lastEdge + id.Value));
        var lm = loops.ToDictionary(id => id, id => new LoopId(lastLoop + id.Value));
        var cm = coedges.ToDictionary(id => id, id => new CoedgeId(lastCoedge + id.Value));
        var fm = walls.ToDictionary(id => id, id => new FaceId(lastFace + id.Value));
        foreach (var cap in caps) fm[cap.Source.Id] = cap.Target.Id;
        var sourceCurves = edges.Select(id => { source.Bindings.TryGetEdgeBinding(id, out var b); return b.CurveGeometryId; }).Distinct();
        var sourceSurfaces = walls.Select(id => { source.Bindings.TryGetFaceBinding(id, out var b); return b.SurfaceGeometryId; }).Distinct();
        var lastCurve = skin.Geometry.Curves.Max(p => p.Key.Value);
        var lastSurface = skin.Geometry.Surfaces.Max(p => p.Key.Value);
        var curves = sourceCurves.ToDictionary(id => id, id => new CurveGeometryId(lastCurve + id.Value));
        var surfaces = sourceSurfaces.ToDictionary(id => id, id => new SurfaceGeometryId(lastSurface + id.Value));
        var topology = new TopologyModel(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var points = new Dictionary<VertexId, Point3D>();
        foreach (var v in skin.Topology.Vertices) { topology.AddVertex(v); if (skin.TryGetVertexPoint(v.Id, out var p)) points[v.Id] = p; }
        foreach (var v in vertices) { topology.AddVertex(new(vm[v])); if (!source.TryGetVertexPoint(v, out var p)) throw new InvalidOperationException("Missing cavity vertex."); points[vm[v]] = p; }
        foreach (var e in skin.Topology.Edges) topology.AddEdge(e);
        foreach (var id in edges) { var e = source.Topology.GetEdge(id); topology.AddEdge(e with { Id = em[id], StartVertexId = vm[e.StartVertexId], EndVertexId = vm[e.EndVertexId] }); }
        foreach (var c in skin.Topology.Coedges) topology.AddCoedge(c);
        foreach (var id in coedges) { var c = source.Topology.GetCoedge(id); topology.AddCoedge(c with { Id = cm[id], EdgeId = em[c.EdgeId], LoopId = lm[c.LoopId], NextCoedgeId = cm[c.NextCoedgeId], PrevCoedgeId = cm[c.PrevCoedgeId] }); }
        foreach (var l in skin.Topology.Loops) topology.AddLoop(l);
        foreach (var id in loops) { var l = source.Topology.GetLoop(id); topology.AddLoop(l with { Id = lm[id], CoedgeIds = l.CoedgeIds.Select(c => cm[c]).ToArray() }); }
        foreach (var f in skin.Topology.Faces) topology.AddFace(f with { LoopIds = f.LoopIds.Concat(caps.Where(c => c.Target.Id == f.Id).SelectMany(c => c.Source.LoopIds.Where(openings.Contains).Select(l => lm[l]))).ToArray() });
        foreach (var id in walls) { var f = source.Topology.GetFace(id); topology.AddFace(f with { Id = fm[id], LoopIds = f.LoopIds.Select(l => lm[l]).ToArray() }); }
        foreach (var shell in skin.Topology.Shells) topology.AddShell(shell with { FaceIds = shell.FaceIds.Concat(walls.Select(id => fm[id])).ToArray() });
        foreach (var b in skin.Topology.Bodies) topology.AddBody(b);
        foreach (var p in skin.Geometry.Curves) geometry.AddCurve(p.Key, p.Value);
        foreach (var p in skin.Geometry.Surfaces) geometry.AddSurface(p.Key, p.Value);
        foreach (var p in curves) { source.Geometry.TryGetCurve(p.Key, out var c); geometry.AddCurve(p.Value, c!); }
        foreach (var p in surfaces) { source.Geometry.TryGetSurface(p.Key, out var s); geometry.AddSurface(p.Value, s!); }
        foreach (var b in skin.Bindings.EdgeBindings) bindings.AddEdgeBinding(b);
        foreach (var b in source.Bindings.EdgeBindings.Where(b => em.ContainsKey(b.EdgeId))) bindings.AddEdgeBinding(b with { EdgeId = em[b.EdgeId], CurveGeometryId = curves[b.CurveGeometryId] });
        foreach (var b in skin.Bindings.FaceBindings) bindings.AddFaceBinding(b);
        foreach (var b in source.Bindings.FaceBindings.Where(b => walls.Contains(b.FaceId))) bindings.AddFaceBinding(b with { FaceId = fm[b.FaceId], SurfaceGeometryId = surfaces[b.SurfaceGeometryId] });
        foreach (var b in skin.Bindings.FaceBoundaryRoleBindings) bindings.AddFaceBoundaryRoleBinding(b);
        foreach (var cap in caps)
        {
            // The finish planner constructed this cap from one outer Profile and
            // no cavities. Its sole loop is known outer, not guessed by order.
            if (cap.Target.LoopIds.Count != 1) throw new InvalidOperationException("Finished cap must have one source-planned outer boundary.");
            var outer = cap.Target.LoopIds[0];
            if (!skin.Bindings.FaceBoundaryRoleBindings.Any(b => b.LoopId == outer))
                bindings.AddFaceBoundaryRoleBinding(new(cap.Target.Id, outer, FaceBoundaryRole.Outer));
        }
        foreach (var b in source.Bindings.FaceBoundaryRoleBindings.Where(b => lm.ContainsKey(b.LoopId))) bindings.AddFaceBoundaryRoleBinding(b with { FaceId = fm[b.FaceId], LoopId = lm[b.LoopId] });
        foreach (var b in skin.Bindings.PcurveBindings) bindings.AddPcurveBinding(b);
        foreach (var b in skin.Bindings.VertexLoopParameterBindings) bindings.AddVertexLoopParameterBinding(b);
        return new(new(topology, geometry, bindings, points, shellRepresentation: skin.ShellRepresentation), vm, em, lm, fm);
    }
}
