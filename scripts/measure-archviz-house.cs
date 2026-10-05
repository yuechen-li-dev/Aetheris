#:project ../Aetheris.Kernel.Firmament/Aetheris.Kernel.Firmament.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Evidence orchestration only. Layout and furniture remain in reviewed Firmament.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Firmament.Scene;

var fixture = Path.GetFullPath(args.Length > 0 ? args[0] : "fixtures/Canonical/Scene/WarmModernHouse/house.firmament");
var output = Path.GetFullPath(args.Length > 1 ? args[1] : "artifacts/local/archviz-house-x0");
Directory.CreateDirectory(output);
var project = Path.Combine(output,"retained-evidence-project");
Directory.CreateDirectory(project);
foreach (var file in Directory.GetFiles(Path.GetDirectoryName(fixture)!,"*.firmament"))
    File.Copy(file,Path.Combine(project,Path.GetFileName(file)),true);
var path = Path.Combine(project,"house.firmament");
var source = File.ReadAllText(path);
var records = new List<object>();
using var retained = new FirmamentSceneSession();
CompiledScene Measure(string name,FirmamentSceneSession session,string text)
{
    var watch = Stopwatch.StartNew();
    var compiled = session.Compile(text,path);
    var milliseconds = watch.Elapsed.TotalMilliseconds;
    if (!compiled.IsSuccess) throw new InvalidOperationException(string.Join("\n",compiled.Diagnostics));
    var scene = compiled.Scene!;
    records.Add(new { name, milliseconds, scene.Performance,
        displayDefinitions=scene.Display.Definitions.Count, displayOccurrences=scene.Display.Occurrences.Count,
        meshOccurrences=scene.Display.Occurrences.Count(o => o.DefinitionId is not null) });
    return scene;
}
var cold = Measure("cold-new-process",retained,source);
using (var warm = new FirmamentSceneSession()) Measure("warm-uncached-session",warm,source);
Measure("retained-unchanged",retained,source);
void ReuseEdit(string name,string changed)
{
    var scene=Measure(name,retained,changed);
    if (scene.Performance.RebuiltEngineeringDefinitions != 0)
        throw new InvalidOperationException(name+" unexpectedly rebuilt engineering geometry.");
}
ReuseEdit("camera-only",source.Replace("[8.1m, .65m, 1.65m]","[8m, .65m, 1.65m]",StringComparison.Ordinal));
ReuseEdit("furniture-guide-only",source.Replace("[2500mm, 1450mm]","[2600mm, 1450mm]",StringComparison.Ordinal));
ReuseEdit("appearance-only",source.Replace("color: [.55, .35, .18]","color: [.58, .38, .2]",StringComparison.Ordinal));
ReuseEdit("room-size-only",source.Replace("size: [2.4m, 2.2m, 3.1m]","size: [2.5m, 2.2m, 3.1m]",StringComparison.Ordinal));
var island = Path.Combine(project,"island.firmament");
File.WriteAllText(island,File.ReadAllText(island).Replace("W:850mm,H:700mm","W:850mm,H:720mm",StringComparison.Ordinal));
var changedObject=Measure("one-island-body-definition",retained,source);
if (changedObject.Performance.RebuiltEngineeringDefinitions != 1 || changedObject.Performance.ReusedEngineeringDefinitions != cold.Performance.UniqueEngineeringDefinitions-1)
    throw new InvalidOperationException("A single island body edit must rebuild one definition and reuse every unchanged definition.");
var watchExport=Stopwatch.StartNew();
var usd=SceneExport.Usd(cold);
var usdMilliseconds=watchExport.Elapsed.TotalMilliseconds;
watchExport.Restart();
var glb=SceneExport.Glb(cold);
var glbMilliseconds=watchExport.Elapsed.TotalMilliseconds;
watchExport.Restart();
var cutaway=SceneExport.Glb(cold,["main.ceiling","main.southWall","main.eastWall","hall.ceiling","hall.southWall","hall.eastWall"]);
var cutawayMilliseconds=watchExport.Elapsed.TotalMilliseconds;
var tree=Directory.GetFiles(Path.GetDirectoryName(fixture)!,"*.firmament").OrderBy(p => p,StringComparer.Ordinal)
    .Select(p => new {file=Path.GetFileName(p),sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))}).ToArray();
var report=new { milestone="ARCHVIZ-HOUSE-X0", sources=tree, builds=records,
    usdExportMilliseconds=usdMilliseconds, glbExportMilliseconds=glbMilliseconds,
    cutawayGlbExportMilliseconds=cutawayMilliseconds, usdBytes=Encoding.UTF8.GetByteCount(usd), glbBytes=glb.Length };
File.WriteAllText(Path.Combine(output,"timings.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true })+"\n");
Console.WriteLine(JsonSerializer.Serialize(report));
