using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// The intentionally small data/template front end for scaffold-backed profile
/// composition.  It is a compiler convenience only: its output is ordinary
/// Point2/Circle2/Line2/Profile/Compose source consumed by the existing route.
/// </summary>
public sealed record StaticGeometryExpansionEvidence(
    IReadOnlyList<string> Records, IReadOnlyList<string> GeneratedProfiles,
    IReadOnlyList<string> GeneratedMembers, IReadOnlyList<string> GeneratedOperations,
    string Template, string Group);

internal static class StaticGeometryExpansion
{
    private const string RecordName = "LobeSpec";
    private static readonly string[] Required = ["Key", "Path", "InnerCenter", "OuterCenter", "Radius", "InnerStart", "InnerEnd", "OuterStart", "OuterEnd", "Sweep", "Role"];
    private static readonly Regex RecordHeader = new(@"\bRecord\s+LobeSpec\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex ArrayHeader = new(@"\bLet\s+Lobes\s*:\s*LobeSpec\[\]\s*=\s*\[", RegexOptions.CultureInvariant);
    private static readonly Regex TemplateHeader = new(@"\bTemplate\s+RoundedLobe\s*<\s*Spec\s*:\s*LobeSpec\s*>", RegexOptions.CultureInvariant);
    private static readonly Regex ExpandHeader = new(@"\bExpand\s+Lobes\s+With\s+RoundedLobe\b", RegexOptions.CultureInvariant);
    private static readonly Regex Point = new(@"^\[\s*(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*\]$", RegexOptions.CultureInvariant);

    public static (string Source, StaticGeometryExpansionEvidence? Evidence, IReadOnlyList<string> Diagnostics) Expand(string source)
    {
        if (!source.Contains("LobeSpec", StringComparison.Ordinal)) return (source, null, []);
        var d = new List<string>();
        if (!RecordHeader.IsMatch(source)) d.Add("static-record-missing:LobeSpec");
        if (!TemplateHeader.IsMatch(source)) d.Add("static-template-argument-type-mismatch:RoundedLobe");
        if (!ExpandHeader.IsMatch(source)) d.Add("static-expansion-source-missing:Expand Lobes With RoundedLobe");
        var array = ArrayHeader.Match(source);
        if (!array.Success) d.Add("static-array-missing-or-non-static:Lobes");
        if (d.Count > 0) return (source, null, d);
        var close = Matching(source, source.IndexOf('[', array.Index + array.Length - 1));
        if (close < 0) return (source, null, ["static-array-unclosed:Lobes"]);
        var rows = Regex.Matches(source[(array.Index + array.Length)..close], @"\bLobeSpec\s*\{(?<body>[\s\S]*?)\}", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        if (rows.Length == 0 || rows.Length > 1024) return (source, null, ["static-array-empty-or-unbounded:Lobes"]);
        var specs = new List<Spec>(); var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var fields = Fields(row.Groups["body"].Value);
            foreach (var required in Required) if (!fields.ContainsKey(required)) d.Add($"static-record-missing-field:LobeSpec.{required}");
            if (d.Count > 0) continue;
            var key = fields["Key"];
            if (!Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant) || !keys.Add(key)) { d.Add($"static-record-duplicate-key:LobeSpec.{key}"); continue; }
            if (!TryPoint(fields["InnerCenter"], out var inner) || !TryPoint(fields["OuterCenter"], out var outer) || !TryPoint(fields["InnerStart"], out var innerStart) || !TryPoint(fields["InnerEnd"], out var innerEnd) || !TryPoint(fields["OuterStart"], out var outerStart) || !TryPoint(fields["OuterEnd"], out var outerEnd) || !TryPath(fields["Path"], out var path) || path.Count != 6 || !TryLength(fields["Radius"], out var radius) || radius <= 0 || fields["Sweep"] is not ("Clockwise" or "CounterClockwise")) { d.Add($"static-record-field-type-mismatch:LobeSpec.{key}"); continue; }
            specs.Add(new(key, path, inner, outer, radius, innerStart, innerEnd, outerStart, outerEnd, fields["Sweep"], fields["Role"]));
        }
        if (d.Count > 0) return (source, null, d.Distinct().ToArray());
        var generated = Generate(source, specs);
        var evidence = new StaticGeometryExpansionEvidence(specs.Select(s => $"record:Lobes[{s.Key}]").ToArray(), specs.Select(s => $"profile:Lobes[{s.Key}].Shape").ToArray(), specs.SelectMany(s => new[] { $"template:RoundedLobe[{s.Key}].Layout.InnerArcGuide", $"template:RoundedLobe[{s.Key}].Layout.OuterArcGuide" }).ToArray(), specs.Select(s => $"compose:Ctc01.Lobes[{s.Key}]").ToArray(), "RoundedLobe", "Lobes");
        return (generated, evidence, []);
    }

    private static string Generate(string source, IReadOnlyList<Spec> specs)
    {
        var scaffold = new StringBuilder("Concept Struct Ctc01BlockoutScaffold On XY {\n  Rect2 PrimaryWebGuide { Center: [0mm, 0mm]; Size: [800mm, 250mm]; Role: ProfileGuide }\n  Rect2 MidLevelGuide { Center: [0mm, 0mm]; Size: [500mm, 300mm]; Role: ProfileGuide }\n");
        var profiles = new StringBuilder();
        foreach (var s in specs)
        {
            for (var i = 0; i < 6; i++) scaffold.Append($"  Point2 {s.Key}P{i} {{ Position: {P(s.Path[i])} }}\n");
            scaffold.Append($"  Point2 {s.Key}InnerCenter {{ Position: {P(s.InnerCenter)} }}\n  Point2 {s.Key}OuterCenter {{ Position: {P(s.OuterCenter)} }}\n  Circle2 {s.Key}InnerArcGuide {{ Center: {s.Key}InnerCenter; Radius: {s.Radius:R}mm }}\n  Circle2 {s.Key}OuterArcGuide {{ Center: {s.Key}OuterCenter; Radius: {s.Radius:R}mm }}\n");
            for (var i = 0; i < 6; i++) if (!Arc(s.Path[i], s.Path[(i + 1) % 6], s.InnerStart, s.InnerEnd) && !Arc(s.Path[i], s.Path[(i + 1) % 6], s.OuterStart, s.OuterEnd)) scaffold.Append($"  Line2 {s.Key}Line{i} {{ From: {s.Key}P{i}; To: {s.Key}P{(i + 1) % 6} }}\n");
            profiles.Append($"Profile {s.Key}Ear Using Ctc01BlockoutScaffold {{ Loop Outer {{\n");
            for (var i = 0; i < 6; i++) { var a = s.Path[i]; var b = s.Path[(i + 1) % 6]; var inner = Arc(a,b,s.InnerStart,s.InnerEnd); var outer = Arc(a,b,s.OuterStart,s.OuterEnd); var label = inner ? "InnerArc" : outer ? "OuterArc" : $"Line{i}"; var trace = inner ? $"{s.Key}InnerArcGuide" : outer ? $"{s.Key}OuterArcGuide" : $"{s.Key}Line{i}"; profiles.Append($"  Segment {label} {{ Trace: {trace}; From: {s.Key}P{i}; To: {s.Key}P{(i + 1) % 6}" + ((inner || outer) ? $"; Sweep: {s.Sweep}" : "") + " }\n"); }
            profiles.Append("} }\n");
        }
        scaffold.Append("}\n");
        // Keep the non-repetitive primary/mid/hex declarations authored in the source.
        var suffixStart = source.IndexOf("Profile PrimaryWeb", StringComparison.Ordinal);
        var suffix = suffixStart >= 0 ? source[suffixStart..] : string.Empty;
        var result = scaffold + profiles.ToString() + suffix;
        // A group is source-only.  Its members retain the data key in the lowered
        // operation name, so the section compiler still sees individual Adds.
        var group = Regex.Match(result, @"\bAdd\s+Lobes\s*\{\s*Profiles\s*:\s*\[(?<profiles>[\w\s,]+)\]\s*;?\s*From\s*:\s*(?<from>[-+.\d]+)mm\s*;?\s*To\s*:\s*(?<to>[-+.\d]+)mm\s*;?\s*Role\s*:\s*(?<role>\w+)\s*\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (group.Success)
        {
            var adds = string.Join("\n", group.Groups["profiles"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(profile =>
            {
                var key = profile.EndsWith("Ear", StringComparison.Ordinal) ? profile[..^3] : profile;
                return $"Add {key}Lobe {{ Profile: {profile}; From: {group.Groups["from"].Value}mm; To: {group.Groups["to"].Value}mm; Role: {group.Groups["role"].Value} }}";
            }));
            result = result.Remove(group.Index, group.Length).Insert(group.Index, adds);
        }
        return result;
    }
    private static bool Arc((double X,double Y) a, (double X,double Y) b, (double X,double Y) x, (double X,double Y) y) => Same(a,x) && Same(b,y);
    private static bool Same((double X,double Y) a, (double X,double Y) b) => Math.Abs(a.X-b.X)<1e-9 && Math.Abs(a.Y-b.Y)<1e-9;
    private static string P((double X,double Y) p) => $"[{p.X:R}mm, {p.Y:R}mm]";
    private static Dictionary<string,string> Fields(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal); var start = 0; var depth = 0;
        for (var i = 0; i <= body.Length; i++)
        {
            if (i < body.Length && body[i] == '[') depth++;
            else if (i < body.Length && body[i] == ']') depth--;
            if (i != body.Length && (body[i] != ';' || depth != 0)) continue;
            var part = body[start..i].Trim(); start = i + 1;
            var colon = part.IndexOf(':'); if (colon <= 0) continue;
            result[part[..colon].Trim()] = part[(colon + 1)..].Trim();
        }
        return result;
    }
    private static bool TryLength(string s,out double v) => double.TryParse(s.Replace("mm", "", StringComparison.Ordinal).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && s.Contains("mm", StringComparison.Ordinal);
    private static bool TryPoint(string s,out (double X,double Y) p) { var m=Point.Match(s); p=m.Success?(double.Parse(m.Groups["x"].Value,CultureInfo.InvariantCulture),double.Parse(m.Groups["y"].Value,CultureInfo.InvariantCulture)):default; return m.Success; }
    private static bool TryPath(string s,out List<(double X,double Y)> p) { p=[]; foreach(Match m in Regex.Matches(s,@"\[\s*(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*\]",RegexOptions.CultureInvariant))p.Add((double.Parse(m.Groups["x"].Value,CultureInfo.InvariantCulture),double.Parse(m.Groups["y"].Value,CultureInfo.InvariantCulture))); return p.Count>0; }
    private static int Matching(string text,int open) { var depth=0; for(var i=open;i<text.Length;i++){if(text[i]=='[')depth++;else if(text[i]==']'&&--depth==0)return i;} return -1; }
    private sealed record Spec(string Key,List<(double X,double Y)> Path,(double X,double Y) InnerCenter,(double X,double Y) OuterCenter,double Radius,(double X,double Y) InnerStart,(double X,double Y) InnerEnd,(double X,double Y) OuterStart,(double X,double Y) OuterEnd,string Sweep,string Role);
}
