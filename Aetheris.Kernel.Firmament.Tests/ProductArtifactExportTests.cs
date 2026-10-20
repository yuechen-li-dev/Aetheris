using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Export;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ProductArtifactExportTests
{
    [Fact]
    public void ExistingExportersProduceReadablePartArtifactsAndPreserveExactStep()
    {
        var fixture = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/DrawingGeneration/inspection-plate.firmament");
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(fixture));
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var step = compiled.Value.StepText;
        var body = Step242Importer.ImportBody(step).Value;
        var tessellation = BrepDisplayTessellator.Tessellate(body).Value;
        var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
        foreach (var face in tessellation.FacePatches)
        {
            var offset = positions.Count / 3;
            positions.AddRange(face.Positions.SelectMany(p => new[] { p.X, p.Y, p.Z }));
            normals.AddRange(face.Normals.SelectMany(p => new[] { p.X, p.Y, p.Z }));
            indices.AddRange(face.TriangleIndices.Select(i => i + offset));
        }
        var display = new AssemblyDisplayMeshDocument("aetheris/display-mesh/1", "Plate", "mm",
            [new("definition", "plate", positions.ToArray(), normals.ToArray(), indices.ToArray())],
            [new("occurrence", "Plate", null, "definition", [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1])]);
        var scratch = Path.Combine(Path.GetDirectoryName(fixture)!, "../../../artifacts/local/tests/product-export");
        var expectedPmi = Drawing.FirmamentDrawingCompiler.AuthoredPmiNames(compiled.Value);
        Assert.Equal(11, expectedPmi.Count);
        var files = ProductArtifactExporter.Export(new("Plate", step, display, ExpectedPmi: expectedPmi),
            ["step", "glb", "usda", "stl", "obj", "pdf", "svg", "pptx"], scratch);
        Assert.Equal(8, files.Count);
        byte[] Bytes(string extension) => files.Single(f => f.FileName.EndsWith("." + extension, StringComparison.Ordinal)).Bytes;
        Assert.Equal(step, Encoding.UTF8.GetString(Bytes("step")));
        Assert.True(Step242Importer.ImportBody(Encoding.UTF8.GetString(Bytes("step"))).IsSuccess);
        var glb = Bytes("glb"); Assert.Equal("glTF", Encoding.ASCII.GetString(glb, 0, 4));
        using var json = JsonDocument.Parse(glb.AsMemory(20, BitConverter.ToInt32(glb, 12)));
        Assert.Single(json.RootElement.GetProperty("meshes").EnumerateArray().Where(mesh => mesh.GetProperty("extras").TryGetProperty("definitionIdentity", out _)));
        Assert.Equal(expectedPmi.Count, json.RootElement.GetProperty("asset").GetProperty("extras").GetProperty("pmi").GetArrayLength());
        Assert.All(json.RootElement.GetProperty("meshes").EnumerateArray(), mesh => Assert.True(mesh.GetProperty("primitives")[0].GetProperty("attributes").TryGetProperty("POSITION", out _)));
        Assert.Contains("metersPerUnit = 0.001", Encoding.UTF8.GetString(Bytes("usda")));
        Assert.Contains("f ", Encoding.UTF8.GetString(Bytes("obj")));
        var stl = Bytes("stl"); Assert.Equal(84L + BitConverter.ToUInt32(stl, 80) * 50L, stl.Length);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(Bytes("pdf"), 0, 5));
        Assert.Contains("<svg", Encoding.UTF8.GetString(Bytes("svg")));
        using var pptx = new ZipArchive(new MemoryStream(Bytes("pptx")));
        Assert.Contains(pptx.Entries, e => e.FullName == "ppt/slides/slide1.xml");
        var package = ProductArtifactExporter.Package(files);
        Assert.Equal("Plate-exports.zip", package.FileName);
        using var zip = new ZipArchive(new MemoryStream(package.Bytes));
        Assert.Equal(8, zip.Entries.Count);
        foreach (var file in files)
        {
            using var stream = zip.GetEntry(file.FileName)!.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy);
            Assert.Equal(file.Bytes, copy.ToArray());
        }
        Assert.Equal(step, compiled.Value.StepText);
        Assert.Empty(Directory.GetDirectories(scratch));
        var rejected = Assert.Throws<InvalidOperationException>(() => ProductArtifactExporter.Export(
            new("Plate", step, display, ExpectedPmi: ["MissingAuthoredRequirement"]), ["pdf"], scratch));
        Assert.StartsWith("drawing-pmi-not-exported:", rejected.Message);
        Assert.Empty(Directory.GetDirectories(scratch));
    }

    [Fact]
    public void AssemblyAvailabilityIsExplicitAndUnknownOrUnavailableFormatsNeverSucceed()
    {
        var fixture = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/annular-pair.firmament");
        var compilation = new AssemblyM1Pipeline().Compile(File.ReadAllText(fixture), fixture);
        Assert.True(compilation.IsSuccess);
        var display = AssemblyDisplayMeshExporter.Export(compilation);
        var source = new ProductExportSource("Pair", null, display, compilation.Ir);
        var scratch = Path.Combine(Path.GetDirectoryName(fixture)!, "../../../artifacts/local/tests/product-export");
        Assert.Contains(ProductArtifactExporter.Options(true, false), o => o.Format == "pdf" && !o.Available && o.Reason is not null);
        Assert.Throws<InvalidOperationException>(() => ProductArtifactExporter.Export(source, ["banana"], scratch));
        Assert.Throws<InvalidOperationException>(() => ProductArtifactExporter.Export(source, ["stl"], scratch));
        var files = ProductArtifactExporter.Export(source, ["glb", "usda"], scratch);
        using var glb = JsonDocument.Parse(files[0].Bytes.AsMemory(20, BitConverter.ToInt32(files[0].Bytes, 12)));
        Assert.Equal(display.Occurrences.Count + 1, glb.RootElement.GetProperty("nodes").GetArrayLength()); // unit/up-axis conversion root
        Assert.Equal(60, display.Occurrences.Single(o => o.Path == "AnnularPair.Second").Transform[12]);
        Assert.Equal("#usda 1.0", Encoding.UTF8.GetString(files[1].Bytes).Split('\n')[0]);
    }
}
