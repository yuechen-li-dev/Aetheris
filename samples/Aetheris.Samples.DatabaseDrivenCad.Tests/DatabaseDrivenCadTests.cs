using System.Text.Json;
using Aetheris.Forge.Host;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Samples.DatabaseDrivenCad.Tests;

public sealed class DatabaseDrivenCadTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"aetheris-database-cad-{Guid.NewGuid():N}");

    [Fact]
    public async Task RealSqliteSchemaSeedAndRelationalLinqQueriesAreDeterministic()
    {
        await using var database = ProductCatalog.Open(Path.Combine(directory, "products.sqlite"));
        await ProductCatalog.SeedAsync(database);

        Assert.True(await database.Database.CanConnectAsync());
        Assert.Equal(4, await database.BearingBlockConfigurations.CountAsync());
        Assert.Equal(2, await database.Materials.CountAsync());
        var aluminum = await ProductCatalog.ProductionAluminum(database).Select(item => new { item.PartNumber, item.Material.Grade }).ToArrayAsync();
        Assert.Equal(new[] { "AB-204", "AB-305" }, aluminum.Select(item => item.PartNumber));
        Assert.All(aluminum, item => Assert.Equal("6061-T6", item.Grade));
        var majorTwo = await ProductCatalog.CurrentMajor(database, 2).Select(item => item.PartNumber).ToArrayAsync();
        Assert.Equal(["AB-204"], majorTwo);
        var sku = await ProductCatalog.WithRelations(database).SingleAsync(item => item.PartNumber == "AB-204");
        Assert.Equal("Aster Works", sku.DrawingMetadata.Company);
    }

    [Fact]
    public async Task DatabaseRowCompilesThroughTypedRecordAndBatchPathsAreDeterministic()
    {
        await using var database = ProductCatalog.Open(Path.Combine(directory, "products.sqlite"));
        await ProductCatalog.SeedAsync(database);
        var rows = await ProductCatalog.ProductionAluminum(database).ToArrayAsync();
        var generator = new BearingBlockGenerator();

        var first = rows.Select(row => generator.Generate(row, Path.Combine(directory, "first"))).ToArray();
        var second = rows.Select(row => generator.Generate(row, Path.Combine(directory, "second"))).ToArray();

        Assert.Equal(new[] { "AB-204", "AB-305" }, first.Select(item => item.PartNumber));
        Assert.Equal(first.Select(item => item.TemplateSpecialization), second.Select(item => item.TemplateSpecialization));
        Assert.Equal(first.Select(item => item.StepSha256), second.Select(item => item.StepSha256));
        Assert.All(first, item =>
        {
            Assert.True(File.Exists(item.StepPath));
            Assert.Contains("ISO-10303-21", File.ReadAllText(item.StepPath), StringComparison.Ordinal);
            Assert.Equal(Path.Combine(directory, "first", item.PartNumber, item.PartNumber + ".step"), item.StepPath);
        });
    }

    [Fact]
    public async Task InvalidConfigurationSurfacesFirmamentRequireDiagnostic()
    {
        await using var database = ProductCatalog.Open(Path.Combine(directory, "products.sqlite"));
        await ProductCatalog.SeedAsync(database);
        var row = await ProductCatalog.WithRelations(database).SingleAsync(item => item.PartNumber == "AB-204");
        row.BoreDiameterMillimeters = row.WidthMillimeters;

        var exception = Assert.Throws<InvalidOperationException>(() => new BearingBlockGenerator().Generate(row, Path.Combine(directory, "invalid")));
        Assert.Contains("require", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownSkuAndMalformedRevisionAreRejected()
    {
        await using var database = ProductCatalog.Open(Path.Combine(directory, "products.sqlite"));
        await ProductCatalog.SeedAsync(database);
        Assert.Empty(await ProductCatalog.WithRelations(database).Where(item => item.PartNumber == "UNKNOWN").ToArrayAsync());

        var row = await ProductCatalog.WithRelations(database).SingleAsync(item => item.PartNumber == "AB-204");
        row.RevisionMajor = -1;
        Assert.Throws<ArgumentOutOfRangeException>(() => BearingBlockBinding.ToSpec(row));
    }

    [Fact]
    public void SampleDependencyGraphExcludesKernelSdkDirectlyAndTransitively()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "samples", "Aetheris.Samples.DatabaseDrivenCad", "Aetheris.Samples.DatabaseDrivenCad.csproj"));
        Assert.DoesNotContain("Aetheris.Forge.KernelSDK", project, StringComparison.OrdinalIgnoreCase);

        var assetsPath = Path.Combine(root, "samples", "Aetheris.Samples.DatabaseDrivenCad", "obj", "project.assets.json");
        Assert.True(File.Exists(assetsPath), "Restore must create project.assets.json before the boundary test runs.");
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var libraries = assets.RootElement.GetProperty("libraries").EnumerateObject().Select(item => item.Name).ToArray();
        Assert.DoesNotContain(libraries, item => item.Contains("Aetheris.Forge.KernelSDK", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(libraries, item => item.Contains("Aetheris.Forge.Host", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExplicitRecordMappingSupportsPrimitivesUnitsNestedRecordsAndCanonicalOrder()
    {
        var child = new ForgeRecordDescriptor<Child>("Child", new Dictionary<string, Func<Child, ForgeValue>>
        {
            ["Name"] = value => ForgeValue.From(value.Name),
            ["Enabled"] = value => ForgeValue.From(value.Enabled),
        });
        var parent = new ForgeRecordDescriptor<Parent>("Parent", new Dictionary<string, Func<Parent, ForgeValue>>
        {
            ["Revision"] = value => ForgeValue.From(value.Revision),
            ["Length"] = value => ForgeValue.From(value.Length),
            ["Count"] = value => ForgeValue.From(value.Count),
            ["Child"] = value => child.Map(value.Child),
        });

        var mapped = parent.Map(new(new Length(12.5), 3, new Version(2, 1, 0), new(true, "nested")));
        Assert.Equal(new[] { "Child", "Count", "Length", "Revision" }, mapped.Fields.Keys);
        Assert.IsType<ForgeRecord>(mapped.Fields["Child"]);
        Assert.Equal(12.5, Assert.IsType<ForgeLength>(mapped.Fields["Length"]).Millimeters);
        Assert.Equal(new Version(2, 1, 0), Assert.IsType<ForgeVersion>(mapped.Fields["Revision"]).Value);
    }

    [Fact]
    public void NestedRecordCompilesThroughFirmamentBinderWithoutSourceGeneration()
    {
        const string source = """
            Record Dimensions { Width: Length Height: Length Depth: Length }
            Record ProductSpec { Dimensions: Dimensions }
            Concept ProductConcept {
                Bounds: Box3
                TopPlane: Plane
                ChamferDistance: Length
            }
            Template < Spec: ProductSpec >
            Struct Product: ProductConcept {
                Require Positive => Spec.Dimensions.Width > 0mm && Spec.Dimensions.Height > 0mm && Spec.Dimensions.Depth > 0mm
                Concept Struct Design: ProductConcept {
                    Bounds: Box3 { Size: [Spec.Dimensions.Width, Spec.Dimensions.Height, Spec.Dimensions.Depth] }
                    TopPlane: Bounds.Face(+Z)
                    ChamferDistance: 1mm
                }
                Box Body { Bounds: Design.Bounds }
                Modify Body {
                    EdgeFinish TopBreak {
                        Face: Design.TopPlane
                        Target: Boundary
                        Kind: Chamfer
                        Distance: Design.ChamferDistance
                    }
                }
                Expose {
                    Bounds: Design.Bounds
                    TopPlane: Body.Top
                    ChamferDistance: Design.ChamferDistance
                }
            }
            """;
        var dimensions = new ForgeRecordDescriptor<Dimensions>("Dimensions", new Dictionary<string, Func<Dimensions, ForgeValue>>
        {
            ["Width"] = value => ForgeValue.From(value.Width),
            ["Height"] = value => ForgeValue.From(value.Height),
            ["Depth"] = value => ForgeValue.From(value.Depth),
        });
        var product = new ForgeRecordDescriptor<Product>("ProductSpec", new Dictionary<string, Func<Product, ForgeValue>>
        {
            ["Dimensions"] = value => dimensions.Map(value.Dimensions),
        });

        var result = new ForgeHost().LoadModule("Nested", source).ResolveTemplate("Product").Invoke("NestedBox")
            .Bind("Spec", product.Map(new(new(new Length(20), new Length(12), new Length(5)))))
            .Compile();

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        Assert.Contains("ISO-10303-21", result.Artifact!.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedNullMappingIsRejectedDeterministically()
    {
        var descriptor = new ForgeRecordDescriptor<string>("Bad", new Dictionary<string, Func<string, ForgeValue>>
        {
            ["Unsupported"] = _ => null!,
        });
        var exception = Assert.Throws<InvalidOperationException>(() => descriptor.Map("value"));
        Assert.Equal("Mapper for field 'Unsupported' returned null.", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed record Child(bool Enabled, string Name);
    private sealed record Parent(Length Length, int Count, Version Revision, Child Child);
    private sealed record Dimensions(Length Width, Length Height, Length Depth);
    private sealed record Product(Dimensions Dimensions);
}
