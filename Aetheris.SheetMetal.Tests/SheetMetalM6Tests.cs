using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM6Tests
{
    private static EnclosureProductSpec Canonical => new(160, 110, 36, 1.2, 1.2, 1.5, 1, 9);

    [Fact]
    public void UserAssemblyTemplate_ComposesTypedSheetMetalPartsThroughInterfaceAndMates()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/FirmamentV2/SheetMetal/m7-network-appliance-product.firmasm"));
        var product = EnclosureProductFamilies.Compile(source, Canonical, "m6-network-appliance-product.firmasm");

        Assert.True(product.Assembly.IsSuccess, Evidence(product));
        Assert.Equal(["Body", "Lid"], product.Assembly.Ir!.Instances.Where(item => item.Kind == AssemblyInstanceKind.Part).Select(item => item.Path.Segments.Last()).Order().ToArray());
        var definition = product.Assembly.Ir.AssemblyDefinitions!.Single();
        Assert.Equal(["Attachments", "Closure"], definition.LocalMates.Select(item => item.Name).Order().ToArray());
        Assert.All(definition.LocalMates, item => Assert.Equal("valid", item.ValidationStatus));
        Assert.Contains(definition.Provenance, item => item.Stage == "assembly-concept-satisfaction" && item.Identity == "EnclosureProduct");
        Assert.Equal(2, product.Assembly.Geometry!.Artifact.Definitions.Count);
        Assert.Equal(SheetMetalDfmStatus.Pass, product.Dfm.Overall);
        Assert.Contains(product.Dfm.Findings, item => item.RuleId == "assembly-dfm-lid-clearance" && Math.Abs(item.Measured!.Value - 1) < 1e-9);
        Assert.Contains(product.SemanticPaths, item => item == "Product.Body.FrontLip");
        Assert.NotNull(product.Body.FlatPattern.ExactBlankContour); Assert.NotNull(product.Lid.FlatPattern.ExactBlankContour);
        Assert.Equal(9, product.Body.FlatPattern.CutLoops.Count); Assert.Single(product.Lid.FlatPattern.CutLoops);
        Assert.Contains(product.Body.SemanticPaths, item => item.Path == "Rear.RearEthernet");
        Assert.Contains(product.Lid.SemanticPaths, item => item.Path == "Top.LidMountA");
    }

    [Fact]
    public void ForgeFacingCall_UsesSameTemplateAndExportsManufacturingPackageAndAp242Hierarchy()
    {
        var first = EnclosureProductFamilies.MakeEnclosureProduct(Canonical, "NetworkApplianceA");
        var second = EnclosureProductFamilies.MakeEnclosureProduct(Canonical with { Width = 190, Depth = 125 }, "NetworkApplianceB");
        Assert.Equal(first.SemanticPaths.Select(Shape), second.SemanticPaths.Select(Shape));
        Assert.NotEqual(first.SpecializationIdentity, second.SpecializationIdentity);
        Assert.Equal(first.Assembly.Geometry!.Artifact.DeterministicSha256,
            EnclosureProductFamilies.MakeEnclosureProduct(Canonical, "NetworkApplianceA").Assembly.Geometry!.Artifact.DeterministicSha256);

        var requestedArtifacts = Environment.GetEnvironmentVariable("AETHERIS_M6_ARTIFACTS");
        var directory = requestedArtifacts is null ? Path.Combine(Path.GetTempPath(), "aetheris-m6-" + Guid.NewGuid().ToString("N")) : Path.GetFullPath(requestedArtifacts);
        try
        {
            var artifacts = first.Export(directory);
            Assert.All(new[] { artifacts.AssemblyStep, artifacts.BodyFormedStep, artifacts.BodyFlatStep, artifacts.BodyFlatSvg, artifacts.LidFormedStep, artifacts.LidFlatStep, artifacts.LidFlatSvg, artifacts.ProductDfmJson, artifacts.FitReportJson }, path => Assert.True(File.Exists(path), path));
            var imported = Step242AssemblyImporter.Import(File.ReadAllText(artifacts.AssemblyStep));
            Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(item => item.Message)));
            Assert.Equal(2, imported.Value.Definitions.Count(item => item.Geometry is not null));
            Assert.Contains(imported.Value.Occurrences, item => item.Name == "Body");
            Assert.Contains(imported.Value.Occurrences, item => item.Name == "Lid");
            foreach (var path in new[] { artifacts.BodyFormedStep, artifacts.BodyFlatStep, artifacts.LidFormedStep, artifacts.LidFlatStep })
            {
                var body = Step242Importer.ImportBody(File.ReadAllText(path));
                Assert.True(body.IsSuccess); Assert.True(BrepExportPreflight.Validate(body.Value).IsValid);
            }
        }
        finally { if (requestedArtifacts is null && Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void MissingProductConceptMember_IsTypedParserDiagnosticBeforeGeometry()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/FirmamentV2/SheetMetal/m7-network-appliance-product.firmasm"))
            .Replace("<Part Lid = RemovablePanLid<Spec: Spec.Lid>>", "<Part Cover = RemovablePanLid<Spec: Spec.Lid>>", StringComparison.Ordinal);
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, item => item.Code == "assembly-concept-missing-member" && item.Message.Contains("Lid", StringComparison.Ordinal));
    }

    [Fact]
    public void ClearanceViolation_IsSemanticProductDfmFailureWithRepairablePath()
    {
        var product = EnclosureProductFamilies.MakeEnclosureProduct(Canonical with { LidClearance = .2 }, "BadClearance");
        var finding = Assert.Single(product.Dfm.Findings, item => item.RuleId == "assembly-dfm-lid-clearance");
        Assert.Equal(SheetMetalDfmStatus.Fail, finding.Status); Assert.Equal("Product.Closure", finding.SemanticPath);
        var repaired = EnclosureProductFamilies.MakeEnclosureProduct(Canonical, "RepairedClearance");
        Assert.Equal(SheetMetalDfmStatus.Pass, repaired.Dfm.Findings.Single(item => item.RuleId == "assembly-dfm-lid-clearance").Status);
    }

    private static string Shape(string path) => path.Replace("NetworkApplianceA", "Product", StringComparison.Ordinal).Replace("NetworkApplianceB", "Product", StringComparison.Ordinal);
    private static string Evidence(ManufacturedEnclosureProduct product) => string.Join(Environment.NewLine, product.Assembly.Diagnostics.Select(item => item.Code + ": " + item.Message));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory?.FullName ?? throw new InvalidOperationException(); }
}
