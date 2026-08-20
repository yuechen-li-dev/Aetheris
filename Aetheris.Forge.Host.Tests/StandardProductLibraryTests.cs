using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Forge.Host.Tests;

public sealed class StandardProductLibraryTests
{
    [Fact]
    public void CatalogExposesEightProductFamiliesAlongsideOtherTemplateDomains()
    {
        var products = new ForgeProtocolHost().ListTemplates().Templates
            .Where(item => item.Id.StartsWith("Standard.Products.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(8, products.Length);
        Assert.All(products, product => Assert.NotNull(new ForgeProtocolHost().DescribeTemplate(product.Id)));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void FlagshipVariantsProduceDeterministicReimportableStep(
        string templateId, IReadOnlyDictionary<string, object?> arguments)
    {
        using var first = TempDirectory.Create();
        using var second = TempDirectory.Create();
        var host = new ForgeProtocolHost();
        var request = new ForgeTemplateInvocationRequest(1, arguments.ToDictionary(
            pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal),
            [ForgeArtifactKind.StepAp242]);
        var a = host.InvokeTemplate(templateId, request, first.Path);
        var b = host.InvokeTemplate(templateId, request, second.Path);
        Assert.True(a.Success, string.Join(Environment.NewLine, a.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.True(b.Success, string.Join(Environment.NewLine, b.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.Equal(a.Identity.Specialization, b.Identity.Specialization);
        Assert.Equal(a.Artifacts.Single().Sha256, b.Artifacts.Single().Sha256);
        var step = File.ReadAllText(System.IO.Path.Combine(first.Path, a.Artifacts.Single().Path));
        Assert.True(Step242Importer.ImportBody(step).IsSuccess);
    }

    [Fact]
    public void MountingPlateConstraintUsesProductSemantics()
    {
        using var output = TempDirectory.Create();
        var values = MountingPlate();
        values["holeSpacingX"] = "121 mm";
        var request = new ForgeTemplateInvocationRequest(1, values.ToDictionary(
            pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal),
            [ForgeArtifactKind.StepAp242]);
        var result = new ForgeProtocolHost().InvokeTemplate("Standard.Products.Mechanical.MountingPlate", request, output.Path);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, item => item.Code == "firmament-template-require-failed" && item.Message.Contains("HoleSpacingXFitsPlate", StringComparison.Ordinal));
    }

    [Fact]
    public void DescribeProjectsResolvedStaticRecordDefaultsToFields()
    {
        var description = Assert.IsType<ForgeTemplateDescription>(
            new ForgeProtocolHost().DescribeTemplate("Standard.Products.Mechanical.MountingPlate"));
        var policy = Assert.Single(description.Parameters);
        Assert.Equal("StandardMountingPlate", policy.Default);
        var fields = policy.Fields!.ToDictionary(field => field.Name, StringComparer.Ordinal);
        Assert.Equal("120mm", fields["Width"].Default);
        Assert.Equal("mm", fields["Width"].Unit);
        Assert.False(fields["Width"].Required);
        Assert.Equal("Standard.Materials.Aluminum.6061_T6", fields["Material"].Default);
    }

    [Fact]
    public void CanonicalStaticDefaultCanBeInvokedWithoutDuplicatingFields()
    {
        using var output = TempDirectory.Create();
        var result = new ForgeProtocolHost().InvokeTemplate(
            "Standard.Products.Mechanical.MountingPlate",
            new ForgeTemplateInvocationRequest(1, new Dictionary<string, JsonElement>(), [ForgeArtifactKind.StepAp242]),
            output.Path);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        var inspection = Step242SemanticPmiInspector.Inspect(File.ReadAllText(
            System.IO.Path.Combine(output.Path, result.Artifacts.Single().Path)));
        Assert.True(inspection.Success);
        Assert.Equal(1, inspection.DatumCount);
        Assert.Equal(1, inspection.DimensionCount);
    }

    [Fact]
    public void EightHoleFlangeSpecializesThroughForge()
    {
        using var output = TempDirectory.Create();
        var values = Values(("outerDiameter", "80 mm"), ("bodyThickness", "12 mm"), ("boreDiameter", "30 mm"),
            ("boltHoleDiameter", "6.6 mm"), ("boltCount", 8), ("material", "Standard.Materials.Aluminum.6061_T6"));
        var result = new ForgeProtocolHost().InvokeTemplate(
            "Standard.Products.Mechanical.FlangedAdapter",
            new ForgeTemplateInvocationRequest(1, values.ToDictionary(pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal), [ForgeArtifactKind.StepAp242]),
            output.Path);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        var imported = Step242Importer.ImportBody(File.ReadAllText(System.IO.Path.Combine(output.Path, result.Artifacts.Single().Path)));
        Assert.True(imported.IsSuccess);
        Assert.Equal(42, imported.Value!.Topology.Faces.Count());
    }

    public static TheoryData<string, IReadOnlyDictionary<string, object?>> Variants() => new()
    {
        { "Standard.Products.Mechanical.MountingPlate", MountingPlate() },
        { "Standard.Products.Mechanical.BearingBlock", Values(("baseWidth", "100 mm"), ("baseDepth", "50 mm"), ("baseThickness", "14 mm"), ("bossDiameter", "36 mm"), ("bossHeight", "18 mm"), ("boreDiameter", "20 mm"), ("mountSpacing", "72 mm"), ("mountHoleDiameter", "8.5 mm"), ("material", "Standard.Materials.Aluminum.6061_T6")) },
        { "Standard.Products.Mechanical.MachinedAngleBracket", Values(("width", "80 mm"), ("height", "60 mm"), ("legWidth", "20 mm"), ("thickness", "10 mm"), ("holeDiameter", "8.5 mm"), ("material", "Standard.Materials.Steel.ASTM_A36")) },
        { "Standard.Products.Mechanical.ShaftCollar", Values(("outerDiameter", "32 mm"), ("boreDiameter", "16 mm"), ("width", "12 mm"), ("material", "Standard.Materials.StainlessSteel.304_Annealed")) },
        { "Standard.Products.Mechanical.FlangedAdapter", Values(("outerDiameter", "80 mm"), ("bodyThickness", "12 mm"), ("boreDiameter", "30 mm"), ("boltHoleDiameter", "6.6 mm"), ("boltCount", 6), ("material", "Standard.Materials.Aluminum.6061_T6")) },
        { "Standard.Products.Electronics.RackPanel", Values(("width", "482.6 mm"), ("height", "44.45 mm"), ("thickness", "3 mm"), ("mountHoleDiameter", "6.5 mm"), ("mountInset", "12.7 mm"), ("material", "Standard.Materials.Aluminum.5052_H32")) },
        { "Standard.Products.Mechanical.Standoff", Values(("outerDiameter", "10 mm"), ("length", "25 mm"), ("boreDiameter", "4.2 mm"), ("material", "Standard.Materials.StainlessSteel.304_Annealed")) },
    };

    private static Dictionary<string, object?> MountingPlate() => Values(
        ("width", "120 mm"), ("height", "80 mm"), ("thickness", "10 mm"), ("holeDiameter", "6.6 mm"),
        ("holeSpacingX", "90 mm"), ("holeSpacingY", "50 mm"), ("counterboreDiameter", "11 mm"),
        ("counterboreDepth", "4 mm"), ("material", "Standard.Materials.Aluminum.6061_T6"));

    private static Dictionary<string, object?> Values(params (string Name, object? Value)[] values) =>
        values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path) => Path = path;
        public string Path { get; }
        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetheris-standard-products-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new(path);
        }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
