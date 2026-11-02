using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Tests.Experiments;

/// <summary>E2 census: what the non-planar refusals of E3 are, geometrically. Gated, writes e2-census.txt.</summary>
public sealed class BlendE2Census
{
    [Fact]
    public void E2_Census()
    {
        if (Environment.GetEnvironmentVariable("AETHERIS_EXPERIMENTS") != "1") return;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        var root = directory!.FullName;
        var pairs = new Dictionary<string, int>();      // strip faces by anchor relation
        var regionWorst = new Dictionary<string, int>(); // region faces by the set of relations they need
        var moved = new Dictionary<string, int>();      // kept edges that would move, by curve kind
        var lines = new List<string>();
        foreach (var path in Directory.GetFiles(Path.Combine(root, "testdata/step242/nist"), "*.stp", SearchOption.AllDirectories).Order()
                     .Concat(Directory.GetFiles(Path.Combine(root, "testdata/step242/community"), "*.step", SearchOption.AllDirectories)))
        {
            if (path.Contains("-tg")) continue;
            var import = Step242Importer.ImportBody(File.ReadAllText(path));
            if (!import.IsSuccess) continue;
            var body = import.Value;
            var probe = BlendRegionProbe.Run(Path.GetFileName(path), body);
            var anchors = BlendNetworkRemovalTests.Anchors(probe);
            foreach (var region in probe.Regions)
            {
                if (region.FaceBlockers.Any(b => b is not null)) continue;
                var faces = region.Faces.Select(f => new FaceId(f)).ToHashSet();
                var removed = BlendNetworkRemoval.Remove(body, faces, anchors);
                if (removed.Body is not null || removed.Refusal?.StartsWith("blend-removal-primary-not-planar") != true) continue;
                var needs = new SortedSet<string>();
                foreach (var f in faces)
                {
                    var a = anchors.GetValueOrDefault(f) ?? [];
                    var key = a.Length == 2 ? $"{Kind(body, f)} between {Relation(body, a[0], a[1])}" : $"joiner {Kind(body, f)} ({a.Length} anchors)";
                    pairs[key] = pairs.GetValueOrDefault(key) + 1;
                    needs.Add(a.Length == 2 ? Relation(body, a[0], a[1]) : "joiner");
                }
                // Joiner corners among curved faces: which surfaces meet at each joiner's neighbourhood.
                foreach (var f in faces.Where(f => (anchors.GetValueOrDefault(f) ?? []).Length != 2))
                {
                    var around = Neighbours(body, f).Where(n => !faces.Contains(n)).Select(n => Kind(body, n)).Order().ToArray();
                    needs.Add("corner[" + string.Join('+', around.Distinct()) + "]");
                }
                foreach (var kind in MovedEdgeKinds(body, faces)) { moved[kind] = moved.GetValueOrDefault(kind) + 1; needs.Add("moves " + kind); }
                var signature = string.Join(" | ", needs);
                regionWorst[signature] = regionWorst.GetValueOrDefault(signature) + faces.Count;
                lines.Add($"   {Path.GetFileNameWithoutExtension(path)} R{region.Id} ({faces.Count}): {signature}");
            }
        }
        lines.Insert(0, "STRIP FACES BY ANCHOR RELATION");
        lines.InsertRange(1, pairs.OrderByDescending(p => p.Value).Select(p => $"{p.Value,5}  {p.Key}"));
        var at = pairs.Count + 1;
        lines.Insert(at++, "KEPT EDGES THAT MOVE, BY CURVE");
        lines.InsertRange(at, moved.OrderByDescending(p => p.Value).Select(p => $"{p.Value,5}  {p.Key}"));
        at += moved.Count;
        lines.Insert(at++, "REGION FACES BY WHAT THEY NEED");
        lines.InsertRange(at, regionWorst.OrderByDescending(p => p.Value).Select(p => $"{p.Value,5}  {p.Key}"));
        File.WriteAllLines(Path.Combine(root, "artifacts/local/blend-probe/e2-census.txt"), lines);
    }

    private static string Kind(BrepBody body, FaceId f) => body.TryGetFaceSurfaceGeometry(f, out var s) ? s!.Kind.ToString() : "?";

    internal static string Relation(BrepBody body, FaceId a, FaceId b)
    {
        body.TryGetFaceSurfaceGeometry(a, out var sa); body.TryGetFaceSurfaceGeometry(b, out var sb);
        if (Rank(sa!) > Rank(sb!)) (sa, sb) = (sb, sa);
        static (Point3D P, Vector3D D)? Axis(SurfaceGeometry s) => s.Cylinder is { } c ? (c.Origin, c.Axis.ToVector())
            : s.Cone is { } k ? (k.Apex, k.Axis.ToVector()) : s.Torus is { } t ? (t.Center, t.Axis.ToVector())
            : s.Sphere is { } p ? (p.Center, Vector3D.Zero) : null;
        var name = $"{sa!.Kind}-{sb!.Kind}";
        if (sa.Plane is { } plane && Axis(sb) is { } ax)
        {
            if (sb.Sphere is not null) return name;
            var c = Math.Abs(plane.Normal.ToVector().Dot(ax.D));
            return name + (c > 1 - 1e-9 ? " (plane across axis: circle)" : c < 1e-9 ? " (plane along axis: lines)" : " (oblique)");
        }
        if (Axis(sa) is { } x && Axis(sb) is { } y)
        {
            if (x.D.Length < 1e-12 || y.D.Length < 1e-12) return name;
            var parallel = Math.Abs(Math.Abs(x.D.Dot(y.D)) - 1) < 1e-9;
            var offset = (y.P - x.P) - x.D * (y.P - x.P).Dot(x.D);
            return name + (parallel && offset.Length < 1e-6 ? " (coaxial: circle)" : parallel ? " (parallel axes)" : " (skew axes)");
        }
        return name;
    }

    private static int Rank(SurfaceGeometry s) => s.Kind switch { SurfaceGeometryKind.Plane => 0, SurfaceGeometryKind.Cylinder => 1, SurfaceGeometryKind.Cone => 2, _ => 3 };

    private static IEnumerable<FaceId> Neighbours(BrepBody body, FaceId f)
    {
        var t = body.Topology;
        var edges = t.GetFace(f).LoopIds.SelectMany(l => t.GetLoop(l).CoedgeIds).Select(c => t.GetCoedge(c).EdgeId).ToHashSet();
        return t.Faces.Where(o => o.Id != f && o.LoopIds.SelectMany(l => t.GetLoop(l).CoedgeIds).Any(c => edges.Contains(t.GetCoedge(c).EdgeId))).Select(o => o.Id);
    }

    private static IEnumerable<string> MovedEdgeKinds(BrepBody body, HashSet<FaceId> faces)
    {
        var t = body.Topology;
        var touched = faces.SelectMany(f => t.GetFace(f).LoopIds.SelectMany(l => t.GetLoop(l).CoedgeIds)).Select(c => t.GetCoedge(c).EdgeId).ToHashSet();
        var vertices = touched.Select(t.GetEdge).SelectMany(e => new[] { e.StartVertexId, e.EndVertexId }).ToHashSet();
        foreach (var e in t.Edges.Where(e => !touched.Contains(e.Id) && (vertices.Contains(e.StartVertexId) || vertices.Contains(e.EndVertexId))))
            yield return body.Geometry.GetCurve(body.Bindings.GetEdgeBinding(e.Id).CurveGeometryId).Kind.ToString();
    }
}
