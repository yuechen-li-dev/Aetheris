using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Semantics;
using Aetheris.Surfacing;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Definition-owned publication, evaluated after typed template specialization.</summary>
internal static class AssemblyPublishedPorts
{
    // Publish semantic intent before a reusable assembly's local solve. This
    // reads authored frames and uses the existing typed specialization/binder;
    // it never materializes a body or imports STEP.
    internal static IReadOnlyList<SemanticValue> BindAuthored(string identity, string declarations,
        string sourceIdentity, List<AssemblyDiagnostic> diagnostics, FirmamentProjectSnapshot? project,
        List<AssemblySourceDependencyIr> dependencies)
    {
        var file = Regex.Match(identity, "^(?:SectionChainFile|LoftFile)<\\\"(?<path>[^\\\"]+)\\\">$");
        if (file.Success)
        {
            var requested = file.Groups["path"].Value;
            string path, source;
            if (project is not null)
            {
                try { path = FirmamentProjectSnapshot.NormalizePath(requested); }
                catch (ArgumentException)
                { diagnostics.Add(new("assembly-profile-invalid-resource-path", $"Invalid port source path '{requested}'.")); return []; }
                if (!path.EndsWith(".firmament", StringComparison.OrdinalIgnoreCase))
                { diagnostics.Add(new("assembly-profile-resource-kind-invalid", "Port sources must be .firmament documents.")); return []; }
                if (!project.TryResolve(path, out source!))
                { diagnostics.Add(new("assembly-profile-unresolved-resource", $"Port source '{path}' was not found in the project snapshot.")); return []; }
            }
            else
            {
                path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourceIdentity))!, requested));
                if (!File.Exists(path))
                { diagnostics.Add(new("assembly-profile-unresolved-resource", $"Port source '{requested}' was not found at '{path}'.")); return []; }
                source = File.ReadAllText(path);
            }
            var resourceDependencies = new List<AssemblySourceDependencyIr>();
            var loaded = AssemblyM0Parser.LoadResource(path, project, resourceDependencies, diagnostics);
            if (loaded is null) return [];
            source = loaded;
            dependencies.AddRange(resourceDependencies.Where(d => !dependencies.Any(existing => existing.Path == d.Path))
                .Select(d => d with { IsRoot = false }));
            if (!dependencies.Any(d => d.Path == path))
                dependencies.Add(new(path, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))), false));
            source = FirmamentSourceSpelling.Normalize(source);
            if (!Regex.IsMatch(source, @"\bExpose\s*\{\s*Semantic\b")) return [];
            var chain = SectionChainAuthoringParser.Compile(Strip(source, diagnostics), materialize: false);
            if (!chain.IsSuccess || chain.Chain is null)
            {
                foreach (var error in chain.Diagnostics) diagnostics.Add(new("assembly-port-section-binding-failed", error));
                return [];
            }
            var owner = Regex.Match(source, @"\bModel\s+(?<name>[A-Za-z_]\w*)\s*\{").Groups["name"].Value;
            return Read(source, owner, identity, path, diagnostics, chain.Chain);
        }
        if (!identity.Contains('<')) return [];
        var templateName = identity[..identity.IndexOf('<')].Trim();
        var header = Regex.Match(declarations, $@"\b(?:Struct|Model)\s+{Regex.Escape(templateName)}(?:\s*:\s*[A-Za-z_]\w*)?\s*\{{");
        if (!header.Success) return [];
        var body = AssemblyM0Parser.BalancedBody(declarations, header.Index + header.Length - 1, diagnostics, "port definition");
        if (body is null || !Regex.IsMatch(body, @"\bExpose\s*\{\s*Semantic\b")) return [];
        var inspectionDiagnostics = new List<string>();
        var kind = FirmamentV2TemplateExpansion.Inspect(declarations, inspectionDiagnostics)
            .SingleOrDefault(t => t.Name == templateName)?.TargetKind ?? "Struct";
        var application = $"{kind} __AssemblyPart = {identity}\n";
        var module = kind == "Model" ? declarations + "\n" + application
            : "Model __AssemblyDefinition {\n Units: mm\n" + declarations + "\n" + application + "}\n";
        var expansion = FirmamentTemplateSourceCompiler.Expand(module, out var errors);
        if (expansion is null || errors.Count > 0)
        {
            foreach (var error in errors) diagnostics.Add(new("assembly-port-specialization-failed", error));
            return [];
        }
        return Read(expansion.ExpandedSource, "__AssemblyPart", identity, sourceIdentity, diagnostics);
    }
    internal static string Strip(string source, List<AssemblyDiagnostic> diagnostics)
    {
        source = FirmamentSourceSpelling.Normalize(source);
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
        source = FirmamentSourceSpelling.Normalize(source);
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
            // Definition-owned origins use the same dimension-checked scalar
            // evaluator as authored placement. Template dimensions may derive
            // mounting stations; bare legacy numbers still inherit millimetres.
            ports = Regex.Replace(ports, @"\bDatumFrame\s+(?<name>[A-Za-z_]\w*)\s*=\s*\[(?<origin>[^]]+)\]", match =>
            {
                var components = match.Groups["origin"].Value.Split(',');
                var values = new double[3];
                if (components.Length != 3 || components.Where((component, index) =>
                        !FirmamentV2FeatureExpansion.TryEvaluateScalar(component.Trim(), out values[index], out var unit)
                        || (unit != "mm" && !(unit.Length == 0 && double.TryParse(component.Trim(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out _))) || !double.IsFinite(values[index])).Any())
                {
                    diagnostics.Add(new("assembly-port-frame-origin-invalid", $"Published DatumFrame '{match.Groups["name"].Value}' requires three finite length components."));
                    return match.Value;
                }
                return $"DatumFrame {match.Groups["name"].Value} = [{string.Join(",", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))}]";
            });
            // Remaining literal vectors retain the definition's millimetre contract.
            ports = Regex.Replace(ports,@"\[[^]]+\]",m => Regex.Replace(m.Value,@"(?<number>[-+0-9.eE]+)mm\b", "${number}"));
            var values = AssemblyM0Parser.ParseSemantics(ports,identity,sourceIdentity,diagnostics).ToArray();
            foreach (var value in values)
            {
                if (value.ExposedMembers.Count == 0)
                    diagnostics.Add(new("assembly-port-empty-or-unresolved",$"Published Semantic '{value.ExposedName}' has no resolved supported members."));
                result.Add(new SemanticValue(value.StableIdentity, value.Type, value.Capabilities.Values, value.Bindings,
                    value.ExposedMembers.Values, [.. value.Provenance, new("definition-port-publication", identity, value.ExposedName ?? "", SemanticSourceSpan.Generated(sourceIdentity))],
                    value.AuthoredSourceSpan, value.GeneratedSourceSpan, value.ExposedName));
            }
            if (!Regex.IsMatch(ports,@"\bSemantic\s+[A-Za-z_]\w*\s*\{"))
                diagnostics.Add(new("assembly-port-invalid-publication","Part Expose requires named Semantic blocks."));
        }
        foreach (var group in result.GroupBy(p => p.ExposedName).Where(g => g.Count()>1))
            diagnostics.Add(new("assembly-port-duplicate-name",$"Published Semantic '{group.Key}' is duplicated on '{identity}'."));
        return result;
    }
}
