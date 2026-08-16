using System.Globalization;
using System.Text.RegularExpressions;

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
    private const RegexOptions Rx=RegexOptions.CultureInvariant|RegexOptions.Singleline;
    private const string Number=@"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)";

    public static SemanticProfileDeltaParseResult Parse(string source)
    {
        var result=new List<ParsedSemanticProfileDelta>();var diagnostics=new List<string>();
        foreach(var block in Blocks(source,"ProfileDelta"))
        {
            var id=block.Name;var on=TokenPath(block.Body,"On");
            var anchorText=Field(block.Body,"Anchor");var sideText=Token(block.Body,"Side");
            if(on is null||anchorText is null||!TryAnchor(anchorText,out var anchor)||sideText is not ("Inward" or "Outward"))
            {diagnostics.Add($"semantic-profile-delta-header-invalid:{id}:requires On, Anchor, and Side");continue;}
            var side=sideText=="Outward"?1:-1;
            var levels=new List<SemanticProfileDeltaLevelIr>();
            foreach(var level in Blocks(block.Body,"Level"))
            {
                if(!Length(level.Body,"Offset",out var offset)||offset<0)
                {diagnostics.Add($"semantic-profile-delta-level-invalid:{id}.{level.Name}");continue;}
                levels.Add(new(level.Name,$"{id}.{level.Name}",offset,$"ProfileDelta {id}.Level {level.Name}"));
            }
            var members=new List<(int Index,SemanticProfileDeltaMemberIr Member)>();
            foreach(var member in Blocks(block.Body,"Span"))
            {
                if(!Length(member.Body,"Run",out var run)||Token(member.Body,"At") is not { } at)
                {diagnostics.Add($"semantic-profile-delta-span-invalid:{id}.{member.Name}");continue;}
                var expose=Token(member.Body,"Expose");var capabilities=List(member.Body,"Capabilities");
                if(expose is not null&&capabilities.Count==0)capabilities=["StableSemanticIdentity"];
                members.Add((member.Index,new(member.Name,$"{id}.{member.Name}",SemanticProfileDeltaMemberKind.Span,run,at,
                    expose is null?null:$"{id}.{expose}",capabilities,$"ProfileDelta {id}.Span {member.Name}")));
            }
            foreach(var member in Blocks(block.Body,"Transition"))
            {
                var kindText=Token(member.Body,"Kind");var to=Token(member.Body,"To");
                var kind=kindText=="Diagonal"?SemanticProfileDeltaMemberKind.Diagonal:kindText=="Step"?SemanticProfileDeltaMemberKind.Step:kindText=="Round"?SemanticProfileDeltaMemberKind.Round:(SemanticProfileDeltaMemberKind?)null;
                var hasRun=Length(member.Body,"Run",out var run);
                var hasRadius=Length(member.Body,"Radius",out var radius);
                var concave=string.Equals(Token(member.Body,"Concave"),"true",StringComparison.OrdinalIgnoreCase);
                if(kind is null||to is null||(kind is SemanticProfileDeltaMemberKind.Diagonal or SemanticProfileDeltaMemberKind.Round&&!hasRun)||(kind==SemanticProfileDeltaMemberKind.Step&&hasRun&&Math.Abs(run)>1e-9)||(kind==SemanticProfileDeltaMemberKind.Round&&!hasRadius))
                {diagnostics.Add($"semantic-profile-delta-transition-invalid:{id}.{member.Name}");continue;}
                members.Add((member.Index,new(member.Name,$"{id}.{member.Name}",kind.Value,kind==SemanticProfileDeltaMemberKind.Step?0:run,to,null,[],
                    $"ProfileDelta {id}.Transition {member.Name}",kind==SemanticProfileDeltaMemberKind.Round?radius:null,concave)));
            }
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

    private static bool TryAnchor(string text,out SemanticEdgeAnchorIr anchor)
    {
        var match=Regex.Match(text,$@"^(?<kind>FromStart|FromEnd|CenteredAt)\s+(?<v>{Number})mm$",Rx);
        anchor=new(SemanticEdgeAnchorKind.FromStart,0);if(!match.Success)return false;
        anchor=new(Enum.Parse<SemanticEdgeAnchorKind>(match.Groups["kind"].Value),double.Parse(match.Groups["v"].Value,CultureInfo.InvariantCulture));return true;
    }
    private static bool Length(string body,string name,out double value)
    {var m=Regex.Match(body,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>{Number})mm\s*;",Rx);value=m.Success?double.Parse(m.Groups["v"].Value,CultureInfo.InvariantCulture):0;return m.Success&&double.IsFinite(value);}
    private static string? Field(string body,string name){var m=Regex.Match(body,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>[^;]+)\s*;",Rx);return m.Success?m.Groups["v"].Value.Trim():null;}
    private static string? Token(string body,string name){var m=Regex.Match(body,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>[A-Za-z_]\w*)\s*;",Rx);return m.Success?m.Groups["v"].Value:null;}
    private static string? TokenPath(string body,string name){var m=Regex.Match(body,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;",Rx);return m.Success?m.Groups["v"].Value:null;}
    private static IReadOnlyList<string> List(string body,string name){var m=Regex.Match(body,$@"\b{Regex.Escape(name)}\s*:\s*\[(?<v>[^]]*)\]\s*;",Rx);return m.Success?m.Groups["v"].Value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries):[];}
    private static IReadOnlyList<Block> Blocks(string source,string keyword)
    {
        var result=new List<Block>();foreach(Match match in Regex.Matches(source,$@"\b{Regex.Escape(keyword)}\s+(?<name>[A-Za-z_]\w*)\s*\{{",Rx))
        {var open=source.IndexOf('{',match.Index);var close=Close(source,open);if(close>open)result.Add(new(match.Groups["name"].Value,source[(open+1)..close],match.Index));}return result;
    }
    private static int Close(string source,int open){var depth=0;for(var i=open;i<source.Length;i++){if(source[i]=='{')depth++;else if(source[i]=='}'&&--depth==0)return i;}return -1;}
    private sealed record Block(string Name,string Body,int Index);
}
