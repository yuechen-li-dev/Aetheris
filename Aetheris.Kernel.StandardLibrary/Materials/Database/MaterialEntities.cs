namespace Aetheris.Kernel.StandardLibrary.Materials.Database;

public class MaterialEntity
{
    public int Id { get; set; }
    public required string CatalogId { get; set; }
    public required string StableId { get; set; }
    public required string FirmamentPath { get; set; }
    public required string Family { get; set; }
    public required string Designation { get; set; }
    public string? Grade { get; set; }
    public string? Temper { get; set; }
    public string? Standard { get; set; }
    public required string DisplayName { get; set; }
    public MaterialConstitutiveClass ConstitutiveClass { get; set; }
    public required string ReferenceCondition { get; set; }
    public ICollection<MaterialPropertyEntity> Properties { get; } = [];
}

public class MaterialPropertyEntity
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public MaterialEntity Material { get; set; } = null!;
    public MaterialPropertyKind Kind { get; set; }
    public double ValueSi { get; set; }
    public required string UnitSymbol { get; set; }
    public required string SourceId { get; set; }
    public required string SourceUri { get; set; }
    public MaterialPropertyAuthority Authority { get; set; }
    public required string Condition { get; set; }
    public double? ReferenceTemperatureKelvin { get; set; }
    public string? Notes { get; set; }
}

public class MaterialCatalogMetadataEntity
{
    public int Id { get; set; }
    public int SchemaVersion { get; set; }
    public required string CatalogId { get; set; }
    public required string SeedVersion { get; set; }
}
