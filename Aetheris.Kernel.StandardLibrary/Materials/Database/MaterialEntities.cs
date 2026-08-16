using Microsoft.EntityFrameworkCore;

namespace Aetheris.Kernel.StandardLibrary.Materials.Database;

public sealed class MaterialEntity
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

public sealed class MaterialPropertyEntity
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

public sealed class MaterialCatalogMetadataEntity
{
    public int Id { get; set; }
    public int SchemaVersion { get; set; }
    public required string CatalogId { get; set; }
    public required string SeedVersion { get; set; }
}

public sealed class MaterialCatalogDbContext(DbContextOptions<MaterialCatalogDbContext> options) : DbContext(options)
{
    public const int SchemaVersion = 1;
    public DbSet<MaterialEntity> Materials => Set<MaterialEntity>();
    public DbSet<MaterialPropertyEntity> MaterialProperties => Set<MaterialPropertyEntity>();
    public DbSet<MaterialCatalogMetadataEntity> CatalogMetadata => Set<MaterialCatalogMetadataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var material = modelBuilder.Entity<MaterialEntity>();
        material.ToTable("Materials");
        material.HasIndex(x => new { x.CatalogId, x.StableId }).IsUnique();
        material.HasIndex(x => x.FirmamentPath).IsUnique();
        material.Property(x => x.ConstitutiveClass).HasConversion<string>();

        var property = modelBuilder.Entity<MaterialPropertyEntity>();
        property.ToTable("MaterialProperties");
        property.HasIndex(x => new { x.MaterialId, x.Kind }).IsUnique();
        property.Property(x => x.Kind).HasConversion<string>();
        property.Property(x => x.Authority).HasConversion<string>();
        property.HasOne(x => x.Material).WithMany(x => x.Properties).HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Cascade);

        var metadata = modelBuilder.Entity<MaterialCatalogMetadataEntity>();
        metadata.ToTable("CatalogMetadata");
        metadata.HasIndex(x => x.CatalogId).IsUnique();
    }
}
