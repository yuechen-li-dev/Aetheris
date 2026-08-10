using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242AssemblyProductStructureTests
{
    [Fact]
    public void RepeatedDefinition_RoundTripsOccurrencesTransformsAndExactGeometry()
    {
        var body = BrepPrimitives.CreateBox(4, 6, 8).Value;
        var identity = new double[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };
        var translated = new double[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 25,5,0,1 };
        var model = new Step242AssemblyExportModel("BoltArray", "root", [new("def:bolt", "Bolt", body)], [
            new("root", "BoltArray", null, null, identity),
            new("bolt-1", "Bolt 1", "root", "def:bolt", identity),
            new("bolt-2", "Bolt 2", "root", "def:bolt", translated)
        ]);

        var first = Step242AssemblyExporter.Export(model);
        var second = Step242AssemblyExporter.Export(model);

        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(1, Count(first.Value, "MANIFOLD_SOLID_BREP("));
        Assert.Equal(2, Count(first.Value, "NEXT_ASSEMBLY_USAGE_OCCURRENCE("));

        var imported = Step242AssemblyImporter.Import(first.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("root", imported.Value.RootDefinitionStableId);
        Assert.Equal(2, imported.Value.Occurrences.Count);
        Assert.Single(imported.Value.Definitions, definition => definition.StableId == "def:bolt" && definition.Geometry is not null);
        Assert.Equal(25, imported.Value.Occurrences.Single(occurrence => occurrence.StableId == "bolt-2").LocalTransform[12], 8);
    }

    [Fact]
    public void NestedHierarchy_IsNotFlattened()
    {
        var body = BrepPrimitives.CreateBox(1, 1, 1).Value;
        var identity = new double[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };
        var model = new Step242AssemblyExportModel("Nested", "root", [new("def:part", "Part", body)], [
            new("root", "Root", null, null, identity),
            new("sub", "SubA", "root", null, identity),
            new("part", "Part1", "sub", "def:part", identity)
        ]);

        var export = Step242AssemblyExporter.Export(model);
        Assert.True(export.IsSuccess);
        var imported = Step242AssemblyImporter.Import(export.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("sub", imported.Value.Occurrences.Single(occurrence => occurrence.StableId == "part").ParentStableId);
    }

    [Fact]
    public void HistoricalOcctAs1_PreservesExplicitProductStructureAndDefinitionReuse()
    {
        var path = Path.Combine(RepositoryRoot(), "testdata", "step242", "OCCT", "as1.step");
        var imported = Step242AssemblyImporter.Import(File.ReadAllText(path));

        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => $"{diagnostic.Source}: {diagnostic.Message}")));
        Assert.Equal(27, imported.Value.Occurrences.Count);
        Assert.Equal(5, imported.Value.Definitions.Count(definition => definition.Geometry is not null));
        Assert.Equal(18, imported.Value.Occurrences.Count(occurrence => imported.Value.Definitions.Single(definition => definition.StableId == occurrence.DefinitionStableId).Geometry is not null));
        Assert.Contains(imported.Value.Occurrences, occurrence => occurrence.ParentStableId is not null);
    }

    private static int Count(string source, string value) => (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate Aetheris repository root.");
    }
}
