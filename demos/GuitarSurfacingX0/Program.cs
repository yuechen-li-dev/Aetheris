using System.Diagnostics;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;

var source = args.Length > 0 ? args[0] : "fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm";
var output = args.Length > 1 ? args[1] : "artifacts/local/guitar-x0/timings.json";
var runs = new List<object>();
using var session = new FirmamentCompilationSession();
string? baselineUsd = null;
for (var i = 0; i < 3; i++)
{
    if (i == 2) session.CompileFile(source); // Populate outside the measured cached run.
    var total = Stopwatch.StartNew();
    var clock = Stopwatch.StartNew();
    var compiled = i < 2 ? new AssemblyM1Pipeline().CompileFile(source) : session.CompileFile(source);
    var compileMilliseconds = clock.Elapsed.TotalMilliseconds;
    if (!compiled.IsSuccess) throw new InvalidOperationException(string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
    clock.Restart();
    var mesh = AssemblyDisplayMeshExporter.Export(compiled);
    var displayMilliseconds = clock.Elapsed.TotalMilliseconds;
    clock.Restart();
    var usd = AssemblyUsdExporter.Serialize(compiled.Ir!, mesh);
    baselineUsd ??= usd;
    if (usd != baselineUsd) throw new InvalidOperationException("Incremental guitar output differs from the uncached USD baseline.");
    runs.Add(new
    {
        lane = i == 0 ? "cold-first-compile-in-fresh-process" : i == 1 ? "warm-recompile-in-same-process" : "incremental-session-recompile",
        reuse = compiled.Reuse,
        matchesUncachedUsd = usd == baselineUsd,
        compileMilliseconds, displayMilliseconds,
        serializationMilliseconds = clock.Elapsed.TotalMilliseconds,
        totalMilliseconds = total.Elapsed.TotalMilliseconds,
        definitions = mesh.Definitions.Count,
        productOccurrences = mesh.Occurrences.Count(o => o.DefinitionId is not null),
        trianglesAcrossSharedDefinitions = mesh.Definitions.Sum(d => d.Indices.Length / 3),
        usdCharacters = usd.Length
    });
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
var json = JsonSerializer.Serialize(new { source, methodology = "Cold includes first-use JIT/library initialization. Warm is uncached; incremental uses a populated in-memory session and still reimports fresh exact bodies. Process startup and dotnet build are excluded.", runs }, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(output, json + "\n");
Console.WriteLine(json);
