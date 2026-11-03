using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests.Experiments;

/// <summary>Runs E0 over authored, NIST and community parts. Set AETHERIS_EXPERIMENTS=1.
/// Output: artifacts/local/blend-probe/*.json (reports with display meshes).</summary>
public sealed class BlendRegionProbeRun
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }

    [Fact]
    public void E0_ClassifiesBlendRegions()
    {
        if (Environment.GetEnvironmentVariable("AETHERIS_EXPERIMENTS") != "1") return;
        var root = Root();
        var output = Directory.CreateDirectory(Path.Combine(root, "artifacts/local/blend-probe")).FullName;
        var summary = new List<object>();

        // Authored pairs: the finish removed from the source is the ground truth.
        var authored = new[]
        {
            "fixtures/Regression/CanonicalGeometry/profile-edgefinish-chimera-fillet.firmament",
            "fixtures/Regression/CanonicalGeometry/profile-edgefinish-chimera-chamfer.firmament",
            "fixtures/Regression/EdgeFinishRobustness/d-profile-fillet.firmament",
            "fixtures/Regression/EdgeFinishRobustness/d-profile-chamfer.firmament",
            "fixtures/Regression/EdgeFinishRobustness/polygon-rim-fillet.firmament",
            "fixtures/Regression/EdgeFinishRobustness/rounded-plate-through-cavities.firmament",
            "fixtures/Canonical/PMI/showcase-bearing-mount.firmament",
        };
        foreach (var relative in authored)
        {
            var source = File.ReadAllText(Path.Combine(root, relative));
            var stripped = Regex.Replace(source, @"\n\s*Modify Body \{[^{}]*EdgeFinish[^{}]*\{[^{}]*\}\s*\}", "\n");
            stripped = Regex.Replace(stripped, @"\n\s*EdgeFinish(<\w+>)?\s+\w+\s*\{[^{}]*\}", "\n");
            var finished = Build(relative, source) ?? throw new InvalidOperationException(relative + " did not build");
            var plain = Build(relative + "#plain", stripped);
            var name = Path.GetFileNameWithoutExtension(relative);
            var a = BlendRegionProbe.Run(name, finished.Body);
            var b = plain is null ? null : BlendRegionProbe.Run(name + "-plain", plain.Value.Body);
            Write(output, a);
            summary.Add(new
            {
                Case = name, Group = "authored", a.Faces, a.Secondaries, Regions = a.Regions.Length,
                PlainFaces = b?.Faces, PlainSecondaries = b?.Secondaries,
                FinishFaceDelta = b is null ? (int?)null : a.Faces - b.Faces,
                SecondaryDelta = b is null ? (int?)null : a.Secondaries - b.Secondaries,
                Shapes = Shapes(a), Blocked = a.Regions.Count(r => r.Blockers.Length > 0),
            });
        }

        foreach (var path in Directory.GetFiles(Path.Combine(root, "testdata/step242/nist"), "*.stp", SearchOption.AllDirectories).Order()
                     .Concat(Directory.GetFiles(Path.Combine(root, "testdata/step242/community"), "*.step", SearchOption.AllDirectories)))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var import = Step242Importer.ImportBody(File.ReadAllText(path));
            if (!import.IsSuccess || import.Value is null) { summary.Add(new { Case = name, Group = "import", Error = import.Diagnostics.FirstOrDefault()?.Message }); continue; }
            try
            {
                var report = BlendRegionProbe.Run(name, import.Value);
                Write(output, report);
                summary.Add(new { Case = name, Group = path.Contains("nist") ? "nist" : "community", report.Faces, report.Secondaries,
                    Regions = report.Regions.Length, Shapes = Shapes(report), Blocked = report.Regions.Count(r => r.Blockers.Length > 0) });
            }
            catch (Exception error) { summary.Add(new { Case = name, Group = "probe", Error = error.Message }); }
        }
        File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static (BrepBody Body, string Step)? Build(string name, string source)
    {
        var result = FirmamentBuildAndExport.CompileSource(source);
        if (!result.IsSuccess) return null;
        var body = Step242Importer.ImportBody(result.Value.StepText);
        return body.IsSuccess && body.Value is not null ? (body.Value, result.Value.StepText) : null;
    }

    private static Dictionary<string, int> Shapes(BlendRegionProbe.Report report) =>
        report.Regions.GroupBy(r => r.Shape).ToDictionary(g => g.Key, g => g.Count());

    private static void Write(string output, BlendRegionProbe.Report report)
    {
        var json = new
        {
            report.Name, report.Faces, report.Secondaries, report.FaceReports, report.Regions,
            Mesh = report.Mesh.ToDictionary(p => p.Key.ToString(), p => new { positions = p.Value.Positions, indices = p.Value.Indices }),
        };
        File.WriteAllText(Path.Combine(output, report.Name + ".json"), JsonSerializer.Serialize(json, new JsonSerializerOptions { IncludeFields = true }));
    }
}
