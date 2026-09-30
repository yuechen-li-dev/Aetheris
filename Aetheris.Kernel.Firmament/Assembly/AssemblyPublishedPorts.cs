using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Semantics;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Definition-owned publication, evaluated after typed template specialization.</summary>
internal static class AssemblyPublishedPorts
{
    internal static string Strip(string source, List<AssemblyDiagnostic> diagnostics)
    {
        var chars = source.ToCharArray();
        foreach (Match expose in Regex.Matches(source,@"\bExpose\s*\{"))
        {
            var body = AssemblyM0Parser.BalancedBody(source,expose.Index+expose.Length-1,diagnostics,"part Expose");
            if (body is not null && Regex.IsMatch(body, @"^\s*Semantic\s+[A-Za-z_]\w*\s*\{"))
                Array.Fill(chars,' ',expose.Index,expose.Length+body.Length+1);
        }
        return new(chars);
    }

    internal static IReadOnlyList<SemanticValue> Read(string source,string owner,string identity,
        string sourceIdentity,List<AssemblyDiagnostic> diagnostics,SectionChain? chain = null)
    {
        var header = Regex.Match(source,$@"\b(?:Struct|Model)\s+{Regex.Escape(owner)}\s*\{{");
        if (!header.Success) return [];
        var body = AssemblyM0Parser.BalancedBody(source,header.Index+header.Length-1,diagnostics,"published part");
        if (body is null) return [];
        var result = new List<SemanticValue>();
        foreach (Match expose in Regex.Matches(body,@"\bExpose\s*\{"))
        {
            var ports = AssemblyM0Parser.BalancedBody(body,expose.Index+expose.Length-1,diagnostics,"part Expose");
            if (ports is null) continue;
            if (!Regex.IsMatch(ports, @"^\s*Semantic\s+[A-Za-z_]\w*\s*\{")) continue;
            ports = Regex.Replace(ports,@"\bDatumFrame\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<chain>[A-Za-z_]\w*)\.Section\.(?<section>[A-Za-z_]\w*)\.Frame\s*;",m =>
            {
                var section = chain?.Sections.SingleOrDefault(s => s.SectionId == m.Groups["section"].Value);
                if (chain is null || chain.StableId != m.Groups["chain"].Value || section is null)
                { diagnostics.Add(new("assembly-port-section-unresolved",$"Published port references missing authored section '{m.Groups["section"].Value}'.")); return m.Value; }
                var f=section.Frame;
                string V(double x,double y,double z) => FormattableString.Invariant($"[{x:R},{y:R},{z:R}]");
                return $"DatumFrame {m.Groups["name"].Value} = {V(f.Origin.X,f.Origin.Y,f.Origin.Z)} x {V(f.XAxis.X,f.XAxis.Y,f.XAxis.Z)} y {V(f.YAxis.X,f.YAxis.Y,f.YAxis.Z)} z {V(f.Normal.X,f.Normal.Y,f.Normal.Z)};";
            });
            // Origin components inherit the definition's millimetre contract.
            ports = Regex.Replace(ports,@"\[[^]]+\]",m => Regex.Replace(m.Value,@"(?<number>[-+0-9.eE]+)mm\b", "${number}"));
            var values = AssemblyM0Parser.ParseSemantics(ports,identity,sourceIdentity,diagnostics).ToArray();
            foreach (var value in values)
            {
                if (value.ExposedMembers.Count == 0)
                    diagnostics.Add(new("assembly-port-empty-or-unresolved",$"Published Semantic '{value.ExposedName}' has no resolved supported members."));
                result.Add(value);
            }
            if (!Regex.IsMatch(ports,@"\bSemantic\s+[A-Za-z_]\w*\s*\{"))
                diagnostics.Add(new("assembly-port-invalid-publication","Part Expose requires named Semantic blocks."));
        }
        foreach (var group in result.GroupBy(p => p.ExposedName).Where(g => g.Count()>1))
            diagnostics.Add(new("assembly-port-duplicate-name",$"Published Semantic '{group.Key}' is duplicated on '{identity}'."));
        return result;
    }
}
