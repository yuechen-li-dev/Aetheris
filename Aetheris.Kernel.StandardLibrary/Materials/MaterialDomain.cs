namespace Aetheris.Kernel.StandardLibrary.Materials;

public enum MaterialConstitutiveClass
{
    LinearElasticIsotropic,
    Orthotropic,
    TemperatureDependent,
    ElasticPlastic,
}

public enum MaterialPropertyKind
{
    Density,
    YoungsModulus,
    PoissonsRatio,
    ShearModulus,
    YieldStrength,
    UltimateTensileStrength,
    ThermalConductivity,
    SpecificHeat,
    CoefficientOfThermalExpansion,
}

public enum MaterialPropertyAuthority
{
    ManufacturerTypical,
    StandardMinimum,
    IndustryReferenceNominal,
    SupplierCertified,
}

public sealed record MaterialIdentity(
    string CatalogId,
    string StableId,
    string FirmamentPath,
    string Family,
    string Designation,
    string? Grade,
    string? Temper,
    string? Standard,
    string DisplayName);

public sealed record MaterialPropertyProvenance(
    string SourceId,
    string SourceUri,
    MaterialPropertyAuthority Authority,
    string Condition,
    double? ReferenceTemperatureKelvin,
    string? Notes);

/// <summary>A scalar in the canonical SI unit named by <see cref="UnitSymbol"/>.</summary>
public sealed record MaterialPropertyValue(
    MaterialPropertyKind Kind,
    double SiValue,
    string UnitSymbol,
    MaterialPropertyProvenance Provenance);

public sealed record StructuralMaterialProperties(
    MaterialPropertyValue Density,
    MaterialPropertyValue YoungsModulus,
    MaterialPropertyValue PoissonsRatio,
    MaterialPropertyValue YieldStrength,
    MaterialPropertyValue UltimateTensileStrength,
    MaterialPropertyValue? ShearModulus);

public sealed record ThermalMaterialProperties(
    MaterialPropertyValue? ThermalConductivity,
    MaterialPropertyValue? SpecificHeat,
    MaterialPropertyValue? CoefficientOfThermalExpansion)
{
    public bool HasAny => ThermalConductivity is not null || SpecificHeat is not null || CoefficientOfThermalExpansion is not null;
}

public sealed record ResolvedMaterial(
    MaterialIdentity Identity,
    MaterialConstitutiveClass ConstitutiveClass,
    string ReferenceCondition,
    StructuralMaterialProperties? Structural,
    ThermalMaterialProperties Thermal,
    IReadOnlyDictionary<MaterialPropertyKind, MaterialPropertyValue> Properties);

public sealed class MaterialCatalogItem
{
    public required string CatalogId { get; init; }
    public required string StableId { get; init; }
    public required string FirmamentPath { get; init; }
    public required string Family { get; init; }
    public required string Designation { get; init; }
    public string? Grade { get; init; }
    public string? Temper { get; init; }
    public string? Standard { get; init; }
    public required string DisplayName { get; init; }
    public MaterialConstitutiveClass ConstitutiveClass { get; init; }
    public required string ReferenceCondition { get; init; }
}

public enum MaterialResolutionError
{
    UnknownMaterial,
    AmbiguousMaterial,
    MissingRequiredStructuralProperty,
    InvalidMaterialData,
}

public sealed record MaterialResolutionResult(ResolvedMaterial? Material, MaterialResolutionError? Error, string? Message)
{
    public bool IsSuccess => Material is not null && Error is null;
    public static MaterialResolutionResult Success(ResolvedMaterial material) => new(material, null, null);
    public static MaterialResolutionResult Failure(MaterialResolutionError error, string message) => new(null, error, message);
}
