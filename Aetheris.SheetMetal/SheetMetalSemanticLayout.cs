using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
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
    internal static bool OwnsDeclaration(string type) => type is "AttachmentPath" or "Datum" or "Tab" or "SteppedNotch"
        or "CornerProfile" or "Pattern" or "Require" or "ProfileDelta";
    private const RegexOptions Rx=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Singleline;
    private sealed record Block(DeclarationSyntax Syntax, FirmamentSyntaxFields Fields, string? Struct)
    {
        internal string Name => Syntax.Name?.Text ?? FirmamentSyntaxValues.Reference(Syntax.Target) ?? "<unnamed>";
        internal IReadOnlySet<string> DatumNames { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    }
    private readonly record struct Point2(double X,double Y);

    internal static SheetMetalSemanticLayoutParseResult Parse(
        AuthoredSheetBaseSpec baseSpec,IReadOnlyList<AuthoredSheetFlangeSpec> flanges,
        IReadOnlyList<AuthoredSheetCutSpec> authoredCuts, FirmamentDomainBinding binding)
    {
        var diagnostics=new List<SheetMetalDiagnostic>();
        var errors = new List<string>();
        var blocks = new List<Block>();
        var scopes = binding.Document.Statements.OfType<DeclarationSyntax>()
            .Where(d => d.Type.Name.Text == "SheetMetal" || d.Type.Name.Text == "Struct" && d.Modifiers.Any(m=>m.Name.Text=="Concept")).ToArray();
        var structNames=scopes.Where(d=>d.Type.Name.Text=="Struct").Select(d=>d.Name!.Text).Order(StringComparer.Ordinal).ToArray();
        var materialScopes = scopes.Where(d=>d.Type.Name.Text=="Struct" && d.Body?.Statements.OfType<DeclarationSyntax>().Any(c=>c.Type.Name.Text is "Pattern" or "Tab" or "SteppedNotch" or "CornerProfile" or "ProfileDelta") == true).ToArray();
        var root = scopes.Single(d=>d.Type.Name.Text=="SheetMetal" && !d.Modifiers.Any(m=>m.Name.Text=="Extend"));
        // Historical module-level semantic operations have one unambiguous sheet
        // owner. Keep that compatibility without scanning unrelated nested scopes.
        var module=root with { Type=root.Type with { Name=root.Type.Name with { Text="module" } },
            Body=root.Body! with { Statements=binding.Document.Statements.OfType<DeclarationSyntax>()
                .Where(d=>OwnsDeclaration(d.Type.Name.Text) && d.Type.Name.Text!="ProfileDelta" && !(d.Type.Name.Text=="Require" && d.Body is null)).Cast<StatementSyntax>().ToArray() } };
        scopes=[..scopes,module];
        if (materialScopes.Length>1)
            return Fail("sheetmetal-layout-owner-ambiguous", "Multiple Concept Structs contain material operations. Move material operations into the owning SheetMetal body and keep reusable layout data in Concept Structs; implicit ownership of multiple legacy effect bundles is not admitted.");
        foreach (var scope in scopes)
        {
            if(scope.Body is null || scope.Name is null)
                return Fail("sheetmetal-semantic-scope-invalid","A sheet/layout scope requires a name and body.");
            var children = scope.Body?.Statements.OfType<DeclarationSyntax>().ToArray() ?? [];
            if (scope.Type.Name.Text=="Struct")
            {
                var xy=scope.Clauses.All(c=>c.Kind==ClauseKind.On && FirmamentSyntaxValues.Reference(c.Value)=="XY");
                var fields = new FirmamentSyntaxFields(scope with { Clauses=xy?[]:scope.Clauses, Body=scope.Body! with { Statements=scope.Body.Statements.Where(s=>
                    s is not DeclarationSyntax d || !OwnsDeclaration(d.Type.Name.Text) && !binding.OwnsData(d) && !binding.OwnsProfileData(d)).ToArray() } },errors,bind:binding.Nominal);
                fields.Complete();
            }
            foreach (var child in children.Where(d=>OwnsDeclaration(d.Type.Name.Text) && d.Type.Name.Text!="ProfileDelta"))
            {
                if(child.Type.Arguments.Count>0 || child.Modifiers.Count>0)
                    return Fail("sheetmetal-semantic-header-invalid",$"{child.Type.Name.Text} does not admit generic arguments or declaration modifiers.");
                if(child.Name is null && child.Target is null)
                    return Fail("sheetmetal-semantic-header-invalid",$"{child.Type.Name.Text} requires a stable authored name or corner target.");
                if (child.Type.Name.Text=="Require" && child.Body is null)
                { if (!binding.CheckRequirement(child)) return BindingFailure(); continue; }
                var allowedChildren=child.Type.Name.Text=="CornerProfile" ? new[]{"Chamfer","Cutback","Taper","NotchCorner","Round"} : [];
                var fields = new FirmamentSyntaxFields(child with { Target=null, Body=child.Body is null ? null : child.Body with { Statements=child.Body.Statements.Where(s=>
                    s is not DeclarationSyntax d || !allowedChildren.Contains(d.Type.Name.Text)).ToArray() } },errors,bind:binding.Nominal);
                blocks.Add(new(child,fields,scope.Type.Name.Text=="Struct"?scope.Name!.Text:null));
            }
        }
        if (errors.Count>0) return SyntaxFailure();
        var datumNames=blocks.Where(b=>b.Syntax.Type.Name.Text=="Datum").Select(Path).ToHashSet(StringComparer.Ordinal);
        blocks=blocks.Select(b=>b with { DatumNames=datumNames }).ToList();
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
        foreach(var block in blocks.Where(b=>b.Syntax.Type.Name.Text=="AttachmentPath"))
        {
            var on=TokenPath(block,"On");
            var owner=on is null?Match.Empty:Regex.Match(on,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>Front|Right|Rear|Left|Outer)$",Rx);
            if(!owner.Success||!regionSizes.TryGetValue(owner.Groups["region"].Value,out var size))
                return Fail("sheetmetal-attachment-path-owner",$"AttachmentPath '{block.Name}' requires On: <planar-region>.<physical-edge>.");
            if(!Scalar(block,"Inset",out var inset)||!Scalar(block,"Span",out var span))
                return Fail("sheetmetal-attachment-path-properties",$"AttachmentPath '{block.Name}' requires Inset and Span.");
            Scalar(block,"SpanOffset",out var spanOffset);
            var authoredEdge=owner.Groups["edge"].Value;
            var edge=char.ToUpperInvariant(authoredEdge[0])+authoredEdge[1..].ToLowerInvariant();
            var carrierLength=edge is "Front" or "Rear"?size.Width:edge is "Right" or "Left"?size.Height:size.Width;
            var maxInset=edge.Equals("Outer",StringComparison.OrdinalIgnoreCase)?size.Height:edge is "Front" or "Rear"?size.Height:size.Width;
            if(!double.IsFinite(inset)||inset<0||inset>=maxInset-1e-8)
                return Fail("sheetmetal-attachment-path-offset",$"AttachmentPath '{block.Name}' Inset must be finite, non-negative, and inside owning region '{owner.Groups["region"].Value}'.");
            if(!double.IsFinite(span)||span<=0||span>carrierLength+1e-8||Math.Abs(spanOffset)>(carrierLength-span)/2d+1e-8)
                return Fail("sheetmetal-attachment-path-span",$"AttachmentPath '{block.Name}' Span {span:G9} mm with offset {spanOffset:G9} mm does not fit carrier length {carrierLength:G9} mm.");
            var release=Token(block,"Release");var releaseToCarrier=release?.Equals("ToCarrier",StringComparison.OrdinalIgnoreCase)==true;
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
        var parsedDeltas=SemanticProfileDeltaParser.ParseDeclarations(AuthoredSheetMetalCompiler.DeltaDeclarations(binding.Document),binding.Nominal);
        if(!parsedDeltas.IsSuccess)return Fail("sheetmetal-profile-delta-parse",string.Join("; ",parsedDeltas.Diagnostics));
        foreach(var parsed in parsedDeltas.Deltas)
        {
            var owner=Regex.Match(parsed.OwnerPath,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>Outer|Root)$",Rx);
            if(owner.Success&&owner.Groups["edge"].Value.Equals("Root",StringComparison.OrdinalIgnoreCase))
            {
                var flange=flanges.FirstOrDefault(x=>x.Name.Equals(owner.Groups["region"].Value,StringComparison.OrdinalIgnoreCase));
                var termination=flange?.StartTermination is not null?$"{flange.Name}Bend.StartTermination":flange?.EndTermination is not null?$"{flange.Name}Bend.EndTermination":null;
                if(termination is not null)return Fail("sheetmetal-bend-termination-profile-delta-conflict",$"ProfileDelta '{parsed.Delta.StableId}' on '{parsed.OwnerPath}' conflicts with bend termination '{termination}'; root ownership cannot be double-trimmed.");
                return Fail("sheetmetal-profile-delta-owner",$"ProfileDelta '{parsed.Delta.StableId}' targets '{parsed.OwnerPath}', but root ProfileDelta is reserved for Sheet Metal bend termination lowering.");
            }
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

        var pendingDatums = blocks.Where(b=>b.Syntax.Type.Name.Text=="Datum").ToList();
        if (pendingDatums.Select(Path).Distinct(StringComparer.Ordinal).Count()!=pendingDatums.Count)
            return Fail("sheetmetal-semantic-datum-duplicate","Datum identities must be unique within each layout.");
        // Forward references use the same lexical lookup as consumers; no suffix-first capture.
        while (pendingDatums.Count>0)
        {
            var resolved=0;string? lastReason=null;
            foreach(var block in pendingDatums.ToArray())
            {
                var region=Token(block,"On");
                if(region is null||!regionSizes.ContainsKey(region))return Fail("sheetmetal-semantic-datum-region",$"Datum '{Path(block)}' references unknown planar region '{region ?? "<missing>"}'.");
                if(!TryPointExpression(block,"At",region,regionSizes,datums,out var point,out var reason)) { lastReason=$"Datum '{Path(block)}': {reason}";continue; }
                datums.Add(new(Path(block),region,point.X,point.Y,"resolved before exact profile lowering"));pendingDatums.Remove(block);resolved++;
            }
            if(resolved==0)return Fail("sheetmetal-semantic-datum-point",lastReason+". Resolve the name in this layout, qualify it, or remove the cyclic reference.");
        }

        foreach(var block in blocks.Where(b=>b.Syntax.Type.Name.Text=="Tab"))
        {
            var on=TokenPath(block,"On");var match=on is null?Match.Empty:Regex.Match(on,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>Outer)$",Rx);
            if(!match.Success||!regionSizes.TryGetValue(match.Groups["region"].Value,out var size))return Fail("sheetmetal-tab-edge",$"Tab '{Path(block)}' requires On: <planar-region>.Outer.");
            if(!Scalar(block,"Center",out var center)||!Scalar(block,"Width",out var width)||!Scalar(block,"Extension",out var extension)||width<=0||extension<=0)
                return Fail("sheetmetal-tab-profile",$"Tab '{Path(block)}' requires Center, positive Width, and positive Extension.");
            if(center-width/2< -1e-8||center+width/2>size.Width+1e-8)return Fail("sheetmetal-tab-outside-edge",$"Tab '{Path(block)}' lies outside '{on}'.");
            if(tabs.Any(x=>x.Region.Equals(match.Groups["region"].Value,StringComparison.OrdinalIgnoreCase)&&center-width/2<x.Center+x.Width/2-1e-8&&center+width/2>x.Center-x.Width/2+1e-8))
                return Fail("sheetmetal-tab-overlap",$"Tab '{Path(block)}' overlaps another tab on '{on}'.");
            tabs.Add(new(Path(block),match.Groups["region"].Value,match.Groups["edge"].Value,center,width,extension));
        }

        foreach(var block in blocks.Where(b=>b.Syntax.Type.Name.Text=="SteppedNotch"))
        {
            var on=TokenPath(block,"On");var match=on is null?Match.Empty:Regex.Match(on,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<edge>Outer)$",Rx);
            if(!match.Success||!regionSizes.TryGetValue(match.Groups["region"].Value,out var size))return Fail("sheetmetal-edge-fragment-owner",$"SteppedNotch '{Path(block)}' requires On: <planar-region>.Outer.");
            if(!Scalar(block,"Center",out var center)||!Scalar(block,"Width",out var width)||!Scalar(block,"Depth",out var depth)||
               !Scalar(block,"ShoulderDepth",out var shoulder)||!Scalar(block,"OuterChamfer",out var outerChamfer)||!Scalar(block,"InnerChamfer",out var innerChamfer))
                return Fail("sheetmetal-edge-fragment-properties",$"SteppedNotch '{Path(block)}' requires Center, Width, Depth, ShoulderDepth, OuterChamfer, and InnerChamfer.");
            var sideToken=Token(block,"Side");var side=sideToken?.Equals("Outward",StringComparison.OrdinalIgnoreCase)==true?1:sideToken?.Equals("Inward",StringComparison.OrdinalIgnoreCase)==true?-1:0;
            if(width<=0||depth<=0||shoulder<0||shoulder>=depth||outerChamfer<0||innerChamfer<=0||2*(outerChamfer+innerChamfer)>=width||side==0)
                return Fail("sheetmetal-edge-fragment-invalid",$"SteppedNotch '{Path(block)}' has impossible dimensions or Side (use Inward/Outward).");
            if(center-width/2< -1e-8||center+width/2>size.Width+1e-8)return Fail("sheetmetal-edge-fragment-outside",$"SteppedNotch '{Path(block)}' lies outside '{on}'.");
            steppedNotches.Add(new(Path(block),match.Groups["region"].Value,match.Groups["edge"].Value,center,width,depth,shoulder,outerChamfer,innerChamfer,side));
        }


        foreach(var block in blocks.Where(b=>b.Syntax.Type.Name.Text=="CornerProfile"))
        {
            var owner=Regex.Match(block.Name,@"^(?<region>[A-Za-z_][A-Za-z0-9_]*)\.(?<corner>RootStart|RootEnd|OuterStart|OuterEnd)$",Rx);
            if(!owner.Success||!regionSizes.ContainsKey(owner.Groups["region"].Value))return Fail("sheetmetal-corner-owner",$"CornerProfile '{Path(block)}' requires <planar-region>.RootStart, .RootEnd, .OuterStart, or .OuterEnd.");
            var operations=block.Syntax.Body?.Statements.OfType<DeclarationSyntax>().Where(d=>d.Type.Name.Text is "Chamfer" or "Cutback" or "Taper" or "NotchCorner" or "Round").ToArray() ?? [];
            if(operations.Length!=1 || operations[0].Name is null || operations[0].Body is null)return Fail("sheetmetal-corner-operation",$"CornerProfile '{Path(block)}' requires exactly one named Chamfer, Cutback, Taper, NotchCorner, or Round operation with a body.");
            var operation=operations[0];var operationBody=new Block(operation,new(operation,errors,bind:binding.Nominal),block.Struct);
            var isRound=operation.Type.Name.Text=="Round";
            var radius=0d;var equal=0d;var setbackA=0d;var setbackB=0d;
            var hasRadius=isRound && Scalar(operationBody,"Radius",out radius);
            var hasEqual=!isRound && Scalar(operationBody,"Setback",out equal);
            var hasA=!isRound && Scalar(operationBody,"SetbackA",out setbackA);var hasB=!isRound && Scalar(operationBody,"SetbackB",out setbackB);
            operationBody.Fields.Complete();
            if(isRound){hasEqual=hasRadius;equal=radius;}
            if(!hasA){setbackA=equal;}if(!hasB){setbackB=equal;}
            var cornerPath=Path(block);var operationPath=$"{cornerPath}.{operation.Name!.Text}";
            if((!hasEqual&&(!hasA||!hasB))||setbackA<=0||setbackB<=0)return Fail("sheetmetal-corner-setback",$"Corner operation '{operationPath}' requires positive Setback or SetbackA and SetbackB.");
            if(corners.Any(x=>x.Region.Equals(owner.Groups["region"].Value,StringComparison.OrdinalIgnoreCase)&&x.Corner.Equals(owner.Groups["corner"].Value,StringComparison.OrdinalIgnoreCase)))
                return Fail("sheetmetal-corner-duplicate",$"Corner '{owner.Groups["region"].Value}.{owner.Groups["corner"].Value}' is authored more than once.");
            corners.Add(new(cornerPath,operationPath,owner.Groups["region"].Value,owner.Groups["corner"].Value,operation.Type.Name.Text,setbackA,setbackB));
        }

        foreach(var block in blocks.Where(b=>b.Syntax.Type.Name.Text=="Pattern"))
        {
            var path=Path(block);var region=Token(block,"On");
            if(region is null||!regionSizes.ContainsKey(region))return Fail("sheetmetal-semantic-pattern-region",$"Pattern '{path}' references unknown planar region '{region ?? "<missing>"}'.");
            if(!Int(block,"Count",out var count)||count<=0||count>1024)return Fail("sheetmetal-semantic-pattern-count",$"Pattern '{path}' requires Count between 1 and 1024.");
            if(!Tuple(block,"Pitch",out var pitch))return Fail("sheetmetal-semantic-pattern-pitch",$"Pattern '{path}' requires a two-dimensional Pitch.");
            if(!TryPointExpression(block,"Center",region,regionSizes,datums,out var center,out var reason))return Fail("sheetmetal-semantic-pattern-center",$"Pattern '{path}' has invalid Center expression: {reason}");
            var feature=block.Fields.Take("Feature") as ObjectSyntax;
            var featureType=FirmamentSyntaxValues.Reference(feature?.Type);
            SheetFeatureKind kind;double? diameter=null,width=null,length=null;
            if (feature is null) return Fail("sheetmetal-semantic-pattern-profile",$"Pattern '{path}' requires an inline Circle/Hole or Slot/Rectangle Feature.");
            var featureFields=FirmamentSyntaxFields.Object(feature,path,errors);
            if(featureType is "Circle" or "Hole")
            {
                kind=SheetFeatureKind.CircularHole;
                if(!featureFields.Measure("Diameter","mm",out var d)||d<=0)return Fail("sheetmetal-semantic-pattern-profile",$"Circular Pattern '{path}' requires positive Diameter.");
                diameter=d;
            }
            else if(featureType is "Slot" or "Rectangle")
            {
                kind=SheetFeatureKind.Slot;
                if(!featureFields.Measure("Width","mm",out var w)||!featureFields.Measure("Length","mm",out var l)||w<=0||l<=0)return Fail("sheetmetal-semantic-pattern-profile",$"Slot Pattern '{path}' requires positive Width and Length.");
                width=w;length=l;
            }
            else return Fail("sheetmetal-semantic-pattern-profile",$"Pattern '{path}' requires a Circle/Hole or Slot/Rectangle Feature. Use a shared Profile Cut for other shapes.");
            featureFields.Complete();

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
        foreach(var block in blocks.Where(b=>b.Syntax.Type.Name.Text=="Require"))
        {
            var path=Path(block);var kindToken=Token(block,"Kind");var members=List(block,"Members");
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
                    if(members.Select(m=>allCuts[m].RegionName).Distinct(StringComparer.Ordinal).Count()!=1)
                        return Fail("sheetmetal-semantic-constraint-frame",$"Require '{path}' compares pitch in different region frames; choose members on one owning region.");
                    if(members.Count<2)return Fail("sheetmetal-semantic-equal-pitch",$"Require '{path}' needs at least two Members.");
                    var axis=Token(block,"Axis");if(axis is not ("X" or "Y" or "x" or "y"))return Fail("sheetmetal-semantic-equal-pitch",$"Require '{path}' needs Axis: X or Axis: Y.");
                    if(!Scalar(block,"Pitch",out var expectedPitch)||expectedPitch<=0)return Fail("sheetmetal-semantic-equal-pitch",$"Require '{path}' needs positive Pitch.");
                    var coordinates=members.Select(x=>axis.Equals("X",StringComparison.OrdinalIgnoreCase)?allCuts[x].X:allCuts[x].Y).ToArray();
                    for(var i=1;i<coordinates.Length;i++)if(Math.Abs(Math.Abs(coordinates[i]-coordinates[i-1])-expectedPitch)>1e-6)
                        return Fail("sheetmetal-semantic-equal-pitch",$"Require '{path}' declares {expectedPitch:G9} mm pitch, but spacing at member {i} is {Math.Abs(coordinates[i]-coordinates[i-1]):G9} mm.");
                    detail=$"{members.Count} members satisfy {expectedPitch:G9} mm {axis.ToUpperInvariant()} pitch";break;
                case SheetMetalSemanticConstraintKind.Mirror:
                    if(members.Count!=2)return Fail("sheetmetal-semantic-mirror",$"Require '{path}' needs exactly two Members.");
                    var about=TokenPath(block,"About");
                    var mirrorAxis=Token(block,"Axis");
                    if(mirrorAxis is not (null or "X" or "Y"))return Fail("sheetmetal-semantic-mirror",$"Require '{path}' Axis requires X or Y.");
                    if(members.Select(m=>allCuts[m].RegionName).Distinct(StringComparer.Ordinal).Count()!=1
                        || about is null||!TryAxis(about,block,allCuts[members[0]].RegionName,mirrorAxis,regionSizes,datums,out var horizontal,out var value))return Fail("sheetmetal-semantic-mirror",$"Require '{path}' needs About: <owning-region>.CenterX/CenterY or a scoped Datum on that region; optional Axis selects X/Y for a Datum.");
                    var left=allCuts[members[0]];var right=allCuts[members[1]];var reflected=horizontal?left with { X=2*value-left.X }:left with { Y=2*value-left.Y };
                    if(Math.Abs(reflected.X-right.X)>1e-6||Math.Abs(reflected.Y-right.Y)>1e-6||left.Kind!=right.Kind||!Near(left.Diameter,right.Diameter)||!Near(left.Width,right.Width)||!Near(left.Length,right.Length))
                        return Fail("sheetmetal-semantic-mirror",$"Require '{path}' is contradicted: '{members[1]}' is not the size-preserving mirror of '{members[0]}' about '{about}'.");
                    detail=$"'{members[0]}' and '{members[1]}' mirror about {about}";break;
                default:throw new InvalidOperationException();
            }
            constraints.Add(new(path,kind,members,"Satisfied",detail));
        }

        foreach (var block in blocks) block.Fields.Complete();
        if (errors.Count>0) return SyntaxFailure();
        return new(true,new(structNames,datums.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),tabs.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),patterns.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),constraints.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),steppedNotches.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),corners.OrderBy(x=>x.CornerPath,StringComparer.Ordinal).ToArray(),attachmentPaths.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray(),profileDeltas.OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray()),generated,diagnostics);

        SheetMetalSemanticLayoutParseResult Fail(string code,string message)=>new(false,SheetMetalSemanticLayout.Empty,[],[SheetMetalSyntaxDiagnostics.At(binding,code,message,root.Span)]);
        SheetMetalSemanticLayoutParseResult SyntaxFailure()=>new(false,SheetMetalSemanticLayout.Empty,[],SheetMetalSyntaxDiagnostics.Codes(errors,binding));
        SheetMetalSemanticLayoutParseResult BindingFailure()=>new(false,SheetMetalSemanticLayout.Empty,[],SheetMetalSyntaxDiagnostics.Binding(binding));
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

    private static bool TryPointExpression(Block block,string field,string region,IReadOnlyDictionary<string,(double Width,double Height)> sizes,IReadOnlyList<SheetMetalSemanticDatum> datums,out Point2 point,out string reason)
    {
        return Resolve(block.Fields.Take(field),out point,out reason);
        bool Resolve(ExpressionSyntax? expression,out Point2 value,out string error)
        {
            value=default;error="expected Point2(x, y), (x, y), a region center or a scoped Datum, optionally plus a Vector2 offset";
            if(TupleValue(expression,out value))return true;
            if(expression is BinarySyntax { Operator: BinaryOperator.Add } sum && Resolve(sum.Left,out var left,out error) && TupleValue(sum.Right,out var right))
            { value=new(left.X+right.X,left.Y+right.Y);return true; }
            var path=FirmamentSyntaxValues.Reference(expression);if(path is null)return false;
            if(path.EndsWith(".Center",StringComparison.Ordinal))
            {
                var target=path[..^7];if(target!=region||!sizes.TryGetValue(target,out var size)){error=$"'{path}' is not the center of owning region '{region}'";return false;}
                value=new(size.Width/2,size.Height/2);return true;
            }
            var datum=ResolveDatum(path,block.Struct,datums,block.DatumNames);
            if(datum is null||datum.Region!=region){error=$"unresolved or ambiguous Datum '{path}' in '{block.Struct ?? "sheet"}' on region '{region}'";return false;}
            value=new(datum.X,datum.Y);return true;
        }
    }
    private static SheetMetalSemanticDatum? ResolveDatum(string path,string? scope,IReadOnlyList<SheetMetalSemanticDatum> datums,IReadOnlySet<string> declared)
    {
        if (!path.Contains('.') && scope is not null && declared.Contains(scope+"."+path)) return datums.SingleOrDefault(d=>d.Path==scope+"."+path);
        var exact=datums.Where(d=>d.Path==path).ToArray();
        return exact.Length==1?exact[0]:null;
    }
    private static bool TryAxis(string path,Block block,string owningRegion,string? axis,IReadOnlyDictionary<string,(double Width,double Height)> sizes,IReadOnlyList<SheetMetalSemanticDatum> datums,out bool xAxis,out double value)
    {
        xAxis=true;value=0;
        if(path.EndsWith(".CenterX",StringComparison.Ordinal)){var region=path[..^8];if(region!=owningRegion||!sizes.TryGetValue(region,out var size)||axis=="Y")return false;value=size.Width/2;return true;}
        if(path.EndsWith(".CenterY",StringComparison.Ordinal)){var region=path[..^8];if(region!=owningRegion||!sizes.TryGetValue(region,out var size)||axis=="X")return false;xAxis=false;value=size.Height/2;return true;}
        var datum=ResolveDatum(path,block.Struct,datums,block.DatumNames);if(datum is null||datum.Region!=owningRegion)return false;
        xAxis=axis!="Y";value=xAxis?datum.X:datum.Y;return true;
    }
    private static string Path(Block block)=>block.Struct is null?block.Name:$"{block.Struct}.{block.Name}";
    private static string? Token(Block block,string name)=>block.Fields.Reference(name);
    private static string? TokenPath(Block block,string name)=>Token(block,name);
    private static IReadOnlyList<string> List(Block block,string name)
    {
        var value=block.Fields.Take(name);
        if(value is SequenceSyntax { Kind: SequenceKind.List } list && list.Items.All(i=>i.Name is null && i.Default is null && SheetMetalSyntaxDiagnostics.Path(i.Value) is not null))
            return list.Items.Select(i=>SheetMetalSyntaxDiagnostics.Path(i.Value)!).ToArray();
        if(value is not null)block.Fields.Invalid(name);return [];
    }
    private static bool Scalar(Block block,string name,out double value)
    {
        value=0;var expression=block.Fields.Take(name);if(expression is null)return false;
        if(FirmamentSyntaxValues.TryMeasure(expression,"mm",out value))return true;
        block.Fields.Invalid(name);return false;
    }
    private static bool Int(Block block,string name,out int value)
    {
        value=0;var expression=block.Fields.Take(name);
        if(FirmamentSyntaxValues.TryMeasure(expression,"",out var number)&&number>=int.MinValue&&number<=int.MaxValue&&number==Math.Truncate(number)){value=(int)number;return true;}
        if(expression is not null)block.Fields.Invalid(name);return false;
    }
    private static bool Tuple(Block block,string name,out Point2 point)=>TupleValue(block.Fields.Take(name),out point);
    private static bool TupleValue(ExpressionSyntax? expression,out Point2 point)
    {
        point=default;
        if(expression is SequenceSyntax { Kind: SequenceKind.Tuple } tuple) expression=tuple with { Kind=SequenceKind.List };
        if(!FirmamentSyntaxValues.Vector(expression,"mm",out var v)||v.Length!=2)return false;
        point=new(v[0],v[1]);return true;
    }
    private static bool Near(double? a,double? b)=>a is null&&b is null||a is not null&&b is not null&&Math.Abs(a.Value-b.Value)<=1e-9;
}
