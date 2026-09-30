using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242AssemblyProductStructureTests
{
    [Fact]
    public void ProductWithTwoRigidRoots_PromotesBodiesUnderEachRepeatedOccurrence()
    {
        var body = BrepPrimitives.CreateBox(2, 3, 4).Value;
        double[] identity = [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1];
        double[] moved = [1,0,0,0, 0,1,0,0, 0,0,1,0, 20,0,0,1];
        var exported = Step242AssemblyExporter.Export(new("Root", "root", [new("multi", "Multi", body)], [
            new("root", "Root", null, null, identity),
            new("first", "First", "root", "multi", identity),
            new("second", "Second", "root", "multi", moved)
        ])).Value;
        var rigidRoot = Regex.Match(exported, @"#(?<id>\d+)=MANIFOLD_SOLID_BREP\('[^']*',#(?<shell>\d+)\);");
        Assert.True(rigidRoot.Success);
        var maxId = Regex.Matches(exported, @"#(?<id>\d+)=").Select(match => int.Parse(match.Groups["id"].Value)).Max();
        var representation = Regex.Match(exported, $@"#\d+=SHAPE_REPRESENTATION\([^\r\n]*\(#{rigidRoot.Groups["id"].Value}\)[^\r\n]*;");
        Assert.True(representation.Success);
        exported = exported.Replace(representation.Value,
            representation.Value.Replace($"(#{rigidRoot.Groups["id"].Value})", $"(#{rigidRoot.Groups["id"].Value},#{maxId + 1})", StringComparison.Ordinal), StringComparison.Ordinal);
        exported = exported.Insert(exported.LastIndexOf("ENDSEC;", StringComparison.Ordinal),
            $"#{maxId + 1}=MANIFOLD_SOLID_BREP('Additional',#{rigidRoot.Groups["shell"].Value});\r\n");

        var imported = Step242AssemblyImporter.Import(exported);

        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(item => item.Message)));
        Assert.Equal(2, imported.Value.Definitions.Count(item => item.Geometry is not null));
        Assert.Equal(4, imported.Value.Occurrences.Count(item => item.DefinitionStableId.StartsWith("multi/body:", StringComparison.Ordinal)));
        Assert.Equal(2, imported.Value.Occurrences.Count(item => item.DefinitionStableId == "multi"));
        Assert.All(imported.Value.Occurrences.Where(item => item.DefinitionStableId.StartsWith("multi/body:", StringComparison.Ordinal)),
            occurrence => Assert.Contains(imported.Value.Occurrences, parent => parent.StableId == occurrence.ParentStableId));
        Assert.Equal(2, imported.Value.Occurrences.Where(item => item.DefinitionStableId.StartsWith("multi/body:", StringComparison.Ordinal))
            .Select(item => item.DefinitionStableId).Distinct().Count());
    }

    [Fact]
    public void FlatMultipleRigidRoots_RecoversSyntheticAssemblyWithoutUnion()
    {
        var step = Step242Exporter.ExportBody(BrepPrimitives.CreateBox(2, 3, 4).Value).Value;
        var root = Regex.Match(step, @"MANIFOLD_SOLID_BREP\('[^']*',#(?<shell>\d+)\)");
        Assert.True(root.Success);
        var maxId = Regex.Matches(step, @"#(?<id>\d+)=").Select(match => int.Parse(match.Groups["id"].Value)).Max();
        var second = $"#{maxId + 1}=MANIFOLD_SOLID_BREP('Second',#{root.Groups["shell"].Value});\r\n";
        step = step.Insert(step.LastIndexOf("ENDSEC;", StringComparison.Ordinal), second);

        var imported = Step242AssemblyImporter.Import(step);

        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(item => item.Message)));
        Assert.Equal("RecoveredMultiBodyAssembly", imported.Value.Provenance);
        Assert.Equal(2, imported.Value.Definitions.Count);
        Assert.Equal(2, imported.Value.Occurrences.Count);
        Assert.Contains(imported.Value.Definitions, definition => definition.Name == "Second");
        Assert.All(imported.Value.Definitions, definition => Assert.NotNull(definition.Geometry));
        Assert.All(imported.Value.Definitions, definition => Assert.Single(definition.Geometry!.Topology.Bodies));
        Assert.False(Step242Importer.ImportBody(step).IsSuccess);
    }

    [Fact]
    public void MissingOccurrenceTransform_FailsWithIdentifiedOccurrence()
    {
        var body = BrepPrimitives.CreateBox(1, 1, 1).Value;
        double[] identity = [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1];
        var exported = Step242AssemblyExporter.Export(new("Root", "root", [new("part", "Part", body)], [
            new("root", "Root", null, null, identity), new("child", "Child", "root", "part", identity)
        ])).Value;
        var broken = exported.Replace("CONTEXT_DEPENDENT_SHAPE_REPRESENTATION(", "UNSUPPORTED_CONTEXT_SHAPE(", StringComparison.Ordinal);

        var imported = Step242AssemblyImporter.Import(broken);

        Assert.False(imported.IsSuccess);
        Assert.Contains(imported.Diagnostics, diagnostic => diagnostic.Source == "Importer.Assembly.MissingOccurrenceTransform"
            && diagnostic.Message.Contains("child", StringComparison.Ordinal));
    }

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
        Assert.EndsWith("\r\n", first.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", first.Value.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(1, Count(first.Value, "MANIFOLD_SOLID_BREP("));
        Assert.Equal(2, Count(first.Value, "NEXT_ASSEMBLY_USAGE_OCCURRENCE("));

        var imported = Step242AssemblyImporter.Import(first.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("root", imported.Value.RootDefinitionStableId);
        Assert.Equal(2, imported.Value.Occurrences.Count);
        Assert.Single(imported.Value.Definitions, definition => definition.StableId == "def:bolt" && definition.Geometry is not null);
        Assert.Equal(25, imported.Value.Occurrences.Single(occurrence => occurrence.StableId == "bolt-2").LocalTransform[12], 8);

        // AP242 occurrence transforms relate the child representation to its parent.
        // A parent-first relationship appears correct to Aetheris' own importer but causes
        // standards-compliant CAD viewers to apply the inverse translation.
        var childRepresentation = imported.Value.Definitions.Single(definition => definition.StableId == "def:bolt").RepresentationEntityId;
        var parentRepresentation = imported.Value.Definitions.Single(definition => definition.StableId == "root").RepresentationEntityId;
        Assert.Contains($"(REPRESENTATION_RELATIONSHIP('bolt-2','',#{childRepresentation},#{parentRepresentation})REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION(#", first.Value, StringComparison.Ordinal);
        Assert.Contains("SHAPE_REPRESENTATION_RELATIONSHIP())", first.Value, StringComparison.Ordinal);
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
