using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using static Aetheris.SheetMetal.RecoverySyntax;

namespace Aetheris.SheetMetal;

/// <summary>
/// Compact engineer-owned interpretation over immutable forensic recovery evidence.
/// Geometry is never silently guessed: every reconstructed declaration binds to one
/// recovered fact while names, grouping, and accepted nominals are explicit authority.
/// </summary>
internal static class ReconstructedSheetMetalFirmament
{
    public static bool IsReconstructed(string source)=>FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document)
        .Where(d=>d.Type.Name.Text=="SheetMetal").Any(d=>d.Body?.Statements.OfType<FieldSyntax>().Any(f=>FirmamentSyntaxFields.SameField(f.Name.Text,"Intent") && FirmamentSyntaxValues.Reference(f.Value)=="Reconstructed")==true
            && d.Body.Statements.OfType<FieldSyntax>().Any(f=>FirmamentSyntaxFields.SameField(f.Name.Text,"EvidenceSource")));

    public static SheetMetalAuthoringResult Compile(string source,string sourcePath)
    {
        var root=Root(source,sourcePath,out var syntaxDiagnostics);if(root is null)return new(false,null,null,null,syntaxDiagnostics);
        if(Token(root,"Intent")!="Reconstructed")return Failure("Expected intent: Reconstructed.");
        var evidenceSource=Quoted(root,"EvidenceSource");if(evidenceSource is null)return Failure("Reconstructed Sheet Metal requires EvidenceSource.");
        var directory=Path.GetDirectoryName(Path.GetFullPath(sourcePath))??Directory.GetCurrentDirectory();var evidencePath=Path.GetFullPath(Path.Combine(directory,evidenceSource.Replace('/',Path.DirectorySeparatorChar)));
        if(!File.Exists(evidencePath))return Failure($"Reconstruction evidence was not found: {evidencePath}");
        var evidence=RecoveredSheetMetalFirmament.Compile(File.ReadAllText(evidencePath),evidencePath);if(!evidence.IsSuccess||evidence.Part is null)return Failure("EvidenceSource did not compile as recovered Sheet Metal evidence.");
        var original=evidence.Part;if(!Scalar(root,"Thickness","mm",out var thickness)||thickness<=0)return Failure("Reconstructed Sheet Metal requires positive Thickness.");
        var k=Scalar(root,"KFactor",null,out var parsedK)?parsedK:original.FlatPatternPolicy.KFactor;if(k is <0 or >1)return Failure("KFactor must be between zero and one.");

        var regionMap=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var m in root.Children("Region"))
        {
            var from=Quoted(m.Body,"FromEvidence");if(from is null||original.Regions.All(r=>r.StableId!=from))return Failure($"Region '{m.Name}' has invalid FromEvidence.");
            if(!regionMap.TryAdd(from,m.Name) || regionMap.Values.Distinct(StringComparer.Ordinal).Count()!=regionMap.Count)return Failure("Reconstructed regions must map distinct evidence and names exactly once.");
        }
        if(regionMap.Count!=original.Regions.Count)return Failure($"Reconstruction must explicitly account for every recovered region ({regionMap.Count}/{original.Regions.Count}).");
        var baseName=Token(root,"BaseRegion");if(baseName is null||!regionMap.Values.Contains(baseName,StringComparer.Ordinal))return Failure("BaseRegion must name a reconstructed Region.");
        var regions=original.Regions.Select(r=>r with{StableId=regionMap[r.StableId],Source=r.Source with{SourceAuthority="forensic recovery evidence; reconstructed naming authority"},Evidence=r.Evidence.Concat([new(SheetEvidenceKind.Authored,"reconstructed-region",$"Engineer mapped recovered region '{r.StableId}' to '{regionMap[r.StableId]}'.",SourceFaceIds:r.Source.FaceIds)]).ToArray()}).ToArray();

        var bends=new List<SheetBendIr>();var mappedBends=new HashSet<string>(StringComparer.Ordinal);
        foreach(var m in root.Children("Bend"))
        {
            var body=m.Body;var from=Quoted(body,"FromEvidence");var originalBend=original.Bends.SingleOrDefault(b=>b.StableId==from);if(originalBend is null)return Failure($"Bend '{m.Name}' has invalid FromEvidence.");
            if(!mappedBends.Add(from!) || bends.Any(b=>b.StableId==m.Name))return Failure("Reconstructed bends must map distinct evidence and names exactly once.");
            if(!Scalar(body,"Angle","deg",out var angle)||!Scalar(body,"InsideRadius","mm",out var radius))return Failure($"Bend '{m.Name}' requires accepted Angle and InsideRadius.");
            bends.Add(originalBend with{StableId=m.Name,BendAngleRadians=angle*Math.PI/180d,InsideRadius=radius,Thickness=thickness,AdjacentRegionA=regionMap[originalBend.AdjacentRegionA],AdjacentRegionB=regionMap[originalBend.AdjacentRegionB],NeutralAxisPolicy=SheetNeutralAxisPolicy.KFactorPolicy(k),Source=originalBend.Source with{SourceAuthority="forensic recovery evidence; reconstructed dimension authority"},Evidence=originalBend.Evidence.Concat([new(SheetEvidenceKind.Authored,"accepted-reconstruction-nominal","Engineer explicitly accepted bend angle/radius values.")]).ToArray()});
        }
        if(bends.Count!=original.Bends.Count)return Failure($"Reconstruction must explicitly account for every recovered bend ({bends.Count}/{original.Bends.Count}).");

        var cuts=new List<SheetFeatureIr>();var mappedCuts=new HashSet<string>(StringComparer.Ordinal);
        foreach(var m in root.Children("Cut"))
        {
            var body=m.Body;var from=Quoted(body,"FromEvidence");var originalCut=original.Features.SingleOrDefault(f=>f.StableId==from);if(originalCut is null)return Failure($"Cut '{m.Name}' has invalid FromEvidence.");
            if(!mappedCuts.Add(from!) || cuts.Any(c=>c.StableId==m.Name))return Failure("Reconstructed cuts must map distinct evidence and names exactly once.");
            cuts.Add(originalCut with{StableId=m.Name,OwningRegionId=regionMap[originalCut.OwningRegionId],Source=originalCut.Source with{SourceAuthority="forensic recovery evidence; reconstructed feature authority"},Evidence=originalCut.Evidence.Concat([new(SheetEvidenceKind.Authored,"reconstructed-cut",$"Engineer grouped/named recovered cut '{originalCut.StableId}'.")]).ToArray()});
        }
        if(cuts.Count!=original.Features.Count)return Failure($"Reconstruction must explicitly account for every recovered cut ({cuts.Count}/{original.Features.Count}).");
        var closed=Close(root);if(closed.Count>0)return new(false,null,null,null,closed);
        var part=new SheetMetalPartIr($"sheetmetal-{root.Name}",thickness,original.Material,baseName,regions,bends,cuts,new(k),SheetMetalRecognitionStatus.Partial,"Reconstructed engineer/LLM-authored interpretation linked to immutable recovered evidence.",[new(SheetEvidenceKind.Authored,"reconstructed-intent","Naming, grouping, and nominal choices are explicit human/LLM engineering authority; geometry bindings remain forensic evidence."),new(SheetEvidenceKind.Authored,"accepted-thickness-nominal","Measured recovery thickness was explicitly nominalized.",thickness,Math.Abs(thickness-original.Thickness))],[],original.FormedBody);
        var flat=SheetMetalFlattener.Flatten(part);return new(flat.Status==FlatPatternStatus.Unsupported?false:true,null,part,flat,flat.Diagnostics);
    }

    private static SheetMetalAuthoringResult Failure(string message)=>new(false,null,null,null,[new("sheetmetal-firmament-reconstruction-invalid",SheetMetalDiagnosticSeverity.Error,message)]);
}
