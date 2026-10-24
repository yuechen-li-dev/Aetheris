using Microsoft.EntityFrameworkCore;

namespace Aetheris.Kernel.StandardLibrary.Materials.Database;

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
