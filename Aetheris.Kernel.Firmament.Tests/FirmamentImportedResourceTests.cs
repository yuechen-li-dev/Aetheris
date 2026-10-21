using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Modules;
using System.Security.Cryptography;
using System.Text;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentImportedResourceTests
{
    static readonly string Part = Step242Exporter.ExportBody(BrepPrimitives.CreateBox(4, 6, 8).Value).Value;
    const string Tree = "Assembly Machine { <Assembly Machine><Part A = bearing>Placement { from: Origin; to: World; }</Part><Part B = bearing>Placement { from: Origin; to: World; translateLocal: [50mm,0mm,0mm]; }</Part></Assembly> Anchor: Machine; }";
    static void Accepted(AssemblyM1CompilationResult result) => Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    static string Lock(string root = "main.firmasm", string? part = null) => new FirmamentDependencyLock(root,
        [new("vendor/bearing.step", "step", "sha256-bytes", Hash(part ?? Part))]).ToToml();

    [Fact]
    public void TypedIncludeAndInventoryUseTheSameSnapshotGeometryAndIdentity()
    {
        const string include = "include bearing: Part = Step(\"vendor/bearing\"); ";
        var parsed = FirmamentSyntaxParser.Parse(include + Tree);
        Assert.True(parsed.IsSuccess);
        Assert.True(FirmamentSyntaxParser.Parse(FirmamentSyntaxWriter.Statements(parsed.Document.Statements)).IsSuccess);
        var shortForm = new AssemblyM1Pipeline().CompileProject(new("main.firmasm", [new("main.firmasm", include + Tree), new("vendor/bearing.step", Part)]));
        Accepted(shortForm);
        var catalog = new AssemblyM1Pipeline().CompileProject(new("main.firmasm", [
            new("main.firmasm", "Inventory Record Components { bearing: Part = Step(\"vendor/bearing\") } " + Tree.Replace("= bearing", "= Components.bearing")), new("vendor/bearing.step", Part)]));
        Accepted(catalog);
        Assert.Single(shortForm.Geometry!.DefinitionBodies);
        Assert.Equal(2, shortForm.Geometry.InstanceBodies.Count);
        Assert.Equal(catalog.Geometry!.DefinitionBodies.Keys, shortForm.Geometry.DefinitionBodies.Keys);
        Assert.Equal(new[] { -2d, -3, -4 }, Assert.Single(shortForm.Geometry.Artifact.Definitions).Metrics.Minimum);
        Assert.Contains(shortForm.Ir!.SourceDependencies!, d => d.Path == "vendor/bearing.step");
    }

    [Fact]
    public void StepAssemblyRetainsHierarchyTransformsAndSharedLeafGeometry()
    {
        var identity = new double[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };
        var translated = (double[])identity.Clone(); translated[12] = 25;
        var asset = Step242AssemblyExporter.Export(new("Imported", "root", [new("bolt", "Bolt", BrepPrimitives.CreateBox(4,6,8).Value)], [
            new("root", "Root", null, null, identity), new("first", "First", "root", "bolt", identity), new("second", "Second", "root", "bolt", translated)]));
        Assert.True(asset.IsSuccess);
        var source = "include drive: Assembly = Step(\"vendor/drive\"); Assembly Machine { <Assembly Machine><Assembly Unit = drive>Placement { from: Origin; to: World; }</Assembly></Assembly> Anchor: Machine; }";
        var result = new AssemblyM1Pipeline().CompileProject(new("main.firmasm", [new("main.firmasm", source), new("vendor/drive.step", asset.Value)]));
        Accepted(result);
        Assert.Single(result.Geometry!.DefinitionBodies);
        Assert.Equal(2, result.Geometry.InstanceBodies.Count);
        Assert.Contains(result.Ir!.Instances, i => i.Path.ToString() == "Machine.Unit.second" && i.ResolvedTransform!.Matrix[12] == 25);
        Assert.True(AssemblyIrAp242Exporter.Export(result).IsSuccess);
    }

    [Fact]
    public void NestedStepAssemblyPreservesLocalTransformsAndRepeatedGeometry()
    {
        double[] Matrix(double x) => [1,0,0,0,0,1,0,0,0,0,1,0,x,0,0,1];
        var asset = Step242AssemblyExporter.Export(new("Imported", "root", [new("bolt","Bolt",BrepPrimitives.CreateBox(4,6,8).Value)], [
            new("root","Root",null,null,Matrix(0)),new("unitA","Unit","root",null,Matrix(20)), new("unitB","Unit","root",null,Matrix(60)),
            new("boltA","Bolt","unitA","bolt",Matrix(5)),new("boltB","Bolt","unitB","bolt",Matrix(5))]));
        Assert.True(asset.IsSuccess);
        var source = "include drive: Assembly = Step(\"drive\"); Assembly Machine { <Assembly Machine><Assembly Drive = drive>Placement { from: Origin; to: World; }</Assembly></Assembly> Anchor: Machine; }";
        var result = new AssemblyM1Pipeline().CompileProject(new("main.firmasm",[new("main.firmasm",source),new("drive.step",asset.Value)]));
        Accepted(result);
        Assert.Single(result.Geometry!.DefinitionBodies);
        Assert.Equal(2,result.Geometry.InstanceBodies.Count);
        Assert.Contains(result.Ir!.Instances,i => i.Path.ToString().EndsWith("unitA.boltA",StringComparison.Ordinal) && i.ResolvedTransform!.Matrix[12] == 25);
        Assert.Contains(result.Ir.Instances,i => i.Path.ToString().EndsWith("unitB.boltB",StringComparison.Ordinal) && i.ResolvedTransform!.Matrix[12] == 65);
        Assert.True(Step242AssemblyImporter.Import(AssemblyIrAp242Exporter.Export(result).Value).IsSuccess);
    }

    [Fact]
    public void AnnotatedStepRequiresExplicitGeometryOnlyAdmission()
    {
        var annotated = Step242Exporter.ExportBody(BrepPrimitives.CreateBox(4,6,8).Value,
            [new Step242SemanticPmiDatum("Body","plane","A","+Z")]).Value;
        var source = "include bearing: Part = Step(\"vendor/bearing\"); " + Tree;
        AssemblyM1CompilationResult Build(string text) => new AssemblyM1Pipeline().CompileProject(new("main.firmasm",[new("main.firmasm",text),new("vendor/bearing.step",annotated)]));
        var refused = Build(source); Assert.False(refused.IsSuccess);
        Assert.Contains(refused.Diagnostics,d => d.Code == "firmament-step-source-pmi-unsupported");
        var admitted = Build(source.Replace("Step(\"vendor/bearing\")","Step(\"vendor/bearing\", geometryOnly: true)")); Accepted(admitted);
        Assert.True(Assert.Single(admitted.Ir!.InventoryBindings).GeometryOnly);
        Assert.Empty(Step242SemanticPmiInspector.Inspect(AssemblyIrAp242Exporter.Export(admitted).Value).Items);
    }

    [Fact]
    public void SnapshotLockIsOptionalButAnExistingReceiptCannotBeBypassed()
    {
        var source = "include bearing: Part = Step(\"vendor/bearing\"); " + Tree;
        var documents = new Dictionary<string,string> { ["main.firmasm"] = source, ["vendor/bearing.step"] = Part, [FirmamentDependencyLock.FileName] = Lock() };
        Accepted(new AssemblyM1Pipeline().CompileProject(new("main.firmasm", documents)));
        documents["vendor/bearing.step"] = Step242Exporter.ExportBody(BrepPrimitives.CreateBox(5,6,8).Value).Value;
        var changed = new AssemblyM1Pipeline().CompileProject(new("main.firmasm", documents));
        Assert.False(changed.IsSuccess);
        Assert.Contains(changed.Diagnostics, d => d.Code == "firmament-lock-dependency-mismatch");
        documents["vendor/bearing.step"] = Part;
        documents[FirmamentDependencyLock.FileName] = Lock("another.firmasm");
        var wrongRoot = new AssemblyM1Pipeline().CompileProject(new("main.firmasm", documents));
        Assert.False(wrongRoot.IsSuccess);
        Assert.Contains(wrongRoot.Diagnostics, d => d.Code == "firmament-lock-root-mismatch");
        // A real file with the source-only adapter's conventional name is still
        // a real root; special filenames cannot bypass root identity checking.
        var specialName = new AssemblyM1Pipeline().CompileProject(new("__provided_source.firmament",[
            new("__provided_source.firmament",source), new("vendor/bearing.step",Part), new(FirmamentDependencyLock.FileName,Lock("another.firmasm"))]));
        Assert.False(specialName.IsSuccess);
        Assert.Contains(specialName.Diagnostics,d => d.Code == "firmament-lock-root-mismatch");
        documents[FirmamentDependencyLock.FileName] = Lock() + "unexpected = true\n";
        Assert.False(new AssemblyM1Pipeline().CompileProject(new("main.firmasm", documents)).IsSuccess);
    }

    [Fact]
    public void StandaloneRootCanLockAnEmptyDependencySet()
    {
        var empty = new FirmamentDependencyLock("standalone.firmament", []);
        var parsed = FirmamentDependencyLock.Parse(empty.ToToml());
        Assert.Equal(empty.Root,parsed.Root);
        Assert.Empty(parsed.Dependencies);
        var invalid = Lock().Replace("vendor/bearing.step","../outside.step",StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => FirmamentDependencyLock.Parse(invalid));
    }

    [Theory]
    [InlineData("from: bearing")]
    [InlineData("from: Components.bearing")]
    [InlineData("path: \"vendor/bearing.step\"")]
    public void InlineStepUsesLockedResourcesWithoutAuthoredHashes(string binding)
    {
        var prefix = binding.Contains("Components") ? "Inventory Record Components { bearing: Part = Step(\"vendor/bearing\") } "
            : binding.StartsWith("from:") ? "include bearing: Part = Step(\"vendor/bearing\"); " : "";
        var source = prefix + "Model Surgery { units: mm InlineStep Source { " + binding + "; geometryOnly: true } }";
        var resources = new Dictionary<string,string> { ["vendor/bearing.step"] = Part, [FirmamentDependencyLock.FileName] = Lock("surgery.firmament") };
        var result = FirmamentBuildAndExport.CompileSource(source, "", resources);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Value.ImportedHoleRevision);
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess);
        Assert.Equal(6, imported.Value.Topology.Faces.Count());
        Assert.Equal(12, imported.Value.Topology.Edges.Count());
        var vertices = imported.Value.Topology.Vertices.Select(v =>
        { Assert.True(imported.Value.TryGetVertexPoint(v.Id,out var point)); return point; }).ToArray();
        Assert.Equal(new[]{-2d,-3,-4},new[]{vertices.Min(p=>p.X),vertices.Min(p=>p.Y),vertices.Min(p=>p.Z)});
        Assert.Equal(new[]{2d,3,4},new[]{vertices.Max(p=>p.X),vertices.Max(p=>p.Y),vertices.Max(p=>p.Z)});
        resources["vendor/bearing.step"] = Part + "\n";
        Assert.False(FirmamentBuildAndExport.CompileSource(source, "", resources).IsSuccess);
    }

    [Fact]
    public void ImportMetadataBindsOrdinaryRecordsAndDoesNotAlterGeometry()
    {
        const string data = "Record VendorInfo { manufacturer: String revision: String } Static Receipt: VendorInfo { manufacturer: \"Acme\" revision: \"A\" } ";
        var source = data + "Inventory Record Components { bearing: Part = Step(\"vendor/bearing\", metadata: Receipt) } " + Tree.Replace("= bearing", "= Components.bearing");
        AssemblyM1CompilationResult Build(string text) => new AssemblyM1Pipeline().CompileProject(new("main.firmasm", [new("main.firmasm", text), new("vendor/bearing.step", Part)]));
        var before = Build(source); Accepted(before);
        var after = Build(source.Replace("revision: \"A\"", "revision: \"B\"")); Accepted(after);
        var receipt = Assert.Single(before.Ir!.InventoryBindings);
        Assert.Equal("VendorInfo", receipt.MetadataType);
        Assert.Equal("Acme", receipt.Metadata["manufacturer"]);
        Assert.Equal("B", Assert.Single(after.Ir!.InventoryBindings).Metadata["revision"]);
        Assert.Equal(before.Geometry!.Artifact.DeterministicSha256, after.Geometry!.Artifact.DeterministicSha256);
        Assert.False(Build(source.Replace("metadata: Receipt", "metadata: Missing")).IsSuccess);
    }

    [Fact]
    public void NestedReleaseRecordsUseTheOrdinaryTypedBinder()
    {
        var source = "Record Person { name: String role: String } Record ReleaseInfo { author: Person date: Date version: Version description: String } "
            + "Static Designer: Person { name: \"Engineer\" role: \"Designer\" } Static Release: ReleaseInfo { author: Designer date: 2026-10-21 version: 1.0.0 description: \"Review witness\" } "
            + "include bearing: Part = Step(\"vendor/bearing\"); " + Tree.Replace("Assembly Machine {", "Assembly Machine { provenance: Release;");
        var result = new AssemblyM1Pipeline().CompileProject(new("main.firmasm", [new("main.firmasm",source), new("vendor/bearing.step",Part)]));
        Accepted(result);
        Assert.Equal("Engineer", result.Ir!.Annotations!.Release!.Fields["Author"]);
        Assert.Equal("Designer", result.Ir.Annotations.Release.Fields["author.role"]);
        var exported = AssemblyIrAp242Exporter.Export(result);
        Assert.True(exported.IsSuccess);
        Assert.Contains("('Engineer')", exported.Value);
    }
}



