using Aetheris.Kernel.StandardLibrary.Materials.Database;
using Aetheris.Kernel.StandardLibrary.Materials.Database.CompiledModel;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Kernel.StandardLibrary.Materials;

/// <summary>Optional SQLite interoperability. The default catalog needs no database.</summary>
public static class SqliteMaterialCatalog
{
    public static MaterialCatalog Open(string databasePath)
    {
        var options = new DbContextOptionsBuilder<MaterialCatalogDbContext>()
            .UseSqlite($"Data Source={Path.GetFullPath(databasePath)};Mode=ReadOnly;Pooling=False")
            .UseModel(MaterialCatalogDbContextModel.Instance).Options;
        using var database = new MaterialCatalogDbContext(options);
        var version = database.CatalogMetadata.AsNoTracking().Single(x => x.CatalogId == "standard").SchemaVersion;
        return new MaterialCatalog(database.Materials.AsNoTracking().Include(x => x.Properties).ToArray(), version);
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
        var options = new DbContextOptionsBuilder<MaterialCatalogDbContext>()
            .UseSqlite($"Data Source={fullPath};Pooling=False")
            .UseModel(MaterialCatalogDbContextModel.Instance)
            .Options;
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
