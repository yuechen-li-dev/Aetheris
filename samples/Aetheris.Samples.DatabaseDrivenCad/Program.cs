using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Aetheris.Samples.DatabaseDrivenCad;

var parsed = CommandLine.Parse(args);
if (parsed.Command is null) { CommandLine.Usage(); return 1; }
await using var database = ProductCatalog.Open(parsed.DatabasePath);

if (parsed.Command == "seed")
{
    await ProductCatalog.SeedAsync(database);
    Console.WriteLine($"Seeded 4 configurations in {Path.GetFullPath(parsed.DatabasePath)}");
    return 0;
}
if (!await database.Database.CanConnectAsync())
{
    Console.Error.WriteLine("Database is missing. Run 'seed' first.");
    return 2;
}

IQueryable<BearingBlockConfiguration> query = parsed.Command switch
{
    "list" => ProductCatalog.WithRelations(database).OrderBy(item => item.BoreDiameterMillimeters).ThenBy(item => item.PartNumber),
    "query" or "generate-query" or "generate-all" => ProductCatalog.ProductionAluminum(database),
    "show" or "generate" when parsed.PartNumber is not null => ProductCatalog.WithRelations(database).Where(item => item.PartNumber == parsed.PartNumber),
    _ => throw new ArgumentException($"Unknown or incomplete command '{parsed.Command}'."),
};

var queryStart = Stopwatch.GetTimestamp();
var rows = await query.ToListAsync();
var queryTime = Stopwatch.GetElapsedTime(queryStart);
if (rows.Count == 0)
{
    Console.Error.WriteLine(parsed.PartNumber is null ? "Query selected no configurations." : $"Unknown part number '{parsed.PartNumber}'.");
    return 3;
}

if (parsed.Command is "list" or "query" or "show")
{
    Console.WriteLine("PartNumber  Width  Height  Depth  Bore  Material  Revision");
    foreach (var row in rows)
        Console.WriteLine($"{row.PartNumber,-11} {row.WidthMillimeters,5:0.##}  {row.HeightMillimeters,6:0.##}  {row.DepthMillimeters,5:0.##}  {row.BoreDiameterMillimeters,4:0.##}  {row.Material.Grade,-8}  {row.RevisionMajor}.{row.RevisionMinor}.{row.RevisionPatch}");
    Console.WriteLine($"SQLite/LINQ: {queryTime.TotalMilliseconds:F2} ms; rows: {rows.Count}");
    return 0;
}

var generator = new BearingBlockGenerator();
var generated = rows.Select(row => generator.Generate(row, parsed.OutputPath)).ToArray();
BearingBlockGenerator.WriteManifest(parsed.OutputPath, generated);
foreach (var item in generated)
{
    Console.WriteLine($"{item.PartNumber}: {item.TemplateSpecialization}");
    Console.WriteLine($"  STEP {item.StepPath}");
    Console.WriteLine($"  SHA256 {item.StepSha256}");
    Console.WriteLine($"  mapping {item.MappingMilliseconds:F2} ms; compile+STEP {item.CompilationMilliseconds:F2} ms");
}
Console.WriteLine($"SQLite/LINQ: {queryTime.TotalMilliseconds:F2} ms; generated: {generated.Length}");
return 0;

internal sealed record CommandLine(string? Command, string? PartNumber, string DatabasePath, string OutputPath)
{
    public static CommandLine Parse(string[] args)
    {
        var positional = args.Where(value => !value.StartsWith("--", StringComparison.Ordinal)).ToArray();
        string Option(string name, string fallback)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        var command = positional.FirstOrDefault();
        var part = command is "show" or "generate" ? positional.Skip(1).FirstOrDefault() : null;
        return new(command, part, Option("--database", Path.Combine(Environment.CurrentDirectory, "products.sqlite")), Option("--output", Path.Combine(Environment.CurrentDirectory, "output")));
    }

    public static void Usage() => Console.WriteLine("Usage: database-cad <seed|list|query|show SKU|generate SKU|generate-all|generate-query> [--database PATH] [--output DIR]");
}
