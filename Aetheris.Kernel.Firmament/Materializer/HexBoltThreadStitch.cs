using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Replaces exactly the two HexBolt shank skin faces with a helical root-stock skin.
/// The original head and tip, the rib skin, and two planar annular shoulders share
/// one shell. This bounded construction does not perform a Boolean union.
/// </summary>
internal static class HexBoltThreadStitch
{
    internal sealed record Result(BrepBody Body, IReadOnlyDictionary<FaceId, FaceId> BoltFaces,
        IReadOnlyDictionary<FaceId, FaceId> ThreadFaces,
        IReadOnlyDictionary<EdgeId, EdgeId> ThreadEdges);

    internal static Result Create(HexBoltDefinition bolt, BrepHelicalRibResult rib)
    {
        var shank = bolt.Semantics.Descendants
            .Where(item => item.StableId.StartsWith(bolt.Semantics.BodyStableId + ".Shank.Face[", StringComparison.Ordinal)
                           && item.Face.HasValue)
            .Select(item => item.Face!.Value).ToHashSet();
        if (shank.Count != 2) throw new InvalidOperationException("HexBolt thread stitch requires exactly two cylindrical shank faces.");
        var retainedBolt = bolt.Body.Topology.Faces.Where(face => !shank.Contains(face.Id)).Select(face => face.Id).ToArray();
        var retainedRib = rib.FaceRoles.Where(item => item.Value is not ("Support.BottomCap" or "Support.TopCap"))
            .Select(item => item.Key).ToArray();
        var shell = new ShellCopy();
        var boltCopy = shell.CopyFaces(bolt.Body, retainedBolt);
        var ribCopy = shell.CopyFaces(rib.Body, retainedRib);

        var shankEdges = EdgesOfFaces(bolt.Body, shank).ToHashSet();
        var retainedEdges = EdgesOfFaces(bolt.Body, retainedBolt).ToHashSet();
        var sharedRings = shankEdges.Intersect(retainedEdges).ToArray();
        if (sharedRings.Length != 4) throw new InvalidOperationException("HexBolt shank must meet head and tip at two half-circle rings.");
        var startX = bolt.Spec.UnderHeadRadius;
        var endX = bolt.Dimensions.TipChamferStartX;
        var lowerArcs = sharedRings.Where(edge => AtX(bolt.Body, edge, startX)).Select(edge => boltCopy.Edges[edge]).ToArray();
        var upperArcs = sharedRings.Where(edge => AtX(bolt.Body, edge, endX)).Select(edge => boltCopy.Edges[edge]).ToArray();
        if (lowerArcs.Length != 2 || upperArcs.Length != 2)
            throw new InvalidOperationException("HexBolt shank boundary rings do not match the planned axial interval.");
        var lowerRoot = ribCopy.Edges[AssertRole(rib, "Support.BottomRim")];
        var upperRoot = ribCopy.Edges[AssertRole(rib, "Support.TopRim")];
        shell.Annulus(startX, lowerArcs, lowerRoot, outwardPositive: true);
        shell.Annulus(endX, upperArcs, upperRoot, outwardPositive: false);
        var body = shell.Complete();
        var bindings = BrepBindingValidator.Validate(body, true);
        if (!bindings.IsSuccess)
            throw new InvalidOperationException("HexBolt thread stitch failed geometry bindings: " + string.Join(" | ",
                bindings.Diagnostics.Take(8).Select(item => item.Message)));
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid)
            throw new InvalidOperationException("HexBolt thread stitch failed BRep preflight: " + string.Join(" | ",
                preflight.Diagnostics.Where(item => item.Severity == BrepExportPreflightSeverity.Error)
                    .Take(8).Select(item => item.Code + ": " + item.Message)));
        var mass = BrepMassProperties.Evaluate(body);
        if (!mass.IsEnclosed || !mass.IsOrientationConsistent)
            throw new InvalidOperationException("HexBolt thread stitch did not produce an enclosed, consistently oriented body: "
                + string.Join(" | ", mass.Diagnostics));
        return new(body, boltCopy.Faces, ribCopy.Faces, ribCopy.Edges);
    }

    private static EdgeId AssertRole(BrepHelicalRibResult rib, string role) =>
        rib.EdgeRoles.Single(item => item.Value == role).Key;

    private static IEnumerable<EdgeId> EdgesOfFaces(BrepBody body, IEnumerable<FaceId> faces)
    {
        foreach (var faceId in faces)
            foreach (var loopId in body.Topology.GetFace(faceId).LoopIds)
                foreach (var coedgeId in body.Topology.GetLoop(loopId).CoedgeIds)
                    yield return body.Topology.GetCoedge(coedgeId).EdgeId;
    }

    private static bool AtX(BrepBody body, EdgeId edgeId, double x)
    {
        var edge = body.Topology.GetEdge(edgeId);
        return body.TryGetVertexPoint(edge.StartVertexId, out var a)
            && body.TryGetVertexPoint(edge.EndVertexId, out var b)
            && Math.Abs(a.X - x) < 1e-7d && Math.Abs(b.X - x) < 1e-7d;
    }

    private sealed class ShellCopy
    {
        private readonly TopologyBuilder topology = new();
        private readonly BrepGeometryStore geometry = new();
        private readonly BrepBindingModel bindings = new();
        private readonly Dictionary<VertexId, Point3D> points = [];
        private readonly List<FaceId> faces = [];
        private int nextCurve = 1;
        private int nextSurface = 1;

        internal sealed record CopyMap(IReadOnlyDictionary<FaceId, FaceId> Faces, IReadOnlyDictionary<EdgeId, EdgeId> Edges);

        internal CopyMap CopyFaces(BrepBody source, IReadOnlyCollection<FaceId> retained)
        {
            var vertexMap = new Dictionary<VertexId, VertexId>();
            var edgeMap = new Dictionary<EdgeId, EdgeId>();
            var curveMap = new Dictionary<CurveGeometryId, CurveGeometryId>();
            var surfaceMap = new Dictionary<SurfaceGeometryId, SurfaceGeometryId>();
            var faceMap = new Dictionary<FaceId, FaceId>();

            VertexId Vertex(VertexId old)
            {
                if (vertexMap.TryGetValue(old, out var mapped)) return mapped;
                if (!source.TryGetVertexPoint(old, out var point)) throw new InvalidOperationException("Unbound stitch vertex.");
                mapped = topology.AddVertex();
                vertexMap.Add(old, mapped);
                points.Add(mapped, point);
                return mapped;
            }

            EdgeId Edge(EdgeId old)
            {
                if (edgeMap.TryGetValue(old, out var mapped)) return mapped;
                var edge = source.Topology.GetEdge(old);
                mapped = topology.AddEdge(Vertex(edge.StartVertexId), Vertex(edge.EndVertexId));
                edgeMap.Add(old, mapped);
                var binding = source.Bindings.GetEdgeBinding(old);
                if (!curveMap.TryGetValue(binding.CurveGeometryId, out var curveId))
                {
                    curveId = new CurveGeometryId(nextCurve++);
                    curveMap.Add(binding.CurveGeometryId, curveId);
                    geometry.AddCurve(curveId, source.Geometry.GetCurve(binding.CurveGeometryId));
                }
                bindings.AddEdgeBinding(binding with { EdgeId = mapped, CurveGeometryId = curveId });
                return mapped;
            }

            SurfaceGeometryId Surface(SurfaceGeometryId old)
            {
                if (surfaceMap.TryGetValue(old, out var mapped)) return mapped;
                mapped = new SurfaceGeometryId(nextSurface++);
                surfaceMap.Add(old, mapped);
                geometry.AddSurface(mapped, source.Geometry.GetSurface(old));
                return mapped;
            }

            foreach (var oldFace in retained.OrderBy(id => id.Value))
            {
                var face = source.Topology.GetFace(oldFace);
                var newLoops = new List<LoopId>();
                var loopMap = new Dictionary<LoopId, LoopId>();
                var oldToNewCoedges = new Dictionary<CoedgeId, CoedgeId>();
                foreach (var oldLoop in face.LoopIds)
                {
                    var loop = source.Topology.GetLoop(oldLoop);
                    if (loop.Kind != LoopKind.Edge) throw new InvalidOperationException("HexBolt thread stitch requires edge loops.");
                    var loopId = topology.AllocateLoopId();
                    var coedgeIds = loop.CoedgeIds.Select(_ => topology.AllocateCoedgeId()).ToArray();
                    for (var i = 0; i < coedgeIds.Length; i++)
                    {
                        var oldCoedge = source.Topology.GetCoedge(loop.CoedgeIds[i]);
                        topology.AddCoedge(new Coedge(coedgeIds[i], Edge(oldCoedge.EdgeId), loopId,
                            coedgeIds[(i + 1) % coedgeIds.Length], coedgeIds[(i + coedgeIds.Length - 1) % coedgeIds.Length],
                            oldCoedge.IsReversed));
                        oldToNewCoedges.Add(oldCoedge.Id, coedgeIds[i]);
                    }
                    topology.AddLoop(new Loop(loopId, coedgeIds));
                    newLoops.Add(loopId);
                    loopMap.Add(oldLoop, loopId);
                }
                var newFace = topology.AddFace(newLoops);
                faces.Add(newFace);
                faceMap.Add(oldFace, newFace);
                var faceBinding = source.Bindings.GetFaceBinding(oldFace);
                var newSurface = Surface(faceBinding.SurfaceGeometryId);
                bindings.AddFaceBinding(faceBinding with { FaceId = newFace, SurfaceGeometryId = newSurface });
                foreach (var loop in loopMap)
                    if (source.Bindings.TryGetFaceBoundaryRoleBinding(loop.Key, out var role))
                        bindings.AddFaceBoundaryRoleBinding(role with { FaceId = newFace, LoopId = loop.Value });
                foreach (var oldCoedge in oldToNewCoedges)
                    if (source.Bindings.TryGetPcurveBinding(oldCoedge.Key, out var pcurve))
                        bindings.AddPcurveBinding(pcurve with { CoedgeId = oldCoedge.Value, FaceId = newFace,
                            SurfaceGeometryId = Surface(pcurve.SurfaceGeometryId) });
                foreach (var oldLoop in face.LoopIds)
                    if (source.Bindings.TryGetVertexLoopParameterBinding(oldLoop, out var parameter))
                        throw new InvalidOperationException("HexBolt thread stitch does not admit vertex-loop faces.");
            }
            return new(faceMap, edgeMap);
        }

        internal void Annulus(double x, IReadOnlyList<EdgeId> outerArcs, EdgeId innerCircle, bool outwardPositive)
        {
            var normal = Direction3D.Create(new Vector3D(outwardPositive ? 1d : -1d, 0d, 0d));
            var radial = Direction3D.Create(new Vector3D(0d, 1d, 0d));
            var surfaceId = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(surfaceId, SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(x, 0d, 0d), normal, radial)));
            var outer = OrderOppositeUses(outerArcs);
            var inner = OrderOppositeUses([innerCircle]);
            var outerLoop = Loop(outer);
            var innerLoop = Loop(inner);
            var face = topology.AddFace([outerLoop, innerLoop]);
            faces.Add(face);
            bindings.AddFaceBinding(new FaceGeometryBinding(face, surfaceId));
            bindings.AddFaceBoundaryRoleBinding(new(face, outerLoop, FaceBoundaryRole.Outer));
            bindings.AddFaceBoundaryRoleBinding(new(face, innerLoop, FaceBoundaryRole.Inner));
        }

        private (EdgeId Edge, bool Reverse)[] OrderOppositeUses(IReadOnlyList<EdgeId> edges)
        {
            var uses = edges.Select(edge => (Edge: edge, Reverse: !ExistingUse(edge))).ToArray();
            if (uses.Length == 1) return uses;
            var first = uses[0];
            var second = uses[1];
            if (End(first) != Start(second)) (first, second) = (second, first);
            if (End(first) != Start(second) || End(second) != Start(first))
                throw new InvalidOperationException("HexBolt stitch boundary arcs do not form a closed ring.");
            return [first, second];
        }

        private bool ExistingUse(EdgeId edge)
        {
            var uses = topology.Model.Coedges.Where(item => item.EdgeId == edge).ToArray();
            if (uses.Length != 1) throw new InvalidOperationException("Stitch boundary must have one retained coedge use.");
            return uses[0].IsReversed;
        }

        private VertexId Start((EdgeId Edge, bool Reverse) use)
        {
            var edge = topology.Model.GetEdge(use.Edge);
            return use.Reverse ? edge.EndVertexId : edge.StartVertexId;
        }

        private VertexId End((EdgeId Edge, bool Reverse) use)
        {
            var edge = topology.Model.GetEdge(use.Edge);
            return use.Reverse ? edge.StartVertexId : edge.EndVertexId;
        }

        private LoopId Loop(IReadOnlyList<(EdgeId Edge, bool Reverse)> uses)
        {
            var loopId = topology.AllocateLoopId();
            var ids = uses.Select(_ => topology.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < ids.Length; i++)
                topology.AddCoedge(new Coedge(ids[i], uses[i].Edge, loopId,
                    ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse));
            topology.AddLoop(new Loop(loopId, ids));
            return loopId;
        }

        internal BrepBody Complete()
        {
            var shell = topology.AddShell(faces);
            topology.AddBody([shell]);
            return new BrepBody(topology.Model, geometry, bindings, points);
        }
    }
}
