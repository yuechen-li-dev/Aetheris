#:project ../Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Scene;

// Evidence orchestration only. Reviewed Firmament is the authoring authority.
var path = Path.GetFullPath(args.Length > 0 ? args[0] : "fixtures/Canonical/Scene/FactoryX0/factory.firmament");
var output = Path.GetFullPath(args.Length > 1 ? args[1] : "artifacts/local/factory-scene-x0");
Directory.CreateDirectory(output);
var source = File.ReadAllText(path);
var records = new List<object>();
using var retained = new FirmamentSceneSession();
CompiledScene Measure(string name,string text,bool requireReuse = false)
{
    var watch = Stopwatch.StartNew();
    var compiled = retained.Compile(text,path);
    var ms = watch.Elapsed.TotalMilliseconds;
    if (!compiled.IsSuccess) throw new InvalidOperationException(string.Join("\n",compiled.Diagnostics));
    var scene = compiled.Scene!;
    if (requireReuse && (scene.Performance.RebuiltEngineeringDefinitions != 0 ||
        scene.Performance.ReusedEngineeringDefinitions != scene.Performance.UniqueEngineeringDefinitions))
        throw new InvalidOperationException(name + " rebuilt geometry.");
    records.Add(new { name, milliseconds=ms, scene.Performance });
    return scene;
}
var cold = Measure("cold-session",source);
Measure("retained-unchanged",source,true);
foreach (var (name,before,after) in new[] {
    ("camera-only","[32m, 3.5m, 4.5m]","[31.5m, 3.5m, 4.5m]"),
    ("layout-pitch-only","step: [0mm, 7000mm, 0mm]","step: [0mm, 7100mm, 0mm]"),
    ("appearance-only","color: [.43, .49, .53]","color: [.45, .5, .54]") })
{
    if (!source.Contains(before,StringComparison.Ordinal)) throw new InvalidOperationException("Missing edit specimen: "+name);
    var edited = Measure(name,source.Replace(before,after,StringComparison.Ordinal),true);
    foreach (var d in cold.Display.Definitions)
    {
        var same = edited.Display.Definitions.Single(v => v.Id == d.Id);
        if (!d.Positions.SequenceEqual(same.Positions) || !d.Indices.SequenceEqual(same.Indices))
            throw new InvalidOperationException(name + " changed geometry.");
    }
}
var formatted = Aetheris.Kernel.Firmament.FirmamentV2.FirmamentLanguageAnalysisService.Format(source,path,"1").Text;
if (formatted != source) throw new InvalidOperationException("Factory source needs preferred-spelling cleanup.");
var report = new { milestone="FACTORY-SCENE-X0", builds=records,
    displayDefinitions=cold.Display.Definitions.Count, displayNodes=cold.Display.Occurrences.Count,
    meshOccurrences=cold.Display.Occurrences.Count(o => o.DefinitionId is not null),
    sources=Directory.GetFiles(Path.GetDirectoryName(path)!,"*.firmament").OrderBy(p => p,StringComparer.Ordinal)
        .Select(p => new { file=Path.GetFileName(p),sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
File.WriteAllText(Path.Combine(output,"timings.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true })+"\n");
Console.WriteLine(JsonSerializer.Serialize(report));
