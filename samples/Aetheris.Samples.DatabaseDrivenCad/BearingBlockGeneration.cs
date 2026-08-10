using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Forge.Host;

namespace Aetheris.Samples.DatabaseDrivenCad;

public sealed record BearingBlockSpec(
    Length Width,
    Length Height,
    Length Depth,
    Length BoreDiameter,
    Length BoreRadius,
    Length BoreTolerancePlus,
    Length BoreToleranceMinus,
    string Material,
    string PartNumber,
    Version Revision,
    string Company,
    string Author,
    string Description);

public sealed record GeneratedProduct(
    string PartNumber,
    string TemplateSpecialization,
    string StepPath,
    string StepSha256,
    IReadOnlyList<ForgeDiagnostic> Diagnostics,
    double MappingMilliseconds,
    double CompilationMilliseconds);

public static class BearingBlockBinding
{
    public static readonly ForgeRecordDescriptor<BearingBlockSpec> Descriptor = new(
        "BearingBlockSpec",
        new Dictionary<string, Func<BearingBlockSpec, ForgeValue>>(StringComparer.Ordinal)
        {
            ["Author"] = value => ForgeValue.From(value.Author),
            ["BoreDiameter"] = value => ForgeValue.From(value.BoreDiameter),
            ["BoreRadius"] = value => ForgeValue.From(value.BoreRadius),
            ["BoreToleranceMinus"] = value => ForgeValue.From(value.BoreToleranceMinus),
            ["BoreTolerancePlus"] = value => ForgeValue.From(value.BoreTolerancePlus),
            ["Company"] = value => ForgeValue.From(value.Company),
            ["Depth"] = value => ForgeValue.From(value.Depth),
            ["Description"] = value => ForgeValue.From(value.Description),
            ["Height"] = value => ForgeValue.From(value.Height),
            ["Material"] = value => ForgeValue.From(value.Material),
            ["PartNumber"] = value => ForgeValue.From(value.PartNumber),
            ["Revision"] = value => ForgeValue.From(value.Revision),
            ["Width"] = value => ForgeValue.From(value.Width),
        });

    public static BearingBlockSpec ToSpec(BearingBlockConfiguration row) => new(
        new(row.WidthMillimeters), new(row.HeightMillimeters), new(row.DepthMillimeters),
        new(row.BoreDiameterMillimeters), new(row.BoreDiameterMillimeters / 2), new(row.BoreTolerancePlusMillimeters), new(row.BoreToleranceMinusMillimeters),
        row.Material.Grade, row.PartNumber,
        new Version(row.RevisionMajor, row.RevisionMinor, row.RevisionPatch),
        row.DrawingMetadata.Company, row.DrawingMetadata.Author, row.DrawingMetadata.Description);

    public static ForgeInvocation CreateInvocation(ForgeModule module, BearingBlockSpec spec) =>
        module.ResolveTemplate("BearingBlock")
            .Invoke(spec.PartNumber.Replace("-", "_", StringComparison.Ordinal))
            .Bind("Spec", Descriptor.Map(spec))
            .WithProvenance("database", "products.sqlite", $"Entity=BearingBlockConfiguration;Key={spec.PartNumber}");
}

public sealed class BearingBlockGenerator
{
    private readonly ForgeModule module;

    public BearingBlockGenerator()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "BearingBlock.firmament");
        module = new ForgeHost().LoadModule(templatePath);
    }

    public GeneratedProduct Generate(BearingBlockConfiguration row, string outputRoot)
    {
        var mappingStart = Stopwatch.GetTimestamp();
        var spec = BearingBlockBinding.ToSpec(row);
        var invocation = BearingBlockBinding.CreateInvocation(module, spec);
        var mappingTime = Stopwatch.GetElapsedTime(mappingStart);
        var compileStart = Stopwatch.GetTimestamp();
        var result = invocation.Compile();
        var compileTime = Stopwatch.GetElapsedTime(compileStart);
        if (!result.IsSuccess || result.Artifact is null)
            throw new InvalidOperationException($"Forge compilation failed for {row.PartNumber}: " + string.Join("; ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));

        var directory = Path.Combine(Path.GetFullPath(outputRoot), row.PartNumber);
        Directory.CreateDirectory(directory);
        var stepPath = Path.Combine(directory, row.PartNumber + ".step");
        File.WriteAllText(stepPath, result.Artifact.StepText, new UTF8Encoding(false));
        return new GeneratedProduct(
            row.PartNumber,
            result.TemplateSpecializationIdentity ?? throw new InvalidOperationException("Host did not return Template specialization identity."),
            stepPath,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Artifact.StepText))),
            result.Diagnostics,
            mappingTime.TotalMilliseconds,
            compileTime.TotalMilliseconds);
    }

    public static void WriteManifest(string outputRoot, IReadOnlyList<GeneratedProduct> products) =>
        File.WriteAllText(
            Path.Combine(Path.GetFullPath(outputRoot), "manifest.json"),
            JsonSerializer.Serialize(products, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
}
