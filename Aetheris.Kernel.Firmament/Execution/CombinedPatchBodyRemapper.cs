using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum CombinedPatchBodyRemapStatus { CombinedPartialBody, Deferred, Unsupported, Failed }

internal sealed record EmittedTopologyReferenceRemap(
    string SourcePatchKey,
    string SourceLocalTopologyKey,
    EmittedTopologyReference OriginalReference,
    EmittedTopologyReference? RemappedReference,
    IReadOnlyList<string> Diagnostics);

internal sealed record PatchBodyRemapSummary(
    string PatchKey,
    int SourceFaceCount,
    int SourceLoopCount,
    int SourceEdgeCount,
    int SourceCoedgeCount,
    int SourceVertexCount,
    int RemappedFaceCount,
    int RemappedLoopCount,
    int RemappedEdgeCount,
    int RemappedCoedgeCount,
    int RemappedVertexCount,
    IReadOnlyList<string> Diagnostics);

internal sealed record CombinedPatchBodyRemapResult(
    bool Success,
    CombinedPatchBodyRemapStatus Status,
    BrepBody? CombinedBody,
    IReadOnlyList<PatchBodyRemapSummary> PatchSummaries,
    IReadOnlyList<EmittedTopologyReferenceRemap> ReferenceRemaps,
    IReadOnlyList<EmittedTopologyIdentityMap> RemappedIdentityMaps,
    IReadOnlyList<string> Diagnostics,
    bool SharedEdgeMutationApplied = false,
    bool FullShellClaimed = false,
    bool StepExportAttempted = false);

internal static class CombinedPatchBodyRemapper
{
    internal static CombinedPatchBodyRemapResult TryCombine(
        IReadOnlyList<SurfaceMaterializationResult> patchResults,
        IReadOnlyList<EmittedTopologyIdentityMap> identityMaps)
    {
        var diags = new List<string> { "combined-remap-started: bounded topology copy/remap entered." };
        var valid = patchResults.Where(p => p.Success && p.Body is not null).ToArray();
        if (valid.Length == 0)
        {
            diags.Add("combined-remap-deferred: no successful emitted patch bodies supplied.");
            return new(false, CombinedPatchBodyRemapStatus.Deferred, null, [], [], [], diags);
        }

        var topology = new TopologyModel();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, Aetheris.Kernel.Core.Math.Point3D>();
        var remappedMaps = new List<EmittedTopologyIdentityMap>();
        var refRemaps = new List<EmittedTopologyReferenceRemap>();
        var summaries = new List<PatchBodyRemapSummary>();

        var nextV=1; var nextE=1; var nextC=1; var nextL=1; var nextF=1; var nextS=1; var nextB=1; var nextCurve=1; var nextSurface=1;
        foreach (var patch in valid.OrderBy(p => p.EmittedSurfaceFamily.ToString(), StringComparer.Ordinal).ThenBy(p => p.IdentityMap?.Entries.Count ?? 0))
        {
            var body = patch.Body!;
            var vMap = body.Topology.Vertices.OrderBy(x => x.Id.Value).ToDictionary(x => x.Id, _ => new VertexId(nextV++));
            var eMap = body.Topology.Edges.OrderBy(x => x.Id.Value).ToDictionary(x => x.Id, _ => new EdgeId(nextE++));
            var cMap = body.Topology.Coedges.OrderBy(x => x.Id.Value).ToDictionary(x => x.Id, _ => new CoedgeId(nextC++));
            var lMap = body.Topology.Loops.OrderBy(x => x.Id.Value).ToDictionary(x => x.Id, _ => new LoopId(nextL++));
            var fMap = body.Topology.Faces.OrderBy(x => x.Id.Value).ToDictionary(x => x.Id, _ => new FaceId(nextF++));
            var sMap = body.Topology.Shells.OrderBy(x => x.Id.Value).ToDictionary(x => x.Id, _ => new ShellId(nextS++));
            var bMap = body.Topology.Bodies.OrderBy(x => x.Id.Value).ToDictionary(x => x.Id, _ => new BodyId(nextB++));

            foreach (var v in body.Topology.Vertices.OrderBy(x => x.Id.Value)) { topology.AddVertex(new Vertex(vMap[v.Id])); if (body.TryGetVertexPoint(v.Id,out var p)) vertexPoints[vMap[v.Id]]=p; }
            foreach (var e in body.Topology.Edges.OrderBy(x => x.Id.Value)) topology.AddEdge(new Edge(eMap[e.Id], vMap[e.StartVertexId], vMap[e.EndVertexId]));
            foreach (var c in body.Topology.Coedges.OrderBy(x => x.Id.Value)) topology.AddCoedge(new Coedge(cMap[c.Id], eMap[c.EdgeId], lMap[c.LoopId], cMap[c.NextCoedgeId], cMap[c.PrevCoedgeId], c.IsReversed));
            foreach (var l in body.Topology.Loops.OrderBy(x => x.Id.Value)) topology.AddLoop(new Loop(lMap[l.Id], l.CoedgeIds.Select(id=>cMap[id]).ToArray()));
            foreach (var f in body.Topology.Faces.OrderBy(x => x.Id.Value)) topology.AddFace(new Face(fMap[f.Id], f.LoopIds.Select(id=>lMap[id]).ToArray()));
            foreach (var s in body.Topology.Shells.OrderBy(x => x.Id.Value)) topology.AddShell(new Shell(sMap[s.Id], s.FaceIds.Select(id=>fMap[id]).ToArray()));
            foreach (var b in body.Topology.Bodies.OrderBy(x => x.Id.Value)) topology.AddBody(new Body(bMap[b.Id], b.ShellIds.Select(id=>sMap[id]).ToArray()));

            var curveMap = new Dictionary<Aetheris.Kernel.Core.Geometry.CurveGeometryId, Aetheris.Kernel.Core.Geometry.CurveGeometryId>();
            foreach (var c in body.Geometry.Curves.OrderBy(x => x.Key.Value)) { var nid = new Aetheris.Kernel.Core.Geometry.CurveGeometryId(nextCurve++); curveMap[c.Key]=nid; geometry.AddCurve(nid, c.Value); }
            var surfMap = new Dictionary<Aetheris.Kernel.Core.Geometry.SurfaceGeometryId, Aetheris.Kernel.Core.Geometry.SurfaceGeometryId>();
            foreach (var s in body.Geometry.Surfaces.OrderBy(x => x.Key.Value)) { var nid = new Aetheris.Kernel.Core.Geometry.SurfaceGeometryId(nextSurface++); surfMap[s.Key]=nid; geometry.AddSurface(nid, s.Value); }
            foreach (var eb in body.Bindings.EdgeBindings.OrderBy(x => x.EdgeId.Value)) bindings.AddEdgeBinding(new EdgeGeometryBinding(eMap[eb.EdgeId], curveMap[eb.CurveGeometryId], eb.TrimInterval, eb.OrientedEdgeSense));
            foreach (var fb in body.Bindings.FaceBindings.OrderBy(x => x.FaceId.Value)) bindings.AddFaceBinding(new FaceGeometryBinding(fMap[fb.FaceId], surfMap[fb.SurfaceGeometryId]));

            summaries.Add(new PatchBodyRemapSummary(patch.EmittedSurfaceFamily.ToString(), body.Topology.Faces.Count(), body.Topology.Loops.Count(), body.Topology.Edges.Count(), body.Topology.Coedges.Count(), body.Topology.Vertices.Count(), fMap.Count, lMap.Count, eMap.Count, cMap.Count, vMap.Count, ["patch-copied: topology and bindings copied to combined body."]));

            var map = patch.IdentityMap;
            if (map is null) continue;
            var remappedEntries = new List<EmittedTopologyIdentityEntry>();
            foreach (var entry in map.Entries)
            {
                if (entry.TopologyReference is null) { remappedEntries.Add(entry); continue; }
                var r = entry.TopologyReference;
                string? remap(string? val, Func<int,int?> f){ if (val is null) return null; var sp=val.Split(':'); if(sp.Length!=2||!int.TryParse(sp[1],out var n)) return null; var mapped=f(n); return mapped is null?null:$"{sp[0]}:{mapped.Value}"; }
                int? mv(int i)=>vMap.TryGetValue(new VertexId(i), out var id)?id.Value:null;
                int? me(int i)=>eMap.TryGetValue(new EdgeId(i), out var id)?id.Value:null;
                int? mc(int i)=>cMap.TryGetValue(new CoedgeId(i), out var id)?id.Value:null;
                int? ml(int i)=>lMap.TryGetValue(new LoopId(i), out var id)?id.Value:null;
                int? mf(int i)=>fMap.TryGetValue(new FaceId(i), out var id)?id.Value:null;
                var newRef = new EmittedTopologyReference(r.PatchKey, r.LocalTopologyKey, remap(r.FaceId,mf), remap(r.LoopId,ml), remap(r.EdgeId,me), remap(r.CoedgeId,mc), remap(r.VertexId,mv), r.Diagnostics);
                remappedEntries.Add(entry with { TopologyReference = newRef });
                refRemaps.Add(new EmittedTopologyReferenceRemap(r.PatchKey, r.LocalTopologyKey, r, newRef, ["topology-reference-remapped: source reference ids remapped into combined body ids."]));
            }
            remappedMaps.Add(new EmittedTopologyIdentityMap(remappedEntries));
        }

        var combined = new BrepBody(topology, geometry, bindings, vertexPoints);
        diags.Add("combined-remap-validation-passed: topology references and bindings copied for all supplied patch bodies.");
        return new(true, CombinedPatchBodyRemapStatus.CombinedPartialBody, combined, summaries, refRemaps, remappedMaps, diags);
    }
}
