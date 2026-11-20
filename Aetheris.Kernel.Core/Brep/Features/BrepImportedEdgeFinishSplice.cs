using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

internal static partial class BrepImportedEdgeFinish
{
    private readonly record struct Use(EdgeId Edge, bool Reverse, CoedgeId? RetainedId = null);
    private sealed record EndPatch(VertexId A, VertexId B, EdgeId Transition);

    private static (BrepBody Body, FaceId Finish) Splice(BrepBody source, Region region, double tolerance)
    {
        var patch = new Patch(source, tolerance, [region.Start.Corner, region.End.Corner]);
        var shortened = new Dictionary<EdgeId, EdgeId>();
        var first = CutEndpoint(region.Start, region.FirstArc);
        var last = CutEndpoint(region.End, region.LastArc);
        var alongA = patch.Line(first.A, last.A);
        var alongB = patch.Line(first.B, last.B);

        Rewrite(region.A.Use.LoopId, region.A.Face, alongA);
        Rewrite(region.B.Use.LoopId, region.B.Face, alongB);
        Rewrite(region.Start.Incoming.LoopId, region.Start.Cap, first.Transition, region.Start);
        Rewrite(region.End.Incoming.LoopId, region.End.Cap, last.Transition, region.End);

        Use[] finishUses = [new(alongA, true), new(first.Transition, false), new(alongB, false), new(last.Transition, true)];
        if (region.A.Use.IsReversed)
            finishUses = finishUses.Reverse().Select(use => use with { Reverse = !use.Reverse }).ToArray();
        var face = patch.Finish(region.Surface, finishUses);
        return (patch.Build(shortened.Keys.Append(region.Selected)), face);

        EndPatch CutEndpoint(Endpoint endpoint, Circle3Curve? arc)
        {
            var a = patch.Vertex(endpoint.A.Cut);
            var b = patch.Vertex(endpoint.B.Cut);
            shortened.Add(endpoint.A.Edge, patch.Shorten(endpoint.A.Edge, endpoint.Corner, a));
            shortened.Add(endpoint.B.Edge, patch.Shorten(endpoint.B.Edge, endpoint.Corner, b));
            var transition = arc is { } circle
                ? patch.Edge(a, b, CurveGeometry.FromCircle(circle), new(0, region.Sweep))
                : patch.Line(a, b);
            return new(a, b, transition);
        }

        void Rewrite(LoopId loop, FaceId face, EdgeId replacement, Endpoint? cap = null)
        {
            var updated = new List<Use>();
            foreach (var use in source.Topology.GetLoop(loop).CoedgeIds.Select(source.Topology.GetCoedge))
            {
                if (use.EdgeId == region.Selected)
                    updated.Add(new(replacement, use.IsReversed));
                else if (shortened.TryGetValue(use.EdgeId, out var shorter))
                    updated.Add(new(shorter, use.IsReversed));
                else
                    updated.Add(new(use.EdgeId, use.IsReversed, use.Id));

                if (cap is not null && cap.Incoming.Id == use.Id)
                    updated.Add(new(replacement, use.EdgeId == cap.B.Edge));
            }
            patch.Loop(loop, face, updated);
        }
    }

    // The two plans supply the changed loops and valid boundary geometry. This
    // assembler preserves the rest of the source and builds only new pcurves.
    private sealed class Patch
    {
        private readonly BrepBody source;
        private readonly double tolerance;
        private readonly TopologyModel topology = new();
        private readonly BrepGeometryStore geometry;
        private readonly BrepBindingModel bindings = new();
        private readonly Dictionary<VertexId, Point3D> points = [];
        private readonly Dictionary<LoopId, Loop> loops = [];
        private readonly Dictionary<CoedgeId, Coedge> uses = [];
        private readonly List<Face> addedFaces = [];
        private readonly int stockUseMax;
        private int vertexNumber, edgeNumber, curveNumber, useNumber, faceNumber, loopNumber, surfaceNumber;

        internal Patch(BrepBody source, double tolerance, HashSet<VertexId> removedVertices)
        {
            this.source = source;
            this.tolerance = tolerance;
            var stock = source.Topology;
            geometry = source.Geometry.Clone();
            vertexNumber = stock.Vertices.Max(vertex => vertex.Id.Value);
            edgeNumber = stock.Edges.Max(edge => edge.Id.Value);
            curveNumber = geometry.Curves.Max(curve => curve.Key.Value);
            useNumber = stockUseMax = stock.Coedges.Max(use => use.Id.Value);
            faceNumber = stock.Faces.Max(face => face.Id.Value);
            loopNumber = stock.Loops.Max(loop => loop.Id.Value);
            surfaceNumber = geometry.Surfaces.Max(surface => surface.Key.Value);

            foreach (var vertex in stock.Vertices.Where(vertex => !removedVertices.Contains(vertex.Id)))
            {
                topology.AddVertex(vertex);
                points.Add(vertex.Id, Point(source, vertex.Id));
            }
            foreach (var binding in source.Bindings.FaceBindings) bindings.AddFaceBinding(binding);
            foreach (var binding in source.Bindings.FaceBoundaryRoleBindings) bindings.AddFaceBoundaryRoleBinding(binding);
            foreach (var binding in source.Bindings.VertexLoopParameterBindings) bindings.AddVertexLoopParameterBinding(binding);
        }

        internal VertexId Vertex(Point3D point)
        {
            var id = new VertexId(++vertexNumber);
            points.Add(id, point);
            topology.AddVertex(new(id));
            return id;
        }

        internal EdgeId Line(VertexId start, VertexId end)
        {
            var delta = points[end] - points[start];
            return Edge(start, end, CurveGeometry.FromLine(new(points[start], Direction3D.Create(delta))), new(0, delta.Length));
        }

        internal EdgeId Edge(VertexId start, VertexId end, CurveGeometry curve, ParameterInterval trim)
        {
            var id = new EdgeId(++edgeNumber);
            var curveId = new CurveGeometryId(++curveNumber);
            topology.AddEdge(new(id, start, end));
            geometry.AddCurve(curveId, curve);
            bindings.AddEdgeBinding(new(id, curveId, trim));
            return id;
        }

        internal EdgeId Shorten(EdgeId id, VertexId corner, VertexId cut)
        {
            var edge = source.Topology.GetEdge(id);
            var old = source.Bindings.GetEdgeBinding(id);
            var line = geometry.GetCurve(old.CurveGeometryId).Line3!.Value;
            var trim = old.TrimInterval!.Value;
            var cutParameter = (points[cut] - line.Origin).Dot(line.Direction.ToVector());
            if (cutParameter <= trim.Start || cutParameter >= trim.End)
                throw new ArgumentException($"The cut point did not land in the trim of edge {id.Value}.");
            var trimsStart = edge.StartVertexId == corner;
            var trimsLowParameter = trimsStart == old.OrientedEdgeSense;
            var replacement = new EdgeId(++edgeNumber);
            topology.AddEdge(new(replacement, trimsStart ? cut : edge.StartVertexId, trimsStart ? edge.EndVertexId : cut));
            bindings.AddEdgeBinding(new(replacement, old.CurveGeometryId,
                trimsLowParameter ? new(cutParameter, trim.End) : new(trim.Start, cutParameter), old.OrientedEdgeSense));
            return replacement;
        }

        internal FaceId Finish(SurfaceGeometry surface, Use[] boundary)
        {
            var face = new FaceId(++faceNumber);
            var loop = new LoopId(++loopNumber);
            var surfaceId = new SurfaceGeometryId(++surfaceNumber);
            geometry.AddSurface(surfaceId, surface);
            bindings.AddFaceBinding(new(face, surfaceId));
            bindings.AddFaceBoundaryRoleBinding(new(face, loop, FaceBoundaryRole.Outer));
            Loop(loop, face, boundary);
            addedFaces.Add(new(face, [loop]));
            return face;
        }

        internal void Loop(LoopId loop, FaceId face, IReadOnlyList<Use> updated)
        {
            var ids = updated.Select(use => use.RetainedId ?? new CoedgeId(++useNumber)).ToArray();
            var faceBinding = bindings.GetFaceBinding(face);
            var surface = geometry.GetSurface(faceBinding.SurfaceGeometryId);
            for (var index = 0; index < updated.Count; index++)
            {
                var use = updated[index];
                var id = ids[index];
                uses.Add(id, new(id, use.Edge, loop, ids[(index + 1) % ids.Length], ids[(index + ids.Length - 1) % ids.Length], use.Reverse));
                if (use.RetainedId.HasValue) continue;

                var edge = bindings.GetEdgeBinding(use.Edge);
                var curve = geometry.GetCurve(edge.CurveGeometryId);
                var pcurve = BrepPcurveRecovery.TryBuildAtEdgeParameter(surface, curve, edge.TrimInterval!.Value, tolerance)
                    ?? throw new ArgumentException($"The new edge trim on face {face.Value} could not be qualified.");
                bindings.AddPcurveBinding(new(id, face, faceBinding.SurfaceGeometryId, pcurve));
            }
            loops.Add(loop, new(loop, ids));
        }

        internal BrepBody Build(IEnumerable<EdgeId> removedEdges)
        {
            var retired = removedEdges.ToHashSet();
            var stock = source.Topology;
            foreach (var edge in stock.Edges.Where(edge => !retired.Contains(edge.Id)))
            {
                topology.AddEdge(edge);
                bindings.AddEdgeBinding(source.Bindings.GetEdgeBinding(edge.Id));
            }
            foreach (var original in stock.Coedges.Where(use => !retired.Contains(use.EdgeId)))
            {
                var updated = uses.GetValueOrDefault(original.Id, original);
                topology.AddCoedge(updated == original ? original : updated);
                if (source.Bindings.TryGetPcurveBinding(original.Id, out var binding)) bindings.AddPcurveBinding(binding);
            }
            foreach (var use in uses.Values.Where(use => use.Id.Value > stockUseMax)) topology.AddCoedge(use);
            foreach (var loop in stock.Loops) topology.AddLoop(loops.GetValueOrDefault(loop.Id, loop));
            foreach (var face in addedFaces)
                foreach (var id in face.LoopIds) topology.AddLoop(loops[id]);
            foreach (var face in stock.Faces.Concat(addedFaces)) topology.AddFace(face);
            foreach (var shell in stock.Shells) topology.AddShell(shell with { FaceIds = [.. shell.FaceIds, .. addedFaces.Select(face => face.Id)] });
            foreach (var body in stock.Bodies) topology.AddBody(body);
            return new(topology, geometry, bindings, points, shellRepresentation: source.ShellRepresentation);
        }
    }
}
