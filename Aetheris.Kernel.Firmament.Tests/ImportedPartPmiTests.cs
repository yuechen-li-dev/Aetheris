using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Export;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Visualization;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ImportedPartPmiTests
{
    private static string Repo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent;
        return d!.FullName;
    }
    private static string Source() => File.ReadAllText(Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi/annotated-part.firmament"));
    private static string Step() => File.ReadAllText(Path.Combine(Repo(), "testdata/firmament/inline-step/canonical-through-hole.step"));
    [Fact]
    public void SnapshotAssets_AreExplicitAndRemainImmutableAfterAdmission()
    {
        var source = Source().Replace("../../../testdata/firmament/inline-step/canonical-through-hole.step", "assets/body.step");
        var assets = new Dictionary<string, string> { ["assets/body.step"] = Step() };
        var project = new FirmamentProjectSnapshot("part.firmament", assets.Append(new KeyValuePair<string, string>("part.firmament", source)));
        Assert.Single(project.SourceDocuments);
        Assert.Empty(FirmamentLanguageAnalysisService.Analyze(project, "part.firmament", "1").Diagnostics.Where(d => d.Severity == "error"));
        var compiled = FirmamentBuildAndExport.CompileSource(source, resources: project.Documents);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics));
        var admitted = Assert.IsType<FirmamentV2InlineStepRecord>(compiled.Value.RuntimeAdmissionDocument!.Solids.Single().Primitive);
        Assert.Equal(Step(), admitted.SourceText);
        assets["assets/body.step"] = "corrupt";
        Assert.Equal(Step(), admitted.SourceText);
        var missing = FirmamentBuildAndExport.CompileSource(Source(), Repo(), new Dictionary<string, string>());
        Assert.False(missing.IsSuccess); // Even an existing disk resource cannot escape an explicit snapshot.
        Assert.Contains(missing.Diagnostics, d => d.Message.Contains(FirmamentV2Parser.InlineStepPathInvalid) || d.Message.Contains(FirmamentV2Parser.InlineStepFileMissing));
    }
    [Theory]
    [InlineData("#51", "#191")]
    [InlineData("#191", "#51")]
    public void IncompatibleImportedPmiSurface_IsRejected(string from, string to)
    {
        var built = FirmamentBuildAndExport.CompileSource(Source().Replace($"face(\"{from}\")", $"face(\"{to}\")"),
            Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("imported-pmi-target-kind"));
    }
    [Fact]
    public void ExistingImportedAnnotations_AreRejectedInsteadOfSilentlyStripped()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source(), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(built.IsSuccess);
        var source = Source().Replace("../../../testdata/firmament/inline-step/canonical-through-hole.step", "annotated.step");
        source = source[..source.IndexOf("    Pmi {", StringComparison.Ordinal)] + "}";
        var imported = FirmamentBuildAndExport.CompileSource(source, resources: new Dictionary<string, string> { ["annotated.step"] = built.Value.StepText });
        Assert.False(imported.IsSuccess);
        Assert.Contains(imported.Diagnostics, d => d.Message.Contains("existing-pmi-unsupported"));
    }
    [Fact]
    public void ImportedTopology_UsesProvenanceRatherThanEntityLineOrder()
    {
        var step = Step();
        var records = System.Text.RegularExpressions.Regex.Matches(step, @"(?m)^#\d+=ADVANCED_FACE[^\r\n]+").Select(m => m.Value).ToArray();
        var shuffled = step.Replace(records[0], "__FIRST_FACE__").Replace(records[^1], records[0]).Replace("__FIRST_FACE__", records[^1]);
        var source = Source().Replace("../../../testdata/firmament/inline-step/canonical-through-hole.step", "body.step");
        var result = FirmamentBuildAndExport.CompileSource(source, resources: new Dictionary<string, string> { ["body.step"] = shuffled });
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        var map = result.Value.RuntimeAdmissionDocument!.Solids.Single().InlineStep!.TopologyMap;
        var body = result.Value.RuntimeBody!;
        foreach (var binding in body.Bindings.FaceBindings)
            Assert.Equal($"face-{binding.FaceId.Value}", map.FaceEntityToFaceId["#" + binding.SourceStepEntityId]);
    }
    [Fact]
    public void AnnotatedGlb_RetainsBodyBuffersAndCarriesSemanticValuesAndSeparatePresentation()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source(), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics));
        var body = built.Value.RuntimeBody!;
        var tess = BrepDisplayTessellator.Tessellate(body, DisplayTessellationOptions.Default).Value;
        var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
        foreach (var patch in tess.FacePatches)
        {
            var start = positions.Count / 3;
            positions.AddRange(patch.Positions.SelectMany(p => new[] { p.X, p.Y, p.Z }));
            normals.AddRange(patch.Normals.SelectMany(p => new[] { p.X, p.Y, p.Z }));
            indices.AddRange(patch.TriangleIndices.Select(i => i + start));
        }
        var display = new AssemblyDisplayMeshDocument("aetheris/display-mesh/1", "Imported", "mm",
            [new("part", "part", positions.ToArray(), normals.ToArray(), indices.ToArray())],
            [new("part:occurrence", "part", null, "part", [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1])]);
        var baseline = AssemblyGlbExporter.Serialize(display);
        var file = Assert.Single(ProductArtifactExporter.Export(new("Imported", built.Value.StepText, display), ["glb"], Path.GetTempPath()));
        using var json = JsonDocument.Parse(file.Bytes.AsMemory(20, BitConverter.ToInt32(file.Bytes, 12)));
        var root = json.RootElement;
        var records = root.GetProperty("asset").GetProperty("extras").GetProperty("pmi");
        Assert.Equal(2, records.GetArrayLength());
        var diameter = records.EnumerateArray().Single(r => r.GetProperty("kind").GetString() == "Diameter");
        Assert.Equal(2, diameter.GetProperty("value").GetDouble());
        Assert.Equal(.1, diameter.GetProperty("tolerancePlus").GetDouble());
        Assert.Equal(.05, diameter.GetProperty("toleranceMinus").GetDouble());
        Assert.Equal(1, diameter.GetProperty("stepFaceEntities").GetArrayLength());
        Assert.Equal(3, root.GetProperty("meshes").GetArrayLength());
        Assert.All(root.GetProperty("meshes").EnumerateArray().Skip(1), mesh => Assert.Equal(1, mesh.GetProperty("primitives")[0].GetProperty("mode").GetInt32()));
        // Body data is a byte-identical prefix; annotation curves are appended after it.
        var beforeOffset = 28 + BitConverter.ToInt32(baseline, 12);
        var afterOffset = 28 + BitConverter.ToInt32(file.Bytes, 12);
        var bodyLength = BitConverter.ToInt32(baseline, beforeOffset - 8);
        Assert.Equal(baseline.AsSpan(beforeOffset, bodyLength).ToArray(), file.Bytes.AsSpan(afterOffset, bodyLength).ToArray());
        Assert.All(PmiDisplayProjection.FromStep(built.Value.StepText), annotation => Assert.NotEmpty(annotation.Lines));
    }
}
