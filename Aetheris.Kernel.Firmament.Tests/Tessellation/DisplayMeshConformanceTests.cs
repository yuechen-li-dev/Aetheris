using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Xunit.Abstractions;

namespace Aetheris.Kernel.Firmament.Tests.Tessellation;

[Trait("Suite", "Language")]
public sealed class DisplayMeshConformanceTests(ITestOutputHelper output)
{
    private static string Repo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent;
        return d!.FullName;
    }

    /// <summary>Triangle edges that only one triangle uses after welding coincident vertices: the cracks.</summary>
    internal static int OpenEdges(DisplayTessellationResult result, double weld = 1e-6) => OpenEdgeFaces(result, weld).Count;

    /// <summary>The face of each open triangle edge.</summary>
    internal static List<int> OpenEdgeFaces(DisplayTessellationResult result, double weld = 1e-6)
    {
        var ids = new Dictionary<(long, long, long), int>();
        int Id(Point3D p) { var k = ((long)double.Round(p.X / weld), (long)double.Round(p.Y / weld), (long)double.Round(p.Z / weld)); if (!ids.TryGetValue(k, out var id)) ids[k] = id = ids.Count; return id; }
        var uses = new Dictionary<(int, int), int>();
        var owner = new Dictionary<(int, int), int>();
        foreach (var patch in result.FacePatches)
        {
            var map = patch.Positions.Select(Id).ToArray();
            for (var t = 0; t < patch.TriangleIndices.Count; t += 3)
                for (var e = 0; e < 3; e++)
                {
                    var (a, b) = (map[patch.TriangleIndices[t + e]], map[patch.TriangleIndices[t + (e + 1) % 3]]);
                    if (a == b) continue;
                    var key = a < b ? (a, b) : (b, a);
                    uses[key] = uses.GetValueOrDefault(key) + 1; owner[key] = patch.FaceId.Value;
                }
        }
        return uses.Where(u => u.Value == 1).Select(u => owner[u.Key]).ToList();
    }

    /// <summary>Visible daylight: open triangle edges whose midpoint is farther than <paramref name="gap"/> from every other
    /// face's boundary. Near-coincident vertices from different faces are not daylight.</summary>
    internal static (int Count, double Max) Gaps(DisplayTessellationResult result, double gap = 1e-3)
    {
        var segments = new List<(Point3D A, Point3D B, int Face)>();
        foreach (var patch in result.FacePatches)
        {
            var used = new Dictionary<(int, int), int>();
            var idx = patch.TriangleIndices;
            for (var t = 0; t < idx.Count; t += 3) for (var k = 0; k < 3; k++) { var (a, b) = (idx[t + k], idx[t + (k + 1) % 3]); var key = a < b ? (a, b) : (b, a); used[key] = used.GetValueOrDefault(key) + 1; }
            segments.AddRange(used.Where(u => u.Value == 1).Select(u => (patch.Positions[u.Key.Item1], patch.Positions[u.Key.Item2], patch.FaceId.Value)));
        }
        const double cell = 2d;
        var grid = new Dictionary<(int, int, int), List<int>>();
        (int, int, int) C(Point3D p) => ((int)double.Floor(p.X / cell), (int)double.Floor(p.Y / cell), (int)double.Floor(p.Z / cell));
        for (var i = 0; i < segments.Count; i++)
        {
            var (a, b) = (C(segments[i].A), C(segments[i].B));
            for (var x = int.Min(a.Item1, b.Item1); x <= int.Max(a.Item1, b.Item1); x++)
                for (var y = int.Min(a.Item2, b.Item2); y <= int.Max(a.Item2, b.Item2); y++)
                    for (var z = int.Min(a.Item3, b.Item3); z <= int.Max(a.Item3, b.Item3); z++)
                    { if (!grid.TryGetValue((x, y, z), out var l)) grid[(x, y, z)] = l = []; l.Add(i); }
        }
        static double Dist(Point3D p, Point3D a, Point3D b) { var ab = b - a; var t = double.Clamp((p - a).Dot(ab) / double.Max(1e-300, ab.Dot(ab)), 0, 1); return (p - (a + ab * t)).Length; }
        var count = 0; var max = 0d;
        for (var i = 0; i < segments.Count; i++)
        {
            var (a, b, face) = segments[i];
            var m = new Point3D((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
            var c = C(m); var best = double.MaxValue;
            for (var x = -1; x <= 1; x++) for (var y = -1; y <= 1; y++) for (var z = -1; z <= 1; z++)
                if (grid.TryGetValue((c.Item1 + x, c.Item2 + y, c.Item3 + z), out var l))
                    foreach (var j in l) if (j != i) best = double.Min(best, Dist(m, segments[j].A, segments[j].B));
            if (best > gap) { count++; if (best < 1e9) max = double.Max(max, best); }
        }
        return (count, max);
    }

    public static TheoryData<string> Parts => new()
    {
        "fixtures/Canonical/Features/Holes/countersink.firmament", "fixtures/Canonical/Learn/03-machined-block.firmament",
        // Planar caps bounded by B-spline and elliptical edges: the plane samples each edge as its drawn polyline does.
        "fixtures/Canonical/ThreeDm/lamp-shade-loft.firmament", "fixtures/Canonical/Boundary2/closed-ellipse-extrusion.firmament",
    };

    [Theory]
    [MemberData(nameof(Parts))]
    public void ViewportMeshIsClosedAcrossCurvedEdges(string fixture)
    {
        var path = Path.Combine(Repo(), fixture);
        var built = FirmamentBuildAndExport.CompileSource(File.ReadAllText(path), Path.GetDirectoryName(path)!);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        var body = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(built.Value.StepText).Value;
        var mesh = BrepDisplayTessellator.Tessellate(body, DisplayTessellationOptions.ForViewport(body));
        Assert.True(mesh.IsSuccess);
        foreach (var g in OpenEdgeFaces(mesh.Value).GroupBy(f => f)) output.WriteLine($"  face {g.Key} {body.GetFaceSurface(new Aetheris.Kernel.Core.Topology.FaceId(g.Key)).Kind}: {g.Count()}");
        Assert.Equal(0, OpenEdges(mesh.Value));
        // Holes and countersinks are single rows of quads between their rims: every vertex is on a rim edge.
        foreach (var patch in mesh.Value.FacePatches.Where(p => body.GetFaceSurface(p.FaceId).Kind is Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder or Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone))
            // n columns on two rims: 2n triangles around a whole wall, 2(n - 1) across a partial one.
            Assert.Contains(patch.TriangleIndices.Count / 3, new[] { patch.Positions.Count, patch.Positions.Count - 2 });
    }

    /// <summary>Visible daylight between faces of NIST models. Curved faces are meshed from their edges' own samples,
    /// drawn edges end exactly on their vertices, and boundary vertices a face re-evaluated onto its surface are moved
    /// back onto the edge's samples, so neighbours meet vertex for vertex. What remains: stc_08's three on a circle,
    /// stc_07's on lines, and ftc_07's, where a helical spline crosses a cylinder's seam in its chart.</summary>
    [Trait("Category", "Slow")]
    [Theory]
    [InlineData("CTC/nist_ctc_02_asme1_ap242-e2.stp", 0)]
    [InlineData("CTC/nist_ctc_03_asme1_ap242-e2.stp", 0)]
    [InlineData("CTC/nist_ctc_04_asme1_ap242-e1.stp", 0)]
    [InlineData("FTC/nist_ftc_07_asme1_ap242-e2.stp", 42)]
    [InlineData("FTC/nist_ftc_08_asme1_ap242-e2.stp", 0)]
    [InlineData("FTC/nist_ftc_10_asme1_ap242-e2.stp", 0)]
    [InlineData("STC/nist_stc_07_asme1_ap242-e3.stp", 3)]
    [InlineData("STC/nist_stc_08_asme1_ap242-e3.stp", 3)]
    [InlineData("STC/nist_stc_09_asme1_ap242-e3.stp", 0)]
    [InlineData("STC/nist_stc_10_asme1_ap242-e2.stp", 0)]
    public void NistModelsShowNoDaylightBetweenFaces(string model, int ceiling)
    {
        var body = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(File.ReadAllText(Path.Combine(Repo(), "testdata/step242/nist", model))).Value;
        var mesh = BrepDisplayTessellator.TessellateBoundedPartial(body, DisplayTessellationOptions.ForViewport(body), TimeSpan.FromSeconds(60));
        var (count, max) = Gaps(mesh);
        output.WriteLine($"{model}: {count} open segments, widest {max:G3} mm");
        Assert.InRange(count, 0, ceiling);
    }

    /// <summary>Refinement stops at the tolerances: a fillet-heavy NIST model stays near 100k viewport triangles
    /// (measured 108k; the general paths it replaced produced 393k).</summary>

    /// <summary>Watertight: every triangle edge used twice once positions weld at a micron, ellipse and spline edges
    /// included. Imported vertices a few microns off their edges' ends, and spline and sphere faces re-evaluating
    /// their boundary from a chart, used to leave hundreds of hairline cracks here.</summary>
    [Trait("Category", "Slow")]
    [Theory]
    [InlineData("CTC/nist_ctc_01_asme1_ap242-e1.stp")]
    [InlineData("CTC/nist_ctc_02_asme1_ap242-e2.stp")]
    [InlineData("CTC/nist_ctc_03_asme1_ap242-e2.stp")]
    [InlineData("CTC/nist_ctc_04_asme1_ap242-e1.stp")]
    [InlineData("CTC/nist_ctc_05_asme1_ap242-e1.stp")]
    [InlineData("FTC/nist_ftc_08_asme1_ap242-e2.stp")]
    [InlineData("FTC/nist_ftc_10_asme1_ap242-e2.stp")]
    [InlineData("STC/nist_stc_09_asme1_ap242-e3.stp")]
    [InlineData("STC/nist_stc_10_asme1_ap242-e2.stp")]
    public void NistModelsAreWatertight(string model)
    {
        var body = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(File.ReadAllText(Path.Combine(Repo(), "testdata/step242/nist", model))).Value;
        var mesh = BrepDisplayTessellator.TessellateBoundedPartial(body, DisplayTessellationOptions.ForViewport(body), TimeSpan.FromSeconds(60));
        Assert.Equal(0, OpenEdges(mesh));
    }
    [Fact]
    public void CurvedFacesRefineOnlyToTheTolerances()
    {
        var body = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(File.ReadAllText(Path.Combine(Repo(), "testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp"))).Value;
        var mesh = BrepDisplayTessellator.TessellateBoundedPartial(body, DisplayTessellationOptions.ForViewport(body), TimeSpan.FromSeconds(60));
        var triangles = mesh.FacePatches.Sum(p => p.TriangleIndices.Count / 3);
        output.WriteLine($"{triangles} triangles");
        Assert.InRange(triangles, 20_000, 150_000);
    }
}
