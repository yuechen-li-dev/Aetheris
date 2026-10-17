using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record ParsedSemanticProfileDelta(
    string OwnerPath,SemanticProfileDeltaIr Delta,
    IReadOnlyList<SemanticProfileDeltaExposure> Exposures);

public sealed record SemanticProfileDeltaExposure(
    string Path,string MemberPath,double StartU,double EndU,double Offset,
    IReadOnlyList<string> Capabilities);

public sealed record SemanticProfileDeltaParseResult(
    IReadOnlyList<ParsedSemanticProfileDelta> Deltas,IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess=>Diagnostics.Count==0;
}

/// <summary>Parser for the finite, template-friendly ProfileDelta semantic substrate.</summary>
public static class SemanticProfileDeltaParser
{

    public static SemanticProfileDeltaParseResult Parse(string source, Func<ExpressionSyntax, ExpressionSyntax>? bind = null)
        => ParseDeclarations(FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document)
            .Where(d=>d.Type.Name.Text=="ProfileDelta" && d.Body is not null),bind);

    public static SemanticProfileDeltaParseResult ParseDeclarations(IEnumerable<DeclarationSyntax> declarations, Func<ExpressionSyntax,ExpressionSyntax>? bind=null)
    {
        var result=new List<ParsedSemanticProfileDelta>();var diagnostics=new List<string>();
        foreach(var block in declarations)
        {
            if(block.Type.Name.Text!="ProfileDelta" || block.Name is null || block.Body is null || block.Type.Arguments.Count>0 || block.Modifiers.Count>0)
            { diagnostics.Add("semantic-profile-delta-header-invalid: requires a concrete named ProfileDelta body");continue; }
            var id=block.Name?.Text ?? "<unnamed>";var fields = Fields(block, diagnostics, bind);var on=fields.Reference("On");
            var anchorText=fields.Take("Anchor");var sideText=fields.Reference("Side");
            if(on is null||anchorText is null||!TryAnchor(anchorText,out var anchor)||sideText is not ("Inward" or "Outward"))
            {diagnostics.Add($"semantic-profile-delta-header-invalid:{id}:requires On, Anchor, and Side");continue;}
            var side=sideText=="Outward"?1:-1;
            var levels=new List<SemanticProfileDeltaLevelIr>();
            foreach(var level in Children(block,"Level"))
            {
                var levelFields = Fields(level, diagnostics, bind);
                if(!Measure(levelFields,"Offset",out var offset)||offset<0)
                {diagnostics.Add($"semantic-profile-delta-level-invalid:{id}.{level.Name!.Text}");continue;}
                levelFields.Complete();
                levels.Add(new(level.Name!.Text,$"{id}.{level.Name.Text}",offset,$"ProfileDelta {id}.Level {level.Name.Text}"));
            }
            var members=new List<(int Index,SemanticProfileDeltaMemberIr Member)>();
            foreach(var member in Children(block,"Span"))
            {
                var memberFields = Fields(member, diagnostics, bind);
                if(!Measure(memberFields,"Run",out var run)||memberFields.Reference("At") is not { } at)
                {diagnostics.Add($"semantic-profile-delta-span-invalid:{id}.{member.Name!.Text}");continue;}
                var expose=memberFields.Reference("Expose");var capabilities=List(memberFields.Take("Capabilities"),diagnostics);
                memberFields.Complete();
                if(expose is not null&&capabilities.Count==0)capabilities=["StableSemanticIdentity"];
                members.Add((member.Span.Start,new(member.Name!.Text,$"{id}.{member.Name.Text}",SemanticProfileDeltaMemberKind.Span,run,at,
                    expose is null?null:$"{id}.{expose}",capabilities,$"ProfileDelta {id}.Span {member.Name!.Text}")));
            }
            foreach(var member in Children(block,"Transition"))
            {
                var memberFields = Fields(member, diagnostics, bind);
                var kindText=memberFields.Reference("Kind");var to=memberFields.Reference("To");
                var kind=kindText=="Diagonal"?SemanticProfileDeltaMemberKind.Diagonal:kindText=="Step"?SemanticProfileDeltaMemberKind.Step:kindText=="Round"?SemanticProfileDeltaMemberKind.Round:(SemanticProfileDeltaMemberKind?)null;
                var hasRun=Measure(memberFields,"Run",out var run);
                var hasRadius=Measure(memberFields,"Radius",out var radius);
                var concaveValue=memberFields.Take("Concave");
                var concave=concaveValue is LiteralSyntax { Kind: LiteralKind.Boolean, Raw: "true" };
                if (concaveValue is not null && concaveValue is not LiteralSyntax { Kind: LiteralKind.Boolean }) memberFields.Invalid("Concave");
                memberFields.Complete();
                if(kind is null||to is null|| (kind!=SemanticProfileDeltaMemberKind.Round && (hasRadius || concaveValue is not null)) ||(kind is SemanticProfileDeltaMemberKind.Diagonal or SemanticProfileDeltaMemberKind.Round&&!hasRun)||(kind==SemanticProfileDeltaMemberKind.Step&&hasRun&&Math.Abs(run)>1e-9)||(kind==SemanticProfileDeltaMemberKind.Round&&!hasRadius))
                {diagnostics.Add($"semantic-profile-delta-transition-invalid:{id}.{member.Name!.Text}");continue;}
                members.Add((member.Span.Start,new(member.Name!.Text,$"{id}.{member.Name.Text}",kind.Value,kind==SemanticProfileDeltaMemberKind.Step?0:run,to,null,[],
                    $"ProfileDelta {id}.Transition {member.Name!.Text}",kind==SemanticProfileDeltaMemberKind.Round?radius:null,concave)));
            }
            fields.Complete();
            if (levels.Select(l=>l.Name).Distinct(StringComparer.Ordinal).Count()!=levels.Count)
            { diagnostics.Add($"semantic-profile-delta-level-duplicate:{id}"); continue; }
            if (members.Select(m=>m.Member.Name).Distinct(StringComparer.Ordinal).Count()!=members.Count)
            { diagnostics.Add($"semantic-profile-delta-member-duplicate:{id}"); continue; }
            var ordered=members.OrderBy(x=>x.Index).Select(x=>x.Member).ToArray();
            var delta=new SemanticProfileDeltaIr(id,id,anchor,side,levels,ordered,$"ProfileDelta {id}");
            var exposures=new List<SemanticProfileDeltaExposure>();var cursor=0d;var current=0d;
            var byLevel=levels.ToDictionary(x=>x.Name,x=>x.Offset,StringComparer.Ordinal);
            foreach(var member in ordered)
            {
                var start=cursor;cursor+=member.Run;if(byLevel.TryGetValue(member.ToLevel,out var target))current=target;
                if(member.ExposeAs is not null)exposures.Add(new(member.ExposeAs,member.StableId,start,cursor,side*current,member.Capabilities));
            }
            result.Add(new(on,delta,exposures));
        }
        return new(result,diagnostics);
    }

    private static bool Measure(FirmamentSyntaxFields fields,string name,out double value)
    {
        value=0;var expression=fields.Take(name);if(expression is null)return false;
        if(FirmamentSyntaxValues.TryMeasure(expression,"mm",out value))return true;
        fields.Invalid(name);return false;
    }
    private static bool TryAnchor(ExpressionSyntax expression,out SemanticEdgeAnchorIr anchor)
    {
        anchor=new(SemanticEdgeAnchorKind.FromStart,0);
        if (expression is not CallSyntax { Target: ReferenceSyntax kind, Arguments.Count: 1 } call
            || kind.Name.Text is not ("FromStart" or "FromEnd" or "CenteredAt")
            || call.Arguments[0].Name is not null || call.Arguments[0].Default is not null
            || !FirmamentSyntaxValues.TryMeasure(call.Arguments[0].Value,"mm",out var value)) return false;
        anchor=new(Enum.Parse<SemanticEdgeAnchorKind>(kind.Name.Text),value);return true;
    }
    private static IEnumerable<DeclarationSyntax> Children(DeclarationSyntax owner,string type) =>
        owner.Body!.Statements.OfType<DeclarationSyntax>().Where(d=>d.Type.Name.Text==type && d.Name is not null && d.Body is not null);
    private static FirmamentSyntaxFields Fields(DeclarationSyntax owner,List<string> diagnostics,Func<ExpressionSyntax,ExpressionSyntax>? bind)
    {
        // The program owns these child productions; all other children remain for
        // the field owner to reject. Geometry program validation remains downstream.
        var children = owner.Type.Name.Text == "ProfileDelta" ? new[]{"Level","Span","Transition"} : [];
        return new(owner with { Body = owner.Body! with { Statements = owner.Body.Statements.Where(s =>
            s is not DeclarationSyntax d || !children.Contains(d.Type.Name.Text) || d.Name is null || d.Body is null).ToArray() } },diagnostics,bind:bind);
    }
    private static IReadOnlyList<string> List(ExpressionSyntax? value,List<string> diagnostics)
    {
        if (value is null) return [];
        if (value is SequenceSyntax { Kind: SequenceKind.List } list && list.Items.All(i=>i.Name is null && i.Default is null && FirmamentSyntaxValues.Reference(i.Value) is not null))
            return list.Items.Select(i=>FirmamentSyntaxValues.Reference(i.Value)!).ToArray();
        diagnostics.Add("semantic-profile-delta-capabilities-invalid");return [];
    }
}
