using Aetheris.Kernel.StandardLibrary.Materials.Database;

namespace Aetheris.Kernel.StandardLibrary.Materials;

public interface IMaterialResolver
{
    MaterialResolutionResult Resolve(string reference);
}

/// <summary>A validated, immutable snapshot of engineering material values and provenance.</summary>
public sealed class MaterialCatalog : IDisposable
{
    public const int ExpectedSeedMaterialCount = 4;
    public const int CurrentSchemaVersion = 1;
    private readonly ResolvedMaterial[] materials;

    public MaterialCatalog(IEnumerable<MaterialEntity> entities, int schemaVersion = CurrentSchemaVersion)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new FormatException($"Unsupported material catalog schema {schemaVersion}.");
        var rows = entities.ToArray();
        var errors = MaterialDataValidator.Validate(rows);
        if (errors.Count > 0) throw new FormatException(string.Join(Environment.NewLine, errors));
        materials = rows.OrderBy(x => x.StableId, StringComparer.Ordinal).Select(Map).ToArray();
    }

    public int SchemaVersion => CurrentSchemaVersion;
    public static MaterialCatalog OpenDefault() => new(MaterialSeedData.Create());
    public IQueryable<MaterialCatalogItem> Materials => materials.Select(x => new MaterialCatalogItem
    {
        CatalogId = x.Identity.CatalogId, StableId = x.Identity.StableId,
        FirmamentPath = x.Identity.FirmamentPath, Family = x.Identity.Family,
        Designation = x.Identity.Designation, Grade = x.Identity.Grade,
        Temper = x.Identity.Temper, Standard = x.Identity.Standard,
        DisplayName = x.Identity.DisplayName, ConstitutiveClass = x.ConstitutiveClass,
        ReferenceCondition = x.ReferenceCondition,
    }).AsQueryable();

    public ResolvedMaterial? GetById(string catalogId, string stableId) => materials.SingleOrDefault(x => x.Identity.CatalogId == catalogId && x.Identity.StableId == stableId);
    public IReadOnlyList<ResolvedMaterial> FindByDesignation(string designation) => materials.Where(x => x.Identity.Designation == designation || x.Identity.Grade == designation).ToArray();
    internal IReadOnlyList<ResolvedMaterial> FindByReference(string reference) => materials.Where(x => x.Identity.FirmamentPath == reference || x.Identity.StableId == reference || (x.Identity.CatalogId + ":" + x.Identity.StableId) == reference).ToArray();

    // Retained for callers that previously scoped the database-backed catalog.
    public void Dispose() { }

    private static ResolvedMaterial Map(MaterialEntity entity)
    {
        var values = entity.Properties.OrderBy(x => x.Kind).ToDictionary(x => x.Kind, x => new MaterialPropertyValue(x.Kind, x.ValueSi, x.UnitSymbol, new(x.SourceId, x.SourceUri, x.Authority, x.Condition, x.ReferenceTemperatureKelvin, x.Notes)));
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
            var normalized = reference.Trim();
            using var catalog = catalogFactory();
            var matches = catalog.FindByReference(normalized);
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
