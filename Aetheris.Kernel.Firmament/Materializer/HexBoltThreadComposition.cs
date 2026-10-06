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
/// Composes the ordinary HexBolt head and tip with its bounded stock-minus-groove
/// shank. The major-radius rims are shared directly: no replacement root-stock
/// rod, annular shoulders, or overlapping volumes are introduced.
/// </summary>
internal static class HexBoltThreadComposition
{
    internal sealed record Result(BrepBody Body, IReadOnlyDictionary<FaceId, FaceId> BoltFaces,
        IReadOnlyDictionary<FaceId, FaceId> ThreadFaces,
        IReadOnlyDictionary<EdgeId, EdgeId> ThreadEdges);
    internal sealed record PatchResult(BrepBody Body, IReadOnlyDictionary<FaceId, FaceId> HostFaces,
        IReadOnlyDictionary<EdgeId, EdgeId> HostEdges, IReadOnlyDictionary<FaceId, FaceId> PatchFaces,
        FaceId ReplacementCap);

    /// <summary>Replaces only the planar head cap with section-stack pocket faces.</summary>
    internal static PatchResult ImprintTop(BrepBody threaded, FaceId topCap,
        BrepBody patch, IReadOnlyCollection<FaceId> patchFaces)
    {
        var retained = threaded.Topology.Faces.Where(face => face.Id != topCap).Select(face => face.Id).ToArray();
        var shell = new ShellCopy();
        var copied = shell.CopyFaces(threaded, retained);
        var hostRim = EdgesOfFaces(threaded, [topCap]).Distinct().ToArray();
        var patchRim = EdgesOfFaces(patch, patchFaces).Distinct()
            .Where(edge => hostRim.Any(host => SameEdge(threaded, host, patch, edge))).ToArray();
        if (patchRim.Length != hostRim.Length)
            throw new InvalidOperationException($"Maker mark rim mismatch: patch={patchRim.Length}, head={hostRim.Length}.");
        var shared = patchRim.ToDictionary(edge => edge,
            edge => copied.Edges[hostRim.Single(host => SameEdge(threaded, host, patch, edge))]);
        var patchCopy = shell.CopyFaces(patch, patchFaces, shared);
        var body = shell.Complete();
        var nonManifold = body.Topology.Coedges.GroupBy(x => x.EdgeId).Where(x => x.Count() != 2).ToArray();
        if (nonManifold.Length != 0)
            throw new InvalidOperationException($"Maker mark shell has {nonManifold.Length} non-manifold edges.");
        var mass = BrepMassProperties.Evaluate(body);
        if (!mass.IsEnclosed || !mass.IsOrientationConsistent)
            throw new InvalidOperationException("Maker mark shell is not enclosed and oriented: " + string.Join(" | ", mass.Diagnostics));
        var cap = patchCopy.Faces.Values.Single(face => shared.Values.All(edge =>
            EdgesOfFaces(body, [face]).Contains(edge)));
        return new(body, copied.Faces, copied.Edges, patchCopy.Faces, cap);
    }

    private static bool SameEdge(BrepBody firstBody, EdgeId firstId, BrepBody secondBody, EdgeId secondId)
    {
        var first = firstBody.Topology.GetEdge(firstId);
        var second = secondBody.Topology.GetEdge(secondId);
        firstBody.TryGetVertexPoint(first.StartVertexId, out var a);
        firstBody.TryGetVertexPoint(first.EndVertexId, out var b);
        secondBody.TryGetVertexPoint(second.StartVertexId, out var c);
        secondBody.TryGetVertexPoint(second.EndVertexId, out var d);
        static bool Near(Point3D x, Point3D y) => (x - y).Length < 1e-5d;
        if (!((Near(a, c) && Near(b, d)) || (Near(a, d) && Near(b, c)))) return false;
        var firstBinding = firstBody.Bindings.GetEdgeBinding(firstId);
        var secondBinding = secondBody.Bindings.GetEdgeBinding(secondId);
        var firstTrim = firstBinding.TrimInterval!.Value;
        var secondTrim = secondBinding.TrimInterval!.Value;
        var firstCircle = firstBody.Geometry.GetCurve(firstBinding.CurveGeometryId).Circle3;
        var secondCircle = secondBody.Geometry.GetCurve(secondBinding.CurveGeometryId).Circle3;
        return firstCircle is { } circleA && secondCircle is { } circleB &&
            Near(circleA.Evaluate((firstTrim.Start + firstTrim.End) / 2d),
                circleB.Evaluate((secondTrim.Start + secondTrim.End) / 2d));
    }

    internal static Result Create(HexBoltDefinition bolt, BrepHelicalRibResult rib)
    {
        if (rib.Authority.Parameters.Intent != HelicalProfileIntent.RemoveGroove ||
            Math.Abs(rib.Authority.Parameters.RootRadiusMm - bolt.Spec.NominalDiameter / 2d) > 1e-7d)
            throw new InvalidOperationException("HexBolt threads require a groove cut into major-diameter stock.");
        var shank = bolt.Semantics.Descendants
            .Where(item => item.StableId.StartsWith(bolt.Semantics.BodyStableId + ".Shank.Face[", StringComparison.Ordinal)
                           && item.Face.HasValue)
            .Select(item => item.Face!.Value).ToHashSet();
        if (shank.Count != 2) throw new InvalidOperationException("HexBolt thread composition requires exactly two cylindrical shank faces.");
        var retainedBolt = bolt.Body.Topology.Faces.Where(face => !shank.Contains(face.Id)).Select(face => face.Id).ToArray();
        var retainedRib = rib.FaceRoles.Where(item => item.Value is not ("Support.BottomCap" or "Support.TopCap"))
            .Select(item => item.Key).ToArray();
        var shell = new ShellCopy();
        var boltCopy = shell.CopyFaces(bolt.Body, retainedBolt);
        var shankEdges = EdgesOfFaces(bolt.Body, shank).ToHashSet();
        var retainedEdges = EdgesOfFaces(bolt.Body, retainedBolt).ToHashSet();
        var sharedRings = shankEdges.Intersect(retainedEdges).ToArray();
        if (sharedRings.Length != 4) throw new InvalidOperationException("HexBolt shank must meet head and tip at two half-circle rings.");
        var rims = rib.EdgeRoles.Where(pair => pair.Value.StartsWith("Support.BottomRim", StringComparison.Ordinal)
            || pair.Value.StartsWith("Support.TopRim", StringComparison.Ordinal)).Select(pair => pair.Key).ToArray();
        if (rims.Length != 4) throw new InvalidOperationException("Grooved stock requires split major-radius rims.");
        var shared = rims.ToDictionary(edge => edge, edge => boltCopy.Edges[
            sharedRings.Single(host => SameEdge(bolt.Body, host, rib.Body, edge))]);
        var ribCopy = shell.CopyFaces(rib.Body, retainedRib, shared);
        var body = shell.Complete();
        var bindings = BrepBindingValidator.Validate(body, true);
        if (!bindings.IsSuccess)
            throw new InvalidOperationException("HexBolt thread composition failed geometry bindings: " + string.Join(" | ",
                bindings.Diagnostics.Take(8).Select(item => item.Message)));
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid)
            throw new InvalidOperationException("HexBolt thread composition failed BRep preflight: " + string.Join(" | ",
                preflight.Diagnostics.Where(item => item.Severity == BrepExportPreflightSeverity.Error)
                    .Take(8).Select(item => item.Code + ": " + item.Message)));
        var mass = BrepMassProperties.Evaluate(body);
        if (!mass.IsEnclosed || !mass.IsOrientationConsistent)
            throw new InvalidOperationException("HexBolt thread composition did not produce an enclosed, consistently oriented body: "
                + string.Join(" | ", mass.Diagnostics));
        return new(body, boltCopy.Faces, ribCopy.Faces, ribCopy.Edges);
    }

    private static IEnumerable<EdgeId> EdgesOfFaces(BrepBody body, IEnumerable<FaceId> faces)
    {
        foreach (var faceId in faces)
            foreach (var loopId in body.Topology.GetFace(faceId).LoopIds)
                foreach (var coedgeId in body.Topology.GetLoop(loopId).CoedgeIds)
                    yield return body.Topology.GetCoedge(coedgeId).EdgeId;
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

        internal CopyMap CopyFaces(BrepBody source, IReadOnlyCollection<FaceId> retained,
            IReadOnlyDictionary<EdgeId, EdgeId>? sharedEdges = null)
        {
            var vertexMap = new Dictionary<VertexId, VertexId>();
            var edgeMap = new Dictionary<EdgeId, EdgeId>();
            var reversedShared = new HashSet<EdgeId>();
            var curveMap = new Dictionary<CurveGeometryId, CurveGeometryId>();
            var surfaceMap = new Dictionary<SurfaceGeometryId, SurfaceGeometryId>();
            var faceMap = new Dictionary<FaceId, FaceId>();

            // Seed shared endpoints before copying any other edge. Equal points
            // must be the same topological vertex at a composed boundary.
            if (sharedEdges is not null)
                foreach (var (oldId, newId) in sharedEdges)
                {
                    var oldEdge = source.Topology.GetEdge(oldId);
                    var newEdge = topology.Model.GetEdge(newId);
                    source.TryGetVertexPoint(oldEdge.StartVertexId, out var start);
                    var reverse = (start - points[newEdge.StartVertexId]).Length > 1e-5d;
                    vertexMap[oldEdge.StartVertexId] = reverse ? newEdge.EndVertexId : newEdge.StartVertexId;
                    vertexMap[oldEdge.EndVertexId] = reverse ? newEdge.StartVertexId : newEdge.EndVertexId;
                }

            VertexId Vertex(VertexId old)
            {
                if (vertexMap.TryGetValue(old, out var mapped)) return mapped;
                if (!source.TryGetVertexPoint(old, out var point)) throw new InvalidOperationException("Unbound composition vertex.");
                mapped = topology.AddVertex();
                vertexMap.Add(old, mapped);
                points.Add(mapped, point);
                return mapped;
            }

            EdgeId Edge(EdgeId old)
            {
                if (edgeMap.TryGetValue(old, out var mapped)) return mapped;
                var edge = source.Topology.GetEdge(old);
                if (sharedEdges is not null && sharedEdges.TryGetValue(old, out mapped))
                {
                    edgeMap.Add(old, mapped);
                    source.TryGetVertexPoint(edge.StartVertexId, out var oldStart);
                    var mappedEdge = topology.Model.GetEdge(mapped);
                    if ((oldStart - points[mappedEdge.StartVertexId]).Length > 1e-5d)
                        reversedShared.Add(old);
                    return mapped;
                }
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
                    if (loop.Kind != LoopKind.Edge) throw new InvalidOperationException("HexBolt thread composition requires edge loops.");
                    var loopId = topology.AllocateLoopId();
                    var coedgeIds = loop.CoedgeIds.Select(_ => topology.AllocateCoedgeId()).ToArray();
                    for (var i = 0; i < coedgeIds.Length; i++)
                    {
                        var oldCoedge = source.Topology.GetCoedge(loop.CoedgeIds[i]);
                        topology.AddCoedge(new Coedge(coedgeIds[i], Edge(oldCoedge.EdgeId), loopId,
                            coedgeIds[(i + 1) % coedgeIds.Length], coedgeIds[(i + coedgeIds.Length - 1) % coedgeIds.Length],
                            oldCoedge.IsReversed ^ reversedShared.Contains(oldCoedge.EdgeId)));
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
                    {
                        var oldEdge = source.Topology.GetCoedge(oldCoedge.Key).EdgeId;
                        if (sharedEdges is not null && sharedEdges.TryGetValue(oldEdge, out var newEdge))
                        {
                            if (pcurve.Pcurve.Kind != PcurveGeometryKind.Line)
                                throw new InvalidOperationException("Shared rim requires a linear pcurve.");
                            var uv = pcurve.Pcurve;
                            var reverse = reversedShared.Contains(oldEdge);
                            var trim = bindings.GetEdgeBinding(newEdge).TrimInterval!.Value;
                            pcurve = pcurve with { Pcurve = PcurveGeometry.Line(trim,
                                uv.Evaluate(reverse ? uv.Domain.End : uv.Domain.Start),
                                uv.Evaluate(reverse ? uv.Domain.Start : uv.Domain.End)) };
                        }
                        bindings.AddPcurveBinding(pcurve with { CoedgeId = oldCoedge.Value, FaceId = newFace,
                            SurfaceGeometryId = Surface(pcurve.SurfaceGeometryId) });
                    }
                foreach (var oldLoop in face.LoopIds)
                    if (source.Bindings.TryGetVertexLoopParameterBinding(oldLoop, out var parameter))
                        throw new InvalidOperationException("HexBolt thread composition does not admit vertex-loop faces.");
            }
            return new(faceMap, edgeMap);
        }

        internal BrepBody Complete()
        {
            var shell = topology.AddShell(faces);
            topology.AddBody([shell]);
            return new BrepBody(topology.Model, geometry, bindings, points);
        }
    }
}
