using Aetheris.Kernel.StandardLibrary.Materials.Database;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Kernel.StandardLibrary.Materials;

public interface IMaterialResolver
{
    MaterialResolutionResult Resolve(string reference);
}

public sealed class MaterialCatalog : IDisposable
{
    public const int ExpectedSeedMaterialCount = 4;
    private readonly MaterialCatalogDbContext database;

    public MaterialCatalog(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        var options = new DbContextOptionsBuilder<MaterialCatalogDbContext>().UseSqlite($"Data Source={fullPath};Mode=ReadOnly;Pooling=False").Options;
        database = new(options);
    }

    public IQueryable<MaterialCatalogItem> Materials => database.Materials.AsNoTracking().Select(x => new MaterialCatalogItem
    {
        CatalogId = x.CatalogId,
        StableId = x.StableId,
        FirmamentPath = x.FirmamentPath,
        Family = x.Family,
        Designation = x.Designation,
        Grade = x.Grade,
        Temper = x.Temper,
        Standard = x.Standard,
        DisplayName = x.DisplayName,
        ConstitutiveClass = x.ConstitutiveClass,
        ReferenceCondition = x.ReferenceCondition,
    });
    public int SchemaVersion => database.CatalogMetadata.AsNoTracking().Single(x => x.CatalogId == "standard").SchemaVersion;

    public static MaterialCatalog OpenDefault() => new(MaterialCatalogDatabase.DefaultDatabasePath);

    public ResolvedMaterial? GetById(string catalogId, string stableId)
    {
        var entity = database.Materials.AsNoTracking().Include(x => x.Properties).SingleOrDefault(x => x.CatalogId == catalogId && x.StableId == stableId);
        return entity is null ? null : Map(entity);
    }

    public IReadOnlyList<ResolvedMaterial> FindByDesignation(string designation) => database.Materials.AsNoTracking().Include(x => x.Properties)
        .Where(x => x.Designation == designation || x.Grade == designation)
        .OrderBy(x => x.StableId).AsEnumerable().Select(Map).ToArray();

    internal IReadOnlyList<ResolvedMaterial> FindByReference(string reference) => database.Materials.AsNoTracking().Include(x => x.Properties)
        .Where(x => x.FirmamentPath == reference || x.StableId == reference || (x.CatalogId + ":" + x.StableId) == reference)
        .OrderBy(x => x.StableId).AsEnumerable().Select(Map).ToArray();

    public void Dispose() => database.Dispose();

    private static ResolvedMaterial Map(MaterialEntity entity)
    {
        var validationErrors = MaterialDataValidator.Validate([entity]);
        if (validationErrors.Count > 0) throw new FormatException(string.Join(Environment.NewLine, validationErrors));
        var values = entity.Properties.ToDictionary(x => x.Kind, x => new MaterialPropertyValue(x.Kind, x.ValueSi, x.UnitSymbol, new(x.SourceId, x.SourceUri, x.Authority, x.Condition, x.ReferenceTemperatureKelvin, x.Notes)));
        var identity = new MaterialIdentity(entity.CatalogId, entity.StableId, entity.FirmamentPath, entity.Family, entity.Designation, entity.Grade, entity.Temper, entity.Standard, entity.DisplayName);
        StructuralMaterialProperties? structural = null;
        if (values.TryGetValue(MaterialPropertyKind.Density, out var density)
            && values.TryGetValue(MaterialPropertyKind.YoungsModulus, out var youngsModulus)
            && values.TryGetValue(MaterialPropertyKind.PoissonsRatio, out var poissonsRatio)
            && values.TryGetValue(MaterialPropertyKind.YieldStrength, out var yieldStrength)
            && values.TryGetValue(MaterialPropertyKind.UltimateTensileStrength, out var ultimateTensileStrength))
            structural = new(density, youngsModulus, poissonsRatio, yieldStrength, ultimateTensileStrength, values.GetValueOrDefault(MaterialPropertyKind.ShearModulus));
        return new(identity, entity.ConstitutiveClass, entity.ReferenceCondition,
            structural,
            new(values.GetValueOrDefault(MaterialPropertyKind.ThermalConductivity), values.GetValueOrDefault(MaterialPropertyKind.SpecificHeat), values.GetValueOrDefault(MaterialPropertyKind.CoefficientOfThermalExpansion)), values);
    }
}

public sealed class MaterialResolver(Func<MaterialCatalog> catalogFactory) : IMaterialResolver
{
    public MaterialResolver() : this(MaterialCatalog.OpenDefault) { }

    public MaterialResolutionResult Resolve(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return MaterialResolutionResult.Failure(MaterialResolutionError.UnknownMaterial, "Material reference is empty.");
        try
        {
            using var catalog = catalogFactory();
            var matches = catalog.FindByReference(reference.Trim());
            return matches.Count switch
            {
                0 => MaterialResolutionResult.Failure(MaterialResolutionError.UnknownMaterial, $"Unknown material reference '{reference}'. Use a stable catalog ID or Firmament material path."),
                > 1 => MaterialResolutionResult.Failure(MaterialResolutionError.AmbiguousMaterial, $"Material reference '{reference}' matched {matches.Count} catalog entries."),
                _ => MaterialResolutionResult.Success(matches[0]),
            };
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or System.Data.Common.DbException or IOException)
        {
            return MaterialResolutionResult.Failure(MaterialResolutionError.InvalidMaterialData, exception.Message);
        }
    }
}

public static class MaterialCatalogDatabase
{
    public static string DefaultDatabasePath => Path.Combine(AppContext.BaseDirectory, "Materials", "aetheris-materials-x1.sqlite");

    public static void Recreate(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        var options = new DbContextOptionsBuilder<MaterialCatalogDbContext>().UseSqlite($"Data Source={fullPath};Pooling=False").Options;
        using var database = new MaterialCatalogDbContext(options);
        database.Database.EnsureCreated();
        var seed = MaterialSeedData.Create();
        var errors = MaterialDataValidator.Validate(seed);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        database.Materials.AddRange(seed);
        database.CatalogMetadata.Add(new MaterialCatalogMetadataEntity { Id = 1, CatalogId = "standard", SchemaVersion = MaterialCatalogDbContext.SchemaVersion, SeedVersion = "MAT-DB-X1" });
        database.SaveChanges();
    }
}
