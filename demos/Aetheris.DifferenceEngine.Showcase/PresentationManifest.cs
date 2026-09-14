using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.DifferenceEngine.Showcase;

public sealed record PresentationBinding(string OccurrenceId,string Kind,double[] Pivot,double Ratio,int Register,int Digit,string Role);
public sealed record PresentationGearLink(string A,string B,double Ratio,int TeethA,int TeethB,double CenterDistanceMm,string Interface);
public sealed record ExplosionBinding(string OccurrenceId,double[] Offset);
public sealed record DemonstrationState(int Result,int FirstDifference,int SecondDifference);
public sealed record DemonstrationProgram(string Id,string Name,IReadOnlyList<DemonstrationState> States);

public static class PresentationManifest
{
    private static readonly JsonSerializerOptions Json = new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true};
    public static void Write(string output,ShowcaseDesign design,AssemblyM1CompilationResult compiled,AssemblyDisplayMeshDocument mesh)
    {
        var ir=compiled.Ir!;var byPath=mesh.Occurrences.ToDictionary(o=>o.Path,StringComparer.Ordinal);
        var byId=mesh.Occurrences.ToDictionary(o=>o.Id,StringComparer.Ordinal);
        var definitions=mesh.Definitions.ToDictionary(d=>d.Id,StringComparer.Ordinal);
        var gearAir=GearAuthoring.ParseDefinitions(File.ReadAllText(Path.Combine(output,"source/parts/Showcase.firmament"))).Gears.ToDictionary(g=>g.Name,StringComparer.Ordinal);
        var links=new List<PresentationGearLink>();
        void Add(MateIr mate,string? localRoot=null,string? occurrencePath=null) {
            if(mate.GearResult is not {Ratio:{} ratio} g)return;
            string Resolve(string id)=>localRoot is null?id:"assembly-instance:"+occurrencePath+id[("assembly-instance:"+localRoot).Length..];
            var a=Resolve(g.A.OccurrenceIdentity);var b=Resolve(g.B.OccurrenceIdentity);
            if(!byId.ContainsKey(a)||!byId.ContainsKey(b))throw new InvalidOperationException("presentation-gear-endpoint-missing");
            links.Add(new(a,b,ratio*g.RotationSign,g.A.Teeth!.Value,g.B.Teeth!.Value,g.ActualCenterDistanceMm,mate.Name));
        }
        foreach(var mate in ir.Mates)Add(mate);
        foreach(var definition in ir.AssemblyDefinitions!)foreach(var occurrence in ir.Instances.Where(i=>i.DefinitionIdentity==definition.DefinitionIdentity))
            foreach(var mate in definition.LocalMates)Add(mate,definition.TemplateName,occurrence.Path.ToString());
        links=links.DistinctBy(l=>l.A+"|"+l.B).OrderBy(l=>l.A,StringComparer.Ordinal).ThenBy(l=>l.B,StringComparer.Ordinal).ToList();
        var ratios=new Dictionary<string,double>(StringComparer.Ordinal);
        var seeds=new[]{"assembly-instance:DifferenceEngine.MainCrank.DriveGear"}
            .Concat(Enumerable.Range(0,4).Select(d=>$"assembly-instance:DifferenceEngine.ResultRegister.Digit{d}.DriveGear"));
        foreach(var seed in seeds) {
            if(ratios.ContainsKey(seed))continue;
            ratios[seed]=1;var queue=new Queue<string>();queue.Enqueue(seed);
            while(queue.TryDequeue(out var current))foreach(var edge in links.Where(l=>l.A==current||l.B==current)) {
                var next=edge.A==current?edge.B:edge.A;var ratio=ratios[current]*(edge.A==current?edge.Ratio:1/edge.Ratio);
                if(ratios.TryGetValue(next,out var existing)){if(double.Abs(existing-ratio)>1e-9)throw new InvalidOperationException("presentation-gear-cycle-conflict");}
                else {ratios[next]=ratio;queue.Enqueue(next);}
            }
        }
        var bindings=new List<PresentationBinding>();
        var explosions=new List<ExplosionBinding>();
        foreach(var occurrence in mesh.Occurrences){
            var path=occurrence.Path;var name=path.Split('.').Last();
            var register=path.Contains(".ResultRegister",StringComparison.Ordinal)?0:path.Contains(".FirstDifferenceRegister",StringComparison.Ordinal)?1:path.Contains(".SecondDifferenceRegister",StringComparison.Ordinal)?2:-1;
            var match=Regex.Match(path,@"\.Digit([0-3])(?:\.|$)");var digit=match.Success?int.Parse(match.Groups[1].Value):-1;
            if(occurrence.DefinitionId is null){
                var offset=name switch {
                    "Frame"=>new[]{0d,110,0}, "MainCrank"=>new[]{-100d,0,0},
                    "ResultRegister"=>new[]{-90d,0,0},"SecondDifferenceRegister"=>new[]{90d,0,0},
                    "Transfer0" or "Transfer1"=>new[]{0d,-100,0}, "Carry"=>new[]{42d,20,0}, "Wheel"=>new[]{0d,0,18},
                    _ when name.StartsWith("Digit",StringComparison.Ordinal)&&digit>=0=>new[]{0d,0,(digit-1.5)*32}, _=>new[]{0d,0,0}};
                explosions.Add(new(occurrence.Id,offset));continue;
            }
            var pivot=new[]{occurrence.Transform[12],occurrence.Transform[13],occurrence.Transform[14]};
            var kind="Static";var ratio=0d;var role=path.Contains(".Frame.",StringComparison.Ordinal)?"Frame":"Structure";
            if(gearAir.TryGetValue(definitions[occurrence.DefinitionId].Identity,out var gear))role=gear.Family.ToString();
            if(ratios.TryGetValue(occurrence.Id,out ratio))kind="GearRatio";
            else if(path.Contains(".Wheel.",StringComparison.Ordinal)){
                kind="IndexedWheel";var owner=byPath[path[..path.IndexOf(".Wheel.",StringComparison.Ordinal)]];
                pivot=[owner.Transform[12],owner.Transform[13],owner.Transform[14]];
            }
            else if(register>=0&&name=="Shaft"){kind="GearRatio";ratio=ratios[$"assembly-instance:DifferenceEngine.{path.Split('.')[1]}.Digit0.DriveGear"];}
            else if(path.Contains(".MainCrank.",StringComparison.Ordinal)&&name is "Shaft" or "Arm" or "Handle") {kind="GearRatio";ratio=1;pivot=[-88,0,0];}
            else if(path.Contains(".Carry.",StringComparison.Ordinal)&&name is "Pawl" or "Lever") {
                kind="CarryLever";var pin=byPath[path[..path.LastIndexOf('.')]+".Pivot"];
                pivot=[pin.Transform[12],pin.Transform[13],pin.Transform[14]+36];
            }
            else if(path.Contains(".Carry.",StringComparison.Ordinal)&&name is "ResetLink" or "LinkPin")kind="CarryLift";
            bindings.Add(new(occurrence.Id,kind,pivot,ratio,register,digit,role));
        }
        if(bindings.Count!=compiled.Geometry!.InstanceBodies.Count)throw new InvalidOperationException("presentation-missing-occurrence");
        var gantryClearances=Enumerable.Range(0,3).Select(r=>{
            var registerName=new[]{"ResultRegister","FirstDifferenceRegister","SecondDifferenceRegister"}[r];
            var crown=compiled.Geometry.Artifact.Instances.Single(i=>i.InstanceStableId==$"assembly-instance:DifferenceEngine.{registerName}.Crown");
            var bridge=compiled.Geometry.Artifact.Instances.Single(i=>i.InstanceStableId==$"assembly-instance:DifferenceEngine.Frame.CrownBridge{r*128}");
            var shaft=compiled.Geometry.Artifact.Instances.Single(i=>i.InstanceStableId==$"assembly-instance:DifferenceEngine.{registerName}.Shaft");
            var clearance=bridge.Metrics.Minimum[2]-crown.Metrics.Maximum[2];
            if(clearance<9.999999||shaft.Metrics.Maximum[2]<bridge.Metrics.Maximum[2]+3.999999)
                throw new InvalidOperationException($"showcase-gantry-clearance:{registerName}:{clearance}");
            return new{register=registerName,crownOccurrenceId=crown.InstanceStableId,bridgeOccurrenceId=bridge.InstanceStableId,clearanceMm=clearance,shaftProjectionMm=shaft.Metrics.Maximum[2]-bridge.Metrics.Maximum[2],placement="Interface<Fixed> GantryMount"+r};
        }).ToArray();
        foreach(var r in Enumerable.Range(0,3))foreach(var d in Enumerable.Range(0,4)) {
            var path=$"DifferenceEngine.{new[]{"ResultRegister","FirstDifferenceRegister","SecondDifferenceRegister"}[r]}.Digit{d}.Wheel.Drum";
            var drum=byPath[path];
            if(double.Abs(drum.Transform[12]-r*design.RegisterPitch)>1e-8||double.Abs(drum.Transform[14]-(design.FirstLevel+d*design.DigitPitch))>1e-8)
                throw new InvalidOperationException($"showcase-nested-placement:{path}");
        }
        var programs=new[]{
            new DemonstrationProgram("squares","Squares",Enumerable.Range(0,101).Select(n=>new DemonstrationState(n*n%10000,2*n+1,2)).ToArray()),
            new DemonstrationProgram("triangular","Triangular numbers",Enumerable.Range(0,101).Select(n=>new DemonstrationState(n*(n+1)/2,n+1,1)).ToArray()),
            new DemonstrationProgram("carry","Carry · 0099 → 0100",new[]{new DemonstrationState(99,1,0),new DemonstrationState(100,1,0)})};
        var manifest=new{schema="aetheris/difference-engine-presentation/1",authority="Prescribed choreography and demonstration sequences; not physical mechanism simulation",cycleSeconds=6,
            design,bindings,gearLinks=links,explosions,programs,
            drums=bindings.Where(b=>byId[b.OccurrenceId].Path.EndsWith(".Drum",StringComparison.Ordinal)).Select(b=>new{b.OccurrenceId,b.Register,b.Digit,radiusMm=34.25,labelZMm=22}),
            provenance=ir.SourceDependencies!.Select(d=>new{path=Path.GetRelativePath(output,d.Path).Replace('\\','/'),d.Sha256}).OrderBy(d=>d.path,StringComparer.Ordinal)};
        File.WriteAllText(Path.Combine(output,"motion.json"),JsonSerializer.Serialize(manifest,Json));
        var inventory=new{schema="aetheris/difference-engine-receipts/1",uniqueDefinitions=mesh.Definitions.Count,physicalOccurrences=bindings.Count,
            registers=3,digitModules=12,gears=bindings.Count(b=>b.Role=="SpurGear"),ratchets=bindings.Count(b=>b.Role=="RatchetGear"),pawls=bindings.Count(b=>b.Role=="Pawl"),
            shafts=bindings.Count(b=>Regex.IsMatch(byId[b.OccurrenceId].Path,@"\.(Shaft|.*Axle)$")),structuralMembers=bindings.Count(b=>b.Role is "Frame" or "Structure"&&b.Kind=="Static"),
            hierarchyDepth=ir.Instances.Max(i=>i.Path.Segments.Count),sourceFiles=ir.SourceDependencies!.Count,uniqueTriangles=mesh.Definitions.Sum(d=>d.Indices.Length/3),
            renderedTriangles=mesh.Occurrences.Where(o=>o.DefinitionId!=null).Sum(o=>definitions[o.DefinitionId!].Indices.Length/3),
            gearInterfaces=links.Count,gantryClearances,stepBytes=new FileInfo(Path.Combine(output,"difference-engine.step")).Length,
            aetherisVersion=typeof(AssemblyM1Pipeline).Assembly.GetName().Version!.ToString(),
            sharedModules=ir.AssemblyDefinitions!.Select(d=>new{definition=d.DefinitionIdentity,occurrences=ir.Instances.Count(i=>i.DefinitionIdentity==d.DefinitionIdentity)}),
            definitions=compiled.Geometry.Artifact.Definitions.Select(d=>new{d.StableId,d.DefinitionIdentity,d.StepSha256,d.Metrics}),
            receipts=new[]{"difference-engine.step","machine.mesh.json","motion.json"}.Select(f=>new{file=f,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output,f))))})};
        File.WriteAllText(Path.Combine(output,"receipts.json"),JsonSerializer.Serialize(inventory,Json));
        var files=Directory.GetFiles(Path.Combine(output,"source"),"*.firmament",SearchOption.AllDirectories).Concat(new[]{"difference-engine.step","machine.mesh.json","motion.json","design.json","receipts.json"}.Select(f=>Path.Combine(output,f))).Order(StringComparer.Ordinal);
        File.WriteAllText(Path.Combine(output,"hashes.json"),JsonSerializer.Serialize(files.ToDictionary(f=>Path.GetRelativePath(output,f).Replace('\\','/'),f=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))),Json));
    }
}
