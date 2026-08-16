using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.SheetMetal;

public enum SheetMetalSemanticConstraintKind { RequiredMembers, EqualSize, EqualPitch, Mirror }

public sealed record SheetMetalSemanticDatum(string Path,string Region,double X,double Y,string Provenance);
public sealed record SheetMetalSemanticAttachmentPath(
    string Path,string Region,string CarrierEdge,double Inset,double Span,double SpanOffset,bool ReleaseToCarrier,
    string? ProfileDeltaPath = null,string? DeltaMemberPath = null);
public sealed record SheetMetalSemanticProfileDelta(string Path,string Region,string Edge,SemanticProfileDeltaIr Program);

public sealed record SheetMetalSemanticTab(string Path,string Region,string Edge,double Center,double Width,double Extension);
public sealed record SheetMetalSemanticSteppedNotch(
    string Path,string Region,string Edge,double Center,double Width,double Depth,
    double ShoulderDepth,double OuterChamfer,double InnerChamfer,int Side);
public sealed record SheetMetalSemanticCornerProfile(
    string CornerPath,string OperationPath,string Region,string Corner,string Operation,double SetbackA,double SetbackB);

public sealed record SheetMetalSemanticPattern(
    string Path,string Name,string Region,SheetFeatureKind FeatureKind,int Count,double CenterX,double CenterY,
    double PitchX,double PitchY,double? Diameter,double? Width,double? Length,IReadOnlyList<string> Members);

public sealed record SheetMetalSemanticConstraint(
    string Path,SheetMetalSemanticConstraintKind Kind,IReadOnlyList<string> Members,string Status,string Detail);

public sealed record SheetMetalSemanticLayout(
    IReadOnlyList<string> Structs,IReadOnlyList<SheetMetalSemanticDatum> Datums,IReadOnlyList<SheetMetalSemanticTab> Tabs,
    IReadOnlyList<SheetMetalSemanticPattern> Patterns,IReadOnlyList<SheetMetalSemanticConstraint> Constraints,
    IReadOnlyList<SheetMetalSemanticSteppedNotch>? SteppedNotches = null,
    IReadOnlyList<SheetMetalSemanticCornerProfile>? Corners = null,
    IReadOnlyList<SheetMetalSemanticAttachmentPath>? AttachmentPaths = null,
    IReadOnlyList<SheetMetalSemanticProfileDelta>? ProfileDeltas = null)
{
    public static SheetMetalSemanticLayout Empty { get; } = new([],[],[],[],[]);
}

internal sealed record SheetMetalSemanticLayoutParseResult(
    bool IsSuccess,SheetMetalSemanticLayout Layout,IReadOnlyList<AuthoredSheetCutSpec> GeneratedCuts,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics);

/// <summary>
/// Bounded semantic-layout pass for authored sheet metal. It resolves named datums and
/// regular feature patterns before exact cut profiles are lowered. This deliberately is
/// not a general sketch solver: every supported relationship has direct, deterministic
/// lowering and a compact engineering diagnostic.
/// </summary>
internal static class SheetMetalSemanticLayoutParser
{
    private const RegexOptions Rx=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Singleline;
    private const string Number=@"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)";
    private sealed record Block(string Keyword,string Name,string Body,int Index,string? Struct);
    private readonly record struct Point2(double X,double Y);

    internal static SheetMetalSemanticLayoutParseResult Parse(
        string source,AuthoredSheetBaseSpec baseSpec,IReadOnlyList<AuthoredSheetFlangeSpec> flanges,
        IReadOnlyList<AuthoredSheetCutSpec> authoredCuts)
    {
        var diagnostics=new List<SheetMetalDiagnostic>();
        var structs=StructBlocks(source);
        var structNames=structs.Select(x=>x.Name).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var datums=new List<SheetMetalSemanticDatum>();
        var tabs=new List<SheetMetalSemanticTab>();
        var steppedNotches=new List<SheetMetalSemanticSteppedNotch>();
        var corners=new List<SheetMetalSemanticCornerProfile>();
        var patterns=new List<SheetMetalSemanticPattern>();
        var generated=new List<AuthoredSheetCutSpec>();
        var constraints=new List<SheetMetalSemanticConstraint>();
        var regionSizes=RegionSizes(baseSpec,flanges);
        var profileDeltas=new List<SheetMetalSemanticProfileDelta>();

        var attachmentPaths=new List<SheetMetalSemanticAttachmentPath>();
        foreach(var block in Blocks(source,"AttachmentPath",structs))
        {
            var on=TokenPath(block.Body,"On");
            var owner=on is null?Match.Empty:Regex.Match(on,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>Front|Right|Rear|Left|Outer)$",Rx);
            if(!owner.Success||!regionSizes.TryGetValue(owner.Groups["region"].Value,out var size))
                return Fail("sheetmetal-attachment-path-owner",$"AttachmentPath '{block.Name}' requires On: <planar-region>.<physical-edge>.");
            if(!Scalar(block.Body,"Inset",out var inset)||!Scalar(block.Body,"Span",out var span))
                return Fail("sheetmetal-attachment-path-properties",$"AttachmentPath '{block.Name}' requires Inset and Span.");
            Scalar(block.Body,"SpanOffset",out var spanOffset);
            var edge=owner.Groups["edge"].Value;var carrierLength=edge is "Front" or "Rear"?size.Width:edge is "Right" or "Left"?size.Height:size.Width;
            var maxInset=edge.Equals("Outer",StringComparison.OrdinalIgnoreCase)?size.Height:edge is "Front" or "Rear"?size.Height:size.Width;
            if(!double.IsFinite(inset)||inset<0||inset>=maxInset-1e-8)
                return Fail("sheetmetal-attachment-path-offset",$"AttachmentPath '{block.Name}' Inset must be finite, non-negative, and inside owning region '{owner.Groups["region"].Value}'.");
            if(!double.IsFinite(span)||span<=0||span>carrierLength+1e-8||Math.Abs(spanOffset)>(carrierLength-span)/2d+1e-8)
                return Fail("sheetmetal-attachment-path-span",$"AttachmentPath '{block.Name}' Span {span:G9} mm with offset {spanOffset:G9} mm does not fit carrier length {carrierLength:G9} mm.");
            var release=Token(block.Body,"Release");var releaseToCarrier=release?.Equals("ToCarrier",StringComparison.OrdinalIgnoreCase)==true;
            if(inset>1e-8&&!releaseToCarrier)
                return Fail("sheetmetal-attachment-path-release",$"Inset AttachmentPath '{block.Name}' requires `Release: ToCarrier;` so the owner and child do not describe double material.");
            if(release is not null&&!releaseToCarrier)
                return Fail("sheetmetal-attachment-path-release",$"AttachmentPath '{block.Name}' uses unsupported Release '{release}'. Supported: ToCarrier.");
            var region=owner.Groups["region"].Value;
            var path=$"{region}.{block.Name}";
            if(attachmentPaths.Any(x=>x.Path.Equals(path,StringComparison.Ordinal)))
                return Fail("sheetmetal-attachment-path-duplicate",$"Attachment path '{path}' is declared more than once.");
            attachmentPaths.Add(new(path,region,edge,inset,span,spanOffset,releaseToCarrier));
        }
        var parsedDeltas=SemanticProfileDeltaParser.Parse(source);
        if(!parsedDeltas.IsSuccess)return Fail("sheetmetal-profile-delta-parse",string.Join("; ",parsedDeltas.Diagnostics));
        foreach(var parsed in parsedDeltas.Deltas)
        {
            var owner=Regex.Match(parsed.OwnerPath,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>Outer)$",Rx);
            if(!owner.Success||!regionSizes.TryGetValue(owner.Groups["region"].Value,out var size))
                return Fail("sheetmetal-profile-delta-owner",$"ProfileDelta '{parsed.Delta.StableId}' requires On: <planar-region>.Outer.");
            var length=size.Width;var span=parsed.Delta.Span;
            var start=parsed.Delta.Anchor.Kind switch
            {
                SemanticEdgeAnchorKind.FromStart=>parsed.Delta.Anchor.Offset,
                SemanticEdgeAnchorKind.FromEnd=>length-parsed.Delta.Anchor.Offset-span,
                SemanticEdgeAnchorKind.CenteredAt=>parsed.Delta.Anchor.Offset-span/2d,
                _=>double.NaN
            };
            if(start< -1e-8||start+span>length+1e-8)
                return Fail("sheetmetal-profile-delta-outside",$"ProfileDelta '{parsed.Delta.StableId}' lies outside '{parsed.OwnerPath}'.");
            var region=owner.Groups["region"].Value;
            var path=$"{region}.{parsed.Delta.StableId}";
            profileDeltas.Add(new(path,region,"Outer",parsed.Delta with { StableId=path,Name=parsed.Delta.Name,
                Levels=parsed.Delta.Levels.Select(level=>level with { StableId=$"{path}.{level.Name}" }).ToArray(),
                Members=parsed.Delta.Members.Select(member=>member with { StableId=$"{path}.{member.Name}",ExposeAs=member.ExposeAs is null?null:$"{path}.{member.ExposeAs.Split('.').Last()}" }).ToArray() }));
            foreach(var exposure in parsed.Exposures)
            {
                var publicName=exposure.Path.Split('.').Last();var publicPath=$"{region}.{publicName}";
                var exposureSpan=exposure.EndU-exposure.StartU;var exposureCenter=start+(exposure.StartU+exposure.EndU)/2d;
                if(exposureSpan<=1e-8||Math.Abs(exposure.Offset)<=1e-8)
                    return Fail("sheetmetal-profile-delta-exposure",$"ProfileDelta exposure '{publicPath}' must be a non-zero inset span.");
                if(!exposure.Capabilities.Contains("FlangeAttachable",StringComparer.OrdinalIgnoreCase))
                    return Fail("sheetmetal-profile-delta-capability",$"ProfileDelta exposure '{publicPath}' must explicitly include FlangeAttachable.");
                attachmentPaths.Add(new(publicPath,region,"Outer",Math.Abs(exposure.Offset),exposureSpan,exposureCenter-length/2d,false,path,$"{path}.{exposure.MemberPath.Split('.').Last()}"));
            }
        }
        foreach(var flange in flanges)
        {
            var attachment=attachmentPaths.SingleOrDefault(x=>x.Path.Equals($"{flange.ParentRegion}.{flange.EdgeName}",StringComparison.OrdinalIgnoreCase));
            if(attachment is not null)regionSizes[flange.Name]=(attachment.Span,flange.Length);
        }

        foreach(var block in Blocks(source,"Datum",structs))
        {
            var region=Token(block.Body,"On");
            if(region is null||!regionSizes.ContainsKey(region))return Fail("sheetmetal-semantic-datum-region",$"Datum '{Path(block)}' references unknown planar region '{region ?? "<missing>"}'.");
            if(!TryPointExpression(block.Body,"At",region,regionSizes,datums,out var point,out var reason))return Fail("sheetmetal-semantic-datum-point",$"Datum '{Path(block)}' has invalid At expression: {reason}");
            datums.Add(new(Path(block),region,point.X,point.Y,"resolved before exact profile lowering"));
        }

        foreach(var block in Blocks(source,"Tab",structs))
        {
            var on=TokenPath(block.Body,"On");var match=on is null?Match.Empty:Regex.Match(on,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>Outer)$",Rx);
            if(!match.Success||!regionSizes.TryGetValue(match.Groups["region"].Value,out var size))return Fail("sheetmetal-tab-edge",$"Tab '{Path(block)}' requires On: <planar-region>.Outer.");
            if(!Scalar(block.Body,"Center",out var center)||!Scalar(block.Body,"Width",out var width)||!Scalar(block.Body,"Extension",out var extension)||width<=0||extension<=0)
                return Fail("sheetmetal-tab-profile",$"Tab '{Path(block)}' requires Center, positive Width, and positive Extension.");
            if(center-width/2< -1e-8||center+width/2>size.Width+1e-8)return Fail("sheetmetal-tab-outside-edge",$"Tab '{Path(block)}' lies outside '{on}'.");
            if(tabs.Any(x=>x.Region.Equals(match.Groups["region"].Value,StringComparison.OrdinalIgnoreCase)&&center-width/2<x.Center+x.Width/2-1e-8&&center+width/2>x.Center-x.Width/2+1e-8))
                return Fail("sheetmetal-tab-overlap",$"Tab '{Path(block)}' overlaps another tab on '{on}'.");
            tabs.Add(new(Path(block),match.Groups["region"].Value,match.Groups["edge"].Value,center,width,extension));
        }

        foreach(var block in Blocks(source,"SteppedNotch",structs))
        {
            var on=TokenPath(block.Body,"On");var match=on is null?Match.Empty:Regex.Match(on,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>Outer)$",Rx);
            if(!match.Success||!regionSizes.TryGetValue(match.Groups["region"].Value,out var size))return Fail("sheetmetal-edge-fragment-owner",$"SteppedNotch '{Path(block)}' requires On: <planar-region>.Outer.");
            if(!Scalar(block.Body,"Center",out var center)||!Scalar(block.Body,"Width",out var width)||!Scalar(block.Body,"Depth",out var depth)||
               !Scalar(block.Body,"ShoulderDepth",out var shoulder)||!Scalar(block.Body,"OuterChamfer",out var outerChamfer)||!Scalar(block.Body,"InnerChamfer",out var innerChamfer))
                return Fail("sheetmetal-edge-fragment-properties",$"SteppedNotch '{Path(block)}' requires Center, Width, Depth, ShoulderDepth, OuterChamfer, and InnerChamfer.");
            var sideToken=Token(block.Body,"Side");var side=sideToken?.Equals("Outward",StringComparison.OrdinalIgnoreCase)==true?1:sideToken?.Equals("Inward",StringComparison.OrdinalIgnoreCase)==true?-1:0;
            if(width<=0||depth<=0||shoulder<0||shoulder>=depth||outerChamfer<0||innerChamfer<=0||2*(outerChamfer+innerChamfer)>=width||side==0)
                return Fail("sheetmetal-edge-fragment-invalid",$"SteppedNotch '{Path(block)}' has impossible dimensions or Side (use Inward/Outward).");
            if(center-width/2< -1e-8||center+width/2>size.Width+1e-8)return Fail("sheetmetal-edge-fragment-outside",$"SteppedNotch '{Path(block)}' lies outside '{on}'.");
            steppedNotches.Add(new(Path(block),match.Groups["region"].Value,match.Groups["edge"].Value,center,width,depth,shoulder,outerChamfer,innerChamfer,side));
        }


        foreach(var block in CornerBlocks(source,structs))
        {
            var owner=Regex.Match(block.Name,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<corner>RootStart|RootEnd|OuterStart|OuterEnd)$",Rx);
            if(!owner.Success||!regionSizes.ContainsKey(owner.Groups["region"].Value))return Fail("sheetmetal-corner-owner",$"CornerProfile '{Path(block)}' requires <planar-region>.RootStart, .RootEnd, .OuterStart, or .OuterEnd.");
            var operations=Regex.Matches(block.Body,@"\b(?<kind>Chamfer|Cutback|Taper|NotchCorner|Round)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",Rx).Cast<Match>().ToArray();
            if(operations.Length!=1)return Fail("sheetmetal-corner-operation",$"CornerProfile '{Path(block)}' requires exactly one Chamfer, Cutback, Taper, NotchCorner, or Round operation.");
            var operation=operations[0];var open=block.Body.IndexOf('{',operation.Index);var close=Close(block.Body,open);var operationBody=close>open?block.Body[(open+1)..close]:string.Empty;
            var isRound=operation.Groups["kind"].Value.Equals("Round",StringComparison.OrdinalIgnoreCase);var hasRadius=Scalar(operationBody,"Radius",out var radius);
            var hasEqual=Scalar(operationBody,"Setback",out var equal);var hasA=Scalar(operationBody,"SetbackA",out var setbackA);var hasB=Scalar(operationBody,"SetbackB",out var setbackB);
            if(isRound){hasEqual=hasRadius;equal=radius;}
            if(!hasA){setbackA=equal;}if(!hasB){setbackB=equal;}
            var cornerPath=Path(block);var operationPath=$"{cornerPath}.{operation.Groups["name"].Value}";
            if((!hasEqual&&(!hasA||!hasB))||setbackA<=0||setbackB<=0)return Fail("sheetmetal-corner-setback",$"Corner operation '{operationPath}' requires positive Setback or SetbackA and SetbackB.");
            if(corners.Any(x=>x.Region.Equals(owner.Groups["region"].Value,StringComparison.OrdinalIgnoreCase)&&x.Corner.Equals(owner.Groups["corner"].Value,StringComparison.OrdinalIgnoreCase)))
                return Fail("sheetmetal-corner-duplicate",$"Corner '{owner.Groups["region"].Value}.{owner.Groups["corner"].Value}' is authored more than once.");
            corners.Add(new(cornerPath,operationPath,owner.Groups["region"].Value,owner.Groups["corner"].Value,operation.Groups["kind"].Value,setbackA,setbackB));
        }

        foreach(var block in Blocks(source,"Pattern",structs))
        {
            var path=Path(block);var region=Token(block.Body,"On");
            if(region is null||!regionSizes.ContainsKey(region))return Fail("sheetmetal-semantic-pattern-region",$"Pattern '{path}' references unknown planar region '{region ?? "<missing>"}'.");
            if(!Int(block.Body,"Count",out var count)||count<=0||count>1024)return Fail("sheetmetal-semantic-pattern-count",$"Pattern '{path}' requires Count between 1 and 1024.");
            if(!Tuple(block.Body,"Pitch",out var pitch))return Fail("sheetmetal-semantic-pattern-pitch",$"Pattern '{path}' requires a two-dimensional Pitch.");
            if(!TryPointExpression(block.Body,"Center",region,regionSizes,datums,out var center,out var reason))return Fail("sheetmetal-semantic-pattern-center",$"Pattern '{path}' has invalid Center expression: {reason}");
            var circle=Regex.Match(block.Body,$@"\bFeature\s*:\s*(?:Circle|Hole)\s*\{{(?<body>[\s\S]*?)\}}\s*;?",Rx);
            var slot=Regex.Match(block.Body,$@"\bFeature\s*:\s*(?:Slot|Rectangle)\s*\{{(?<body>[\s\S]*?)\}}\s*;?",Rx);
            SheetFeatureKind kind;double? diameter=null,width=null,length=null;
            if(circle.Success)
            {
                kind=SheetFeatureKind.CircularHole;
                if(!Scalar(circle.Groups["body"].Value,"Diameter",out var d)||d<=0)return Fail("sheetmetal-semantic-pattern-profile",$"Circular Pattern '{path}' requires positive Diameter.");
                diameter=d;
            }
            else if(slot.Success)
            {
                kind=SheetFeatureKind.Slot;
                if(!Scalar(slot.Groups["body"].Value,"Width",out var w)||!Scalar(slot.Groups["body"].Value,"Length",out var l)||w<=0||l<=0)return Fail("sheetmetal-semantic-pattern-profile",$"Slot Pattern '{path}' requires positive Width and Length.");
                width=w;length=l;
            }
            else return Fail("sheetmetal-semantic-pattern-profile",$"Pattern '{path}' requires a Circle/Hole or Slot/Rectangle Feature.");

            var members=new List<string>();
            for(var i=0;i<count;i++)
            {
                var member=$"{path}[{i}]";var offset=i-(count-1)/2d;
                generated.Add(new(member,region,kind,center.X+offset*pitch.X,center.Y+offset*pitch.Y,diameter,width,length,kind==SheetFeatureKind.Slot));members.Add(member);
            }
            patterns.Add(new(path,block.Name,region,kind,count,center.X,center.Y,pitch.X,pitch.Y,diameter,width,length,members));
            constraints.Add(new($"{path}.EqualPitch",SheetMetalSemanticConstraintKind.EqualPitch,members,"Satisfied",$"generated at pitch ({pitch.X:G9} mm, {pitch.Y:G9} mm)"));
            constraints.Add(new($"{path}.EqualSize",SheetMetalSemanticConstraintKind.EqualSize,members,"Satisfied","all members lower from one analytic feature specification"));
        }

        var combinedCuts=authoredCuts.Concat(generated).ToArray();
        var duplicateCut=combinedCuts.GroupBy(x=>x.Name,StringComparer.Ordinal).FirstOrDefault(x=>x.Count()>1);
        if(duplicateCut is not null)return Fail("sheetmetal-semantic-duplicate-feature",$"Semantic feature path '{duplicateCut.Key}' is declared more than once.");
        var allCuts=combinedCuts.ToDictionary(x=>x.Name,StringComparer.Ordinal);
        foreach(var block in Blocks(source,"Require",structs))
        {
            var path=Path(block);var kindToken=Token(block.Body,"Kind");var members=List(block.Body,"Members");
            if(!Enum.TryParse<SheetMetalSemanticConstraintKind>(kindToken,true,out var kind))return Fail("sheetmetal-semantic-constraint-kind",$"Require '{path}' uses unsupported Kind '{kindToken ?? "<missing>"}'. Supported: RequiredMembers, EqualSize, EqualPitch, Mirror.");
            if(members.Count==0)return Fail("sheetmetal-semantic-constraint-members",$"Require '{path}' must name Members.");
            var missing=members.Where(x=>!allCuts.ContainsKey(x)).ToArray();
            if(missing.Length>0)return Fail("sheetmetal-semantic-required-member",$"Require '{path}' references missing semantic feature(s): {string.Join(", ",missing)}.");
            string detail;
            switch(kind)
            {
                case SheetMetalSemanticConstraintKind.RequiredMembers:
                    detail=$"all {members.Count} required members exist";break;
                case SheetMetalSemanticConstraintKind.EqualSize:
                    var first=allCuts[members[0]];
                    var mismatch=members.Skip(1).Select(x=>allCuts[x]).FirstOrDefault(x=>x.Kind!=first.Kind||!Near(x.Diameter,first.Diameter)||!Near(x.Width,first.Width)||!Near(x.Length,first.Length));
                    if(mismatch is not null)return Fail("sheetmetal-semantic-equal-size",$"Require '{path}' is contradicted by '{mismatch.Name}': feature kind or nominal size differs from '{first.Name}'.");
                    detail=$"{members.Count} members share kind and nominal size";break;
                case SheetMetalSemanticConstraintKind.EqualPitch:
                    if(members.Count<2)return Fail("sheetmetal-semantic-equal-pitch",$"Require '{path}' needs at least two Members.");
                    var axis=Token(block.Body,"Axis");if(axis is not ("X" or "Y" or "x" or "y"))return Fail("sheetmetal-semantic-equal-pitch",$"Require '{path}' needs Axis: X or Axis: Y.");
                    if(!Scalar(block.Body,"Pitch",out var expectedPitch)||expectedPitch<=0)return Fail("sheetmetal-semantic-equal-pitch",$"Require '{path}' needs positive Pitch.");
                    var coordinates=members.Select(x=>axis.Equals("X",StringComparison.OrdinalIgnoreCase)?allCuts[x].X:allCuts[x].Y).ToArray();
                    for(var i=1;i<coordinates.Length;i++)if(Math.Abs(Math.Abs(coordinates[i]-coordinates[i-1])-expectedPitch)>1e-6)
                        return Fail("sheetmetal-semantic-equal-pitch",$"Require '{path}' declares {expectedPitch:G9} mm pitch, but spacing at member {i} is {Math.Abs(coordinates[i]-coordinates[i-1]):G9} mm.");
                    detail=$"{members.Count} members satisfy {expectedPitch:G9} mm {axis.ToUpperInvariant()} pitch";break;
                case SheetMetalSemanticConstraintKind.Mirror:
                    if(members.Count!=2)return Fail("sheetmetal-semantic-mirror",$"Require '{path}' needs exactly two Members.");
                    var about=TokenPath(block.Body,"About");
                    if(about is null||!TryAxis(about,regionSizes,datums,out var horizontal,out var value))return Fail("sheetmetal-semantic-mirror",$"Require '{path}' needs About: <Region>.CenterX/CenterY or a named Datum path.");
                    var left=allCuts[members[0]];var right=allCuts[members[1]];var reflected=horizontal?left with { X=2*value-left.X }:left with { Y=2*value-left.Y };
                    if(Math.Abs(reflected.X-right.X)>1e-6||Math.Abs(reflected.Y-right.Y)>1e-6||left.Kind!=right.Kind||!Near(left.Diameter,right.Diameter)||!Near(left.Width,right.Width)||!Near(left.Length,right.Length))
                        return Fail("sheetmetal-semantic-mirror",$"Require '{path}' is contradicted: '{members[1]}' is not the size-preserving mirror of '{members[0]}' about '{about}'.");
                    detail=$"'{members[0]}' and '{members[1]}' mirror about {about}";break;
                default:throw new InvalidOperationException();
            }
            constraints.Add(new(path,kind,members,"Satisfied",detail));
        }

        return new(true,new(structNames,datums.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),tabs.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),patterns.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),constraints.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),steppedNotches.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),corners.OrderBy(x=>x.CornerPath,StringComparer.Ordinal).ToArray(),attachmentPaths.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),profileDeltas.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray()),generated,diagnostics);

        SheetMetalSemanticLayoutParseResult Fail(string code,string message)=>new(false,SheetMetalSemanticLayout.Empty,[],[new(code,SheetMetalDiagnosticSeverity.Error,message)]);
    }

    private static Dictionary<string,(double Width,double Height)> RegionSizes(AuthoredSheetBaseSpec baseSpec,IReadOnlyList<AuthoredSheetFlangeSpec> flanges)
    {
        var result=new Dictionary<string,(double Width,double Height)>(StringComparer.OrdinalIgnoreCase){{baseSpec.Name,(baseSpec.Width,baseSpec.Depth)}};
        var pending=flanges.ToList();var guard=0;
        while(pending.Count>0&&guard++<=flanges.Count)foreach(var flange in pending.ToArray())
        {
            if(!result.TryGetValue(flange.ParentRegion,out var parent))continue;
            var width=flange.SpanLength??(flange.ParentRegion.Equals(baseSpec.Name,StringComparison.OrdinalIgnoreCase)
                ? flange.EdgeName is "Front" or "Rear"?baseSpec.Width:baseSpec.Depth
                : parent.Width);
            result[flange.Name]=(width,flange.Length);pending.Remove(flange);
        }
        return result;
    }

    private static bool TryPointExpression(string body,string field,string region,IReadOnlyDictionary<string,(double Width,double Height)> sizes,IReadOnlyList<SheetMetalSemanticDatum> datums,out Point2 point,out string reason)
    {
        point=default;reason="expected `(xmm, ymm)`, `<Region>.Center`, or a named Datum path, optionally followed by `+ (dxmm, dymm)`";
        var line=Regex.Match(body,$@"\b{Regex.Escape(field)}\s*:\s*(?<v>[^;]+)\s*;",Rx);if(!line.Success)return false;var value=line.Groups["v"].Value.Trim();
        if(TupleValue(value,out point))return true;
        var reference=Regex.Match(value,$@"^(?<path>[A-Za-z_][A-Za-z0-9_.]*)(?:\s*\+\s*(?<offset>\([^)]*\)))?$",Rx);if(!reference.Success)return false;
        var path=reference.Groups["path"].Value;
        if(path.EndsWith(".Center",StringComparison.OrdinalIgnoreCase))
        {
            var target=path[..^7];if(!target.Equals(region,StringComparison.OrdinalIgnoreCase)||!sizes.TryGetValue(target,out var size)){reason=$"'{path}' is not the center of owning region '{region}'";return false;}
            point=new(size.Width/2,size.Height/2);
        }
        else
        {
            var datum=datums.FirstOrDefault(x=>x.Path.Equals(path,StringComparison.Ordinal)||x.Path.EndsWith("."+path,StringComparison.Ordinal));
            if(datum is null||!datum.Region.Equals(region,StringComparison.OrdinalIgnoreCase)){reason=$"unknown datum '{path}' on region '{region}'";return false;}point=new(datum.X,datum.Y);
        }
        if(reference.Groups["offset"].Success){if(!TupleValue(reference.Groups["offset"].Value,out var offset))return false;point=new(point.X+offset.X,point.Y+offset.Y);}return true;
    }

    private static bool TryAxis(string path,IReadOnlyDictionary<string,(double Width,double Height)> sizes,IReadOnlyList<SheetMetalSemanticDatum> datums,out bool xAxis,out double value)
    {
        xAxis=true;value=0;
        if(path.EndsWith(".CenterX",StringComparison.OrdinalIgnoreCase)){var region=path[..^8];if(!sizes.TryGetValue(region,out var size))return false;value=size.Width/2;return true;}
        if(path.EndsWith(".CenterY",StringComparison.OrdinalIgnoreCase)){var region=path[..^8];if(!sizes.TryGetValue(region,out var size))return false;xAxis=false;value=size.Height/2;return true;}
        var datum=datums.FirstOrDefault(x=>x.Path.Equals(path,StringComparison.Ordinal)||x.Path.EndsWith("."+path,StringComparison.Ordinal));if(datum is null)return false;value=datum.X;return true;
    }

    private static IReadOnlyList<Block> StructBlocks(string source)
    {
        var result=new List<Block>();foreach(Match match in Regex.Matches(source,@"\bConcept\s+Struct\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",Rx))
        {var open=source.IndexOf('{',match.Index);var close=Close(source,open);if(close>open)result.Add(new("Concept Struct",match.Groups["name"].Value,source[(open+1)..close],match.Index,null));}return result;
    }
    private static IReadOnlyList<Block> Blocks(string source,string keyword,IReadOnlyList<Block> structs)
    {
        var result=new List<Block>();foreach(Match match in Regex.Matches(source,$@"\b{Regex.Escape(keyword)}\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{{",Rx))
        {var open=source.IndexOf('{',match.Index);var close=Close(source,open);if(close<=open)continue;var owner=structs.Where(x=>match.Index>x.Index&&match.Index<x.Index+x.Body.Length+(source.IndexOf('{',x.Index)-x.Index)+2).OrderByDescending(x=>x.Index).FirstOrDefault()?.Name;result.Add(new(keyword,match.Groups["name"].Value,source[(open+1)..close],match.Index,owner));}return result;
    }
    private static IReadOnlyList<Block> CornerBlocks(string source,IReadOnlyList<Block> structs)
    {
        var result=new List<Block>();foreach(Match match in Regex.Matches(source,@"\bCornerProfile\s+(?<name>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)\s*\{",Rx))
        {var open=source.IndexOf('{',match.Index);var close=Close(source,open);if(close<=open)continue;var owner=structs.Where(x=>match.Index>x.Index&&match.Index<x.Index+x.Body.Length+(source.IndexOf('{',x.Index)-x.Index)+2).OrderByDescending(x=>x.Index).FirstOrDefault()?.Name;result.Add(new("CornerProfile",match.Groups["name"].Value,source[(open+1)..close],match.Index,owner));}return result;
    }
    private static int Close(string source,int open){var depth=0;for(var i=open;i<source.Length;i++){if(source[i]=='{')depth++;else if(source[i]=='}'&&--depth==0)return i;}return -1;}
    private static string Path(Block block)=>block.Struct is null?block.Name:$"{block.Struct}.{block.Name}";
    private static string? Token(string text,string name){var m=Regex.Match(text,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_]*)\s*;",Rx);return m.Success?m.Groups["v"].Value:null;}
    private static string? TokenPath(string text,string name){var m=Regex.Match(text,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>[A-Za-z_][A-Za-z0-9_.]*)\s*;",Rx);return m.Success?m.Groups["v"].Value:null;}
    private static IReadOnlyList<string> List(string text,string name){var m=Regex.Match(text,$@"\b{Regex.Escape(name)}\s*:\s*\[(?<v>[^]]*)\]\s*;",Rx);return m.Success?m.Groups["v"].Value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries):[];}
    private static bool Scalar(string text,string name,out double value){var m=Regex.Match(text,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>{Number})\s*mm\s*;",Rx);value=m.Success?double.Parse(m.Groups["v"].Value,CultureInfo.InvariantCulture):0;return m.Success;}
    private static bool Int(string text,string name,out int value){var m=Regex.Match(text,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>[0-9]+)\s*;",Rx);return int.TryParse(m.Groups["v"].Value,NumberStyles.None,CultureInfo.InvariantCulture,out value);}
    private static bool Tuple(string text,string name,out Point2 point){var m=Regex.Match(text,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>\([^;]+\))\s*;",Rx);return TupleValue(m.Groups["v"].Value,out point);}
    private static bool TupleValue(string value,out Point2 point){var m=Regex.Match(value,$@"^\s*\(\s*(?<x>{Number})\s*mm\s*,\s*(?<y>{Number})\s*mm\s*\)\s*$",Rx);point=m.Success?new(double.Parse(m.Groups["x"].Value,CultureInfo.InvariantCulture),double.Parse(m.Groups["y"].Value,CultureInfo.InvariantCulture)):default;return m.Success;}
    private static bool Near(double? a,double? b)=>a is null&&b is null||a is not null&&b is not null&&Math.Abs(a.Value-b.Value)<=1e-9;
}
