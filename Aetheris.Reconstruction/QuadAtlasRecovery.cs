using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Surfacing;

namespace Aetheris.Reconstruction;

public enum QuadAtlasSide { South, East, North, West }
public enum LayoutTraceTermination { Junction, SourceBoundary, UnmatchedTransition, Invalid }

public sealed record CrossFieldSingularity(
    string StableId, Point3D Location, IReadOnlyList<int> SourceVertices, double QuarterIndex,
    int ImpliedQuadValence, bool IsBoundary, double Confidence, string Evidence);

public sealed record QuadAtlasSeamUse(string SeamId, QuadAtlasSide Side, bool Reversed);

public sealed record QuadAtlasSeam(
    string StableId, int StartVertex, int EndVertex, Point3D Start, Point3D End,
    IReadOnlyList<string> ChartUses, bool IsSourceBoundary, double FieldDeviationDegrees,
    LayoutTraceTermination Termination, string Authority);

public sealed record QuadAtlasChart(
    string StableId, IReadOnlyList<int> SourceTriangles, IReadOnlyList<int> CornerVertices,
    IReadOnlyList<QuadAtlasSeamUse> OrderedSides, double Area, double AspectRatio,
    double CrossFieldAlignmentDegrees, double AngleDistortionDegrees, double AreaDistortion,
    int Foldovers, string Parameterization, PanelIr StrictPanel);

public sealed record QuadAtlasJudgmentCandidate(
    string Name, bool Admissible, double Utility, string Reason,
    double FieldAlignment, double ShapeQuality, double NormalCompatibility);

public sealed record QuadAtlasJudgmentTrace(
    string DecisionId, int CandidateCount, string Winner,
    IReadOnlyList<QuadAtlasJudgmentCandidate> TopCandidates,
    int HardRejectCount, IReadOnlyDictionary<string, int> HardRejectReasons);

public sealed record QuadAtlasTopologyAudit(
    int ChartCount, IReadOnlyDictionary<string, int> SideCountHistogram,
    IReadOnlyDictionary<string, int> DominantCauses, IReadOnlyList<string> WorstCharts);

public sealed record QuadAtlas(
    IReadOnlyList<QuadAtlasChart> Charts, IReadOnlyList<QuadAtlasSeam> Seams,
    IReadOnlyList<RecoveredJunction> Junctions, IReadOnlyList<CrossFieldSingularity> Singularities,
    IReadOnlyList<SourceBoundaryLoopCorrespondence> OpenBoundaryLoops,
    IReadOnlyList<int> UnresolvedTriangles, IReadOnlyList<QuadAtlasJudgmentTrace> JudgmentTraces,
    int UnintendedBoundaryLoops, string DeterministicHash)
{
    public bool IsGloballyValid => Charts.All(c => c.OrderedSides.Count == 4 && c.CornerVertices.Count == 4 && c.Foldovers == 0)
        && Seams.All(s => s.ChartUses.Count == (s.IsSourceBoundary ? 1 : 2)) && UnintendedBoundaryLoops == 0;
}

/// <summary>Canonical topology lowering from strict quad Panels, retaining typed triangle transitions only where matching is unresolved.</summary>
public static class QuadAtlasSurfaceMeshLowering
{
    public static CanonicalReconstructionMesh Lower(TriangleSurfaceMesh source, QuadAtlas atlas)
    {
        var vertices = source.Vertices.Select((p, i) => new SurfaceMeshVertex(i + 1, p)).ToArray();
        var patches = new List<SurfacePatch>(); var faceIds = new Dictionary<string, FaceId>(StringComparer.Ordinal); var nextFace = 1;
        foreach (var chart in atlas.Charts)
        {
            var points = chart.CornerVertices.Select(v => source.Vertices[v]).ToArray(); var normal = (points[1] - points[0]).Cross(points[3] - points[0]); if (!normal.TryNormalize(out normal)) normal = new(0, 0, 1);
            var x = points[1] - points[0]; if (!x.TryNormalize(out x)) x = new(1, 0, 0);
            var id = new FaceId(nextFace++); faceIds[chart.StableId] = id;
            patches.Add(new(id, new(SurfaceMeshSupportKind.Plane, new(points[0], Direction3D.Create(normal), Direction3D.Create(x))), [],
                [new QuadCell(chart.CornerVertices.Select(v => v + 1).ToArray())], true, chart.StrictPanel.StableId,
                ChartId: chart.StableId, PlanarPlannerPath: "QuadAtlas strict Panel boundary topology; SurfaceMeshIR plane support is a cell-carrier approximation for non-planar BoundaryPatch"));
        }
        foreach (var face in atlas.UnresolvedTriangles)
        {
            var t = source.Triangles[face]; var p = source.Vertices[t.A]; var n = (source.Vertices[t.B] - p).Cross(source.Vertices[t.C] - p); if (!n.TryNormalize(out n)) n = new(0, 0, 1); var x = source.Vertices[t.B] - p; if (!x.TryNormalize(out x)) x = new(1, 0, 0);
            patches.Add(new(new(nextFace++), new(SurfaceMeshSupportKind.Plane, new(p, Direction3D.Create(n), Direction3D.Create(x))), [],
                [new TriangleCell([t.A + 1, t.B + 1, t.C + 1]) { Provenance = SurfaceMeshCellProvenance.ResidualTransition, ExceptionalReason = "unmatched dual-graph transition; not represented as a strict Panel" }], true,
                "quad-atlas-transition", ChartId: $"transition-{face:D6}", PlanarPlannerPath: "explicit unresolved transition"));
        }
        var shared = atlas.Seams.Where(s => s.IsSourceBoundary || s.ChartUses.Count == 2).Select((s, i) =>
        {
            var uses = s.ChartUses.Select((chart, side) => new FaceBoundaryUse(faceIds[chart], new(faceIds[chart].Value), new(i * 2 + side + 1), false)).ToArray();
            return new SharedEdgeSamplePlan(new(i + 1), CurveGeometryKind.Line3, new(0, 1), [vertices[s.StartVertex], vertices[s.EndVertex]], uses, false, 0);
        }).ToArray();
        var topology = Topology(patches.SelectMany(p => p.Cells), vertices); var payload = atlas.DeterministicHash + ":" + string.Join('|', patches.SelectMany(p => p.Cells).Select(c => string.Join(',', c.VertexIds)));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var metrics = new SurfaceMeshMetrics(patches.Count, shared.Length, patches.Count, atlas.Charts.Count, atlas.UnresolvedTriangles.Count,
            atlas.UnresolvedTriangles.Count, 0, 0, 0, topology.Min, topology.Max, hash, 0, topology.NonManifold, atlas.UnresolvedTriangles.Count,
            atlas.Charts.Select(c => c.AspectRatio).DefaultIfEmpty(0).Max(), atlas.Charts.Select(c => c.AngleDistortionDegrees).DefaultIfEmpty(0).Max());
        var document = new SurfaceMeshDocument(vertices, patches, shared, metrics); var valid = SurfaceMeshIrValidator.TryValidate(document, out var failure);
        return new(document, 0, atlas.OpenBoundaryLoops.Count, topology.Boundary, topology.NonManifold, atlas.Charts.Count, atlas.UnresolvedTriangles.Count,
            valid ? "Pass" : "Fail: " + failure, hash);
    }

    private static (int Boundary, int NonManifold, double Min, double Max) Topology(IEnumerable<SurfaceMeshCell> cells, IReadOnlyList<SurfaceMeshVertex> vertices)
    {
        var points = vertices.ToDictionary(v => v.Id, v => v.Position); var edges = new Dictionary<(int, int), (int Count, double Length)>();
        foreach (var cell in cells) for (var i = 0; i < cell.VertexIds.Count; i++) { var a = cell.VertexIds[i]; var b = cell.VertexIds[(i + 1) % cell.VertexIds.Count]; var key = a < b ? (a, b) : (b, a); edges[key] = (edges.GetValueOrDefault(key).Count + 1, (points[a] - points[b]).Length); }
        return (edges.Count(e => e.Value.Count == 1), edges.Count(e => e.Value.Count > 2), edges.Values.Min(e => e.Length), edges.Values.Max(e => e.Length));
    }
}

/// <summary>
/// Recovers an explicit four-sided atlas by selecting a deterministic, field-scored matching
/// in the triangle dual graph. A matched pair is a disk with four source-supported boundary
/// edges. Faces which cannot participate remain explicit transition exceptions; they are never
/// relabeled as four-sided Panels.
/// </summary>
public static class QuadAtlasRecovery
{
    private sealed record PairCandidate(int FaceA, int FaceB, int[] Boundary, double Field, double Shape, double Normal)
    {
        public string Name => $"pair-{FaceA:D6}-{FaceB:D6}";
        public double Utility => .50 * Field + .35 * Shape + .15 * Normal;
    }

    public static QuadAtlasTopologyAudit Audit(ChartNetwork network)
    {
        var sides = network.Charts.ToDictionary(c => c.StableId, _ => 0, StringComparer.Ordinal);
        foreach (var seam in network.Seams)
        {
            sides[seam.ChartA]++;
            if (seam.ChartB is not null) sides[seam.ChartB]++;
        }
        string Bucket(int n) => n switch { 3 => "3-sided", 4 => "4-sided", 5 => "5-sided", _ when n >= 6 => "6+-sided", _ => "0-2-sided" };
        var histogram = sides.Values.GroupBy(Bucket).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var name in new[] { "0-2-sided", "3-sided", "4-sided", "5-sided", "6+-sided" }) histogram.TryAdd(name, 0);
        var causes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["segmentation boundary placement"] = sides.Count(p => p.Value != 4),
            ["chart merge history"] = sides.Count(p => p.Value >= 6),
            ["source open boundary"] = network.Seams.Where(s => s.Classification == RecoveredSeamClassification.SourceOpenBoundary).Select(s => s.ChartA).Distinct().Count()
        };
        return new(network.Charts.Count, histogram, causes,
            sides.OrderByDescending(p => Math.Abs(p.Value - 4)).ThenBy(p => p.Key, StringComparer.Ordinal).Take(20).Select(p => $"{p.Key}:{p.Value}").ToArray());
    }

    public static QuadAtlas Build(TriangleSurfaceMesh mesh, IReadOnlyList<DifferentialSample> field)
    {
        ArgumentNullException.ThrowIfNull(mesh); ArgumentNullException.ThrowIfNull(field);
        if (field.Count != mesh.Triangles.Count) throw new ArgumentException("Cross-field sample count must equal triangle count.", nameof(field));
        var edgeFaces = EdgeFaces(mesh);
        var candidates = BuildCandidates(mesh, field, edgeFaces);
        var byFace = candidates.SelectMany(c => new[] { (c.FaceA, c), (c.FaceB, c) }).GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.Select(x => x.c).OrderByDescending(c => c.Utility).ThenBy(c => c.Name, StringComparer.Ordinal).ToArray());
        var matched = new bool[mesh.Triangles.Count]; var selected = new List<PairCandidate>(); var traces = new List<QuadAtlasJudgmentTrace>();
        foreach (var face in Enumerable.Range(0, mesh.Triangles.Count).OrderBy(f => byFace.GetValueOrDefault(f)?.Length ?? int.MaxValue).ThenBy(f => f))
        {
            if (matched[face] || !byFace.TryGetValue(face, out var options)) continue;
            var available = options.Where(c => !matched[c.FaceA] && !matched[c.FaceB]).ToArray();
            if (available.Length == 0) continue;
            var judgmentCandidates = available.Select((c, i) => new JudgmentCandidate<PairCandidate>(c.Name, _ => true, _ => c.Utility, null, i)).ToArray();
            var result = new JudgmentEngine<PairCandidate>().Evaluate(available[0], judgmentCandidates);
            var winner = available.Single(c => c.Name == result.Selection!.Value.Candidate.Name);
            matched[winner.FaceA] = matched[winner.FaceB] = true; selected.Add(winner);
            if (available.Length > 1)
                traces.Add(new($"route-face-{face:D6}", available.Length, winner.Name,
                    available.OrderByDescending(c => c.Utility).ThenBy(c => c.Name, StringComparer.Ordinal).Take(3)
                        .Select(c => new QuadAtlasJudgmentCandidate(c.Name, true, c.Utility, "finite disk; four distinct corners; non-folded center", c.Field, c.Shape, c.Normal)).ToArray(), 0,
                    new Dictionary<string, int>()));
        }

        ImproveMatching(selected, matched, byFace);
        selected = selected.OrderBy(c => Math.Min(c.FaceA, c.FaceB)).ToList();
        var labels = Enumerable.Repeat<string?>(null, mesh.Triangles.Count).ToArray();
        for (var i = 0; i < selected.Count; i++) { labels[selected[i].FaceA] = labels[selected[i].FaceB] = $"quad-{i:D6}"; }
        var acceptedLabels = labels.Select((label, face) => label ?? $"transition-{face:D6}").ToArray();
        var recovered = RecoveredSeamNetworkBuilder.Build(mesh, acceptedLabels);
        var seams = new List<QuadAtlasSeam>(); var seamByEdge = new Dictionary<(int, int), QuadAtlasSeam>();
        foreach (var pair in edgeFaces.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2))
        {
            var uses = pair.Value.Select(f => labels[f]).Where(id => id is not null).Distinct().Cast<string>().Order(StringComparer.Ordinal).ToArray();
            if (uses.Length == 0) continue;
            var isSource = pair.Value.Count == 1;
            if (!isSource && uses.Length == 1 && pair.Value.All(f => labels[f] == uses[0])) continue; // matched-pair diagonal
            if (uses.Length > 2) continue;
            var a = pair.Key.Item1; var b = pair.Key.Item2; var pa = mesh.Vertices[a]; var pb = mesh.Vertices[b];
            var deviation = uses.Select(id => selected[int.Parse(id.AsSpan(5))]).SelectMany(c => new[] { c.FaceA, c.FaceB })
                .Distinct().Select(f => FieldDeviation(pb - pa, field[f])).DefaultIfEmpty(0).Average();
            var id = "atlas-seam-" + Hash($"{a}:{b}:{string.Join(',', uses)}")[..16];
            var seam = new QuadAtlasSeam(id, a, b, pa, pb, uses, isSource, deviation,
                isSource ? LayoutTraceTermination.SourceBoundary : uses.Length == 2 ? LayoutTraceTermination.Junction : LayoutTraceTermination.UnmatchedTransition,
                "single source edge evaluated once; adjacent Panel edges carry the same SourceCurveStableId");
            seams.Add(seam); seamByEdge[pair.Key] = seam;
        }

        var charts = new List<QuadAtlasChart>();
        foreach (var pair in selected)
        {
            var chartId = labels[pair.FaceA]!; var boundary = pair.Boundary;
            var chartSeams = Enumerable.Range(0, 4).Select(i => seamByEdge[Key(boundary[i], boundary[(i + 1) % 4])]).ToArray();
            var panelResult = MaterializePanel(chartId, boundary.Select(v => mesh.Vertices[v]).ToArray(), chartSeams);
            if (!panelResult.IsSuccess) throw new InvalidOperationException($"Strict quad Panel '{chartId}' failed: {string.Join("; ", panelResult.Diagnostics.Select(d => d.Message))}");
            var lengths = Enumerable.Range(0, 4).Select(i => (mesh.Vertices[boundary[(i + 1) % 4]] - mesh.Vertices[boundary[i]]).Length).ToArray();
            var area = TriangleArea(mesh, pair.FaceA) + TriangleArea(mesh, pair.FaceB);
            var angle = CornerAngleDistortion(boundary.Select(v => mesh.Vertices[v]).ToArray());
            charts.Add(new(chartId, [pair.FaceA, pair.FaceB], boundary,
                chartSeams.Select((s, i) => new QuadAtlasSeamUse(s.StableId, (QuadAtlasSide)i, s.StartVertex != boundary[i])).ToArray(),
                area, lengths.Max() / Math.Max(1e-15, lengths.Min()), 90 * (1 - pair.Field), angle, 1, 0,
                "four-corner transfinite (Coons/bilinear for line authority) map to [0,1] x [0,1]", panelResult.Panel!));
        }
        var singularities = DetectSingularities(mesh, field, edgeFaces);
        var junctionVertices = seams.SelectMany(s => new[] { s.StartVertex, s.EndVertex }).ToHashSet();
        var junctions = recovered.Junctions.Where(j => junctionVertices.Contains(j.SourceVertexIndex)).ToArray();
        var unresolved = Enumerable.Range(0, matched.Length).Where(i => !matched[i]).ToArray();
        var payload = string.Join('|', charts.Select(c => $"{c.StableId}:{string.Join(',', c.CornerVertices)}")) + ";" + string.Join('|', seams.Select(s => $"{s.StableId}:{s.StartVertex}-{s.EndVertex}"));
        return new(charts, seams.OrderBy(s => s.StableId, StringComparer.Ordinal).ToArray(), junctions, singularities,
            recovered.SourceBoundaryLoops, unresolved, traces.Take(500).ToArray(),
            seams.Count(s => !s.IsSourceBoundary && s.ChartUses.Count == 1), Hash(payload));
    }

    private static PanelResult MaterializePanel(string id, Point3D[] p, QuadAtlasSeam[] seams)
    {
        // BoundaryPatch expects South/North west-to-east and West/East south-to-north.
        var south = new RuledBoundary.Line(seams[0].StableId, p[0], p[1]);
        var east = new RuledBoundary.Line(seams[1].StableId, p[1], p[2]);
        var north = new RuledBoundary.Line(seams[2].StableId, p[3], p[2]);
        var west = new RuledBoundary.Line(seams[3].StableId, p[0], p[3]);
        var provenance = seams.Select(s => new BoundaryProvenance(s.StableId, "triangle-surface", "quad-atlas authoritative seam")).ToArray();
        return PanelFactory.FromBoundaryPatch(new(id, south, north, west, east, provenance));
    }

    private static List<PairCandidate> BuildCandidates(TriangleSurfaceMesh mesh, IReadOnlyList<DifferentialSample> field, Dictionary<(int, int), List<int>> edges)
    {
        var result = new List<PairCandidate>();
        foreach (var item in edges.Where(e => e.Value.Count == 2))
        {
            var a = item.Value[0]; var b = item.Value[1]; if (!TryBoundary(mesh.Triangles[a], mesh.Triangles[b], out var boundary)) continue;
            var points = boundary.Select(v => mesh.Vertices[v]).ToArray(); if (!NonFolded(points)) continue;
            var fieldScore = Enumerable.Range(0, 4).Select(i => 1 - FieldDeviation(mesh.Vertices[boundary[(i + 1) % 4]] - mesh.Vertices[boundary[i]], field[a]) / 45d).Average();
            var lengths = Enumerable.Range(0, 4).Select(i => (points[(i + 1) % 4] - points[i]).Length).ToArray();
            var shape = Math.Clamp(lengths.Min() / Math.Max(1e-15, lengths.Max()), 0, 1);
            var normal = Math.Clamp((field[a].Normal.Dot(field[b].Normal) + 1) / 2, 0, 1);
            result.Add(new(Math.Min(a, b), Math.Max(a, b), boundary, fieldScore, shape, normal));
        }
        return result;
    }

    // Deterministic bounded alternating-path search removes avoidable greedy transition faces.
    // It intentionally stops short of pretending to be a blossom-capable maximum matcher.
    private static void ImproveMatching(List<PairCandidate> selected, bool[] matched, Dictionary<int, PairCandidate[]> byFace)
    {
        var mate = new int[matched.Length]; Array.Fill(mate, -1);
        foreach (var p in selected) { mate[p.FaceA] = p.FaceB; mate[p.FaceB] = p.FaceA; }
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var start in Enumerable.Range(0, mate.Length).Where(i => mate[i] < 0))
            {
                var visited = new HashSet<int> { start }; var additions = new List<PairCandidate>(); var removals = new List<(int, int)>();
                if (!Search(start, 0)) continue;
                foreach (var remove in removals) selected.RemoveAll(p => Key(p.FaceA, p.FaceB) == remove);
                selected.AddRange(additions); Array.Fill(mate, -1); foreach (var p in selected) { mate[p.FaceA] = p.FaceB; mate[p.FaceB] = p.FaceA; }
                Array.Fill(matched, false); foreach (var p in selected) matched[p.FaceA] = matched[p.FaceB] = true;
                changed = true; break;

                bool Search(int current, int depth)
                {
                    foreach (var candidate in byFace.GetValueOrDefault(current) ?? [])
                    {
                        var next = candidate.FaceA == current ? candidate.FaceB : candidate.FaceA; if (!visited.Add(next)) continue;
                        additions.Add(candidate);
                        if (mate[next] < 0) return true;
                        var partner = mate[next];
                        if (depth < 15 && visited.Add(partner))
                        {
                            removals.Add(Key(next, partner));
                            if (Search(partner, depth + 1)) return true;
                            removals.RemoveAt(removals.Count - 1); visited.Remove(partner);
                        }
                        additions.RemoveAt(additions.Count - 1); visited.Remove(next);
                    }
                    return false;
                }
            }
        }
    }

    private static IReadOnlyList<CrossFieldSingularity> DetectSingularities(TriangleSurfaceMesh mesh, IReadOnlyList<DifferentialSample> field, Dictionary<(int, int), List<int>> edges)
    {
        var boundary = edges.Where(e => e.Value.Count == 1).SelectMany(e => new[] { e.Key.Item1, e.Key.Item2 }).ToHashSet();
        var incident = Enumerable.Range(0, mesh.Vertices.Count).Select(_ => new List<int>()).ToArray();
        for (var f = 0; f < mesh.Triangles.Count; f++) foreach (var v in Vertices(mesh.Triangles[f])) incident[v].Add(f);
        var candidates = new Dictionary<int, (double Index, double Confidence)>();
        for (var v = 0; v < incident.Length; v++)
        {
            var faces = incident[v].Where(f => field[f].DirectionKnown).ToArray(); if (faces.Length < 3) continue;
            var normal = faces.Select(f => field[f].Normal).Aggregate(Vector3D.Zero, (a, b) => a + b); if (!normal.TryNormalize(out normal)) continue;
            var first = FaceCentroid(mesh, faces.Min()) - mesh.Vertices[v]; first -= normal * first.Dot(normal); if (!first.TryNormalize(out first)) continue; var second = normal.Cross(first); second.TryNormalize(out second);
            double Around(int f) { var d=FaceCentroid(mesh,f)-mesh.Vertices[v];return Math.Atan2(d.Dot(second),d.Dot(first)); }
            double FieldAngle(int f) { var d=field[f].CrossDirection-normal*field[f].CrossDirection.Dot(normal);if(!d.TryNormalize(out d))return 0;return Math.Atan2(d.Dot(second),d.Dot(first)); }
            var ordered=faces.OrderBy(Around).ThenBy(f=>f).ToArray();var limit=boundary.Contains(v)?ordered.Length-1:ordered.Length;var winding=0d;
            for(var i=0;i<limit;i++){var delta=FieldAngle(ordered[(i+1)%ordered.Length])-FieldAngle(ordered[i]);while(delta>Math.PI/4)delta-=Math.PI/2;while(delta<=-Math.PI/4)delta+=Math.PI/2;winding+=delta;}
            var index=Math.Round((winding/(2*Math.PI))*4)/4d;if(Math.Abs(index)<.125)continue;
            candidates[v]=(index,Math.Min(1,faces.Length/(double)Math.Max(1,incident[v].Count))*(boundary.Contains(v)?.5:1));
        }
        // Consolidate adjacent noisy vertex candidates of compatible sign into one stable entity.
        var parent=candidates.Keys.ToDictionary(v=>v,v=>v);int Root(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        foreach(var edge in edges.Keys)if(candidates.TryGetValue(edge.Item1,out var a)&&candidates.TryGetValue(edge.Item2,out var b)&&Math.Sign(a.Index)==Math.Sign(b.Index)){var ra=Root(edge.Item1);var rb=Root(edge.Item2);if(ra!=rb)parent[Math.Max(ra,rb)]=Math.Min(ra,rb);}
        return candidates.Keys.GroupBy(Root).OrderBy(g=>g.Key).Select(g=>
        {
            var vertices=g.Order().ToArray();var index=Math.Round(vertices.Average(x=>candidates[x].Index)*4)/4d;var point=new Point3D(vertices.Average(x=>mesh.Vertices[x].X),vertices.Average(x=>mesh.Vertices[x].Y),vertices.Average(x=>mesh.Vertices[x].Z));var isBoundary=vertices.Any(boundary.Contains);
            return new CrossFieldSingularity("singularity-"+Hash($"{string.Join(',',vertices)}:{index:R}")[..16],point,vertices,index,4-(int)Math.Round(4*index),isBoundary,vertices.Average(x=>candidates[x].Confidence),
                "discrete quarter-turn winding over ordered incident-face loop; adjacent same-sign candidates consolidated to a representative quarter index rather than summing noisy samples; boundary confidence reduced because the fan is open");
        }).ToArray();
    }

    private static bool TryBoundary(Triangle a, Triangle b, out int[] boundary)
    {
        var directed = a.DirectedEdges().Concat(b.DirectedEdges()).ToArray();
        var outer = directed.Where(e => directed.Count(o => Key(e.A, e.B) == Key(o.A, o.B)) == 1).ToArray();
        if (outer.Length != 4) { boundary = []; return false; }
        var next = outer.ToDictionary(e => e.A, e => e.B); var start = outer.Min(e => e.A); var list = new List<int> { start }; var current = start;
        for (var i = 0; i < 3; i++) { if (!next.TryGetValue(current, out current) || list.Contains(current)) { boundary = []; return false; } list.Add(current); }
        if (!next.TryGetValue(current, out var close) || close != start) { boundary = []; return false; }
        boundary = list.ToArray(); return true;
    }

    private static bool NonFolded(Point3D[] p)
    {
        var n = (p[1] - p[0]).Cross(p[2] - p[0]) + (p[2] - p[0]).Cross(p[3] - p[0]);
        return n.Length > 1e-15 && Enumerable.Range(0, 4).All(i => (p[(i + 1) % 4] - p[i]).Cross(p[(i + 2) % 4] - p[(i + 1) % 4]).Dot(n) >= -1e-12);
    }
    private static double CornerAngleDistortion(Point3D[] p) => Enumerable.Range(0, 4).Select(i =>
    {
        var a = p[(i + 3) % 4] - p[i]; var b = p[(i + 1) % 4] - p[i];
        return a.TryNormalize(out a) && b.TryNormalize(out b) ? Math.Abs(90 - Math.Acos(Math.Clamp(a.Dot(b), -1, 1)) * 180 / Math.PI) : 90;
    }).Max();
    private static double FieldDeviation(Vector3D edge, DifferentialSample sample)
    {
        edge -= sample.Normal * edge.Dot(sample.Normal); if (!edge.TryNormalize(out edge) || !sample.DirectionKnown) return 45;
        var d = Math.Abs(edge.Dot(sample.CrossDirection)); var q = Math.Abs(edge.Dot(sample.Normal.Cross(sample.CrossDirection)));
        return Math.Acos(Math.Clamp(Math.Max(d, q), -1, 1)) * 180 / Math.PI;
    }
    private static double TriangleArea(TriangleSurfaceMesh mesh, int f) { var t = mesh.Triangles[f]; return .5 * (mesh.Vertices[t.B] - mesh.Vertices[t.A]).Cross(mesh.Vertices[t.C] - mesh.Vertices[t.A]).Length; }
    private static Point3D FaceCentroid(TriangleSurfaceMesh mesh,int f){var t=mesh.Triangles[f];var a=mesh.Vertices[t.A];var b=mesh.Vertices[t.B];var c=mesh.Vertices[t.C];return new((a.X+b.X+c.X)/3,(a.Y+b.Y+c.Y)/3,(a.Z+b.Z+c.Z)/3);}
    private static Dictionary<(int, int), List<int>> EdgeFaces(TriangleSurfaceMesh mesh)
    {
        var result = new Dictionary<(int, int), List<int>>(); for (var f = 0; f < mesh.Triangles.Count; f++) foreach (var e in mesh.Triangles[f].DirectedEdges()) { var key = Key(e.A, e.B); if (!result.TryGetValue(key, out var faces)) result[key] = faces = []; faces.Add(f); } return result;
    }
    private static int[] Vertices(Triangle t) => [t.A, t.B, t.C];
    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
