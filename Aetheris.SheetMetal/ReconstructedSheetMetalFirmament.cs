using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.SheetMetal;

/// <summary>
/// Compact engineer-owned interpretation over immutable forensic recovery evidence.
/// Geometry is never silently guessed: every reconstructed declaration binds to one
/// recovered fact while names, grouping, and accepted nominals are explicit authority.
/// </summary>
internal static class ReconstructedSheetMetalFirmament
{
    private const RegexOptions Rx=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Singleline;
    private const string Number=@"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[Ee][+-]?[0-9]+)?";
    public static bool IsReconstructed(string source)=>Regex.IsMatch(source,@"\bIntent\s*:\s*Reconstructed\s*;",Rx);

    public static SheetMetalAuthoringResult Compile(string source,string sourcePath)
    {
        var header=Regex.Match(source,@"\bSheetMetal\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",Rx);
        var evidenceMatch=Regex.Match(source,"\\bEvidenceSource\\s*:\\s*\"(?<v>[^\"]+)\"\\s*;",Rx);
        if(!header.Success||!evidenceMatch.Success)return Failure("Reconstructed Sheet Metal requires a header and EvidenceSource.");
        var directory=Path.GetDirectoryName(Path.GetFullPath(sourcePath))??Directory.GetCurrentDirectory();var evidencePath=Path.GetFullPath(Path.Combine(directory,evidenceMatch.Groups["v"].Value.Replace('/',Path.DirectorySeparatorChar)));
        if(!File.Exists(evidencePath))return Failure($"Reconstruction evidence was not found: {evidencePath}");
        var evidence=RecoveredSheetMetalFirmament.Compile(File.ReadAllText(evidencePath),evidencePath);if(!evidence.IsSuccess||evidence.Part is null)return Failure("EvidenceSource did not compile as recovered Sheet Metal evidence.");
        var original=evidence.Part;if(!Scalar(source,"Thickness","mm",out var thickness)||thickness<=0)return Failure("Reconstructed Sheet Metal requires positive Thickness.");
        var k=Scalar(source,"KFactor",null,out var parsedK)?parsedK:original.FlatPatternPolicy.KFactor;if(k is <0 or >1)return Failure("KFactor must be between zero and one.");

        var regionMap=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(Match m in Regex.Matches(source,@"\bRegion\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}",Rx))
        {
            var from=Quoted(m.Groups["body"].Value,"FromEvidence");if(from is null||original.Regions.All(r=>r.StableId!=from))return Failure($"Region '{m.Groups["name"].Value}' has invalid FromEvidence.");
            regionMap[from]=m.Groups["name"].Value;
        }
        if(regionMap.Count!=original.Regions.Count)return Failure($"Reconstruction must explicitly account for every recovered region ({regionMap.Count}/{original.Regions.Count}).");
        var baseName=Token(source,"BaseRegion");if(baseName is null||!regionMap.Values.Contains(baseName,StringComparer.Ordinal))return Failure("BaseRegion must name a reconstructed Region.");
        var regions=original.Regions.Select(r=>r with{StableId=regionMap[r.StableId],Source=r.Source with{SourceAuthority="forensic recovery evidence; reconstructed naming authority"},Evidence=r.Evidence.Concat([new(SheetEvidenceKind.Authored,"reconstructed-region",$"Engineer mapped recovered region '{r.StableId}' to '{regionMap[r.StableId]}'.",SourceFaceIds:r.Source.FaceIds)]).ToArray()}).ToArray();

        var bends=new List<SheetBendIr>();
        foreach(Match m in Regex.Matches(source,@"\bBend\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}",Rx))
        {
            var body=m.Groups["body"].Value;var from=Quoted(body,"FromEvidence");var originalBend=original.Bends.SingleOrDefault(b=>b.StableId==from);if(originalBend is null)return Failure($"Bend '{m.Groups["name"].Value}' has invalid FromEvidence.");
            if(!Scalar(body,"Angle","deg",out var angle)||!Scalar(body,"InsideRadius","mm",out var radius))return Failure($"Bend '{m.Groups["name"].Value}' requires accepted Angle and InsideRadius.");
            bends.Add(originalBend with{StableId=m.Groups["name"].Value,BendAngleRadians=angle*Math.PI/180d,InsideRadius=radius,Thickness=thickness,AdjacentRegionA=regionMap[originalBend.AdjacentRegionA],AdjacentRegionB=regionMap[originalBend.AdjacentRegionB],NeutralAxisPolicy=SheetNeutralAxisPolicy.KFactorPolicy(k),Source=originalBend.Source with{SourceAuthority="forensic recovery evidence; reconstructed dimension authority"},Evidence=originalBend.Evidence.Concat([new(SheetEvidenceKind.Authored,"accepted-reconstruction-nominal","Engineer explicitly accepted bend angle/radius values.")]).ToArray()});
        }
        if(bends.Count!=original.Bends.Count)return Failure($"Reconstruction must explicitly account for every recovered bend ({bends.Count}/{original.Bends.Count}).");

        var cuts=new List<SheetFeatureIr>();
        foreach(Match m in Regex.Matches(source,@"\bCut\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}",Rx))
        {
            var body=m.Groups["body"].Value;var from=Quoted(body,"FromEvidence");var originalCut=original.Features.SingleOrDefault(f=>f.StableId==from);if(originalCut is null)return Failure($"Cut '{m.Groups["name"].Value}' has invalid FromEvidence.");
            cuts.Add(originalCut with{StableId=m.Groups["name"].Value,OwningRegionId=regionMap[originalCut.OwningRegionId],Source=originalCut.Source with{SourceAuthority="forensic recovery evidence; reconstructed feature authority"},Evidence=originalCut.Evidence.Concat([new(SheetEvidenceKind.Authored,"reconstructed-cut",$"Engineer grouped/named recovered cut '{originalCut.StableId}'.")]).ToArray()});
        }
        if(cuts.Count!=original.Features.Count)return Failure($"Reconstruction must explicitly account for every recovered cut ({cuts.Count}/{original.Features.Count}).");
        var part=new SheetMetalPartIr($"sheetmetal-{header.Groups["name"].Value}",thickness,original.Material,baseName,regions,bends,cuts,new(k),SheetMetalRecognitionStatus.Partial,"Reconstructed engineer/LLM-authored interpretation linked to immutable recovered evidence.",[new(SheetEvidenceKind.Authored,"reconstructed-intent","Naming, grouping, and nominal choices are explicit human/LLM engineering authority; geometry bindings remain forensic evidence."),new(SheetEvidenceKind.Authored,"accepted-thickness-nominal","Measured recovery thickness was explicitly nominalized.",thickness,Math.Abs(thickness-original.Thickness))],[],original.FormedBody);
        var flat=SheetMetalFlattener.Flatten(part);return new(flat.Status==FlatPatternStatus.Unsupported?false:true,null,part,flat,flat.Diagnostics);
    }

    private static bool Scalar(string text,string name,string? unit,out double value){var suffix=unit is null?"":@"\s*"+Regex.Escape(unit);var m=Regex.Match(text,@"\b"+Regex.Escape(name)+@"\s*:\s*(?<v>"+Number+")"+suffix+@"\s*;",Rx);value=m.Success?double.Parse(m.Groups["v"].Value,NumberStyles.Float,CultureInfo.InvariantCulture):0;return m.Success;}
    private static string? Quoted(string text,string name){var m=Regex.Match(text,"\\b"+Regex.Escape(name)+"\\s*:\\s*\"(?<v>[^\"]+)\"\\s*;",Rx);return m.Success?m.Groups["v"].Value:null;}
    private static string? Token(string text,string name){var m=Regex.Match(text,@"\b"+Regex.Escape(name)+@"\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)\s*;",Rx);return m.Success?m.Groups["v"].Value:null;}
    private static SheetMetalAuthoringResult Failure(string message)=>new(false,null,null,null,[new("sheetmetal-firmament-reconstruction-invalid",SheetMetalDiagnosticSeverity.Error,message)]);
}
