using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Scene;

public sealed record SceneBoundary(string Path, string Room, string Kind, double[] Frame, double WidthMm, double HeightMm,
    IReadOnlyList<string> Openings);
public sealed record SceneNode(string Path, string Kind, string DefinitionIdentity, string PlacementAuthority,
    SemanticSourceSpan Span, string? PatternKey = null);
public sealed record ScenePerformance(double BindMilliseconds, double DefinitionCompilationMilliseconds, double CompositionMilliseconds,
    int UniqueEngineeringDefinitions, int ReusedEngineeringDefinitions, int RebuiltEngineeringDefinitions, double DisplayPreparationMilliseconds);
public sealed record CompiledScene(SceneSource Source, AssemblyDisplayMeshDocument Display, IReadOnlyList<SceneBoundary> Boundaries,
    IReadOnlyList<SceneNode> Nodes, IReadOnlyDictionary<string, AssemblyAppearanceBinding> Appearances,
    IReadOnlyList<AssemblyDefinitionReuse> Reuse, ScenePerformance Performance, double[] MinimumMm, double[] MaximumMm);
public sealed record SceneCompilationResult(CompiledScene? Scene, IReadOnlyList<AssemblyDiagnostic> Diagnostics)
{ public bool IsSuccess => Scene is not null && Diagnostics.All(d => d.Severity != AssemblyDiagnosticSeverity.Error); }

/// <summary>Retained scene composition reuses the existing immutable STEP definition cache.
/// Each independently engineered assembly keeps its own ordinary compiler/session.
/// Scene owns display/environment and occurrence state, never a BRep product.</summary>
public sealed class FirmamentSceneSession : IDisposable
{
    private readonly object gate = new();
    private readonly AssemblyDefinitionCache parts = new(256);
    private readonly Dictionary<string,FirmamentCompilationSession> assemblies = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;
    public SceneCompilationResult CompileFile(string path) => Compile(File.ReadAllText(path),Path.GetFullPath(path));
    public SceneCompilationResult Compile(string source, string sourceIdentity = "<memory>")
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            var watch = Stopwatch.StartNew(); var parsed = SceneAuthoring.Parse(source,sourceIdentity);
            if (!parsed.IsSuccess) return new(null,parsed.Diagnostics);
            var bindMs=watch.Elapsed.TotalMilliseconds; watch.Restart(); parts.BeginBuild();
            var s=parsed.Source!; var diagnostics=parsed.Diagnostics.ToList();
            var definitions=new Dictionary<string,AssemblyDisplayMeshDefinition>(StringComparer.Ordinal);
            var occurrences=new List<AssemblyDisplayMeshOccurrence>(); var nodes=new List<SceneNode>();
            var looks=new Dictionary<string,AssemblyAppearanceBinding>(StringComparer.Ordinal);
            var compiledAssemblies=new Dictionary<string,AssemblyM1CompilationResult>(StringComparer.OrdinalIgnoreCase);
            var preparedAssemblies=new Dictionary<string,AssemblyDisplayMeshDocument>(StringComparer.OrdinalIgnoreCase);
            double displayMs=0;
            var materializedParts=new Dictionary<string,MaterializedAssemblyDefinition>(StringComparer.Ordinal);
            foreach (var item in s.Occurrences)
            {
                if (item.Kind == "Assembly")
                {
                    var match=Regex.Match(item.Definition,"^AssemblyFile<\\s*\"(?<path>[^\"]+)\"\\s*>$");
                    if (!match.Success) { Error("scene-assembly-definition-invalid","X0 assembly occurrences require AssemblyFile<\"relative.firmament\">; their document compiles independently."); continue; }
                    var path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourceIdentity))!,match.Groups["path"].Value));
                    var allowedRoot=AssemblyM0Parser.FindAllowedSourceRoot(Path.GetFullPath(sourceIdentity));
                    var allowedPrefix=Path.TrimEndingDirectorySeparator(allowedRoot)+Path.DirectorySeparatorChar;
                    if (!path.StartsWith(allowedPrefix,StringComparison.OrdinalIgnoreCase))
                    { Error("scene-assembly-file-outside-root","AssemblyFile must stay inside the existing allowed source root."); continue; }
                    if (!path.EndsWith(".firmament",StringComparison.OrdinalIgnoreCase)) { Error("scene-assembly-file-invalid","AssemblyFile requires a .firmament source document."); continue; }
                    if (!compiledAssemblies.ContainsKey(path))
                    {
                        if (!File.Exists(path)) { Error("scene-assembly-file-missing",path); continue; }
                        if (!assemblies.TryGetValue(path,out var session))
                        {
                            if (assemblies.Count >= 256) { Error("scene-definition-session-capacity-exceeded","The retained Scene session supports at most 256 independent Assembly sources; start a fresh session."); continue; }
                            assemblies[path]=session=new();
                        }
                        var assembly=session.CompileFile(path); compiledAssemblies.Add(path,assembly);
                        diagnostics.AddRange(assembly.Diagnostics);
                    }
                }
                else if (!materializedParts.ContainsKey(item.Definition))
                {
                    var part=parts.Materialize(item.Definition,s.DefinitionSource,sourceIdentity,diagnostics,null,
                        () => AssemblyDefinitionMaterializer.TryMaterialize(item.Definition,s.DefinitionSource,sourceIdentity,diagnostics));
                    if (part is not null) materializedParts.Add(item.Definition,part);
                }
            }
            var definitionMs=watch.Elapsed.TotalMilliseconds; watch.Restart();
            if (diagnostics.Any(d => d.Severity == AssemblyDiagnosticSeverity.Error)) return new(null,diagnostics);
            var boundaries=s.Rooms.SelectMany(RoomBoundaries).ToArray();
            AddNode(s.Name,"Scene",s.Name,null,Transform3D.Identity.ToRowMajor(),null,new(sourceIdentity,0,source.Length));
            foreach (var room in s.Rooms)
            {
                var roomPath=s.Name+"."+room.Name; var transform=Transform3D.CreateTranslation(new(room.AtMm[0],room.AtMm[1],room.AtMm[2]));
                AddNode(roomPath,"Room",room.Name,s.Name,transform.ToRowMajor(),null,room.Span);
                foreach (var boundary in boundaries.Where(b => b.Room == room.Name))
                {
                    var wallPath=s.Name+"."+boundary.Path;
                    AddNode(wallPath,"SceneBoundary",boundary.Path,roomPath,transform.ToRowMajor(),null,room.Span);
                    foreach (var panel in Panels(room,boundary.Kind,s.Openings.Where(o => o.Boundary == boundary.Path).ToArray()))
                    {
                        var key="scene-box:"+string.Join(":",panel.Size.Select(F)); var id=Id(key);
                        if (!definitions.ContainsKey(id)) definitions.Add(id,Box(id,key,panel.Size));
                        var panelPath=wallPath+".panel"+panel.Index;
                        var world=Transform3D.CreateTranslation(new(panel.At[0],panel.At[1],panel.At[2]))*transform;
                        AddNode(panelPath,"EnvironmentPanel",key,wallPath,world.ToRowMajor(),id,room.Span);
                        ApplyLook(panelPath,room.Appearance);
                    }
                    foreach (var opening in s.Openings.Where(o => o.Boundary == boundary.Path))
                    {
                        var frame=Transform3D.FromRowMajor(boundary.Frame);
                        var aperture=Transform3D.CreateTranslation(new(opening.AlongMm,opening.SillMm,0))*frame;
                        AddNode(wallPath+"."+opening.Name,opening.Kind,opening.Name,wallPath,aperture.ToRowMajor(),null,opening.Span);
                    }
                }
            }
            foreach (var item in s.Occurrences)
            {
                // Existing frame composition owns rotation/basis/translation; no Mate is synthesized.
                Transform3D target;
                if (item.Placement.From == "World") target=Transform3D.Identity;
                else if (boundaries.SingleOrDefault(b => b.Path == item.Placement.From) is { } boundary) target=Transform3D.FromRowMajor(boundary.Frame);
                else { Error("scene-frame-unresolved",$"Unknown Scene target frame '{item.Placement.From}'."); continue; }
                var world=AssemblyFrameAuthoring.Compose(target,item.Placement,diagnostics);
                var segments=item.Path.Split('.');
                for (var n=2;n<segments.Length;n++)
                {
                    var group=string.Join('.',segments.Take(n));
                    if (!occurrences.Any(o => o.Path == group)) AddNode(group,"PatternGroup",group,string.Join('.',segments.Take(n-1)),Transform3D.Identity.ToRowMajor(),null,item.Span,item.PatternKey);
                }
                var parent=string.Join('.',segments.SkipLast(1));
                if (item.Kind == "Part")
                {
                    if (item.From != "Origin") { Error("scene-part-source-frame-unsupported","X0 Part Scene placement starts at Origin; assembly published source frames are supported."); continue; }
                    var part=materializedParts[item.Definition]; var id=Id("part:"+item.Definition);
                    if (!definitions.ContainsKey(id))
                    { var preparation=Stopwatch.StartNew(); definitions.Add(id,AssemblyDisplayMeshExporter.PrepareDefinition(id,item.Definition,part.Body)); displayMs+=preparation.Elapsed.TotalMilliseconds; }
                    AddNode(item.Path,"Part",item.Definition,parent,world.ToRowMajor(),id,item.Span,item.PatternKey); ApplyLook(item.Path,item.Appearance);
                }
                else
                {
                    var match=Regex.Match(item.Definition,"\"(?<path>[^\"]+)\"");
                    var path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourceIdentity))!,match.Groups["path"].Value));
                    var assembly=compiledAssemblies[path];
                    if (!preparedAssemblies.TryGetValue(path,out var mesh))
                    { var preparation=Stopwatch.StartNew(); preparedAssemblies[path]=mesh=AssemblyDisplayMeshExporter.Export(assembly); displayMs+=preparation.Elapsed.TotalMilliseconds; }
                    if (item.From != "Origin")
                    {
                        var root=assembly.Ir!.Instances.Single(i => i.ParentStableId is null);
                        var reference=AssemblyPath.Parse(root.Path+"."+item.From);
                        if (!AssemblyM0Compiler.TryResolve(reference,assembly.Ir.Instances,out var resolved) || !resolved!.Value.TryBinding<ExactDatumFrameBinding>(out var binding))
                        { Error("scene-source-frame-unresolved",$"Assembly does not publish source frame '{item.From}'."); continue; }
                        world=AssemblyFrameAuthoring.Matrix(binding).Inverse()*world;
                    }
                    var prefix=Id(path); var rootPath=mesh.Occurrences.Single(o => o.ParentId is null).Path;
                    foreach (var d in mesh.Definitions)
                    {
                        var id=prefix+":"+d.Id;
                        if (!definitions.ContainsKey(id)) definitions.Add(id,d with { Id=id, Identity=path+"::"+d.Identity });
                    }
                    foreach (var o in mesh.Occurrences)
                    {
                        var scenePath=item.Path+o.Path[rootPath.Length..];
                        var parentPath=o.ParentId is null ? parent : item.Path+mesh.Occurrences.Single(p => p.Id == o.ParentId).Path[rootPath.Length..];
                        var instance=assembly.Ir!.Instances.Single(i => i.StableId == o.Id);
                        AddNode(scenePath,instance.Kind.ToString(),instance.DefinitionIdentity,parentPath,(Transform3D.FromRowMajor(o.Transform)*world).ToRowMajor(),o.DefinitionId is null ? null : prefix+":"+o.DefinitionId,item.Span,item.PatternKey);
                        if (instance.Appearance is { } look) looks[Id(scenePath)]=look;
                        ApplyLook(scenePath,item.Appearance);
                    }
                }
            }
            if (diagnostics.Any(d => d.Severity == AssemblyDiagnosticSeverity.Error)) return new(null,diagnostics);
            var display=new AssemblyDisplayMeshDocument("aetheris/scene-display-mesh/1",s.Name,"mm",definitions.Values.ToArray(),occurrences);
            var bounds=WorldBounds(display);
            var reuse=parts.Evidence.Concat(compiledAssemblies.Values.SelectMany(a => a.Reuse?.Definitions ?? [])).ToArray();
            var perf=new ScenePerformance(bindMs,definitionMs,watch.Elapsed.TotalMilliseconds-displayMs,reuse.Length,reuse.Count(r => r.Reused),reuse.Count(r => !r.Reused),displayMs);
            return new(new(s,display,boundaries,nodes,looks,reuse,perf,bounds.Min,bounds.Max),diagnostics);

            IEnumerable<SceneBoundary> RoomBoundaries(SceneRoom room)
            {
                var w=room.SizeMm[0]; var d=room.SizeMm[1]; var h=room.SizeMm[2];
                foreach (var (kind,frame,width,height) in new (string,double[],double,double)[] {
                    ("floor",[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],w,d),
                    ("ceiling",[1,0,0,0,0,1,0,0,0,0,1,0,0,0,h,1],w,d),
                    ("southWall",[1,0,0,0,0,0,1,0,0,-1,0,0,0,0,0,1],w,h),
                    ("northWall",[1,0,0,0,0,0,1,0,0,-1,0,0,0,d,0,1],w,h),
                    ("westWall",[0,1,0,0,0,0,1,0,1,0,0,0,0,0,0,1],d,h),
                    ("eastWall",[0,1,0,0,0,0,1,0,1,0,0,0,w,0,0,1],d,h) })
                {
                    var world=Transform3D.FromRowMajor(frame)*Transform3D.CreateTranslation(new(room.AtMm[0],room.AtMm[1],room.AtMm[2]));
                    var path=room.Name+"."+kind;
                    yield return new(path,room.Name,kind,world.ToRowMajor(),width,height,s.Openings.Where(o => o.Boundary == path).Select(o => o.Name).ToArray());
                }
            }
            void Error(string code,string message) => diagnostics.Add(new(code,message));
            void AddNode(string path,string kind,string definition,string? parent,double[] transform,string? def,SemanticSourceSpan span,string? key=null)
            { occurrences.Add(new(Id(path),path,parent is null ? null : Id(parent),def,transform,path)); nodes.Add(new(path,kind,definition,"SceneFrame",span,key)); }
            void ApplyLook(string path,string? look)
            { if (look is not null) looks[Id(path)]=new("","",look,s.Looks.Appearances[look].Preview,"scene-appearance"); }
        }
    }
    public void Dispose() { lock(gate) { foreach (var session in assemblies.Values) session.Dispose(); assemblies.Clear(); parts.Clear(); disposed=true; } }
    private static string F(double value) => value.ToString("R",CultureInfo.InvariantCulture);
    private static string Id(string value) => "scene:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24];
    private sealed record Panel(int Index,double[] At,double[] Size);
    private static IEnumerable<Panel> Panels(SceneRoom r,string wall,IReadOnlyList<SceneOpening> openings)
    {
        var w=r.SizeMm[0]; var d=r.SizeMm[1]; var h=r.SizeMm[2]; var t=r.ThicknessMm;
        if (wall is "floor" or "ceiling") { yield return new(0,[0,0,wall == "floor" ? -t : h],[w,d,t]); yield break; }
        var horizontal=wall is "southWall" or "northWall"; var width=horizontal ? w : d;
        var xs=new[] {0d,width}.Concat(openings.SelectMany(o => new[] {o.AlongMm,o.AlongMm+o.WidthMm})).Distinct().Order().ToArray();
        var zs=new[] {0d,h}.Concat(openings.SelectMany(o => new[] {o.SillMm,o.SillMm+o.HeightMm})).Distinct().Order().ToArray();
        var index=0;
        for(var x=0;x+1<xs.Length;x++) for(var z=0;z+1<zs.Length;z++)
        {
            if (openings.Any(o => (xs[x]+xs[x+1])/2 > o.AlongMm && (xs[x]+xs[x+1])/2 < o.AlongMm+o.WidthMm && (zs[z]+zs[z+1])/2 > o.SillMm && (zs[z]+zs[z+1])/2 < o.SillMm+o.HeightMm)) continue;
            yield return horizontal ? new(index++,[xs[x],wall == "southWall" ? -t : d,zs[z]],[xs[x+1]-xs[x],t,zs[z+1]-zs[z]])
                : new(index++,[wall == "westWall" ? -t : w,xs[x],zs[z]],[t,xs[x+1]-xs[x],zs[z+1]-zs[z]]);
        }
    }
    private static AssemblyDisplayMeshDefinition Box(string id,string identity,double[] s)
    {
        double[][] p=[[0,0,0],[s[0],0,0],[s[0],s[1],0],[0,s[1],0],[0,0,s[2]],[s[0],0,s[2]],[s[0],s[1],s[2]],[0,s[1],s[2]]];
        int[][] faces=[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]];
        double[][] normals=[[0,0,-1],[0,0,1],[0,-1,0],[0,1,0],[-1,0,0],[1,0,0]];
        var positions=new List<double>(); var ns=new List<double>(); var indices=new List<int>();
        for(var f=0;f<6;f++) { var start=positions.Count/3; foreach(var v in faces[f]) { positions.AddRange(p[v]); ns.AddRange(normals[f]); } indices.AddRange([start,start+1,start+2,start,start+2,start+3]); }
        return new(id,identity,positions.ToArray(),ns.ToArray(),indices.ToArray(),"SceneRectangularBoundary");
    }
    private static (double[] Min,double[] Max) WorldBounds(AssemblyDisplayMeshDocument d)
    {
        var min=new[] {double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity}; var max=new[] {double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity};
        var defs=d.Definitions.ToDictionary(v => v.Id);
        foreach(var o in d.Occurrences.Where(o => o.DefinitionId is not null))
        {
            var p=defs[o.DefinitionId!].Positions; var t=Transform3D.FromRowMajor(o.Transform);
            for(var i=0;i<p.Length;i+=3) { var point=t.Apply(new Point3D(p[i],p[i+1],p[i+2])); var xyz=new[] {point.X,point.Y,point.Z}; for(var a=0;a<3;a++) { min[a]=Math.Min(min[a],xyz[a]); max[a]=Math.Max(max[a],xyz[a]); } }
        }
        return double.IsFinite(min[0]) ? (min,max) : (new double[3],new double[3]);
    }
}
