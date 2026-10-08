using System.Buffers.Binary;
using System.Text.Json;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyGlbExportTests
{
    [Fact]
    public void SharedMeshesHierarchySharpNormalsAndWorldFramesSurviveBinaryLowering()
    {
        var compilation = Compile("two-link-arm-usd.firmament");
        var pose = AssemblyKinematics.Evaluate(compilation.Ir!, new Dictionary<string, double> { ["Shoulder"] = 35, ["Elbow"] = 65 });
        var mesh = AssemblyDisplayMeshExporter.Export(compilation, pose: pose);
        var bytes = AssemblyGlbExporter.Serialize(mesh);
        Assert.Equal(bytes, AssemblyGlbExporter.Serialize(mesh));
        var (json, binary) = Read(bytes); using var document = json;
        var root = json.RootElement;
        Assert.Equal(mesh.Definitions.Count, root.GetProperty("meshes").GetArrayLength());
        Assert.Equal(mesh.Occurrences.Count + 1, root.GetProperty("nodes").GetArrayLength());
        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        var byName = nodes.Select((n, i) => (n, i)).ToDictionary(p => p.n.GetProperty("name").GetString()!, p => p.i);
        double[] World(int index)
        {
            var local = nodes[index].GetProperty("matrix").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            var parent = Enumerable.Range(0, nodes.Length).Where(i => nodes[i].TryGetProperty("children", out var c) && c.EnumerateArray().Any(v => v.GetInt32() == index)).ToArray();
            return parent.Length == 0 ? local : Multiply(local, World(Assert.Single(parent)));
        }
        foreach (var o in mesh.Occurrences)
        {
            var world = World(byName[o.Path]);
            var source = Transform3D.FromRowMajor(o.Transform).Apply(new Point3D(11, 22, 33));
            var p = new[] { 11d, 22, 33, 1 };
            var actual = Enumerable.Range(0, 3).Select(c => Enumerable.Range(0, 4).Sum(r => p[r] * world[r * 4 + c])).ToArray();
            Assert.Equal(source.X * .001, actual[0], 10); Assert.Equal(source.Z * .001, actual[1], 10); Assert.Equal(-source.Y * .001, actual[2], 10);
        }
        foreach (var (d, index) in mesh.Definitions.OrderBy(d => d.Id, StringComparer.Ordinal).Select((d, i) => (d, i)))
        {
            var attributes = root.GetProperty("meshes")[index].GetProperty("primitives")[0].GetProperty("attributes");
            foreach (var (name, source) in new[] { ("POSITION", d.Positions), ("NORMAL", d.Normals) })
            {
                var accessor = root.GetProperty("accessors")[attributes.GetProperty(name).GetInt32()];
                var view = root.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
                var offset = view.GetProperty("byteOffset").GetInt32();
                Assert.Equal(source.Length / 3, accessor.GetProperty("count").GetInt32());
                for (var i = 0; i < source.Length; i++) Assert.Equal((float)source[i], BitConverter.ToSingle(binary, offset + i * 4));
            }
        }
    }

    [Fact]
    public void PbrAndTextureBytesAreEmbeddedWithExplicitUvs()
    {
        var mesh = Triangle();
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=");
        var appearance = new AssemblyGlbAppearance(new(.2,.3,.4,.6,.7), [0,0,1,0,0,1], png, "image/png");
        var (json, binary) = Read(AssemblyGlbExporter.Serialize(mesh, new Dictionary<string, AssemblyGlbAppearance> { ["triangle"] = appearance }));
        using var document = json; var root = json.RootElement;
        var image = root.GetProperty("images")[0]; Assert.False(image.TryGetProperty("uri", out _));
        var view = root.GetProperty("bufferViews")[image.GetProperty("bufferView").GetInt32()];
        Assert.Equal(png, binary.Skip(view.GetProperty("byteOffset").GetInt32()).Take(view.GetProperty("byteLength").GetInt32()));
        var pbr = root.GetProperty("materials")[0].GetProperty("pbrMetallicRoughness");
        Assert.Equal(.6, pbr.GetProperty("metallicFactor").GetDouble()); Assert.Equal(.7, pbr.GetProperty("roughnessFactor").GetDouble());
        Assert.Equal(.2, pbr.GetProperty("baseColorFactor")[0].GetDouble());
        Assert.False(root.GetProperty("buffers")[0].TryGetProperty("uri", out _));
    }

    [Fact]
    public void InvalidHierarchyUnitsNormalsIndicesAndAppearanceFailExplicitly()
    {
        var mesh = Triangle(); var d = mesh.Definitions[0]; var o = mesh.Occurrences[0];
        Assert.Throws<InvalidOperationException>(() => AssemblyGlbExporter.Serialize(mesh with { Units = "m" }));
        Assert.Throws<InvalidOperationException>(() => AssemblyGlbExporter.Serialize(mesh with { Occurrences = [o with { ParentId = o.Id }] }));
        Assert.Throws<InvalidOperationException>(() => AssemblyGlbExporter.Serialize(mesh with { Occurrences = [o with { ParentId = "missing" }] }));
        Assert.Throws<InvalidOperationException>(() => AssemblyGlbExporter.Serialize(mesh with { Definitions = [d with { Indices = [0,1,3] }] }));
        Assert.Throws<InvalidOperationException>(() => AssemblyGlbExporter.Serialize(mesh with { Definitions = [d with { Normals = new double[9] }] }));
        Assert.Throws<InvalidOperationException>(() => AssemblyGlbExporter.Serialize(mesh, new Dictionary<string, AssemblyGlbAppearance> { ["missing"] = new() }));
        Assert.Throws<InvalidOperationException>(() => AssemblyGlbExporter.Serialize(mesh, new Dictionary<string, AssemblyGlbAppearance> { ["triangle"] = new(new(Roughness: double.NaN)) }));
        Assert.Throws<InvalidOperationException>(() => AssemblyGlbExporter.Serialize(mesh, new Dictionary<string, AssemblyGlbAppearance> { ["triangle"] = new(BaseColorTexture: [1,2,3], TextureMimeType: "image/png") }));
    }

    [Theory]
    [InlineData("IndustrialAtlas/atlas-industrial.firmament")]
    [InlineData("GuitarX0/guitar.firmasm")]
    public void PresentationWitnessUsesRealDisplayPath(string path)
    {
        var fullPath = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/" + path);
        var compilation = path.EndsWith(".firmasm", StringComparison.Ordinal)
            ? new FirmamentAssemblyDocumentCompiler().CompileFile(fullPath).Compilation : new AssemblyM1Pipeline().CompileFile(fullPath);
        Assert.True(compilation.IsSuccess, string.Join("\n", compilation.Diagnostics));
        var display = AssemblyDisplayMeshExporter.Export(compilation);
        var (json, _) = Read(AssemblyGlbExporter.Serialize(display)); using var document = json;
        Assert.Equal(display.Definitions.Count, json.RootElement.GetProperty("meshes").GetArrayLength());
        Assert.Equal(display.Occurrences.Count + 1, json.RootElement.GetProperty("nodes").GetArrayLength());
        Assert.True(display.Definitions.Count > 20); Assert.True(display.Occurrences.Count > 50);
    }

    [Fact]
    public void IndustrialExportPreservesSourceOwnedOccurrenceMaterials()
    {
        var compilation = Compile("IndustrialAtlas/atlas-industrial.firmament");
        Assert.True(compilation.IsSuccess, string.Join("\n", compilation.Diagnostics));
        var (json, _) = Read(AssemblyGlbExporter.Export(compilation));
        using var document = json;
        var root = document.RootElement;
        foreach (var instance in compilation.Ir!.Instances.Where(instance => instance.Kind == AssemblyInstanceKind.Part))
        {
            var node = root.GetProperty("nodes").EnumerateArray().Single(node => node.GetProperty("name").GetString() == instance.Path.ToString());
            var mesh = root.GetProperty("meshes")[node.GetProperty("mesh").GetInt32()];
            var material = root.GetProperty("materials")[mesh.GetProperty("primitives")[0].GetProperty("material").GetInt32()];
            var pbr = material.GetProperty("pbrMetallicRoughness");
            Assert.Equal(instance.Appearance!.Preview.Red, pbr.GetProperty("baseColorFactor")[0].GetDouble());
            Assert.Equal(instance.Appearance.Preview.Metallic, pbr.GetProperty("metallicFactor").GetDouble());
            Assert.Equal(instance.Appearance.Preview.Roughness, pbr.GetProperty("roughnessFactor").GetDouble());
        }
    }

    private static AssemblyM1CompilationResult Compile(string path) => new AssemblyM1Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/" + path));
    private static AssemblyDisplayMeshDocument Triangle() => new("aetheris/assembly-display-mesh/1", "Triangle", "mm",
        [new("d", "triangle", [0,0,0,10,0,0,0,10,0], [0,0,1,0,0,1,0,0,1], [0,1,2])],
        [new("o", "Triangle", null, "d", Transform3D.Identity.ToRowMajor())]);
    private static (JsonDocument, byte[]) Read(byte[] bytes)
    {
        Assert.Equal(0x46546C67u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal((uint)bytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        Assert.Equal(0, length % 4); Assert.Equal(0x4E4F534Au, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)));
        Assert.Equal(0x004E4942u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24 + length)));
        return (JsonDocument.Parse(bytes.AsMemory(20, length)), bytes[(28 + length)..]);
    }
    private static double[] Multiply(double[] a, double[] b) => Enumerable.Range(0, 16).Select(i => Enumerable.Range(0, 4).Sum(k => a[i / 4 * 4 + k] * b[k * 4 + i % 4])).ToArray();
}
