using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Xunit.Abstractions;

namespace Aetheris.Kernel.Firmament.Tests.Experiments;

/// <summary>E1: author a part with a finish, remove the probe's strips, compare with the part
/// authored without the finish.</summary>
public sealed class BlendNetworkRemovalTests(ITestOutputHelper output)
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }

    private static string Rect(string finish) => $$"""
        Model R {
            units: mm
            Path Outline {
                start: Point2(0mm, 0mm)
                heading: 0deg
                Line South { length: 40mm }
                Line East { turn: 90deg; length: 25mm }
                Line North { turn: 90deg; length: 40mm }
                Close West
            }
            Profile Section from Outline
            Struct Body { Extrude Stock { profile: Section; from: 0mm; to: 10mm } }
            {{finish}}
        }
        """;

    public static TheoryData<string, string> Admitted => new()
    {
        { "rect-top-fillet", Rect("Modify Body { EdgeFinish Rim { target: Section.Outer; on: Top; kind: Fillet; radius: 2mm } }") },
        { "rect-top-chamfer", Rect("Modify Body { EdgeFinish Rim { target: Section.Outer; on: Top; kind: Chamfer; distance: 2mm } }") },
        { "rect-bottom-fillet", Rect("Modify Body { EdgeFinish Rim { target: Section.Outer; on: Bottom; kind: Fillet; radius: 3mm } }") },
        { "polygon-rim-fillet", "fixtures/Regression/EdgeFinishRobustness/polygon-rim-fillet.firmament" },
    };

    [Theory]
    [MemberData(nameof(Admitted))]
    public void RemovingTheStrips_ReturnsThePartWithoutTheFinish(string name, string sourceOrPath)
    {
        var source = sourceOrPath.StartsWith("fixtures/", StringComparison.Ordinal) ? File.ReadAllText(Path.Combine(Root(), sourceOrPath)) : sourceOrPath;
        var finished = Import(source);
        var stripped = Regex.Replace(source, @"Modify Body \{\s*EdgeFinish[^{}]*\{[^{}]*\}\s*\}", "");
        Assert.DoesNotContain("EdgeFinish", stripped);
        var plain = Import(Regex.Replace(source, @"Modify Body \{\s*EdgeFinish[^{}]*\{[^{}]*\}\s*\}", ""));

        var probe = BlendRegionProbe.Run(name, finished);
        var strips = probe.Regions.SelectMany(r => r.Faces).Select(f => new FaceId(f)).ToHashSet();
        Assert.NotEmpty(strips);
        var removed = BlendNetworkRemoval.Remove(finished, strips, Anchors(probe));
        Assert.True(removed.Body is not null, removed.Refusal);
        output.WriteLine($"{name}: removed {strips.Count} faces, {removed.NewEdges} sharp edges, {removed.Clusters} corners");

        var body = removed.Body!;
        Assert.True(TopologyGraphValidator.Validate(body.Topology).IsSuccess);
        Assert.True(BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true).IsSuccess);
        Assert.Equal(plain.Topology.Faces.Count(), body.Topology.Faces.Count());
        Assert.Equal(plain.Topology.Edges.Count(), body.Topology.Edges.Count());
        Assert.Equal(plain.Topology.Vertices.Count(), body.Topology.Vertices.Count());
        Assert.Equal(Points(plain), Points(body));

        Assert.True(BrepMassProperties.TryEvaluateExactVolume(plain, out var expected));
        Assert.True(BrepMassProperties.TryEvaluateExactVolume(body, out var actual), actual?.Status.ToString());
        Assert.Equal(expected.AbsoluteVolume, actual.AbsoluteVolume, 6);

        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess, string.Join("\n", step.Diagnostics.Select(d => d.Message)));
        var reimported = Step242Importer.ImportBody(step.Value);
        Assert.True(reimported.IsSuccess, string.Join("\n", reimported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(Points(plain), Points(reimported.Value));
    }

    [Theory]
    [InlineData("fixtures/Regression/EdgeFinishRobustness/d-profile-fillet.firmament", "blend-removal-primary-not-planar")]
    [InlineData("fixtures/Regression/CanonicalGeometry/profile-edgefinish-chimera-fillet.firmament", "blend-removal-")]
    public void CurvedNeighbours_Refuse_WithAReason(string path, string code)
    {
        var finished = Import(File.ReadAllText(Path.Combine(Root(), path)));
        var probe = BlendRegionProbe.Run(path, finished);
        var removed = BlendNetworkRemoval.Remove(finished, probe.Regions.SelectMany(r => r.Faces).Select(f => new FaceId(f)).ToHashSet(), Anchors(probe));
        Assert.Null(removed.Body);
        Assert.StartsWith(code, removed.Refusal);
        output.WriteLine(removed.Refusal);
    }

    internal static Dictionary<FaceId, FaceId[]> Anchors(BlendRegionProbe.Report probe) => probe.Regions
        .SelectMany(r => r.Faces.Zip(r.FaceAnchors)).ToDictionary(p => new FaceId(p.First), p => p.Second.Select(a => new FaceId(a)).ToArray());

    private static BrepBody Import(string source)
    {
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var body = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(body.IsSuccess);
        return body.Value;
    }

    private static string[] Points(BrepBody body) => body.Topology.Vertices
        .Select(v => { body.TryGetVertexPoint(v.Id, out var p); return $"{Math.Round(p.X, 6) + 0.0:F6},{Math.Round(p.Y, 6) + 0.0:F6},{Math.Round(p.Z, 6) + 0.0:F6}"; })
        .Order(StringComparer.Ordinal).ToArray();
}

/// <summary>E1 reach over imported parts, one region at a time. AETHERIS_EXPERIMENTS=1.</summary>
public sealed class BlendNetworkRemovalReach
{
    [Fact]
    public void E1_Reach()
    {
        if (Environment.GetEnvironmentVariable("AETHERIS_EXPERIMENTS") != "1") return;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        var root = directory!.FullName;
        var lines = new List<string>();
        var total = new Dictionary<string, int>();
        var wroteSample = new HashSet<string>();
        foreach (var path in Directory.GetFiles(Path.Combine(root, "testdata/step242/nist"), "*.stp", SearchOption.AllDirectories).Order()
                     .Concat(Directory.GetFiles(Path.Combine(root, "testdata/step242/community"), "*.step", SearchOption.AllDirectories)))
        {
            var import = Step242Importer.ImportBody(File.ReadAllText(path));
            if (!import.IsSuccess || path.Contains("-tg")) continue;
            var body = import.Value;
            BrepMassProperties.TryEvaluateExactVolume(body, out var before);
            var probe = BlendRegionProbe.Run(Path.GetFileName(path), body);
            var outcome = new Dictionary<string, int>();
            foreach (var region in probe.Regions)
            {
                string verdict;
                if (region.FaceBlockers.Any(b => b is not null)) verdict = "blocked:" + region.FaceBlockers.First(b => b is not null)!.Split(':')[0];
                else if (region.FaceBridges.Any(b => b != 2) && region.Shape != "chamfer") verdict = "joiner(E3)";
                else
                {
                    var removed = BlendNetworkRemoval.Remove(body, region.Faces.Select(f => new FaceId(f)).ToHashSet(), BlendNetworkRemovalTests.Anchors(probe));
                    if (removed.Body is null) { verdict = "refused:" + removed.Refusal!.Split(':')[0]; lines.Add($"   {Path.GetFileNameWithoutExtension(path)} R{region.Id}: {removed.Refusal}"); }
                    else
                    {
                        var valid = TopologyGraphValidator.Validate(removed.Body.Topology).IsSuccess
                                    && BrepBindingValidator.Validate(removed.Body, requireAllEdgeAndFaceBindings: true).IsSuccess;
                        var step = valid ? Step242Exporter.ExportBody(removed.Body) : null;
                        var back = step is { IsSuccess: true } ? Step242Importer.ImportBody(step.Value) : null;
                        // Exact volume where available; otherwise the tessellated evaluation for both bodies.
                        var grew = BrepMassProperties.TryEvaluateExactVolume(removed.Body, out var after) && before is not null
                                   ? after.AbsoluteVolume - before.AbsoluteVolume
                                   : BrepMassProperties.Evaluate(removed.Body).AbsoluteVolume - BrepMassProperties.Evaluate(body).AbsoluteVolume;
                        // A convex blend's removal adds material and a concave one's removes it; either way the
                        // change is bounded by the blend's area times the largest admitted radius.
                        var stripArea = region.Faces.Sum(f => probe.FaceReports.Single(r => r.Face == f).Area);
                        verdict = !valid ? "removed-invalid" : back is not { IsSuccess: true } ? "removed-reimport-failed"
                            : (Math.Abs(grew) > 1e-9 && Math.Abs(grew) <= stripArea * 5 && !wroteSample.Contains(path) && wroteSample.Add(path)
                               && WriteSample(root, Path.GetFileNameWithoutExtension(path), region.Id, body, removed.Body)) ? "unreachable"
                            : !(Math.Abs(grew) > 1e-9 && Math.Abs(grew) <= stripArea * 5) ? $"removed-volume-implausible(d={grew:G4},bound={stripArea * 5:G4},faces={string.Join(' ', region.Faces)})" : grew > 0 ? "removed-ok(convex)" : "removed-ok(concave)";
                    }
                }
                outcome[verdict] = outcome.GetValueOrDefault(verdict) + region.Faces.Length;
                total[verdict] = total.GetValueOrDefault(verdict) + region.Faces.Length;
            }
            lines.Add($"{Path.GetFileNameWithoutExtension(path),-32} " + string.Join(", ", outcome.OrderByDescending(p => p.Value).Select(p => $"{p.Key}={p.Value}")));
        }
        lines.Add("TOTAL (faces) " + string.Join(", ", total.OrderByDescending(p => p.Value).Select(p => $"{p.Key}={p.Value}")));
        Directory.CreateDirectory(Path.Combine(root, "artifacts/local/blend-probe"));
        File.WriteAllLines(Path.Combine(root, "artifacts/local/blend-probe/e1-reach.txt"), lines);
    }

    private static bool WriteSample(string root, string name, int region, BrepBody before, BrepBody after)
    {
        // Before/after probe reports (with meshes) for review; never changes the verdict.
        foreach (var (tag, b) in new[] { ("before", before), ("after", after) })
        {
            var report = BlendRegionProbe.Run($"e1-{name}-R{region}-{tag}", b);
            File.WriteAllText(Path.Combine(root, "artifacts/local/blend-probe", report.Name + ".json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                report.Name, report.Faces, report.Secondaries, report.FaceReports, report.Regions,
                Mesh = report.Mesh.ToDictionary(p => p.Key.ToString(), p => new { positions = p.Value.Positions, indices = p.Value.Indices }),
            }));
        }
        return false;
    }
}
