using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.FEA.Analysis;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Semantics;

namespace Aetheris.FEA.Firmament;

public sealed record FirmamentAnalysisResource(string Name,string ContentHash,BrepBody Body);
public sealed record FirmamentAnalysisCompilation(LinearElasticAnalysisIr? Analysis,IReadOnlyList<AnalysisDiagnostic> Diagnostics,TimeSpan CompilationTime)
{
    public bool IsSuccess=>Analysis is not null&&!Diagnostics.Any(item=>item.Severity==AnalysisDiagnosticSeverity.Error);
}

/// <summary>Bounded M5 parser/lowerer for declarative analysis blocks embedded in Firmament source.</summary>
public static class FirmamentAnalysisCompiler
{
    private static readonly Regex Header=new(@"\banalysis\s+LinearElastic\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",RegexOptions.CultureInvariant);
    public static FirmamentAnalysisCompilation Compile(string source,string? sourcePath=null,string? sourceDirectory=null,IReadOnlyDictionary<string,FirmamentAnalysisResource>? resources=null)
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();var diagnostics=new List<AnalysisDiagnostic>();var match=Header.Match(source);
        if(!match.Success)return Done(null,[Error("firmament-analysis-missing","No 'analysis LinearElastic' declaration was found.",sourcePath)],started);
        var open=source.IndexOf('{',match.Index);var close=MatchingBrace(source,open);if(close<0)return Done(null,[Error("firmament-analysis-malformed","Analysis block has no closing brace.",sourcePath)],started);
        var block=source[(open+1)..close];var analysisSpan=new AnalysisProvenance(sourcePath??"<memory>",match.Index,close-match.Index+1,match.Value);
        var stripped=source.Remove(match.Index,close-match.Index+1);
        var bodyName=Scalar(block,"body")??string.Empty;var resourceName=(Scalar(block,"bodyResource")??string.Empty).TrimStart('$');
        IContinuumRegion? region=null;string sourceKind;string? resourceHash=null;string? brepBodyId=null;string bodyId;IReadOnlyDictionary<string,string> semanticFaceIds=new Dictionary<string,string>();var namedRegions=new Dictionary<string,SemanticValue>(StringComparer.Ordinal);
        var sourceParse=FirmamentV2Parser.Parse(stripped,sourceDirectory);var directInline=sourceParse.Document?.Solids.SingleOrDefault(item=>item.Name==bodyName)?.InlineStep;
        if(resourceName.Length>0||directInline is not null)
        {
            bodyId=bodyName.Length>0?bodyName:resourceName;sourceKind="InlineStep";
            FirmamentAnalysisResource resource;
            if(resourceName.Length>0)
            {
                if(resources is null||!resources.TryGetValue(resourceName,out resource!)){diagnostics.Add(Error("firmament-analysis-resource-missing",$"InlineStep analysis resource '${resourceName}' is not bound.",sourcePath));return Done(null,diagnostics,started);}
            }
            else
            {
                var imported=Step242Importer.ImportBody(File.ReadAllText(directInline!.NormalizedPath));
                if(!imported.IsSuccess||imported.Value is null){diagnostics.Add(Error("firmament-analysis-inline-step-import-failed",string.Join("; ",imported.Diagnostics.Select(item=>item.Message)),sourcePath));return Done(null,diagnostics,started);}
                resource=new(bodyId,directInline.ContentHash,imported.Value);
                if(sourceParse.Document!.RecognizedRegions?.Count>0)
                {
                    var faceMap=new Dictionary<string,FaceId>(StringComparer.Ordinal);
                    foreach(var pair in directInline.TopologyMap.FaceEntityToFaceId){var token=pair.Value.StartsWith("face-",StringComparison.Ordinal)?pair.Value[5..]:pair.Value;if(int.TryParse(token,out var id))faceMap[pair.Key]=new(id);}
                    var semanticRoot=FirmamentSemanticValues.FromRecognizedRegions(sourceParse.Document,resource.Body,faceMap,directInline.ContentHash,new(sourcePath??"<memory>",0,stripped.Length)).Single(value=>value.Type.Name=="ImportedBody");
                    foreach(var member in semanticRoot.ExposedMembers)namedRegions[bodyId+"."+member.Key]=member.Value;
                }
            }
            var points=resource.Body.Topology.Vertices.Select(v=>resource.Body.TryGetVertexPoint(v.Id,out var point)?point:(Point3D?)null).Where(p=>p.HasValue).Select(p=>p!.Value).ToArray();
            if(points.Length==0){diagnostics.Add(Error("firmament-analysis-inline-step-bounds-unavailable","Imported STEP has no exact vertex positions for bounded M5 CIR recognition.",sourcePath));return Done(null,diagnostics,started);}
            var rawMin=new Point3D(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z));var rawMax=new Point3D(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));const double stepMeters=0.001;var size=(rawMax-rawMin)*stepMeters;var center=new Vector3D((rawMin.X+rawMax.X)*stepMeters/2,(rawMin.Y+rawMax.Y)*stepMeters/2,(rawMin.Z+rawMax.Z)*stepMeters/2);
            // M5's InlineStep seam admits an exact six-planar-face box; other imported topology fails explicitly.
            if(resource.Body.Topology.Faces.Count()!=6){diagnostics.Add(Error("firmament-analysis-inline-step-cir-unsupported","M5 InlineStep mechanics admits exact planar boxes; this imported body needs a broader BRep-to-CIR recognizer.",sourcePath));return Done(null,diagnostics,started);}
            var faceIds=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var binding in resource.Body.Bindings.FaceBindings)
            {
                var surface=resource.Body.Geometry.GetSurface(binding.SurfaceGeometryId);if(surface.Kind!=SurfaceGeometryKind.Plane||surface.Plane is not { } plane)continue;
                var n=plane.Normal.ToVector();string token=double.Abs(n.X)>0.9?(double.Abs(plane.Origin.X-rawMin.X)<double.Abs(plane.Origin.X-rawMax.X)?"x-min":"x-max"):double.Abs(n.Y)>0.9?(double.Abs(plane.Origin.Y-rawMin.Y)<double.Abs(plane.Origin.Y-rawMax.Y)?"y-min":"y-max"):(double.Abs(plane.Origin.Z-rawMin.Z)<double.Abs(plane.Origin.Z-rawMax.Z)?"z-min":"z-max");faceIds[token]=binding.FaceId.Value.ToString(CultureInfo.InvariantCulture);
            }
            region=new ExactBrepBoxContinuumRegion(new RegionId(bodyId+":inline-cir"),size.X,size.Y,size.Z,Transform3D.CreateTranslation(center),faceIds);semanticFaceIds=faceIds;resourceHash=resource.ContentHash;brepBodyId=resource.Body.Topology.Bodies.Single().Id.Value.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var parse=FirmamentV2Parser.Parse(stripped,sourceDirectory);
            var units=Regex.Match(stripped,@"\bunits\s+(?<u>[A-Za-z]+)",RegexOptions.CultureInvariant).Groups["u"].Value;
            var boxHeader=Regex.Match(stripped,$@"\bsolid\s+{Regex.Escape(bodyName)}\s*:\s*Box\s*\{{",RegexOptions.CultureInvariant);
            if(!boxHeader.Success){diagnostics.Add(Error("firmament-analysis-body-unsupported",$"Body '{bodyName}' is not a Firmament Box/box-with-hole solid.",sourcePath));return Done(null,diagnostics,started);}
            var boxOpen=stripped.IndexOf('{',boxHeader.Index);var boxClose=MatchingBrace(stripped,boxOpen);var sizeRaw=Numbers(Scalar(stripped[(boxOpen+1)..boxClose],"size"));
            if(sizeRaw.Length!=3){diagnostics.Add(Error("firmament-analysis-body-size-invalid",$"Body '{bodyName}' requires three Box dimensions.",sourcePath));return Done(null,diagnostics,started);}
            var scale=UnitScale(units);var size=sizeRaw.Select(value=>value*scale).ToArray();var bounds=new BoundingBox3D(new(0,0,0),new(size[0],size[1],size[2]));
            var modify=Regex.Match(stripped,$@"\bmodify\s+{Regex.Escape(bodyName)}\s*\{{",RegexOptions.CultureInvariant);double? holeRadius=null;
            if(modify.Success){var modifyOpen=stripped.IndexOf('{',modify.Index);var modifyClose=MatchingBrace(stripped,modifyOpen);var radiusMatch=Regex.Match(stripped[(modifyOpen+1)..modifyClose],@"\bradius\s*:\s*(?<v>[-+0-9.eE]+)",RegexOptions.CultureInvariant);if(radiusMatch.Success)holeRadius=Number(radiusMatch.Groups["v"].Value)*scale;}
            region=holeRadius is null?new AxisAlignedBoxRegion(new RegionId(bodyName+":cir"),bounds):new BlockWithCylindricalHoleRegion(new RegionId(bodyName+":cir"),bounds,holeRadius.Value);
            if(!parse.IsSuccess) diagnostics.Add(new("firmament-analysis-geometry-parser-fallback",AnalysisDiagnosticSeverity.Warning,"The bounded analysis body recognizer admitted Box/through-cylinder syntax while the general geometry parser reported: "+string.Join(",",parse.Diagnostics),analysisSpan));
            bodyId=bodyName;sourceKind="FirmamentNative";
        }
        var materialBlock=Nested(block,"material",out var materialName,out var materialStart);if(materialBlock is null){diagnostics.Add(Error("fea-missing-material","Analysis has no material declaration.",sourcePath));return Done(null,diagnostics,started);}
        var young=Stress(Scalar(materialBlock,"youngsModulus"));var poisson=Number(Scalar(materialBlock,"poissonRatio"));var density=Density(Scalar(materialBlock,"density"));
        var materialProv=new AnalysisProvenance(sourcePath??"<memory>",open+materialStart,materialBlock.Length,"material "+materialName);
        var material=new LinearElasticMaterialIr(materialName,young,poisson,density,bodyId,materialProv);
        (SemanticRegionBinding? Region,AnalysisDiagnostic? Diagnostic) NormalizeRegion(string path,AnalysisProvenance provenance)
        {
            if(namedRegions.TryGetValue(path,out var semantic))
            {
                var span=new SemanticSourceSpan(provenance.Source,provenance.Start,provenance.Length);var normalized=AnalysisSemanticRegionNormalizer.Normalize(new(semantic,[new(path,span)],span));
                return normalized.Diagnostic is null?(normalized.Region,null):(null,new(normalized.Diagnostic.Code,AnalysisDiagnosticSeverity.Error,normalized.Diagnostic.Message,provenance));
            }
            return FirmamentAnalysisSemanticProducer.Normalize(path,bodyId,semanticFaceIds,provenance,sourceKind);
        }
        var constraints=new List<DisplacementConstraintIr>();
        foreach(var nested in NestedAll(block,"fixed"))
        {
            var path=Scalar(nested.Body,"region")??"";var components=Components(Scalar(nested.Body,"components"));if(components.Count==0)components=[DisplacementComponent.X,DisplacementComponent.Y,DisplacementComponent.Z];var prov=new AnalysisProvenance(sourcePath??"<memory>",open+nested.Start,nested.Length,"fixed "+nested.Name);
            var normalized=NormalizeRegion(path,prov);if(normalized.Diagnostic is not null){diagnostics.Add(normalized.Diagnostic);continue;}
            constraints.Add(new(nested.Name,normalized.Region!,components,new(0,0,0),prov));
        }
        var loads=new List<BoundaryLoadIr>();
        foreach(var keyword in new[]{("traction",BoundaryLoadKind.Traction),("force",BoundaryLoadKind.ResultantForce),("pressure",BoundaryLoadKind.Pressure)})foreach(var nested in NestedAll(block,keyword.Item1))
        {
            var path=Scalar(nested.Body,"region")??"";var prov=new AnalysisProvenance(sourcePath??"<memory>",open+nested.Start,nested.Length,keyword.Item1+" "+nested.Name);var vector=Vector(Scalar(nested.Body,"vector"),keyword.Item2==BoundaryLoadKind.ResultantForce);var pressure=keyword.Item2==BoundaryLoadKind.Pressure?Stress(Scalar(nested.Body,"value")):0;
            var normalized=NormalizeRegion(path,prov);if(normalized.Diagnostic is not null){diagnostics.Add(normalized.Diagnostic);continue;}
            loads.Add(new(nested.Name,keyword.Item2,normalized.Region!,vector,pressure,prov));
        }
        var latticeValues=Numbers(Scalar(block,"lattice"));var lattice=latticeValues.Length==3?new LatticeSpec(region.Bounds,(int)latticeValues[0],(int)latticeValues[1],(int)latticeValues[2]):new LatticeSpec(region.Bounds,12,6,2);
        var requested=(Scalar(block,"results")??"Displacement,Strain,Stress,ReactionForce").Trim('[',']').Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(Enum.Parse<AnalysisResultField>).ToHashSet();
        var bodyProv=new AnalysisProvenance(sourcePath??"<memory>",match.Index,match.Length,"body "+bodyId,ExactBrepFaceId:brepBodyId);var ir=new LinearElasticAnalysisIr(match.Groups["name"].Value,AnalysisKind.LinearStaticElasticity,new(bodyId,sourceKind,region,brepBodyId,resourceHash,bodyProv),[material],constraints,loads,requested,lattice,analysisSpan);
        diagnostics.AddRange(AnalysisIrValidator.Validate(ir));return Done(ir,diagnostics,started);
    }

    private sealed record NestedBlock(string Name,string Body,int Start,int Length);
    private static IEnumerable<NestedBlock> NestedAll(string source,string keyword){var regex=new Regex($@"\b{Regex.Escape(keyword)}\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{{",RegexOptions.CultureInvariant);foreach(Match m in regex.Matches(source)){var open=source.IndexOf('{',m.Index);var close=MatchingBrace(source,open);if(close>open)yield return new(m.Groups["name"].Value,source[(open+1)..close],m.Index,close-m.Index+1);}}
    private static string? Nested(string source,string keyword,out string name,out int start){var item=NestedAll(source,keyword).FirstOrDefault();name=item?.Name??"";start=item?.Start??0;return item?.Body;}
    private static string? Scalar(string source,string name){var m=Regex.Match(source,$@"(?m)^\s*{Regex.Escape(name)}\s*:\s*(?<value>[^\r\n}}]+)",RegexOptions.CultureInvariant);return m.Success?m.Groups["value"].Value.Trim():null;}
    private static int MatchingBrace(string source,int open){var depth=0;for(var i=open;i<source.Length;i++){if(source[i]=='{')depth++;else if(source[i]=='}'&&--depth==0)return i;}return-1;}
    private static double UnitScale(string unit)=>unit.ToLowerInvariant() switch{"m"=>1,"cm"=>.01,"mm"=>.001,_=>throw new FormatException($"Unsupported length unit '{unit}'.")};
    private static double Number(string? text)=>double.Parse(text??"NaN",NumberStyles.Float,CultureInfo.InvariantCulture);
    private static double Stress(string? text){if(text is null)return double.NaN;var m=Regex.Match(text,@"^(?<v>[-+0-9.eE]+)\s*(?<u>GPa|MPa|kPa|Pa)$",RegexOptions.IgnoreCase);if(!m.Success)return double.NaN;var v=Number(m.Groups["v"].Value);return v*(m.Groups["u"].Value.ToLowerInvariant() switch{"gpa"=>1e9,"mpa"=>1e6,"kpa"=>1e3,_=>1});}
    private static double? Density(string? text){if(string.IsNullOrWhiteSpace(text))return null;var m=Regex.Match(text,@"^(?<v>[-+0-9.eE]+)\s*kg/m3$",RegexOptions.IgnoreCase);return m.Success?Number(m.Groups["v"].Value):double.NaN;}
    private static double[] Numbers(string? text)=>Regex.Matches(text??"",@"[-+0-9.eE]+").Select(m=>Number(m.Value)).ToArray();
    private static Vector3D Vector(string? text,bool force){var parts=(text??"").Trim('[',']').Split(',',StringSplitOptions.TrimEntries);if(parts.Length!=3)return new(double.NaN,double.NaN,double.NaN);double Parse(string p){var m=Regex.Match(p,@"^(?<v>[-+0-9.eE]+)\s*(?<u>N|Pa)$",RegexOptions.IgnoreCase);return m.Success&&((force&&m.Groups["u"].Value.Equals("N",StringComparison.OrdinalIgnoreCase))||(!force&&m.Groups["u"].Value.Equals("Pa",StringComparison.OrdinalIgnoreCase)))?Number(m.Groups["v"].Value):double.NaN;}return new(Parse(parts[0]),Parse(parts[1]),Parse(parts[2]));}
    private static HashSet<DisplacementComponent> Components(string? text){var result=new HashSet<DisplacementComponent>();foreach(var item in (text??"").Trim('[',']').Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))if(Enum.TryParse<DisplacementComponent>(item,true,out var value))result.Add(value);return result;}
    private static FirmamentAnalysisCompilation Done(LinearElasticAnalysisIr? ir,IReadOnlyList<AnalysisDiagnostic> d,long started)=>new(ir,d,System.Diagnostics.Stopwatch.GetElapsedTime(started));
    private static AnalysisDiagnostic Error(string code,string message,string? source)=>new(code,AnalysisDiagnosticSeverity.Error,message,new(source??"<memory>",0,0,code));
}
