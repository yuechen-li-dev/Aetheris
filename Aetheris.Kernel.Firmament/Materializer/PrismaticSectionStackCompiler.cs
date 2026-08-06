using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Construction-owned face identity used by feature planners; never recovered from final geometry.</summary>
public sealed record PrismaticSectionStackFacePlanMapping(
    FaceId FaceId, string Kind, string SourceStableId, string ConstructionStableId,
    double? SlabFrom, double? SlabTo, IReadOnlyList<string> Provenance);

/// <summary>
/// Immutable-by-convention entity plan for a normalized section stack.  The
/// materializer may instantiate this topology but must not decide its faces,
/// senses, bindings, or provenance.
/// </summary>
public sealed record PrismaticSectionStackTopologyPlan(
    string StableId, TopologyModel Topology, BrepGeometryStore Geometry, BrepBindingModel Bindings,
    IReadOnlyDictionary<VertexId, Point3D> VertexPoints, IReadOnlyList<PrismaticSectionStackFacePlanMapping> FaceMappings,
    SemanticTopologyCorrespondence Correspondence, IReadOnlyList<string> Provenance);

public sealed record PrismaticSectionStackBrepPlan(string Signature, int Vertices, int Edges, int Faces, string Policy, bool Authoritative,
    SemanticTopologyCorrespondence? Correspondence = null, PrismaticSectionStackTopologyPlan? TopologyPlan = null);
public sealed record PrismaticSectionStackEmissionResult(BrepBody? Body, PrismaticSectionStackBrepPlan? Plan, IReadOnlyList<string> Diagnostics, SemanticTopologyCorrespondence? Correspondence = null);
public sealed record PrismaticSectionStackPlanResult(PrismaticSectionStackBrepPlan? Plan, IReadOnlyList<string> Diagnostics, SemanticTopologyCorrespondence? Correspondence = null);

/// <summary>Instantiates a section-stack body from a fully-decided plan.</summary>
public static class PrismaticSectionStackBrepMaterializer
{
    public static (BrepBody? Body, IReadOnlyList<string> Diagnostics) TryMaterialize(PrismaticSectionStackTopologyPlan plan)
    {
        var diagnostics = new List<string>();
        if (plan.Topology.Bodies.Count() != 1 || plan.Topology.Shells.Count() != 1)
            diagnostics.Add("SectionStackPlanInvalidShellCardinality");
        foreach (var face in plan.Topology.Faces)
            if (!plan.Bindings.TryGetFaceBinding(face.Id, out _)) diagnostics.Add($"SectionStackPlanMissingFaceBinding:face={face.Id.Value}");
        foreach (var edge in plan.Topology.Edges)
            if (!plan.Bindings.TryGetEdgeBinding(edge.Id, out _)) diagnostics.Add($"SectionStackPlanMissingEdgeBinding:edge={edge.Id.Value}");
        if (diagnostics.Count != 0) return (null, diagnostics);
        // A body must not share the plan's mutable backing stores.  Feature
        // planners can retain the immutable plan as the authority while the
        // materialized body remains an independently instantiable snapshot.
        return (new BrepBody(CloneTopology(plan.Topology), CloneGeometry(plan.Geometry), CloneBindings(plan.Bindings),
            plan.VertexPoints.ToDictionary(x => x.Key, x => x.Value)), diagnostics);
    }

    private static TopologyModel CloneTopology(TopologyModel source)
    {
        var clone = new TopologyModel();
        foreach (var vertex in source.Vertices.OrderBy(x => x.Id.Value)) clone.AddVertex(vertex);
        foreach (var edge in source.Edges.OrderBy(x => x.Id.Value)) clone.AddEdge(edge);
        foreach (var coedge in source.Coedges.OrderBy(x => x.Id.Value)) clone.AddCoedge(coedge);
        foreach (var loop in source.Loops.OrderBy(x => x.Id.Value)) clone.AddLoop(new Loop(loop.Id, loop.CoedgeIds.ToArray()));
        foreach (var face in source.Faces.OrderBy(x => x.Id.Value)) clone.AddFace(new Face(face.Id, face.LoopIds.ToArray()));
        foreach (var shell in source.Shells.OrderBy(x => x.Id.Value)) clone.AddShell(new Shell(shell.Id, shell.FaceIds.ToArray()));
        foreach (var body in source.Bodies.OrderBy(x => x.Id.Value)) clone.AddBody(new Body(body.Id, body.ShellIds.ToArray()));
        return clone;
    }

    private static BrepGeometryStore CloneGeometry(BrepGeometryStore source)
    {
        var clone = new BrepGeometryStore();
        foreach (var curve in source.Curves.OrderBy(x => x.Key.Value)) clone.AddCurve(curve.Key, curve.Value);
        foreach (var surface in source.Surfaces.OrderBy(x => x.Key.Value)) clone.AddSurface(surface.Key, surface.Value);
        return clone;
    }

    private static BrepBindingModel CloneBindings(BrepBindingModel source)
    {
        var clone = new BrepBindingModel();
        foreach (var binding in source.EdgeBindings.OrderBy(x => x.EdgeId.Value)) clone.AddEdgeBinding(binding);
        foreach (var binding in source.FaceBindings.OrderBy(x => x.FaceId.Value)) clone.AddFaceBinding(binding);
        return clone;
    }
}

/// <summary>Normalizes bounded line/arc Profiles through a planar material arrangement into slabs.</summary>
public static class PrismaticSectionStackCompiler
{
    private const double Tol = 1e-7;

    public static PrismaticSectionStackConstruction? Normalize(PrismaticProfileCompositionParseResult parsed, out IReadOnlyList<string> diagnostics)
    {
        var d = parsed.Diagnostics.ToList();
        if (parsed.Feature is null) { diagnostics = d; return null; }
        var slabs = new List<PrismaticSectionSlab>();
        foreach (var pair in parsed.Feature.CriticalLevels.Zip(parsed.Feature.CriticalLevels.Skip(1)))
        {
            var mid = (pair.First + pair.Second) / 2d;
            var active = parsed.Feature.Operations.Where(o => o.From < mid && mid < o.To).ToArray();
            if (active.Length == 0) continue;
            var arrangement = ProfileArrangementBuilder.Compose(parsed.Feature.Frame, active, parsed.Profiles, $"slab=({pair.First:R},{pair.Second:R})");
            d.AddRange(arrangement.Arrangement.Diagnostics);
            if (arrangement.Region is not null) slabs.Add(new(pair.First, pair.Second, arrangement.Region, active.Select(x => x.Name).Order().ToArray(), arrangement.Arrangement));
        }
        if (slabs.Count == 0) d.Add("compose-no-material-slabs");
        var transitions = new List<PrismaticSectionTransition>();
        foreach (var level in parsed.Feature.CriticalLevels)
        {
            var below = slabs.SingleOrDefault(s => Math.Abs(s.To - level) < Tol)?.Region;
            var above = slabs.SingleOrDefault(s => Math.Abs(s.From - level) < Tol)?.Region;
            var upwardResult = ProfileArrangementBuilder.Difference(parsed.Feature.Frame, below, above, $"transition={level:R}:below-minus-above");
            var downwardResult = ProfileArrangementBuilder.Difference(parsed.Feature.Frame, above, below, $"transition={level:R}:above-minus-below");
            d.AddRange(upwardResult.Arrangement.Diagnostics);
            d.AddRange(downwardResult.Arrangement.Diagnostics);
            var upward = upwardResult.MaterialRegions;
            var downward = downwardResult.MaterialRegions;
            if (upward.Count > 0 || downward.Count > 0) transitions.Add(new(level, upward, downward));
        }
        var volume = slabs.Sum(s => Area(s.Region) * (s.To - s.From));
        diagnostics = d.Distinct().ToArray();
        return d.Any(x => x.Contains("rejected", StringComparison.Ordinal))
            ? null
            : new(parsed.Feature, slabs, transitions, volume, diagnostics);
    }

    public static double Area(PrismaticSectionRegion region) => Math.Abs(ProfileArea(region.Outer)) - region.Holes.Sum(x => Math.Abs(ProfileArea(x)));
    /// <summary>Signed source-loop area in the owning profile frame, retained for inspection evidence.</summary>
    public static double ProfileArea(ResolvedProfile2D profile)
    {
        var sum = 0d;
        foreach (var curve in profile.Loops[0].Segments.Select(s => s.Geometry))
            sum += curve switch
            {
                LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
                LineArcCircularArc2D arc => ArcArea(arc),
                _ => 0d
            };
        return sum / 2d;
    }
    private static double ArcArea(LineArcCircularArc2D arc)
    {
        var a = arc.StartAngleRadians; var b = a + arc.SweepAngleRadians; var r = arc.Radius; var (x, y) = arc.Center;
        return x * r * (Math.Sin(b) - Math.Sin(a)) - y * r * (Math.Cos(b) - Math.Cos(a)) + r * r * (b - a);
    }
}

/// <summary>One topology plan and one body for the normalized stack.  Slab partitions are preserved for stable semantic selection.</summary>
public static class PrismaticSectionStackEmitter
{
    private const double Tol = 1e-6;
    public static PrismaticSectionStackEmissionResult Emit(PrismaticSectionStackConstruction stack)
    {
        var planned = TryPlan(stack);
        if (planned.Plan?.TopologyPlan is null) return new(null, planned.Plan, planned.Diagnostics, planned.Correspondence);
        var materialized = PrismaticSectionStackBrepMaterializer.TryMaterialize(planned.Plan.TopologyPlan);
        return new(materialized.Body, planned.Plan, planned.Diagnostics.Concat(materialized.Diagnostics).Distinct().ToArray(), planned.Correspondence);
    }

    /// <summary>
    /// The authoritative construction phase.  It makes every topology and
    /// provenance decision before the materializer is permitted to create a
    /// <see cref="BrepBody"/>.
    /// </summary>
    public static PrismaticSectionStackPlanResult TryPlan(PrismaticSectionStackConstruction stack)
    {
        var d = stack.Diagnostics.ToList();
        var builder = new TopologyBuilder(); var points = new Dictionary<VertexId, Point3D>(); var curves = new Dictionary<EdgeId, CurveGeometry>(); var profileCurves = new Dictionary<EdgeId, LineArcProfileCurve2D>();
        var vertices = new Dictionary<(long X, long Y, long Z), VertexId>(); var edges = new Dictionary<string, EdgeId>();
        var sideFaces = new List<(LoopId Loop, SurfaceGeometry Surface, bool SameSense, string Source, string Construction, double From, double To)>(); var capFaces = new List<(FaceId Face, double Z, bool Up)>();
        var splitPoints = stack.Slabs.SelectMany(s => Loops(s.Region)).Concat(stack.Transitions.SelectMany(t =>
                t.UpwardRegions.Concat(t.DownwardRegions).SelectMany(Loops)))
            .SelectMany(x => x.Profile.Loops[0].Segments).SelectMany(x => Ends(x.Geometry)).DistinctBy(p => $"{Math.Round(p.X / Tol):F0},{Math.Round(p.Y / Tol):F0}").ToArray();
        (long X, long Y, long Z) Key((double X, double Y) p, double z) => ((long)Math.Round(p.X / Tol), (long)Math.Round(p.Y / Tol), (long)Math.Round(z / Tol));
        VertexId Vertex((double X, double Y) p, double z) { var key = Key(p, z); if (vertices.TryGetValue(key, out var id)) return id; id = builder.AddVertex(); vertices[key] = id; points[id] = new(p.X, p.Y, z); return id; }
        (EdgeId Edge, bool Reverse) Edge(LineArcProfileCurve2D curve, double z)
        {
            var endpoints = Ends(curve); var a = endpoints[0]; var b = endpoints[1]; var forward = CurveKey(curve, z, a, b); var reverse = CurveKey(curve, z, b, a);
            if (edges.TryGetValue(forward, out var existing)) return (existing, false);
            if (edges.TryGetValue(reverse, out existing)) return (existing, true);
            var created = builder.AddEdge(Vertex(a, z), Vertex(b, z)); edges[forward] = created; curves[created] = Curve(curve, z); profileCurves[created] = curve; return (created, false);
        }
        (EdgeId Edge, bool Reverse) Vertical((double X, double Y) p, double from, double to)
        {
            var key = $"V:{Key(p, from)}:{Key(p, to)}"; var reverse = $"V:{Key(p, to)}:{Key(p, from)}";
            if (edges.TryGetValue(key, out var e)) return (e, false); if (edges.TryGetValue(reverse, out e)) return (e, true);
            var created = builder.AddEdge(Vertex(p, from), Vertex(p, to)); edges[key] = created; curves[created] = CurveGeometry.FromLine(new Line3Curve(points[Vertex(p, from)], Direction3D.Create(points[Vertex(p, to)] - points[Vertex(p, from)]))); return (created, false);
        }
        foreach (var slab in stack.Slabs)
            foreach (var item in Loops(slab.Region))
                foreach (var segment in item.Profile.Loops[0].Segments)
                foreach (var curve in Split(segment.Geometry, splitPoints))
                {
                    var endpoints = Ends(curve); var a = endpoints[0]; var b = endpoints[1]; var bottom = Edge(curve, slab.From); var top = Edge(curve, slab.To); var v0 = Vertical(a, slab.From, slab.To); var v1 = Vertical(b, slab.From, slab.To);
                    var loop = AddLoop(builder, [new(bottom.Edge, bottom.Reverse), new(v1.Edge, v1.Reverse), new(top.Edge, !top.Reverse), new(v0.Edge, !v0.Reverse)]);
                    var source = SourceId(segment.Provenance.StableId);
                    sideFaces.Add((loop, SideSurface(curve, slab.From, item.IsHole), !(item.IsHole && curve is LineArcCircularArc2D), source, $"slab:{slab.From:R}..{slab.To:R}:{segment.Provenance.Derivation}", slab.From, slab.To));
                }
        foreach (var transition in stack.Transitions)
        {
            foreach (var region in transition.UpwardRegions) capFaces.Add((AddCap(builder, region, transition.Level, Edge, splitPoints), transition.Level, true));
            foreach (var region in transition.DownwardRegions) capFaces.Add((AddCap(builder, region, transition.Level, Edge, splitPoints), transition.Level, false));
        }
        var faces = new List<FaceId>();
        foreach (var cap in capFaces) faces.Add(cap.Face);
        foreach (var side in sideFaces) faces.Add(builder.AddFace([side.Loop]));
        var shell = builder.AddShell(faces); builder.AddBody([shell]);
        var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var curveId = 1;
        foreach (var edge in builder.Model.Edges.OrderBy(e => e.Id.Value))
        {
            var curve = curves[edge.Id];
            geometry.AddCurve(new CurveGeometryId(curveId), curve);
            var (trim, oriented) = profileCurves.TryGetValue(edge.Id, out var sourceCurve) ? CurveTrim(sourceCurve) : (new ParameterInterval(0d, (points[edge.EndVertexId] - points[edge.StartVertexId]).Length), true);
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge.Id, new CurveGeometryId(curveId), trim, OrientedEdgeSense: oriented));
            curveId++;
        }
        var surfaceId = 1;
        foreach (var cap in capFaces) { geometry.AddSurface(new SurfaceGeometryId(surfaceId), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, cap.Z), Direction3D.Create(new Vector3D(0, 0, cap.Up ? 1 : -1)), Direction3D.Create(new Vector3D(1, 0, 0))))); bindings.AddFaceBinding(new FaceGeometryBinding(cap.Face, new SurfaceGeometryId(surfaceId++))); }
        foreach (var side in sideFaces) { var face = faces[capFaces.Count + sideFaces.IndexOf(side)]; geometry.AddSurface(new SurfaceGeometryId(surfaceId), side.Surface); bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(surfaceId++), side.SameSense)); }
        foreach (var incidence in builder.Model.Coedges.GroupBy(x => x.EdgeId).Where(x => x.Count() != 2))
        {
            var edge = builder.Model.Edges.Single(x => x.Id == incidence.Key);
            var a = points[edge.StartVertexId]; var b = points[edge.EndVertexId];
            d.Add($"compose-rejected:non-manifold-edge-use:edge={incidence.Key.Value}:uses={incidence.Count()}:curve={curves[incidence.Key].Kind}:from=({a.X:R},{a.Y:R},{a.Z:R}):to=({b.X:R},{b.Y:R},{b.Z:R})");
        }
        var descendants = new List<SemanticTopologyDescendant>();
        var topLevels = stack.Feature.Operations.GroupBy(x => x.ProfileReference, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Max(y => y.To), StringComparer.Ordinal);
        var bottomLevels = stack.Feature.Operations.GroupBy(x => x.ProfileReference, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Min(y => y.From), StringComparer.Ordinal);
        for (var i = 0; i < sideFaces.Count; i++)
        {
            var side = sideFaces[i]; var face = faces[capFaces.Count + i]; var loop = builder.Model.Loops.Single(x => x.Id == side.Loop);
            foreach (var coedgeId in loop.CoedgeIds)
            {
                var coedge = builder.Model.Coedges.Single(x => x.Id == coedgeId);
                var edge = builder.Model.Edges.Single(x => x.Id == coedge.EdgeId);
                // Horizontal uses are cap/boundary descendants; vertical uses are side descendants.
                var horizontal = Math.Abs(points[edge.StartVertexId].Z - points[edge.EndVertexId].Z) <= Tol;
                if (!horizontal) continue;
                var z = points[edge.StartVertexId].Z;
                var profile = side.Source.StartsWith("profile:", StringComparison.Ordinal) ? side.Source["profile:".Length..].Split('.')[0] : string.Empty;
                if (Math.Abs(z - side.To) <= Tol && topLevels.TryGetValue(profile, out var topLevel) && Math.Abs(z - topLevel) <= Tol) descendants.Add(new($"plan:{stack.Feature.Name}:{side.Construction}:edge:{edge.Id.Value}:top", "Edge", SemanticTopologyRole.TopBoundary, side.Source, Edge: edge.Id, ParentStableId: side.Construction));
                if (Math.Abs(z - side.From) <= Tol && bottomLevels.TryGetValue(profile, out var bottomLevel) && Math.Abs(z - bottomLevel) <= Tol) descendants.Add(new($"plan:{stack.Feature.Name}:{side.Construction}:edge:{edge.Id.Value}:bottom", "Edge", SemanticTopologyRole.BottomBoundary, side.Source, Edge: edge.Id, ParentStableId: side.Construction));
            }
            descendants.Add(new($"plan:{stack.Feature.Name}:{side.Construction}:face:{face.Value}", "Face", SemanticTopologyRole.ExtrusionSideFace, side.Source, Face: face, ParentStableId: side.Construction));
        }
        foreach (var fragment in stack.Slabs.Where(s => s.Arrangement is not null).SelectMany(s => s.Arrangement!.AtomicFragments))
        {
            var source = fragment.Source.Provenance.StableId;
            descendants.Add(new($"construction:{stack.Feature.Name}:{fragment.StableId}", "ArrangementFragment", SemanticTopologyRole.Unknown, source, ParentStableId: fragment.Source.StableId));
        }
        foreach (var hole in stack.Feature.ShaftHoles ?? [])
        {
            var profilePrefix = $"profile:{hole.ProfileReference}.Outer.";
            // ThroughAll is declared over the composition interval, but a removal can only exist where
            // its host has material.  Derive entry and exit from the retained wall boundary rather than
            // from the declaration's nominal levels (for CTC-01 the host stops at Z=0 although the
            // composition continues to Z=50 elsewhere).
            var holeSides = sideFaces.Select((side, index) => (Side: side, Face: faces[capFaces.Count + index]))
                .Where(x => x.Side.Source.StartsWith(profilePrefix, StringComparison.Ordinal)).ToArray();
            var horizontalEdges = holeSides.SelectMany(x => builder.Model.Loops.Single(loop => loop.Id == x.Side.Loop).CoedgeIds)
                .Select(id => builder.Model.Coedges.Single(coedge => coedge.Id == id).EdgeId).Distinct()
                .Select(id => (Edge: id, Topology: builder.Model.Edges.Single(edge => edge.Id == id)))
                .Where(x => Math.Abs(points[x.Topology.StartVertexId].Z - points[x.Topology.EndVertexId].Z) <= Tol)
                .Select(x => (x.Edge, Z: points[x.Topology.StartVertexId].Z)).ToArray();
            var entryZ = horizontalEdges.Max(x => x.Z);
            var exitZ = horizontalEdges.Min(x => x.Z);
            var entryEdges = horizontalEdges.Where(x => Math.Abs(x.Z - entryZ) <= Tol).Select(x => x.Edge).Distinct().OrderBy(x => x.Value).ToArray();
            var exitEdges = horizontalEdges.Where(x => Math.Abs(x.Z - exitZ) <= Tol).Select(x => x.Edge).Distinct().OrderBy(x => x.Value).ToArray();
            var wallFaces = holeSides.Select(x => x.Face).Distinct().OrderBy(x => x.Value).ToArray();
            foreach (var edge in entryEdges) descendants.Add(new($"material:{hole.StableId}:entry-edge:{edge.Value}", "Edge", SemanticTopologyRole.TopBoundary, hole.StableId, Edge: edge, ParentStableId: hole.StableId, GeometryPreview: $"diameter={hole.Diameter:R}"));
            foreach (var edge in exitEdges) descendants.Add(new($"material:{hole.StableId}:exit-edge:{edge.Value}", "Edge", SemanticTopologyRole.BottomBoundary, hole.StableId, Edge: edge, ParentStableId: hole.StableId, GeometryPreview: $"diameter={hole.Diameter:R}"));
            foreach (var face in wallFaces) descendants.Add(new($"material:{hole.StableId}:wall:{face.Value}", "Face", SemanticTopologyRole.HoleWallFace, hole.StableId, Face: face, ParentStableId: hole.StableId, GeometryPreview: $"diameter={hole.Diameter:R}"));
            LoopId? LoopFor(IReadOnlyList<EdgeId> boundary)
            {
                var expected = boundary.ToHashSet();
                return builder.Model.Loops.Select(loop => (loop.Id, Edges: loop.CoedgeIds.Select(id => builder.Model.Coedges.Single(x => x.Id == id).EdgeId).ToHashSet()))
                    .Where(x => x.Edges.SetEquals(expected)).Select(x => (LoopId?)x.Id).SingleOrDefault();
            }
            if (entryEdges.Length > 0 && LoopFor(entryEdges) is { } entryLoop) descendants.Add(new($"material:{hole.StableId}:entry-loop", "Loop", SemanticTopologyRole.HoleEntryLoop, hole.StableId, Loop: entryLoop, ParentStableId: hole.StableId, GeometryPreview: $"diameter={hole.Diameter:R}"));
            if (exitEdges.Length > 0 && LoopFor(exitEdges) is { } exitLoop) descendants.Add(new($"material:{hole.StableId}:exit-loop", "Loop", SemanticTopologyRole.HoleExitLoop, hole.StableId, Loop: exitLoop, ParentStableId: hole.StableId, GeometryPreview: $"diameter={hole.Diameter:R}"));
        }
        foreach (var hole in stack.Feature.CounterboreHoles ?? [])
        {
            var borePrefix = $"profile:{hole.CounterboreProfileReference}.Outer.";
            var shaftPrefix = $"profile:{hole.ShaftProfileReference}.Outer.";
            var boreSides = sideFaces.Select((side, index) => (Side: side, Face: faces[capFaces.Count + index]))
                .Where(item => item.Side.Source.StartsWith(borePrefix, StringComparison.Ordinal)).ToArray();
            var shaftSides = sideFaces.Select((side, index) => (Side: side, Face: faces[capFaces.Count + index]))
                .Where(item => item.Side.Source.StartsWith(shaftPrefix, StringComparison.Ordinal)).ToArray();
            if (boreSides.Length == 0 || shaftSides.Length == 0) { d.Add($"ComposeCounterboreSemanticSourceMissing:{hole.Name}"); continue; }
            var boreEdges = boreSides.SelectMany(item => builder.Model.Loops.Single(loop => loop.Id == item.Side.Loop).CoedgeIds)
                .Select(id => builder.Model.Coedges.Single(coedge => coedge.Id == id).EdgeId).Distinct()
                .Select(id => (Edge: id, Topology: builder.Model.Edges.Single(edge => edge.Id == id)))
                .Where(item => Math.Abs(points[item.Topology.StartVertexId].Z - points[item.Topology.EndVertexId].Z) <= Tol)
                .Select(item => (item.Edge, points[item.Topology.StartVertexId].Z)).ToArray();
            var shaftEdges = shaftSides.SelectMany(item => builder.Model.Loops.Single(loop => loop.Id == item.Side.Loop).CoedgeIds)
                .Select(id => builder.Model.Coedges.Single(coedge => coedge.Id == id).EdgeId).Distinct()
                .Select(id => (Edge: id, Topology: builder.Model.Edges.Single(edge => edge.Id == id)))
                .Where(item => Math.Abs(points[item.Topology.StartVertexId].Z - points[item.Topology.EndVertexId].Z) <= Tol)
                .Select(item => (item.Edge, points[item.Topology.StartVertexId].Z)).ToArray();
            var mouthZ = boreEdges.Max(item => item.Z); var shoulderZ = boreEdges.Min(item => item.Z); var exitZ = shaftEdges.Min(item => item.Z);
            var mouthEdges = boreEdges.Where(item => Math.Abs(item.Z - mouthZ) <= Tol).Select(item => item.Edge).Distinct().OrderBy(id => id.Value).ToArray();
            var shoulderEdges = boreEdges.Where(item => Math.Abs(item.Z - shoulderZ) <= Tol).Select(item => item.Edge).Distinct().OrderBy(id => id.Value).ToArray();
            var exitEdges = shaftEdges.Where(item => Math.Abs(item.Z - exitZ) <= Tol).Select(item => item.Edge).Distinct().OrderBy(id => id.Value).ToArray();
            LoopId? LoopFor(IReadOnlyList<EdgeId> boundary) => builder.Model.Loops.Select(loop => (loop.Id, Edges: loop.CoedgeIds.Select(id => builder.Model.Coedges.Single(x => x.Id == id).EdgeId).ToHashSet()))
                .Where(item => item.Edges.SetEquals(boundary.ToHashSet())).Select(item => (LoopId?)item.Id).SingleOrDefault();
            foreach (var face in boreSides.Select(item => item.Face).Distinct().OrderBy(id => id.Value)) descendants.Add(new($"material:{hole.StableId}:counterbore-wall:{face.Value}", "Face", SemanticTopologyRole.HoleCounterboreWallFace, hole.StableId, Face: face, ParentStableId: hole.StableId, GeometryPreview: $"diameter={hole.CounterboreDiameter:R}"));
            foreach (var face in shaftSides.Select(item => item.Face).Distinct().OrderBy(id => id.Value)) descendants.Add(new($"material:{hole.StableId}:shaft-wall:{face.Value}", "Face", SemanticTopologyRole.HoleCounterboreShaftWallFace, hole.StableId, Face: face, ParentStableId: hole.StableId, GeometryPreview: $"diameter={hole.Diameter:R}"));
            if (mouthEdges.Length > 0 && LoopFor(mouthEdges) is { } mouthLoop) descendants.Add(new($"material:{hole.StableId}:mouth", "Loop", SemanticTopologyRole.HoleCounterboreMouthLoop, hole.StableId, Loop: mouthLoop, ParentStableId: hole.StableId));
            if (shoulderEdges.Length > 0 && LoopFor(shoulderEdges) is { } shoulderLoop) descendants.Add(new($"material:{hole.StableId}:shoulder", "Loop", SemanticTopologyRole.HoleCounterboreShoulderLoop, hole.StableId, Loop: shoulderLoop, ParentStableId: hole.StableId));
            if (exitEdges.Length > 0 && LoopFor(exitEdges) is { } exitLoop) descendants.Add(new($"material:{hole.StableId}:exit", "Loop", SemanticTopologyRole.HoleExitLoop, hole.StableId, Loop: exitLoop, ParentStableId: hole.StableId));
        }
        foreach (var slot in stack.Feature.AllSlotProfiles)
        {
            var prefix = $"profile:{slot.ProfileReference}.Outer.";
            var slotSides = sideFaces.Select((side, index) => (Side: side, Face: faces[capFaces.Count + index]))
                .Where(x => x.Side.Source.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (slotSides.Length == 0) { d.Add($"SlotCorrespondenceMissing:{slot.Name}"); continue; }
            var horizontalEdges = slotSides.SelectMany(x => builder.Model.Loops.Single(loop => loop.Id == x.Side.Loop).CoedgeIds)
                .Select(id => builder.Model.Coedges.Single(coedge => coedge.Id == id).EdgeId).Distinct()
                .Select(id => (Edge: id, Topology: builder.Model.Edges.Single(edge => edge.Id == id)))
                .Where(x => Math.Abs(points[x.Topology.StartVertexId].Z - points[x.Topology.EndVertexId].Z) <= Tol)
                .Select(x => (x.Edge, Z: points[x.Topology.StartVertexId].Z)).ToArray();
            var entryZ=horizontalEdges.Max(x=>x.Z); var exitZ=horizontalEdges.Min(x=>x.Z);
            var entryEdges=horizontalEdges.Where(x=>Math.Abs(x.Z-entryZ)<=Tol).Select(x=>x.Edge).Distinct().OrderBy(x=>x.Value).ToArray();
            var exitEdges=horizontalEdges.Where(x=>Math.Abs(x.Z-exitZ)<=Tol).Select(x=>x.Edge).Distinct().OrderBy(x=>x.Value).ToArray();
            foreach(var edge in entryEdges) descendants.Add(new($"material:{slot.StableId}:entry-edge:{edge.Value}","Edge",SemanticTopologyRole.TopBoundary,slot.StableId,Edge:edge,ParentStableId:slot.StableId,GeometryPreview:"capsule-entry"));
            foreach(var edge in exitEdges) descendants.Add(new($"material:{slot.StableId}:exit-edge:{edge.Value}","Edge",SemanticTopologyRole.BottomBoundary,slot.StableId,Edge:edge,ParentStableId:slot.StableId,GeometryPreview:"capsule-exit"));
            foreach(var side in slotSides)
            {
                var role = side.Side.Source.EndsWith(".PositiveSide",StringComparison.Ordinal) || side.Side.Source.EndsWith(".NegativeSide",StringComparison.Ordinal) || side.Side.Source.EndsWith(".StartSide",StringComparison.Ordinal) || side.Side.Source.EndsWith(".EndSide",StringComparison.Ordinal) ? SemanticTopologyRole.SlotStraightWallFace : SemanticTopologyRole.SlotEndWallFace;
                descendants.Add(new($"material:{slot.StableId}:wall:{side.Face.Value}","Face",SemanticTopologyRole.SlotWallFace,slot.StableId,Face:side.Face,ParentStableId:slot.StableId));
                descendants.Add(new($"material:{slot.StableId}:{role}:{side.Face.Value}","Face",role,slot.StableId,Face:side.Face,ParentStableId:slot.StableId));
            }
            LoopId? LoopFor(IReadOnlyList<EdgeId> boundary) => builder.Model.Loops.Select(loop => (loop.Id, Edges: loop.CoedgeIds.Select(id => builder.Model.Coedges.Single(x => x.Id == id).EdgeId).ToHashSet())).Where(x => x.Edges.SetEquals(boundary.ToHashSet())).Select(x => (LoopId?)x.Id).SingleOrDefault();
            if (entryEdges.Length > 0 && LoopFor(entryEdges) is { } entryLoop) descendants.Add(new($"material:{slot.StableId}:entry-loop","Loop",SemanticTopologyRole.SlotEntryLoop,slot.StableId,Loop:entryLoop,ParentStableId:slot.StableId));
            if (exitEdges.Length > 0 && LoopFor(exitEdges) is { } exitLoop) descendants.Add(new($"material:{slot.StableId}:exit-loop","Loop",SemanticTopologyRole.SlotExitLoop,slot.StableId,Loop:exitLoop,ParentStableId:slot.StableId));
        }
        var provenance = new List<string> { "ProfileArrangement2D", "PrismaticSectionStackConstruction", "PrismaticSectionStackBrepPlan", "AuthoritativeBRepPlan" };
        if ((stack.Feature.ShaftHoles?.Count ?? 0) > 0) provenance.Insert(0, "SemanticHoleComposeLowering");
        if ((stack.Feature.CounterboreHoles?.Count ?? 0) > 0) provenance.Insert(0, "SemanticCounterboreComposeLowering");
        if (stack.Feature.AllSlotProfiles.Any()) provenance.Insert(0, "SemanticSlotComposeLowering");
        var correspondence = new SemanticTopologyCorrespondence(stack.Feature.Name, descendants.DistinctBy(x => x.StableId).ToArray(), provenance);
        var faceMappings = new List<PrismaticSectionStackFacePlanMapping>();
        foreach (var cap in capFaces)
            faceMappings.Add(new(cap.Face, "TransitionCap", $"transition:{cap.Z:R}", $"transition:{cap.Z:R}", null, null,
                ["PrismaticSectionTransition", cap.Up ? "UpwardRegion" : "DownwardRegion"]));
        for (var i = 0; i < sideFaces.Count; i++)
        {
            var side = sideFaces[i]; var face = faces[capFaces.Count + i];
            faceMappings.Add(new(face, "PrismaticSide", side.Source, side.Construction, side.From, side.To,
                ["PrismaticSectionStackConstruction", "ProfileArrangement2D", side.Source, side.Construction]));
        }
        var topologyPlan = new PrismaticSectionStackTopologyPlan($"compose-plan:{stack.Feature.Name}", builder.Model, geometry, bindings,
            points.ToDictionary(x => x.Key, x => x.Value), faceMappings, correspondence, provenance);
        var plan = new PrismaticSectionStackBrepPlan($"compose:{stack.Feature.Name}:slabs={stack.Slabs.Count}:transitions={stack.Transitions.Count}", points.Count, builder.Model.Edges.Count(), faces.Count, "deterministic-slab-partitions", true, correspondence, topologyPlan);
        d.Add("compose-authoritative-section-stack-brep-plan"); d.Add("compose-no-3d-boolean-used");
        d.Add("compose-plan-first-materialization-boundary");
        return new(plan, d.Distinct().ToArray(), correspondence);
    }
    private static FaceId AddCap(TopologyBuilder builder, PrismaticSectionRegion region, double z, Func<LineArcProfileCurve2D, double, (EdgeId Edge, bool Reverse)> edge, IReadOnlyList<(double X, double Y)> splitPoints)
    {
        var loops = new List<LoopId>();
        foreach (var item in Loops(region))
        {
            var segments = item.IsHole ? item.Profile.Loops[0].Segments.Reverse() : item.Profile.Loops[0].Segments;
            var curves = segments.SelectMany(s => Split(s.Geometry, splitPoints)).ToArray();
            loops.Add(AddLoop(builder, curves.Select(curve => { var e = edge(curve, z); return new Use(e.Edge, item.IsHole ? !e.Reverse : e.Reverse); }).ToArray()));
        }
        return builder.AddFace(loops);
    }
    private static IEnumerable<(ResolvedProfile2D Profile, bool IsHole)> Loops(PrismaticSectionRegion region) { yield return (region.Outer, false); foreach (var h in region.Holes) yield return (h, true); }
    private static (double X, double Y)[] Ends(LineArcProfileCurve2D curve) => curve switch { LineArcLineSegment2D l => [l.Start, l.End], LineArcCircularArc2D a => [(a.Center.X + a.Radius * Math.Cos(a.StartAngleRadians), a.Center.Y + a.Radius * Math.Sin(a.StartAngleRadians)), (a.Center.X + a.Radius * Math.Cos(a.StartAngleRadians + a.SweepAngleRadians), a.Center.Y + a.Radius * Math.Sin(a.StartAngleRadians + a.SweepAngleRadians))], _ => throw new NotSupportedException("X1 composition requires bounded line/arc segments.") };
    private static IEnumerable<LineArcProfileCurve2D> Split(LineArcProfileCurve2D curve, IReadOnlyList<(double X, double Y)> points)
    {
        var parameters = points.Select(p => Parameter(curve, p)).Where(x => x is not null).Select(x => x!.Value).Append(0d).Append(1d).Order().Aggregate(new List<double>(), (list, value) => { if (list.Count == 0 || Math.Abs(list[^1] - value) > Tol) list.Add(value); return list; });
        for (var i = 0; i < parameters.Count - 1; i++) if (parameters[i + 1] - parameters[i] > Tol) yield return Trim(curve, parameters[i], parameters[i + 1]);
    }
    private static double? Parameter(LineArcProfileCurve2D curve, (double X, double Y) p)
    {
        if (curve is LineArcLineSegment2D line) { var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length2 = dx * dx + dy * dy; var t = ((p.X - line.Start.X) * dx + (p.Y - line.Start.Y) * dy) / length2; return t >= -Tol && t <= 1d + Tol && Math.Abs((line.Start.X + t * dx - p.X)) <= Tol && Math.Abs((line.Start.Y + t * dy - p.Y)) <= Tol ? Math.Clamp(t, 0d, 1d) : null; }
        if (curve is LineArcCircularArc2D arc) { if (Math.Abs(Math.Sqrt((p.X - arc.Center.X) * (p.X - arc.Center.X) + (p.Y - arc.Center.Y) * (p.Y - arc.Center.Y)) - arc.Radius) > Tol) return null; var delta = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X) - arc.StartAngleRadians; if (arc.SweepAngleRadians >= 0d) while (delta < 0d) delta += 2d * Math.PI; else while (delta > 0d) delta -= 2d * Math.PI; var t = delta / arc.SweepAngleRadians; return t >= -Tol && t <= 1d + Tol ? Math.Clamp(t, 0d, 1d) : null; }
        return null;
    }
    private static LineArcProfileCurve2D Trim(LineArcProfileCurve2D curve, double from, double to) => curve switch { LineArcLineSegment2D line => new LineArcLineSegment2D((line.Start.X + (line.End.X - line.Start.X) * from, line.Start.Y + (line.End.Y - line.Start.Y) * from), (line.Start.X + (line.End.X - line.Start.X) * to, line.Start.Y + (line.End.Y - line.Start.Y) * to)), LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngleRadians + arc.SweepAngleRadians * from, arc.SweepAngleRadians * (to - from)), _ => throw new NotSupportedException() };
    private static LineArcProfileCurve2D Reverse(LineArcProfileCurve2D curve) => curve switch { LineArcLineSegment2D line => new LineArcLineSegment2D(line.End, line.Start), LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngleRadians + arc.SweepAngleRadians, -arc.SweepAngleRadians), _ => throw new NotSupportedException() };
    private static string CurveKey(LineArcProfileCurve2D curve, double z, (double X, double Y) a, (double X, double Y) b) => curve switch
    {
        LineArcLineSegment2D => $"L:{P(a)}:{P(b)}:{Q(z)}",
        LineArcCircularArc2D arc => $"A:{P(arc.Center)}:{Q(arc.Radius)}:{P(a)}:{P(b)}:{Q(z)}",
        _ => throw new NotSupportedException()
    };
    private static string P((double X, double Y) point) => $"{Q(point.X)},{Q(point.Y)}";
    private static long Q(double value) => (long)Math.Round(value / Tol);
    private static string SourceId(string stableId)
    {
        const string marker = ".arrangement.";
        var index = stableId.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? stableId[..index] : stableId;
    }
    private static (ParameterInterval Trim, bool Oriented) CurveTrim(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => (new ParameterInterval(0d, Math.Sqrt((line.End.X - line.Start.X) * (line.End.X - line.Start.X) + (line.End.Y - line.Start.Y) * (line.End.Y - line.Start.Y))), true),
        LineArcCircularArc2D arc when arc.SweepAngleRadians >= 0d => (new ParameterInterval(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians), true),
        LineArcCircularArc2D arc => (new ParameterInterval(arc.StartAngleRadians + arc.SweepAngleRadians, arc.StartAngleRadians), false),
        _ => throw new NotSupportedException()
    };
    private static CurveGeometry Curve(LineArcProfileCurve2D curve, double z) => curve switch { LineArcLineSegment2D l => CurveGeometry.FromLine(new Line3Curve(new Point3D(l.Start.X, l.Start.Y, z), Direction3D.Create(new Vector3D(l.End.X - l.Start.X, l.End.Y - l.Start.Y, 0)))), LineArcCircularArc2D a => CurveGeometry.FromCircle(new Circle3Curve(new Point3D(a.Center.X, a.Center.Y, z), Direction3D.Create(new Vector3D(0, 0, 1)), a.Radius, Direction3D.Create(new Vector3D(1, 0, 0)))), _ => throw new NotSupportedException() };
    private static SurfaceGeometry SideSurface(LineArcProfileCurve2D curve, double z, bool hole) => curve switch { LineArcLineSegment2D l => SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(l.Start.X, l.Start.Y, z), Direction3D.Create(new Vector3D(hole ? l.Start.Y - l.End.Y : l.End.Y - l.Start.Y, hole ? l.End.X - l.Start.X : l.Start.X - l.End.X, 0)), Direction3D.Create(new Vector3D(0, 0, 1)))), LineArcCircularArc2D a => SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(a.Center.X, a.Center.Y, z), Direction3D.Create(new Vector3D(0, 0, 1)), a.Radius, Direction3D.Create(new Vector3D(1, 0, 0)))), _ => throw new NotSupportedException() };
    private static LoopId AddLoop(TopologyBuilder builder, IReadOnlyList<Use> uses) { var id = builder.AllocateLoopId(); var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray(); for (var i = 0; i < uses.Count; i++) builder.AddCoedge(new Coedge(coedges[i], uses[i].Edge, id, coedges[(i + 1) % coedges.Length], coedges[(i + coedges.Length - 1) % coedges.Length], uses[i].Reverse)); builder.AddLoop(new Loop(id, coedges)); return id; }
    private readonly record struct Use(EdgeId Edge, bool Reverse);
}
