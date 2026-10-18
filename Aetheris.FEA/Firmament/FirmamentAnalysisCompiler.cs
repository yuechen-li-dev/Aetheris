using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Scope = Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxScope;
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
using Aetheris.Kernel.StandardLibrary.Materials;
using Aetheris.FEA.Geometry;
using Aetheris.SheetMetal;
using System.Security.Cryptography;
using System.Text;

namespace Aetheris.FEA.Firmament;

public sealed record FirmamentAnalysisResource(string Name,string ContentHash,BrepBody Body);
public sealed record FirmamentAnalysisCompilation(LinearElasticAnalysisIr? Analysis,IReadOnlyList<AnalysisDiagnostic> Diagnostics,TimeSpan CompilationTime)
{
    public bool IsSuccess=>Analysis is not null&&!Diagnostics.Any(item=>item.Severity==AnalysisDiagnosticSeverity.Error);
}

/// <summary>Firmament analysis-intent compiler. Geometry, materials and selections are fully resolved before solver execution.</summary>
public static class FirmamentAnalysisCompiler
{
    public static IReadOnlyList<Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic> KernelDiagnostics(FirmamentAnalysisCompilation compilation) =>
        compilation.Diagnostics.Select(d=>new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(
            Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            d.Severity==AnalysisDiagnosticSeverity.Error?Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error:Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Warning,
            d.Code+": "+d.Message,"Firmament.Analysis")
        {OriginalSourceSpan=d.Provenance is {Length:>0} span?new(span.Start,span.Length):null}).ToArray();
    public static bool IsAnalysisSource(string source) => source.Contains("LinearElastic", StringComparison.Ordinal)
        && FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Any(d => d.Type.Name.Text == "LinearElastic" && d.Modifiers.Any(m=>m.Name.Text is "Analysis" or "analysis"));
    public static FirmamentAnalysisCompilation Compile(string source,string? sourcePath=null,string? sourceDirectory=null,IReadOnlyDictionary<string,FirmamentAnalysisResource>? resources=null,IMaterialResolver? materialResolver=null)
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();var diagnostics=new List<AnalysisDiagnostic>();
        var binding=new FirmamentDomainBinding(source,sourcePath??"<memory>","LinearElastic",new HashSet<string>(StringComparer.Ordinal));
        diagnostics.AddRange(binding.Diagnostics.Select(d=>new AnalysisDiagnostic(d.Code,AnalysisDiagnosticSeverity.Error,d.Message,
            new(sourcePath??"<memory>",d.SourceSpan?.Start??0,d.SourceSpan?.Length??0,d.Target??"analysis admission"))));
        if(diagnostics.Count>0)return Done(null,diagnostics,started);
        source=binding.Source;
        var declarations=FirmamentSyntaxTraversal.Declarations(binding.Document).ToArray();
        var analyses=declarations.Where(d=>d.Type.Name.Text=="LinearElastic" && d.Modifiers.Any(m=>m.Name.Text is "Analysis" or "analysis")).ToArray();
        if(analyses.Length!=1)return Done(null,[Error("firmament-analysis-cardinality","Exactly one Analysis LinearElastic declaration is required.",sourcePath)],started);
        var declaration=analyses[0];
        var block=new Scope(declaration,source,sourcePath??"<memory>",binding.Nominal,binding.OriginalSpan).AllowModifier("Analysis").AllowModifier("analysis");
        var open=0;
        var analysisSpan=new AnalysisProvenance(sourcePath??"<memory>",declaration.Span.Start,declaration.Span.Length,"Analysis LinearElastic "+declaration.Name?.Text);
        var chars=source.ToCharArray();for(var i=declaration.Span.Start;i<declaration.Span.Start+declaration.Span.Length;i++)if(chars[i] is not ('\r' or '\n'))chars[i]=' ';
        var stripped=new string(chars);
        var modeText=Scalar(block,"mode")??nameof(AnalysisMode.ProductionSolid);
        if((!Enum.TryParse<AnalysisMode>(modeText,true,out var mode)||!Enum.IsDefined(mode))){diagnostics.Add(Error("fea-analysis-mode-invalid",$"Analysis mode '{modeText}' is unsupported.",sourcePath));return Done(null,diagnostics,started);}
        var bodyExpression=Scalar(block,"body")??string.Empty;
        var bodyValue=block.Take("Body");
        var inlineCall=bodyValue as CallSyntax;
        var inlinePath=inlineCall is {Arguments.Count:1} && FirmamentSyntaxValues.Reference(inlineCall.Target) is "inlineSTEP" or "InlineStep"
            && inlineCall.Arguments[0].Name is null && inlineCall.Arguments[0].Value is LiteralSyntax {Kind:LiteralKind.String}
            ? FirmamentSyntaxValues.Reference(inlineCall.Arguments[0].Value):null;
        var bodyName=inlinePath is not null?"body":bodyExpression;var resourceName=(Scalar(block,"bodyResource")??string.Empty).TrimStart('$');
        IContinuumRegion? region=null;string sourceKind;string? resourceHash=null;string? brepBodyId=null;string bodyId;IReadOnlyDictionary<string,string> semanticFaceIds=new Dictionary<string,string>();var namedRegions=new Dictionary<string,SemanticValue>(StringComparer.Ordinal);MidsurfacePatchGraph? midsurfacePatchGraph=null;SheetMetalPartIr? sheetMetalPart=null;
        var sourceParse=FirmamentV2Parser.Parse(stripped,sourceDirectory);var directInline=sourceParse.Document?.Solids.SingleOrDefault(item=>item.Name==bodyName)?.InlineStep;
        if(SheetMetalFirmament.LooksLikeSheetMetal(stripped))
        {
            if(mode!=AnalysisMode.ExperimentalShell){diagnostics.Add(Error("thinwall-mode-required","Native SheetMetal analysis requires explicit Mode: ExperimentalShell.",sourcePath));return Done(null,diagnostics,started);}
            var authored=SheetMetalFirmament.Compile(stripped,sourcePath??"<memory>");
            if(!authored.IsSuccess||authored.Part is null){diagnostics.AddRange(authored.Diagnostics.Select(x=>new AnalysisDiagnostic(x.Code,AnalysisDiagnosticSeverity.Error,x.Message,analysisSpan)));return Done(null,diagnostics,started);}
            sheetMetalPart=authored.Part;bodyId=bodyName;sourceKind=nameof(AnalysisGeometrySourceKind.NativeSheetMetal);
            var expectedBody=sheetMetalPart.StableId.StartsWith("sheetmetal-",StringComparison.Ordinal)?sheetMetalPart.StableId[11..]:sheetMetalPart.StableId;
            if(!bodyId.Equals(expectedBody,StringComparison.Ordinal)){diagnostics.Add(Error("thinwall-sheetmetal-body-mismatch",$"Analysis Body '{bodyId}' does not name native SheetMetal body '{expectedBody}'.",sourcePath));return Done(null,diagnostics,started);}
            var projected=ThinWallAnalysisAuthority.Project(sheetMetalPart,analysisSpan);diagnostics.AddRange(projected.Diagnostics);
            if(!projected.IsSuccess)return Done(null,diagnostics,started);
            midsurfacePatchGraph=projected.Graph;
            var combined=midsurfacePatchGraph!.Patches.Select(x=>x.ParameterDomain.Bounds).ToArray();
            var maxR=combined.Max(x=>x.Max.X-x.Min.X);var maxS=combined.Max(x=>x.Max.Y-x.Min.Y);
            region=new AxisAlignedBoxRegion(new RegionId(bodyId+":sheetmetal-analysis-projection"),new(new(0,0,-1),new(maxR,maxS,1)));
        }
        else if(resourceName.Length>0||directInline is not null||inlinePath is not null)
        {
            if(!sourceParse.IsSuccess && !AdmitResourceDocument(binding,source,sourcePath,declaration,diagnostics))return Done(null,diagnostics,started);
            bodyId=bodyName.Length>0?bodyName:resourceName;sourceKind="InlineStep";
            FirmamentAnalysisResource resource;
            if(resourceName.Length>0)
            {
                if(resources is null||!resources.TryGetValue(resourceName,out resource!)){diagnostics.Add(Error("firmament-analysis-resource-missing",$"InlineStep analysis resource '${resourceName}' is not bound.",sourcePath));return Done(null,diagnostics,started);}
            }
            else if(directInline is not null)
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
            else
            {
                var relativePath=inlinePath!;
                var normalizedPath=Path.GetFullPath(relativePath,sourceDirectory??Directory.GetCurrentDirectory());
                if(!File.Exists(normalizedPath)){diagnostics.Add(Error("firmament-analysis-inline-step-file-missing",$"InlineStep analysis file was not found: {normalizedPath}",sourcePath));return Done(null,diagnostics,started);}
                var stepText=File.ReadAllText(normalizedPath);var imported=Step242Importer.ImportBody(stepText);
                if(!imported.IsSuccess||imported.Value is null){diagnostics.Add(Error("firmament-analysis-inline-step-import-failed",string.Join("; ",imported.Diagnostics.Select(item=>item.Message)),sourcePath));return Done(null,diagnostics,started);}
                resource=new(bodyId,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stepText))).ToLowerInvariant(),imported.Value);
                var entityFaceIds=new Dictionary<string,string>(StringComparer.Ordinal);var ordinal=0;
                foreach(Match entity in Regex.Matches(stepText,@"(?m)^\s*(#[0-9]+)\s*=\s*ADVANCED_FACE\s*\(",RegexOptions.CultureInvariant))entityFaceIds[entity.Groups[1].Value]=(++ordinal).ToString(CultureInfo.InvariantCulture);
                semanticFaceIds=entityFaceIds;
            }
            var points=resource.Body.Topology.Vertices.Select(v=>resource.Body.TryGetVertexPoint(v.Id,out var point)?point:(Point3D?)null).Where(p=>p.HasValue).Select(p=>p!.Value).ToArray();
            if(points.Length==0){diagnostics.Add(Error("firmament-analysis-inline-step-bounds-unavailable","Imported STEP has no exact vertex positions for bounded M5 CIR recognition.",sourcePath));return Done(null,diagnostics,started);}
            var rawMin=new Point3D(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z));var rawMax=new Point3D(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));const double stepMeters=0.001;var size=(rawMax-rawMin)*stepMeters;var center=new Vector3D((rawMin.X+rawMax.X)*stepMeters/2,(rawMin.Y+rawMax.Y)*stepMeters/2,(rawMin.Z+rawMax.Z)*stepMeters/2);
            if(resource.Body.Topology.Faces.Count()==6&&resource.Body.Bindings.FaceBindings.All(binding=>resource.Body.Geometry.GetSurface(binding.SurfaceGeometryId).Kind==SurfaceGeometryKind.Plane))
            {
                var faceIds=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var faceBinding in resource.Body.Bindings.FaceBindings)
                {
                    var plane=resource.Body.Geometry.GetSurface(faceBinding.SurfaceGeometryId).Plane!.Value;var n=plane.Normal.ToVector();
                    var token=double.Abs(n.X)>0.9?(double.Abs(plane.Origin.X-rawMin.X)<double.Abs(plane.Origin.X-rawMax.X)?"x-min":"x-max")
                        :double.Abs(n.Y)>0.9?(double.Abs(plane.Origin.Y-rawMin.Y)<double.Abs(plane.Origin.Y-rawMax.Y)?"y-min":"y-max")
                        :(double.Abs(plane.Origin.Z-rawMin.Z)<double.Abs(plane.Origin.Z-rawMax.Z)?"z-min":"z-max");
                    faceIds[token]=faceBinding.FaceId.Value.ToString(CultureInfo.InvariantCulture);
                }
                region=new ExactBrepBoxContinuumRegion(new RegionId(bodyId+":imported-box-cir"),size.X,size.Y,size.Z,Transform3D.CreateTranslation(center),faceIds);
                semanticFaceIds=faceIds;
            }
            else
            {
                ImportedBrepAnalysisRegion importedRegion;
                try{importedRegion=new ImportedBrepAnalysisRegion(new RegionId(bodyId+":imported-brep-cir"),resource.Body,stepMeters);}
                catch(Exception exception) when(exception is NotSupportedException or InvalidOperationException or ArgumentException)
                {diagnostics.Add(Error("firmament-analysis-inline-step-cir-unsupported",$"Imported STEP cannot be lowered to the bounded analysis domain: {exception.Message}",sourcePath));return Done(null,diagnostics,started);}
                if(!importedRegion.TryValidateOccupancySupport(out var evidence)){diagnostics.Add(Error("firmament-analysis-inline-step-containment-unsupported",$"Imported STEP cannot provide deterministic occupied-material classification. {evidence}",sourcePath));return Done(null,diagnostics,started);}
                region=importedRegion;
            }
            resourceHash=resource.ContentHash;brepBodyId=resource.Body.Topology.Bodies.Single().Id.Value.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var parse=FirmamentV2Parser.Parse(stripped,sourceDirectory);
            var parsedSolid=parse.Document?.Solids.SingleOrDefault(item=>item.Name==bodyName);
            var parsedBox=parsedSolid?.Box;
            if(parsedSolid is not null && (parsedSolid.ConstructionPolicy!=FirmamentV2ConstructionPolicy.Solid || parsedSolid.Hollow is not null))
                return Done(null,[Error("firmament-analysis-native-feature-unsupported","Native analysis cannot replace a hollow body with a filled Box. Analyze the exported STEP body.",sourcePath)],started);
            var units=parse.Document?.Units??declarations.SelectMany(d=>d.Body?.Statements.OfType<FieldSyntax>()??[]).Where(f=>FirmamentSyntaxFields.SameField(f.Name.Text,"Units")).Select(f=>FirmamentSyntaxValues.Reference(f.Value)).FirstOrDefault()??"";
            double[] sizeRaw;
            if(parsedBox is not null)sizeRaw=parsedBox.Size.ToArray();
            else
            {
                var legacyBoxes=declarations.Where(d=>d.Type.Name.Text=="solid" && d.Name?.Text==bodyName && FirmamentSyntaxValues.Reference(d.Annotation)=="Box").ToArray();
                if(legacyBoxes.Length!=1){diagnostics.Add(Error("firmament-analysis-body-unsupported",$"Body '{bodyName}' is not an admitted Box body.",sourcePath));return Done(null,diagnostics,started);}
                var boxScope=new Scope(legacyBoxes[0] with {Annotation=null},source,sourcePath??"<memory>",binding.Nominal,binding.OriginalSpan);
                sizeRaw=Numbers(boxScope.Take("Size"));
                diagnostics.AddRange(boxScope.CompleteTree().Select(d=>new AnalysisDiagnostic(d.Code,AnalysisDiagnosticSeverity.Error,d.Message,analysisSpan)));
            }
            if(sizeRaw.Length!=3){diagnostics.Add(Error("firmament-analysis-body-size-invalid",$"Body '{bodyName}' requires three Box dimensions.",sourcePath));return Done(null,diagnostics,started);}
            var scale=UnitScale(units);if(scale is null){diagnostics.Add(Error("firmament-analysis-length-unit-unsupported",$"Analysis body length unit '{units}' is unsupported; use mm, cm, or m.",sourcePath));return Done(null,diagnostics,started);}
            var size=sizeRaw.Select(value=>value*scale.Value).ToArray();var bounds=new BoundingBox3D(new(0,0,0),new(size[0],size[1],size[2]));
            var legacyModify=declarations.Where(d=>d.Type.Name.Text=="modify" && d.Name?.Text==bodyName).ToArray();double? holeRadius=null;
            var legacy = declarations.Any(d=>d.Type.Name.Text=="solid" && FirmamentSyntaxValues.Reference(d.Annotation)=="Box");
            if(legacy && !AdmitLegacyGeometry(binding.Document,source,sourcePath,bodyName,declaration,diagnostics))return Done(null,diagnostics,started);
            Point3D? holeCenter = null;
            var modifications = parse.Document?.ModifyBlocks?.Where(item => item.TargetSolid == bodyName).ToArray() ?? [];
            var semanticHoles = modifications.SelectMany(item => item.SemanticHoles).ToArray();
            if (semanticHoles.Length > 0)
            {
                // Consume the parser's normalized engineering feature, not a textual radius
                // search: canonical Hole<Shaft> expresses Diameter and otherwise vanished.
                var hole = semanticHoles[0];
                if (semanticHoles.Length != 1 || modifications.Any(item => item.Regions.Count > 0 || item.EdgeFinishes?.Count > 0)
                    || hole.Variant != FirmamentV2SemanticHoleVariant.Shaft
                    || hole.EndCondition.Kind != FirmamentV2SemanticHoleEndKind.ThroughAll
                    || hole.EntryFace.Axis is not ("+Z" or "-Z")
                    || hole.Placement is FirmamentV2ConstructionPlaneHolePlacement)
                    return Done(null, [Error("firmament-analysis-native-feature-unsupported",
                        "Native analysis admits one face-local Z through-shaft hole in a Box. Analyze exported STEP for other feature combinations.", sourcePath)], started);
                holeRadius = hole.ShaftDiameter * scale.Value / 2;
                // Native analysis retains its established [0, size] coordinates. CAD's
                // face-local coordinates are relative to the center of the Box face.
                holeCenter = new(size[0] / 2 + hole.Center.U * scale.Value,
                    size[1] / 2 + hole.Center.V * scale.Value, size[2] / 2);
                if (!double.IsFinite(holeRadius.Value) || holeRadius <= 0
                    || holeCenter.Value.X - holeRadius <= 0 || holeCenter.Value.X + holeRadius >= size[0]
                    || holeCenter.Value.Y - holeRadius <= 0 || holeCenter.Value.Y + holeRadius >= size[1])
                    return Done(null, [Error("firmament-analysis-native-hole-outside", "The analysis hole must remain strictly inside the Box's XY boundary.", sourcePath)], started);
            }
            else if(legacyModify.Length>0)
            {
                var cuts=legacyModify.SelectMany(d=>FirmamentSyntaxTraversal.Declarations(d)).Where(d=>d.Type.Name.Text=="cut" && d.Name?.Text=="Cylinder").ToArray();
                if(cuts.Length!=1)return Done(null,[Error("firmament-analysis-native-feature-unsupported","Legacy native analysis admits exactly one through-cylinder cut.",sourcePath)],started);
                var cutScope=new Scope(cuts[0],source,sourcePath??"<memory>");var radius=cutScope.Take("Radius");var through=cutScope.Take("Through");
                if(!Face(through,"-Z"))cutScope.Invalid("Through","Legacy analysis cut must reach face(-Z).");
                if(!FirmamentSyntaxValues.TryMeasure(radius,"",out var r)||!double.IsFinite(r)||r<=0)cutScope.Invalid("Radius");
                holeRadius=r*scale.Value;
                diagnostics.AddRange(cutScope.CompleteTree().Select(d=>new AnalysisDiagnostic(d.Code,AnalysisDiagnosticSeverity.Error,d.Message,analysisSpan)));
            }
            if(semanticHoles.Length==0 && modifications.Length>0)return Done(null,[Error("firmament-analysis-native-feature-unsupported","Native analysis cannot omit Modify features; analyze exported STEP for this feature combination.",sourcePath)],started);
            region=holeRadius is null?new AxisAlignedBoxRegion(new RegionId(bodyName+":cir"),bounds):new BlockWithCylindricalHoleRegion(new RegionId(bodyName+":cir"),bounds,holeRadius.Value,holeCenter);
            if(!parse.IsSuccess && !legacy)
                return Done(null,parse.Diagnostics.Select(code=>Error(code,"Native V2 geometry must pass normal frontend admission; no analysis fallback is permitted.",sourcePath)).ToArray(),started);
            bodyId=bodyName;sourceKind="FirmamentNative";
        }
        var materialBlock=Nested(block,"material",out var materialName,out var materialStart);
        var materialReference=materialBlock is null?(Scalar(block,"material")??sheetMetalPart?.Material):null;
        if(materialBlock is null&&string.IsNullOrWhiteSpace(materialReference)){diagnostics.Add(Error("fea-missing-material","Analysis has no material declaration or catalog material reference.",sourcePath));return Done(null,diagnostics,started);}
        LinearElasticMaterialIr material;
        if(materialBlock is not null)
        {
            var young=Stress(Scalar(materialBlock,"youngsModulus"));var poisson=Number(Scalar(materialBlock,"poissonRatio"));var density=Density(Scalar(materialBlock,"density"));
            var materialProv=new AnalysisProvenance(sourcePath??"<memory>",open+materialStart,materialBlock.Length,"material "+materialName);
            material=new LinearElasticMaterialIr(materialName,young,poisson,density,bodyId,materialProv);
        }
        else
        {
            materialReference=materialReference!.Trim();
            var referenceOffset=(block.Declaration.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f=>FirmamentSyntaxFields.SameField(f.Name.Text,"Material"))?.Value.Span.Start??block.Start)-1;var materialProv=new AnalysisProvenance(sourcePath??"<memory>",open+1+referenceOffset,materialReference.Length,"material: "+materialReference);
            var materialLookup=sheetMetalPart is null?materialReference:NativeSheetMetalMaterialReference(materialReference);var resolution=(materialResolver??new MaterialResolver()).Resolve(materialLookup);
            if(!resolution.IsSuccess)
            {
                var code=resolution.Error switch{MaterialResolutionError.UnknownMaterial=>"firmament-material-unknown",MaterialResolutionError.AmbiguousMaterial=>"firmament-material-ambiguous",MaterialResolutionError.MissingRequiredStructuralProperty=>"fea-material-missing-structural-properties",_=>"firmament-material-invalid"};
                diagnostics.Add(new(code,AnalysisDiagnosticSeverity.Error,resolution.Message??$"Material '{materialReference}' could not be resolved.",materialProv));return Done(null,diagnostics,started);
            }
            var resolved=resolution.Material!;var structural=resolved.Structural;
            if(structural is null){diagnostics.Add(new("fea-material-missing-structural-properties",AnalysisDiagnosticSeverity.Error,$"Material '{materialReference}' does not provide density, Young's modulus, Poisson ratio, yield strength, and ultimate tensile strength for its reference condition.",materialProv));return Done(null,diagnostics,started);}
            material=new LinearElasticMaterialIr(resolved.Identity.DisplayName,structural.YoungsModulus.SiValue,structural.PoissonsRatio.SiValue,structural.Density.SiValue,bodyId,materialProv,$"{resolved.Identity.CatalogId}:{resolved.Identity.StableId}",resolved.ConstitutiveClass,structural.YieldStrength.SiValue,resolved);
        }
        (SemanticRegionBinding? Region,AnalysisDiagnostic? Diagnostic) NormalizeRegion(string path,AnalysisProvenance provenance)
        {
            if(midsurfacePatchGraph is not null)
            {
                var prefix=bodyId+".";var relative=path.StartsWith(prefix,StringComparison.Ordinal)?path[prefix.Length..]:path;
                var split=relative.LastIndexOf('.');
                if(split>0&&midsurfacePatchGraph.Patches.Any(x=>x.Identity.Equals(relative[..split],StringComparison.Ordinal))&&TryPatchEdge(relative[(split+1)..],out _))
                    return(new(bodyId,path,Provenance:provenance,SemanticStableId:relative,CapabilityEvidence:["NativeSheetMetal","ExactPatchEdge"],ExactBindingKind:"MidsurfacePatchEdge"),null);
                return(null,new("thinwall-sheetmetal-boundary-unknown",AnalysisDiagnosticSeverity.Error,$"Native SheetMetal boundary '{path}' must identify an exact patch edge as '<Body>.<Patch>.r-min|r-max|s-min|s-max'.",provenance));
            }
            if(namedRegions.TryGetValue(path,out var semantic))
            {
                var span=new SemanticSourceSpan(provenance.Source,provenance.Start,provenance.Length);var normalized=AnalysisSemanticRegionNormalizer.Normalize(new(semantic,[new(path,span)],span));
                return normalized.Diagnostic is null?(normalized.Region,null):(null,new(normalized.Diagnostic.Code,AnalysisDiagnosticSeverity.Error,normalized.Diagnostic.Message,provenance));
            }
            return FirmamentAnalysisSemanticProducer.Normalize(path,bodyId,semanticFaceIds,provenance,sourceKind);
        }
        var constraints=new List<DisplacementConstraintIr>();
        foreach(var nested in NestedAll(block,"Fixed"))
        {
            var path=Scalar(nested.Body,"region")??"";var componentText=Scalar(nested.Body,"components");var components=Components(componentText,diagnostics,sourcePath);
            if(componentText is null)components=[DisplacementComponent.X,DisplacementComponent.Y,DisplacementComponent.Z];
            else if(components.Count==0)diagnostics.Add(Error("fea-displacement-component-empty","Fixed components must name at least one of X, Y or Z; omit the field for all three.",sourcePath));var prov=new AnalysisProvenance(sourcePath??"<memory>",open+nested.Start,nested.Length,"fixed "+nested.Name);
            var normalized=NormalizeRegion(path,prov);if(normalized.Diagnostic is not null){diagnostics.Add(normalized.Diagnostic);continue;}
            constraints.Add(new(nested.Name,normalized.Region!,components,new(0,0,0),prov));
        }
        var loads=new List<BoundaryLoadIr>();
        foreach(var keyword in new[]{("Traction",BoundaryLoadKind.Traction),("Force",BoundaryLoadKind.ResultantForce),("Pressure",BoundaryLoadKind.Pressure)})foreach(var nested in NestedAll(block,keyword.Item1))
        {
            var path=Scalar(nested.Body,"region")??"";var prov=new AnalysisProvenance(sourcePath??"<memory>",open+nested.Start,nested.Length,keyword.Item1+" "+nested.Name);var vector=keyword.Item2==BoundaryLoadKind.Pressure?Vector3D.Zero:Vector(Scalar(nested.Body,"vector"),keyword.Item2==BoundaryLoadKind.ResultantForce);var pressure=keyword.Item2==BoundaryLoadKind.Pressure?Stress(Scalar(nested.Body,"value")):0;
            var normalized=NormalizeRegion(path,prov);if(normalized.Diagnostic is not null){diagnostics.Add(normalized.Diagnostic);continue;}
            var distribution=keyword.Item2 switch{BoundaryLoadKind.ResultantForce=>LoadDistributionPolicy.TotalResultantOverSelectedArea,BoundaryLoadKind.Traction=>LoadDistributionPolicy.TractionPerUnitArea,_=>LoadDistributionPolicy.PressureNormalToSurface};
            loads.Add(new(nested.Name,keyword.Item2,normalized.Region!,vector,pressure,prov,distribution));
        }
        var bodyForces=new List<BodyForceIr>();
        foreach(var nested in NestedAll(block,"BodyForce"))
        {
            var prov=new AnalysisProvenance(sourcePath??"<memory>",open+nested.Start,nested.Length,"BodyForce "+nested.Name);
            bodyForces.Add(new(nested.Name,Acceleration(Scalar(nested.Body,"acceleration")),prov));
        }
        var latticeExpression=block.Take("Lattice");var latticeValues=Numbers(latticeExpression);
        if(latticeExpression is not null && latticeValues.Length!=3 ||latticeValues.Any(value=>!double.IsInteger(value)||value<1||value>int.MaxValue)){diagnostics.Add(Error("fea-invalid-lattice-dimensions","Lattice must contain exactly three positive integer dimensions.",sourcePath));return Done(null,diagnostics,started);}
        var lattice=latticeValues.Length==3?new LatticeSpec(region.Bounds,(int)latticeValues[0],(int)latticeValues[1],(int)latticeValues[2]):new LatticeSpec(region.Bounds,12,6,2);
        ExperimentalShellSettings? shellSettings=null;
        if(mode==AnalysisMode.ExperimentalShell)
        {
            var thicknessText=Scalar(block,"thickness");
            if(string.IsNullOrWhiteSpace(thicknessText)&&sheetMetalPart is null){diagnostics.Add(Error("thinwall-thickness-missing","ExperimentalShell requires an explicit Thickness for synthetic/native Box geometry in X0.",sourcePath));return Done(null,diagnostics,started);}
            var masterValues=Numbers(block.Take("MasterGrid"));
            if(masterValues.Length!=2||masterValues.Any(value=>!double.IsInteger(value)||value<1||value>int.MaxValue)){diagnostics.Add(Error("thinwall-master-grid-invalid","ExperimentalShell MasterGrid must contain exactly two positive integer dimensions.",sourcePath));return Done(null,diagnostics,started);}
            var orderText=Scalar(block,"order");var order=OptionalInteger(orderText,3);
            var alphaText=Scalar(block,"alphaFictitious");var alpha=string.IsNullOrWhiteSpace(alphaText)?1e-12:ScalarNumber(alphaText);
            var depthText=Scalar(block,"maxSubdivisionDepth");var depth=OptionalInteger(depthText,4);
            var quadratureText=Scalar(block,"quadratureOrder");int? quadrature=string.IsNullOrWhiteSpace(quadratureText)?null:OptionalInteger(quadratureText,int.MinValue);
            var mapText=Scalar(block,"geometryMap")??nameof(ThinWallGeometryMapKind.Flat);
            if((!Enum.TryParse<ThinWallGeometryMapKind>(mapText,true,out var map)||!Enum.IsDefined(map))){diagnostics.Add(Error("thinwall-geometry-unsupported",$"ExperimentalShell geometry map '{mapText}' is unsupported.",sourcePath));return Done(null,diagnostics,started);}
            var radiusText=Scalar(block,"cylinderRadius");var angleText=Scalar(block,"cylinderAngle");
            var resolvedThickness=sheetMetalPart is null?Length(thicknessText):sheetMetalPart.Thickness*.001;
            shellSettings=new ExperimentalShellSettings(resolvedThickness,order,(int)masterValues[0],(int)masterValues[1],alpha,depth,quadrature,map,
                string.IsNullOrWhiteSpace(radiusText)?null:Length(radiusText),string.IsNullOrWhiteSpace(angleText)?null:Angle(angleText));
            lattice=new LatticeSpec(region.Bounds,shellSettings.MasterCellsR,shellSettings.MasterCellsS,1);
            var sourceThickness=sheetMetalPart is null?region.Bounds.Max.Z-region.Bounds.Min.Z:sheetMetalPart.Thickness*.001;
            if(double.IsFinite(shellSettings.ThicknessMeters)&&double.Abs(sourceThickness-shellSettings.ThicknessMeters)>double.Max(1e-12,sourceThickness*1e-9))
                diagnostics.Add(Error("thinwall-thickness-source-mismatch",$"ExperimentalShell Thickness ({shellSettings.ThicknessMeters:R} m) must match the native/synthetic body's semantic thickness ({sourceThickness:R} m) in X0.",sourcePath));
            if(sheetMetalPart is not null&&!string.IsNullOrWhiteSpace(thicknessText)&&double.Abs(Length(thicknessText)-sourceThickness)>double.Max(1e-12,sourceThickness*1e-9))
                diagnostics.Add(Error("thinwall-thickness-source-mismatch",$"Analysis Thickness must not override native SheetMetal thickness {sourceThickness:R} m.",sourcePath));
        }
        var requested=new HashSet<AnalysisResultField>();foreach(var item in (Scalar(block,"results")??"Displacement,Strain,Stress,ReactionForce").Trim('[',']').Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)){if(Enum.TryParse<AnalysisResultField>(item,true,out var field)&&Enum.IsDefined(field))requested.Add(field);else diagnostics.Add(Error("fea-invalid-result-request",$"Analysis result '{item}' is unsupported.",sourcePath));}
        if(requested.Count==0){diagnostics.Add(Error("fea-invalid-result-request","Analysis must request at least one supported result field.",sourcePath));return Done(null,diagnostics,started);}
        var bodyProv=new AnalysisProvenance(sourcePath??"<memory>",declaration.Span.Start,declaration.Span.Length,"body "+bodyId,ExactBrepFaceId:brepBodyId);var ir=new LinearElasticAnalysisIr(declaration.Name!.Text,AnalysisKind.LinearStaticElasticity,new(bodyId,sourceKind,region,brepBodyId,resourceHash,bodyProv),[material],constraints,loads,requested,lattice,analysisSpan,mode,shellSettings,midsurfacePatchGraph,bodyForces);
        diagnostics.AddRange(block.CompleteTree().Select(d=>new AnalysisDiagnostic(d.Code,AnalysisDiagnosticSeverity.Error,d.Message,
            new(sourcePath??"<memory>",d.SourceSpan?.Start??0,d.SourceSpan?.Length??0,d.Target??"analysis binding"))));
        diagnostics.AddRange(AnalysisIrValidator.Validate(ir));return Done(ir,diagnostics,started);
    }

    private sealed record NestedBlock(string Name,Scope Body,int Start,int Length);
    // Explicit compatibility production. It never retries a failed V2 document by scanning text.
    private static bool Face(ExpressionSyntax? expression,string axis) => expression is CallSyntax call
        && FirmamentSyntaxValues.Reference(call.Target)=="face" && call.Arguments.Count==1
        && call.Arguments[0].Name is null && FirmamentSyntaxValues.Reference(call.Arguments[0].Value)==axis;
    private static bool AdmitResourceDocument(FirmamentDomainBinding binding,string source,string? path,DeclarationSyntax analysis,List<AnalysisDiagnostic> diagnostics)
    {
        var document=new DeclarationSyntax(new(new("document",binding.Document.Span),[],binding.Document.Span),null,[],[],null,null,[],null,new(binding.Document.Statements,binding.Document.Span),false,binding.Document.Span);
        var root=new Scope(document,source,path??"<memory>",binding.Nominal,binding.OriginalSpan);
        var models=root.Children("Model").Concat(root.Children("model")).ToArray();
        if(models.Length>1){diagnostics.Add(Error("firmament-analysis-resource-owner-invalid","Use one Model for an imported-resource analysis.",path));return false;}
        foreach(var scope in new[]{root}.Concat(models))
        {
            scope.Take("Units");
            foreach(var item in scope.Declaration.Body?.Statements??[])
                if(ReferenceEquals(item,analysis) || item is DeclarationSyntax data && binding.OwnsData(data))scope.Consume(item);
        }
        diagnostics.AddRange(root.CompleteTree().Select(d=>new AnalysisDiagnostic(d.Code,AnalysisDiagnosticSeverity.Error,d.Message,new(path??"<memory>",d.SourceSpan?.Start??0,d.SourceSpan?.Length??0,"analysis resource document"))));
        return diagnostics.All(d=>d.Severity!=AnalysisDiagnosticSeverity.Error);
    }

    private static bool AdmitLegacyGeometry(DocumentSyntax document,string source,string? path,string body,DeclarationSyntax analysis,List<AnalysisDiagnostic> diagnostics)
    {
        var models=document.Statements.OfType<DeclarationSyntax>().Where(d=>d.Type.Name.Text is "model" or "Model").ToArray();
        if(models.Length!=1 || document.Statements.Count!=1){diagnostics.Add(Error("firmament-analysis-legacy-document-unsupported","Legacy analysis requires one model containing one Box and its bounded through cut.",path));return false;}
        var root=new Scope(models[0],source,path??"<memory>");root.Take("Units");root.Consume(analysis);
        var solids=root.Children("solid");
        if(solids.Count!=1 || solids[0].Name!=body || FirmamentSyntaxValues.Reference(solids[0].Declaration.Annotation)!="Box")
            diagnostics.Add(Error("firmament-analysis-legacy-body-unsupported","Legacy analysis admits exactly one solid body: Box.",path));
        foreach(var solid in solids){solid.AdmitAnnotation();solid.Take("Size");}
        foreach(var modify in root.Children("modify"))
        {
            if(modify.Name!=body)modify.Invalid("target","Modify must target the analysis body.");
            var regions=modify.Children("region");if(regions.Count!=1)modify.Invalid("region","Exactly one face-local through cut is supported.");
            foreach(var region in regions)
            {
                if(region.Declaration.Clauses.Count!=1 || region.Declaration.Clauses[0].Kind!=ClauseKind.On || !Face(region.Declaration.Clauses[0].Value,"+Z"))
                    region.Invalid("on","Legacy analysis region must be on face(+Z).");
                region.AdmitPlacement();
                var cuts=region.Children("cut");if(cuts.Count!=1 || cuts[0].Name!="Cylinder")region.Invalid("cut");
                foreach(var cut in cuts){cut.Take("Radius");if(!Face(cut.Take("Through"),"-Z"))cut.Invalid("Through");}
            }
        }
        diagnostics.AddRange(root.CompleteTree().Select(d=>new AnalysisDiagnostic(d.Code,AnalysisDiagnosticSeverity.Error,d.Message,new(path??"<memory>",d.SourceSpan?.Start??0,d.SourceSpan?.Length??0,"legacy analysis geometry"))));
        return diagnostics.All(d=>d.Severity!=AnalysisDiagnosticSeverity.Error);
    }

    private static IEnumerable<NestedBlock> NestedAll(Scope scope,string keyword)=>scope.Children(keyword,legacyCase:true).Select(s=>new NestedBlock(s.Name,s,s.Start,s.Length));
    private static Scope? Nested(Scope scope,string keyword,out string name,out int start)
    {
        var matches=scope.Children(keyword,legacyCase:true);name=matches.FirstOrDefault()?.Name??"";start=matches.FirstOrDefault()?.Start??0;
        if(matches.Count>1)scope.Invalid(keyword,"Only one nested declaration of this kind is admitted.");
        return matches.Count==1?matches[0]:null;
    }
    private static string? Scalar(Scope scope,string name)=>scope.Raw(name);
    private static double? UnitScale(string unit)=>unit.ToLowerInvariant() switch{"m"=>1,"cm"=>.01,"mm"=>.001,_=>null};
    private static double Number(string? text)=>double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:double.NaN;
    private static double ScalarNumber(string? text)=>double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:double.NaN;
    private static int OptionalInteger(string? text,int defaultValue){if(string.IsNullOrWhiteSpace(text))return defaultValue;var value=ScalarNumber(text);return double.IsInteger(value)&&value>=int.MinValue&&value<=int.MaxValue?(int)value:int.MinValue;}
    private static double Length(string? text){if(text is null)return double.NaN;var m=Regex.Match(text,@"^(?<v>[-+0-9.eE]+)\s*(?<u>mm|cm|m)$",RegexOptions.IgnoreCase);if(!m.Success)return double.NaN;return Number(m.Groups["v"].Value)*(UnitScale(m.Groups["u"].Value)??double.NaN);}
    private static double Angle(string? text){if(text is null)return double.NaN;var m=Regex.Match(text,@"^(?<v>[-+0-9.eE]+)\s*(?<u>deg|rad)$",RegexOptions.IgnoreCase);if(!m.Success)return double.NaN;var value=Number(m.Groups["v"].Value);return m.Groups["u"].Value.Equals("deg",StringComparison.OrdinalIgnoreCase)?value*double.Pi/180:value;}
    private static double Stress(string? text){if(text is null)return double.NaN;var m=Regex.Match(text,@"^(?<v>[-+0-9.eE]+)\s*(?<u>GPa|MPa|kPa|Pa)$",RegexOptions.IgnoreCase);if(!m.Success)return double.NaN;var v=Number(m.Groups["v"].Value);return v*(m.Groups["u"].Value.ToLowerInvariant() switch{"gpa"=>1e9,"mpa"=>1e6,"kpa"=>1e3,_=>1});}
    private static double? Density(string? text){if(string.IsNullOrWhiteSpace(text))return null;var m=Regex.Match(text,@"^(?<v>[-+0-9.eE]+)\s*kg/m3$",RegexOptions.IgnoreCase);return m.Success?Number(m.Groups["v"].Value):double.NaN;}
    private static double[] Numbers(ExpressionSyntax? value)
    {
        if(value is null)return [];
        return FirmamentSyntaxValues.Vector(value,"",out var values)&&values.All(double.IsFinite)?values:[double.NaN];
    }
    private static Vector3D Vector(string? text,bool force){var parts=(text??"").Trim('[',']').Split(',',StringSplitOptions.TrimEntries);if(parts.Length!=3)return new(double.NaN,double.NaN,double.NaN);double Parse(string p){var m=Regex.Match(p,@"^(?<v>[-+0-9.eE]+)\s*(?<u>N|Pa)$",RegexOptions.IgnoreCase);return m.Success&&((force&&m.Groups["u"].Value.Equals("N",StringComparison.OrdinalIgnoreCase))||(!force&&m.Groups["u"].Value.Equals("Pa",StringComparison.OrdinalIgnoreCase)))?Number(m.Groups["v"].Value):double.NaN;}return new(Parse(parts[0]),Parse(parts[1]),Parse(parts[2]));}
    private static Vector3D Acceleration(string? text){var parts=(text??"").Trim('[',']').Split(',',StringSplitOptions.TrimEntries);if(parts.Length!=3)return new(double.NaN,double.NaN,double.NaN);double Parse(string p){var m=Regex.Match(p,@"^(?<v>[-+0-9.eE]+)\s*m/s(?:\^?2|²)$",RegexOptions.IgnoreCase);return m.Success?Number(m.Groups["v"].Value):double.NaN;}return new(Parse(parts[0]),Parse(parts[1]),Parse(parts[2]));}
    private static HashSet<DisplacementComponent> Components(string? text,List<AnalysisDiagnostic> diagnostics,string? source)
    {
        var result=new HashSet<DisplacementComponent>();
        foreach(var item in (text??"").Trim('[',']').Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
            if(Enum.TryParse<DisplacementComponent>(item,true,out var value)&&Enum.IsDefined(value))result.Add(value);
            else diagnostics.Add(Error("fea-displacement-component-unsupported",$"Unknown displacement component '{item}'; use X, Y or Z.",source));
        return result;
    }
    private static bool TryPatchEdge(string token,out MidsurfacePatchEdge edge)=>Enum.TryParse(token.Replace("-minimum","Minimum",StringComparison.OrdinalIgnoreCase).Replace("-maximum","Maximum",StringComparison.OrdinalIgnoreCase).Replace("-min","Minimum",StringComparison.OrdinalIgnoreCase).Replace("-max","Maximum",StringComparison.OrdinalIgnoreCase),true,out edge);
    private static string NativeSheetMetalMaterialReference(string reference)
    {
        if(reference.Contains("5052",StringComparison.OrdinalIgnoreCase)&&reference.Contains("H32",StringComparison.OrdinalIgnoreCase)&&(reference.Contains("Aluminum",StringComparison.OrdinalIgnoreCase)||reference.Contains("Aluminium",StringComparison.OrdinalIgnoreCase)))return "Standard.Materials.Aluminum.5052_H32";
        return reference;
    }
    private static FirmamentAnalysisCompilation Done(LinearElasticAnalysisIr? ir,IReadOnlyList<AnalysisDiagnostic> d,long started)=>new(ir,d,System.Diagnostics.Stopwatch.GetElapsedTime(started));
    private static AnalysisDiagnostic Error(string code,string message,string? source)=>new(code,AnalysisDiagnosticSeverity.Error,message,new(source??"<memory>",0,0,code));
}
