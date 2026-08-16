using Aetheris.Kernel.StandardLibrary.Materials;
using Aetheris.Kernel.StandardLibrary.Materials.Database;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Kernel.StandardLibrary.Tests;

public sealed class MaterialCatalogTests
{
    [Fact]
    public void CheckedInDatabase_HasDeterministicSchemaAndRepresentativeSeedCatalog()
    {
        Assert.True(File.Exists(MaterialCatalogDatabase.DefaultDatabasePath), MaterialCatalogDatabase.DefaultDatabasePath);
        using var catalog = MaterialCatalog.OpenDefault();
        Assert.Equal(MaterialCatalogDbContext.SchemaVersion, catalog.SchemaVersion);
        var materials = catalog.Materials.OrderBy(x => x.StableId).ToArray();
        Assert.Equal(MaterialCatalog.ExpectedSeedMaterialCount, materials.Length);
        Assert.Equal(materials.Length, materials.Select(x => (x.CatalogId, x.StableId)).Distinct().Count());
        Assert.Contains(materials, x => x.StableId == "aluminum/5052-h32" && x.Temper == "H32");
        Assert.Contains(materials, x => x.StableId == "aluminum/6061-t6" && x.Temper == "T6");
        Assert.Contains(materials, x => x.StableId == "steel/astm-a36");
        Assert.Contains(materials, x => x.StableId == "stainless-steel/304-annealed");
        Assert.All(materials, x => Assert.Equal(MaterialConstitutiveClass.LinearElasticIsotropic, x.ConstitutiveClass));
    }

    [Fact]
    public void ReproducibleGeneration_CreatesLoadableEfCoreSqliteCatalog()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aetheris-materials-{Guid.NewGuid():N}.sqlite");
        var secondPath = Path.Combine(Path.GetTempPath(), $"aetheris-materials-{Guid.NewGuid():N}.sqlite");
        try
        {
            MaterialCatalogDatabase.Recreate(path);
            MaterialCatalogDatabase.Recreate(secondPath);
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(secondPath));
            using var catalog = new MaterialCatalog(path);
            Assert.Equal(MaterialCatalog.ExpectedSeedMaterialCount, catalog.Materials.Count());
            Assert.NotNull(catalog.GetById("standard", "aluminum/5052-h32"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(secondPath)) File.Delete(secondPath);
        }
    }

    [Fact]
    public void LinqAndResolver_PreserveAlloyTemperDistinctionsAndTypedSiProperties()
    {
        using var catalog = MaterialCatalog.OpenDefault();
        var aluminum = catalog.Materials.Where(x => x.Family == "Aluminum").OrderBy(x => x.Designation).ToArray();
        Assert.Equal(["5052", "6061"], aluminum.Select(x => x.Designation));
        Assert.Equal(["H32", "T6"], aluminum.Select(x => x.Temper));

        var resolved = new MaterialResolver().Resolve("Standard.Materials.Aluminum.5052_H32");
        Assert.True(resolved.IsSuccess, resolved.Message);
        Assert.Equal(2680, resolved.Material!.Structural!.Density.SiValue);
        Assert.Equal("kg/m^3", resolved.Material.Structural.Density.UnitSymbol);
        Assert.Equal(70.3e9, resolved.Material.Structural.YoungsModulus.SiValue);
        Assert.NotEmpty(resolved.Material.Structural.YieldStrength.Provenance.SourceUri);
        Assert.True(resolved.Material.Thermal.HasAny);
    }

    [Fact]
    public void UnknownReference_FailsWithoutFallback()
    {
        var result = new MaterialResolver().Resolve("Standard.Materials.Aluminum.Generic");
        Assert.False(result.IsSuccess);
        Assert.Equal(MaterialResolutionError.UnknownMaterial, result.Error);
        Assert.Contains("Unknown material reference", result.Message);
    }

    [Fact]
    public void Validation_RejectsDuplicateIdentityAndImpossibleIsotropicValues()
    {
        var first = Invalid("duplicate", -1, .75, 300, 200);
        var second = Invalid("duplicate", 1000, .3, 100, 200);
        var errors = MaterialDataValidator.Validate([first, second]);
        Assert.Contains(errors, x => x.Contains("Duplicate stable material identity", StringComparison.Ordinal));
        Assert.Contains(errors, x => x.Contains("Density must be positive", StringComparison.Ordinal));
        Assert.Contains(errors, x => x.Contains("PoissonsRatio must be in", StringComparison.Ordinal));
        Assert.Contains(errors, x => x.Contains("UltimateTensileStrength must be at least", StringComparison.Ordinal));
    }

    private static MaterialEntity Invalid(string stableId, double density, double poisson, double yield, double ultimate)
    {
        var material = new MaterialEntity { CatalogId = "test", StableId = stableId, FirmamentPath = "Test." + Guid.NewGuid().ToString("N"), Family = "Test", Designation = stableId, DisplayName = stableId, ConstitutiveClass = MaterialConstitutiveClass.LinearElasticIsotropic, ReferenceCondition = "test" };
        material.Properties.Add(Property(MaterialPropertyKind.Density, density));
        material.Properties.Add(Property(MaterialPropertyKind.PoissonsRatio, poisson));
        material.Properties.Add(Property(MaterialPropertyKind.YieldStrength, yield));
        material.Properties.Add(Property(MaterialPropertyKind.UltimateTensileStrength, ultimate));
        return material;
    }

    private static MaterialPropertyEntity Property(MaterialPropertyKind kind, double value) => new() { Kind = kind, ValueSi = value, UnitSymbol = "SI", SourceId = "test", SourceUri = "test://source", Authority = MaterialPropertyAuthority.IndustryReferenceNominal, Condition = "test" };
}
