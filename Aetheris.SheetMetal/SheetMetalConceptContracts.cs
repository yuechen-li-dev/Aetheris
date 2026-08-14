using System.Text.RegularExpressions;

namespace Aetheris.SheetMetal;

internal static class SheetMetalConceptContracts
{
    internal sealed record Failure(string Code,string Message);

    internal static Failure? Validate(string source,SheetMetalConstructionSpec spec)
    {
        var concept=spec.SatisfiesConcept;
        if(string.IsNullOrWhiteSpace(concept))return null;
        var declaration=Regex.Match(source,$@"\bConcept\s+{Regex.Escape(concept)}\s*\{{(?<body>[\s\S]*?)\}}",RegexOptions.CultureInvariant);
        if(!declaration.Success)return new("firmament-template-unknown-concept-constraint",$"SheetMetal '{spec.Name}' claims unknown Concept '{concept}'.");
        var available=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [spec.Base.Name]="SheetRegion",
            ["Flat"]="FlatPattern"
        };
        foreach(var flange in spec.Flanges)
        {
            available[flange.Name]="SheetFlange";
            available[flange.Name+"Bend"]="SheetBend";
        }
        foreach(Match requirement in Regex.Matches(declaration.Groups["body"].Value,@"(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*$",RegexOptions.CultureInvariant))
        {
            var name=requirement.Groups["name"].Value;var expected=requirement.Groups["type"].Value;
            if(!available.TryGetValue(name,out var actual))return new("firmament-concept-missing-member",$"SheetMetal '{spec.Name}' claims to satisfy '{concept}' but does not expose required semantic member '{name}: {expected}'.");
            if(!string.Equals(expected,actual,StringComparison.Ordinal))return new("firmament-concept-type-mismatch",$"SheetMetal '{spec.Name}.{name}' exposes '{actual}', but Concept '{concept}' requires '{expected}'.");
        }
        return null;
    }
}
