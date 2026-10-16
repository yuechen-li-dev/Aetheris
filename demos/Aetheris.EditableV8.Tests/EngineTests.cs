using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.EditableV8;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.EditableV8.Tests;

public sealed class EngineTests
{
    [Fact]
    public void RevisionRebuildsGeometryAndMotionWithoutChangingOccurrenceIdentity()
    {
        var baseline = new EngineAuthor(new()); var revised = new EngineAuthor(new(Stroke: 94));
        var source = baseline.Engine(8); var revision = revised.Engine(8);
        Assert.NotEqual(source.Documents["design.firmament"], revision.Documents["design.firmament"]);
        var a = baseline.Motion(); var b = revised.Motion();
        Assert.Equal(a.Bindings.Select(x => x.Name), b.Bindings.Select(x => x.Name));
        Assert.Equal(3, b.Spec.DeckHeight - a.Spec.DeckHeight);
        Assert.Equal(3, b.Spec.CrankRadius - a.Spec.CrankRadius);
        Assert.NotEqual(a.Spec.CrankcaseFloor, b.Spec.CrankcaseFloor);
        var repeat = new EngineAuthor(new()); Assert.Equal(source.Documents.OrderBy(p => p.Key), repeat.Engine(8).Documents.OrderBy(p => p.Key));
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(repeat.Motion()));
        EngineMotion.Validate(a); EngineMotion.Validate(b);
        foreach (var motion in new[] { a, b }) foreach (var binding in motion.Bindings.Where(b => !b.Kind.EndsWith("Spring", StringComparison.Ordinal)))
        {
            var atZero = EngineMotion.Evaluate(motion, binding, 0);
            var atCycle = EngineMotion.Evaluate(motion, binding, 720);
            Assert.All(atZero.Zip(binding.Rest), p => Assert.InRange(double.Abs(p.First - p.Second), 0, 1e-10));
            Assert.All(atZero.Zip(atCycle), p => Assert.InRange(double.Abs(p.First - p.Second), 0, 1e-10));
        }
    }

    [Fact]
    public void PhysicalEngineUsesInterfacePlacementAndDimensionalFits()
    {
        var engine = new EngineAuthor(new()); var source = engine.Engine(8);
        var result = new AssemblyM1Pipeline().CompileProject(source.Snapshot);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.All(source.Documents.Values, text => Assert.DoesNotContain("LegacyExplicit", text));
        Assert.Equal(21, result.Ir!.Instances.Count(i => i.Kind == AssemblyInstanceKind.Assembly && i.ParentStableId is not null));
        Assert.Contains(result.Ir.Instances, i => i.Path.ToString() == "EditableV8.Crankshaft.Crankpin1");
        Assert.Contains(result.Ir.Instances, i => i.Path.ToString() == "EditableV8.Cylinders.C1.Cylinder.Piston");
        Assert.Equal(32, EngineMotion.Fits(result.Ir).Count);
        Assert.All(EngineMotion.Fits(result.Ir), fit => Assert.True(fit.Compatible));
        Assert.All(result.Geometry!.Artifact.MateResiduals, residual => Assert.True(residual.Passed));
        var localPlacements = result.Ir.AssemblyDefinitions!.SelectMany(d => d.LocalPlacements).ToArray();
        Assert.NotEmpty(localPlacements);
        Assert.All(localPlacements, p => Assert.True(p.Status is PlacementStatus.Anchored or PlacementStatus.Resolved));
        var export = AssemblyDisplayMeshExporter.Export(result);
        Assert.Equal(engine.Bindings.Count, export.Occurrences.Count(o => o.DefinitionId != null));
        Assert.Equal(export.Occurrences.Count, export.Occurrences.Select(o => o.Id).Distinct().Count());
        Assert.Equal(21, export.Definitions.Count);
        var solved = EngineMotion.BindAssembly(engine.Motion(), result);
        AssertGeometryReference(export, solved);
        EngineMotion.ValidateSpringAlignment(solved);
        Assert.Equal(55, solved.Bindings.Single(b => b.Name == "MainJournal1").Rest[14] - engine.Bindings.Single(b => b.Name == "MainJournal1").Rest[14], 8);
        Assert.All(solved.Bindings, b => Assert.Equal("assembly-instance:" + b.SourcePath, b.OccurrenceId));
        foreach (var binding in solved.Bindings.Where(b => b.SpringAxis != null))
        {
            var atZero = EngineMotion.Evaluate(solved, binding, 0);
            var atCycle = EngineMotion.Evaluate(solved, binding, 720);
            Assert.All(atZero.Zip(atCycle), p => Assert.InRange(double.Abs(p.First - p.Second), 0, 1e-10));
        }
    }

    [Fact]
    public void InvalidDesignFailsBeforeArtifactGeneration()
    {
        Assert.Throws<ArgumentException>(() => new V8Spec(Stroke: double.NaN).Validate());
        Assert.Throws<ArgumentException>(() => new V8Spec(BankAngle: 60).Validate());
        var unsafeDesign = new EngineAuthor(new(Bore: 80, Stroke: 100, RodLength: 135));
        unsafeDesign.Engine(8);
        var failure = Assert.Throws<InvalidOperationException>(() => EngineMotion.Validate(unsafeDesign.Motion()));
        Assert.Contains("engine-sweep-failed", failure.Message);
    }
    private static void AssertGeometryReference(AssemblyDisplayMeshDocument mesh, EngineMotionManifest motion)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName,
            "fixtures/Canonical/EditableV8/geometry-reference.expected.json")));
        using var meshes = ZipFile.OpenRead(Path.Combine(directory.FullName,
            "fixtures/Canonical/EditableV8/geometry-reference.meshes.zip"));
        var definitions = mesh.Definitions.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var occurrences = mesh.Occurrences.ToDictionary(o => o.Id, StringComparer.Ordinal);
        var bindings = motion.Bindings.ToDictionary(b => b.Name, StringComparer.Ordinal);
        Assert.Equal(reference.RootElement.GetProperty("bodyCount").GetInt32(), bindings.Count);
        Assert.Equal(reference.RootElement.GetProperty("definitionCount").GetInt32(), definitions.Count);
        var compared = new HashSet<(string DefinitionId, string Hash)>();
        foreach (var row in reference.RootElement.GetProperty("bindings").EnumerateArray()) {
            var binding = bindings[row.GetProperty("name").GetString()!];
            var definition = definitions[occurrences[binding.OccurrenceId!].DefinitionId!];
            var hash = row.GetProperty("meshSha256").GetString()!;
            if (compared.Add((definition.Id, hash)))
            {
                var entry = meshes.GetEntry(hash + ".bin");
                Assert.NotNull(entry);
                using var data = new MemoryStream();
                using (var stream = entry.Open()) stream.CopyTo(data);
                // Keep the original reference bytes; only the live comparison is tolerant.
                Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(data.ToArray())));
                data.Position = 0;
                using var reader = new BinaryReader(data);
                foreach (var (values, channel) in new[] { (definition.Positions, "positions"), (definition.Normals, "normals") })
                {
                    Assert.Equal(reader.ReadInt32(), values.Length);
                    for (var i = 0; i < values.Length; i++)
                    {
                        var expected = reader.ReadDouble();
                        Assert.True(ComponentMatches(expected, values[i]),
                            $"Part geometry changed for {binding.Name}: {channel}[{i}] expected {expected:R}, actual {values[i]:R}");
                    }
                }
                Assert.Equal(reader.ReadInt32(), definition.Indices.Length);
                foreach (var index in definition.Indices) Assert.Equal(reader.ReadInt32(), index);
                Assert.Equal(data.Length, data.Position);
            }
            var oldRest = row.GetProperty("rest").EnumerateArray().Select(v => v.GetDouble());
            Assert.True(binding.Rest.Zip(oldRest).All(p => double.Abs(p.First - p.Second) <= 1e-9), "Rest geometry changed for " + binding.Name);
        }
    }

    // libm and numerically differentiated spring normals vary across platforms.
    // Positions are in mm; normals are unitless. Topology remains exact above.
    private static bool ComponentMatches(double expected, double actual) =>
        double.IsFinite(expected) && double.IsFinite(actual) && double.Abs(expected - actual) <= 1e-9;

    [Theory]
    [InlineData(20d, 20.0000000001d, true)]
    [InlineData(20d, 20.00001d, false)]
    [InlineData(0d, double.NaN, false)]
    [InlineData(0d, double.PositiveInfinity, false)]
    public void GeometryComparisonAdmitsRoundoffButRejectsChanges(double expected, double actual, bool matches)
    {
        Assert.Equal(matches, ComponentMatches(expected, actual));
    }

}
