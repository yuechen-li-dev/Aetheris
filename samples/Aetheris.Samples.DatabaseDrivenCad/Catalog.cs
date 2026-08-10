using Microsoft.EntityFrameworkCore;

namespace Aetheris.Samples.DatabaseDrivenCad;

public sealed class Material
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Grade { get; set; }
    public bool IsAluminum { get; set; }
    public ICollection<BearingBlockConfiguration> Configurations { get; } = [];
}

public sealed class DrawingMetadata
{
    public int Id { get; set; }
    public required string Company { get; set; }
    public required string Author { get; set; }
    public required string Description { get; set; }
    public ICollection<BearingBlockConfiguration> Configurations { get; } = [];
}

public sealed class BearingBlockConfiguration
{
    public int Id { get; set; }
    public required string PartNumber { get; set; }
    public double WidthMillimeters { get; set; }
    public double HeightMillimeters { get; set; }
    public double DepthMillimeters { get; set; }
    public double BoreDiameterMillimeters { get; set; }
    public double BoreTolerancePlusMillimeters { get; set; }
    public double BoreToleranceMinusMillimeters { get; set; }
    public int RevisionMajor { get; set; }
    public int RevisionMinor { get; set; }
    public int RevisionPatch { get; set; }
    public bool IsProduction { get; set; }
    public bool IsCurrent { get; set; }
    public int MaterialId { get; set; }
    public required Material Material { get; set; }
    public int DrawingMetadataId { get; set; }
    public required DrawingMetadata DrawingMetadata { get; set; }
}

public sealed class ProductCatalogContext(DbContextOptions<ProductCatalogContext> options) : DbContext(options)
{
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<DrawingMetadata> DrawingMetadata => Set<DrawingMetadata>();
    public DbSet<BearingBlockConfiguration> BearingBlockConfigurations => Set<BearingBlockConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>().HasIndex(item => item.Grade).IsUnique();
        modelBuilder.Entity<BearingBlockConfiguration>().HasIndex(item => item.PartNumber).IsUnique();
        modelBuilder.Entity<BearingBlockConfiguration>()
            .HasOne(item => item.Material).WithMany(item => item.Configurations).HasForeignKey(item => item.MaterialId);
        modelBuilder.Entity<BearingBlockConfiguration>()
            .HasOne(item => item.DrawingMetadata).WithMany(item => item.Configurations).HasForeignKey(item => item.DrawingMetadataId);
    }
}

public static class ProductCatalog
{
    public static ProductCatalogContext Open(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var options = new DbContextOptionsBuilder<ProductCatalogContext>()
            .UseSqlite($"Data Source={fullPath};Pooling=False")
            .Options;
        return new ProductCatalogContext(options);
    }

    public static async Task SeedAsync(ProductCatalogContext database, CancellationToken cancellationToken = default)
    {
        await database.Database.EnsureDeletedAsync(cancellationToken);
        await database.Database.EnsureCreatedAsync(cancellationToken);

        var aluminum = new Material { Name = "Aluminium 6061-T6", Grade = "6061-T6", IsAluminum = true };
        var steel = new Material { Name = "Alloy steel 4140", Grade = "4140", IsAluminum = false };
        var drawing = new DrawingMetadata { Company = "Aster Works", Author = "CAD Automation", Description = "Production bearing block" };
        database.AddRange(aluminum, steel, drawing);
        database.BearingBlockConfigurations.AddRange(
            Config("AB-101", 80, 50, 12, 18, aluminum, drawing, 1, 0, 0, true, true),
            Config("AB-204", 100, 60, 16, 24, aluminum, drawing, 2, 1, 0, true, true),
            Config("AB-305", 120, 70, 20, 30, aluminum, drawing, 3, 0, 1, true, true),
            Config("SB-210", 100, 60, 18, 22, steel, drawing, 2, 0, 0, true, false));
        await database.SaveChangesAsync(cancellationToken);
    }

    public static IQueryable<BearingBlockConfiguration> WithRelations(ProductCatalogContext database) =>
        database.BearingBlockConfigurations.AsNoTracking().Include(item => item.Material).Include(item => item.DrawingMetadata);

    public static IQueryable<BearingBlockConfiguration> ProductionAluminum(ProductCatalogContext database, double minimumBoreMillimeters = 20) =>
        WithRelations(database)
            .Where(item => item.IsProduction && item.Material.IsAluminum && item.BoreDiameterMillimeters >= minimumBoreMillimeters)
            .OrderBy(item => item.BoreDiameterMillimeters).ThenBy(item => item.PartNumber);

    public static IQueryable<BearingBlockConfiguration> CurrentMajor(ProductCatalogContext database, int major) =>
        WithRelations(database).Where(item => item.IsCurrent && item.RevisionMajor == major).OrderBy(item => item.PartNumber);

    private static BearingBlockConfiguration Config(
        string partNumber, double width, double height, double depth, double bore,
        Material material, DrawingMetadata drawing, int major, int minor, int patch, bool production, bool current) => new()
    {
        PartNumber = partNumber,
        WidthMillimeters = width,
        HeightMillimeters = height,
        DepthMillimeters = depth,
        BoreDiameterMillimeters = bore,
        BoreTolerancePlusMillimeters = 0.05,
        BoreToleranceMinusMillimeters = 0.02,
        Material = material,
        DrawingMetadata = drawing,
        RevisionMajor = major,
        RevisionMinor = minor,
        RevisionPatch = patch,
        IsProduction = production,
        IsCurrent = current,
    };
}
