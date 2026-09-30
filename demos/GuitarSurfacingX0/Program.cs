using System.Diagnostics;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Assembly;

var source = args.Length > 0 ? args[0] : "fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm";
var output = args.Length > 1 ? args[1] : "artifacts/local/guitar-x0/timings.json";
var runs = new List<object>();
for (var i = 0; i < 2; i++)
{
    var total = Stopwatch.StartNew();
    var clock = Stopwatch.StartNew();
    var compiled = new AssemblyM1Pipeline().CompileFile(source);
    var compileMilliseconds = clock.Elapsed.TotalMilliseconds;
    if (!compiled.IsSuccess) throw new InvalidOperationException(string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
    clock.Restart();
    var mesh = AssemblyDisplayMeshExporter.Export(compiled);
    var displayMilliseconds = clock.Elapsed.TotalMilliseconds;
    clock.Restart();
    var usd = AssemblyUsdExporter.Serialize(compiled.Ir!, mesh);
    runs.Add(new
    {
        lane = i == 0 ? "cold-first-compile-in-fresh-process" : "warm-recompile-in-same-process",
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
var json = JsonSerializer.Serialize(new { source, methodology = "Cold includes first-use JIT and library initialization; warm recompiles the same source without a persistent model cache. Neither includes dotnet build or process startup.", runs }, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(output, json + "\n");
Console.WriteLine(json);
