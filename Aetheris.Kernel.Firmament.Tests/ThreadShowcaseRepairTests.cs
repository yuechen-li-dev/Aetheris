using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ThreadShowcaseRepairTests
{
    [Fact]
    public void RecordCompactnessBenchmarkWhenRequested()
    {
        if (Environment.GetEnvironmentVariable("AETHERIS_STEP_COMPACTNESS_DIR") is not { Length: > 0 } output) return;
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        output = Path.GetFullPath(output, root.FullName);
        Directory.CreateDirectory(output);
        var source = File.ReadAllText(Path.Combine(root.FullName, "fixtures", "Thread", "hexbolt-showcase.firmament"));
        foreach (var threaded in new[] { true, false })
        {
            var ordinary = source[..source.IndexOf("    Thread MainThread", StringComparison.Ordinal)] + "}\n";
            ordinary = System.Text.RegularExpressions.Regex.Replace(ordinary, @"^.*MakerMark.*\r?\n", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            var compiled = FirmamentBuildAndExport.CompileSource(threaded ? source : ordinary);
            Assert.True(compiled.IsSuccess, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
            var body = compiled.Value.RuntimeBody!;
            var options = threaded ? new Step242ExportOptions
            {
                ProductName = "ThreadedHexBoltMakerMark", ApplicationName = "Aetheris.Firmament.MakerMark.X2",
                BrepExportPreflightMode = BrepExportPreflightMode.Enforce
            } : new Step242ExportOptions();
            var exports = new List<double>();
            var imports = new List<double>();
            for (var repeat = 0; repeat < 4; repeat++)
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var exported = Step242Exporter.ExportBody(body, options);
                timer.Stop();
                Assert.True(exported.IsSuccess);
                Assert.Equal(compiled.Value.StepText, exported.Value);
                if (repeat > 0) exports.Add(timer.Elapsed.TotalMilliseconds);
                timer.Restart();
                var imported = Step242Importer.ImportBody(exported.Value);
                timer.Stop();
                Assert.True(imported.IsSuccess);
                Assert.Equal(body.Topology.Faces.Count(), imported.Value.Topology.Faces.Count());
                Assert.Equal(body.Topology.Edges.Count(), imported.Value.Topology.Edges.Count());
                Assert.Equal(body.Topology.Vertices.Count(), imported.Value.Topology.Vertices.Count());
                if (repeat > 0) imports.Add(timer.Elapsed.TotalMilliseconds);
            }
            var name = threaded ? "threaded" : "ordinary";
            File.WriteAllText(Path.Combine(output, name + ".step"), compiled.Value.StepText);
            File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new
            {
                bytes = System.Text.Encoding.UTF8.GetByteCount(compiled.Value.StepText),
                exportMilliseconds = exports, importMilliseconds = imports,
                solids = body.Topology.Bodies.Count(), faces = body.Topology.Faces.Count(),
                edges = body.Topology.Edges.Count(), vertices = body.Topology.Vertices.Count(),
                compiled.Value.Thread, compiled.Value.MakerMark
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void ReusablePartPathRejectsAnEngravingItCannotApply()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "fixtures", "Thread", "hexbolt-showcase.firmament"));
        var parsed = FirmamentV2.FirmamentV2Parser.Parse(File.ReadAllText(path));
        var authored = parsed.Document!.Solid.StandardPart!;
        var stock = StandardLibrary.StandardLibraryReusableParts.TryCreate(authored.Family, authored.Parameters);
        Assert.False(stock.IsSuccess);
        Assert.Contains(stock.Diagnostics, d => d.Message.Contains("assembly engraving is not yet supported", StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalThreadDisplayAudit()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root.FullName, "fixtures", "Thread", "hexbolt-showcase.firmament"));
        var compiled = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(compiled.IsSuccess, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, compiled.Value.MakerMark!.CounterCount);
        Assert.Equal("stock-minus-groove", compiled.Value.Thread!.Construction);
        Assert.Equal(38, compiled.Value.Thread.Turns);
        Assert.Equal(1.25, compiled.Value.Thread.PitchMm);
        Assert.Equal(.953125, compiled.Value.Thread.HeadSideLandMm, 8);
        Assert.Equal(1.315625, compiled.Value.Thread.TipSideLandMm, 8);
        var imported = Step242Importer.ImportBody(compiled.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        Assert.Single(imported.Value.Topology.Bodies);
        Assert.Single(imported.Value.Topology.Shells);
        Assert.Equal(compiled.Value.RuntimeBody!.Topology.Faces.Count(), imported.Value.Topology.Faces.Count());
        var display = BrepDisplayTessellator.TessellateBounded(imported.Value, executionTimeout: TimeSpan.FromSeconds(60));
        Assert.True(display.IsSuccess, string.Join(" | ", display.Diagnostics.Select(d => d.Message)));
        var shaded = display.Value.FacePatches.Where(p => p.TriangleIndices.Count > 0).Select(p => p.FaceId).ToHashSet();
        var missing = imported.Value.Topology.Faces.Where(f => !shaded.Contains(f.Id)).Select(f => new
        {
            face = f.Id.Value,
            surface = imported.Value.Geometry.GetSurface(imported.Value.Bindings.GetFaceBinding(f.Id).SurfaceGeometryId).Kind.ToString(),
            diagnostic = display.Value.FaceDiagnostics?.Where(d => d.FaceId == f.Id).ToArray()
        }).ToArray();
        if (Environment.GetEnvironmentVariable("AETHERIS_THREAD_REPAIR_ARTIFACT_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "bolt.step"), compiled.Value.StepText);
            File.WriteAllText(Path.Combine(output, "audit.json"), JsonSerializer.Serialize(new
            {
                compiled.Value.Thread,
                compiled.Value.MakerMark,
                mass = BrepMassProperties.Evaluate(imported.Value),
                wireOnlyFaces = missing,
                diagnostics = display.Diagnostics.Select(d => d.Message).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.Empty(missing);
        var mass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(mass.IsEnclosed);
        Assert.True(mass.IsOrientationConsistent);
        Assert.All(imported.Value.Topology.Coedges.GroupBy(c => c.EdgeId), uses =>
        {
            Assert.Equal(2, uses.Count());
        });
    }

    [Fact]
    public void RecordOriginalProductDisplayBudgetWhenRequested()
    {
        if (Environment.GetEnvironmentVariable("AETHERIS_THREAD_REPAIR_BASELINE") is not { Length: > 0 } path) return;
        var imported = Step242Importer.ImportBody(File.ReadAllText(path));
        Assert.True(imported.IsSuccess);
        var complete = DisplayPreparationFallbackBuilder.Build(imported.Value);
        var display = complete.IsSuccess ? complete.Value : BrepDisplayTessellator.TessellateBoundedPartial(imported.Value);
        var shaded = display.FacePatches.Where(p => p.TriangleIndices.Count > 0).Select(p => p.FaceId).ToHashSet();
        File.WriteAllText(Path.ChangeExtension(path, "product-display.json"), JsonSerializer.Serialize(new
        {
            complete.IsSuccess,
            missing = imported.Value.Topology.Faces.Where(f => !shaded.Contains(f.Id)).Select(f => new
            {
                face = f.Id.Value,
                surface = imported.Value.Geometry.GetSurface(imported.Value.Bindings.GetFaceBinding(f.Id).SurfaceGeometryId).Kind.ToString(),
                diagnostics = display.FaceDiagnostics?.Where(d => d.FaceId == f.Id).ToArray()
            }).ToArray(),
            diagnostics = complete.Diagnostics.Select(d => d.Message).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
