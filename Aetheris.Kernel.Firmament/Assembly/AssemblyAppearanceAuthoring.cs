using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Assembly;

[FirmamentConstruct("Appearance", "Appearance", Context = "Declaration", Entry = "Appearance Name { Color: [0.5,0.5,0.5]; Metallic: 0; Roughness: 0.3; }", Description = "A finite preview look; shader realization stays downstream.")]
[FirmamentField("Color", "Color", FirmamentSchemaValueKind.Vector, Required = true)]
[FirmamentField("Metallic", "Metallic", FirmamentSchemaValueKind.Scalar, Required = true)]
[FirmamentField("Roughness", "Roughness", FirmamentSchemaValueKind.Scalar, Required = true)]
public static class AssemblyAppearanceDeclaration { }
public sealed record AssemblyAppearance(string Name, AssemblyUsdMaterial Preview);

[FirmamentConstruct("PhysicalMaterial", "Material", Context = "Declaration", Entry = "Material Steel { Identity: \"steel\"; Appearance: PolishedMetal; }", Description = "Physical identity with a default appearance; this declaration does not invent engineering properties.")]
[FirmamentField("Identity", "Identity", FirmamentSchemaValueKind.String, Required = true)]
[FirmamentField("Appearance", "Appearance", FirmamentSchemaValueKind.ConstructReference, Required = true)]
public static class AssemblyPhysicalMaterialDeclaration { }
public sealed record AssemblyPhysicalMaterial(string Name, string Identity, string Appearance);
public sealed record AssemblyAppearanceCatalog(IReadOnlyDictionary<string, AssemblyAppearance> Appearances,
    IReadOnlyDictionary<string, AssemblyPhysicalMaterial> Materials);
public sealed record AssemblyMaterialSelection(string Material, string? AppearanceOverride = null);
public sealed record AssemblyAppearanceBinding(string Material, string PhysicalIdentity, string Appearance,
    AssemblyUsdMaterial Preview, string Authority);

/// <summary>Checked physical identity/default look and occurrence finish overrides.
/// No FEA properties or geometry selectors are inferred from display color.</summary>
internal static class AssemblyAppearanceAuthoring
{
    internal static AssemblyAppearanceCatalog Parse(ref string source, List<AssemblyDiagnostic> diagnostics,
        out IReadOnlyList<(int Start, int Length)> erasedSpans)
    {
        var appearances = new Dictionary<string, AssemblyAppearance>(StringComparer.Ordinal);
        var materials = new Dictionary<string, AssemblyPhysicalMaterial>(StringComparer.Ordinal);
        var changes = new List<(int Start, int Length)>();
        foreach (Match header in Regex.Matches(source, @"\b(?<kind>Appearance|Material)\s+(?<name>[A-Za-z_]\w*)\s*\{"))
        {
            var close = source.IndexOf('}', header.Index + header.Length);
            if (close < 0) { Error("declaration-malformed", header.Value); continue; }
            var body = source[(header.Index + header.Length)..close]; var name = header.Groups["name"].Value;
            if (body.Contains('{')) { Error("declaration-fields-invalid", name); continue; }
            var fields = Fields(body);
            var allowed = header.Groups["kind"].Value == "Appearance" ? new[] { "Color", "Metallic", "Roughness" } : new[] { "Identity", "Appearance" };
            if (fields.Count != allowed.Length || !allowed.All(fields.ContainsKey)) { Error("declaration-fields-invalid", name); continue; }
            if (allowed.Length == 3)
            {
                var color = Regex.Match(fields["Color"], @"^\[(?<v>[^]]+)\]$");
                var rgb = color.Success ? color.Groups["v"].Value.Split(',').Select(Number).ToArray() : [];
                var metal = Number(fields["Metallic"]); var rough = Number(fields["Roughness"]);
                if (rgb.Length != 3 || rgb.Concat([metal, rough]).Any(v => !double.IsFinite(v) || v < 0 || v > 1)) Error("values-invalid", name);
                else if (!appearances.TryAdd(name, new(name, new(rgb[0], rgb[1], rgb[2], metal, rough)))) Error("duplicate-appearance", name);
            }
            else
            {
                var identity = Regex.Match(fields["Identity"], "^\"(?<id>[^\"\\r\\n]+)\"$");
                if (!identity.Success || !Regex.IsMatch(fields["Appearance"], @"^[A-Za-z_]\w*$")) Error("material-invalid", name);
                else if (!materials.TryAdd(name, new(name, identity.Groups["id"].Value, fields["Appearance"]))) Error("duplicate-material", name);
            }
            changes.Add((header.Index, close - header.Index + 1));
        }
        foreach (var material in materials.Values)
            if (!appearances.ContainsKey(material.Appearance)) Error("unknown-appearance", material.Name + ":" + material.Appearance);
        var chars = source.ToCharArray();
        foreach (var change in changes) Array.Fill(chars, ' ', change.Start, change.Length);
        source = new(chars);
        erasedSpans = changes;
        return new(appearances, materials);

        Dictionary<string, string> Fields(string body)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var matches = Regex.Matches(body, @"(?<key>[A-Za-z_]\w*)\s*:\s*(?<value>\[[^]]*\]|""[^""]*""|[^;{}]+)\s*;");
            var remaining = body;
            foreach (Match match in matches)
                if (!result.TryAdd(match.Groups["key"].Value, match.Groups["value"].Value.Trim())) Error("duplicate-field", match.Groups["key"].Value);
            foreach (Match match in matches.Cast<Match>().Reverse()) remaining = remaining.Remove(match.Index, match.Length);
            if (!string.IsNullOrWhiteSpace(remaining)) Error("declaration-fields-invalid", body);
            return result;
        }
        void Error(string code, string subject) => diagnostics.Add(new("assembly-appearance-" + code, subject));
    }

    internal static AssemblyMaterialSelection? Selection(string body, List<AssemblyDiagnostic> diagnostics)
    {
        var fields = Regex.Matches(body, @"\bMaterial\s*:\s*(?<material>[A-Za-z_]\w*)(?:\s+with\s*\{\s*Appearance\s*:\s*(?<look>[A-Za-z_]\w*)\s*;\s*\})?\s*;");
        if (fields.Count > 1 || Regex.Matches(body, @"\bMaterial\s*:").Count != fields.Count)
            diagnostics.Add(new("assembly-appearance-selection-invalid", "Expected one Material identity and optional with { Appearance: Name; } override."));
        return fields.Count == 1 ? new(fields[0].Groups["material"].Value, fields[0].Groups["look"].Success ? fields[0].Groups["look"].Value : null) : null;
    }

    internal static AssemblyIr Bind(AssemblyIr ir, AssemblyAppearanceCatalog? catalog, List<AssemblyDiagnostic> diagnostics)
    {
        // Locally solved subassemblies retain selections until the root catalog binds them.
        if (catalog is null) return ir;
        return ir with { Instances = ir.Instances.Select(instance =>
        {
            if (instance.MaterialSelection is not { } selection) return instance;
            if (!catalog.Materials.TryGetValue(selection.Material, out var material))
            { diagnostics.Add(new("assembly-appearance-unknown-material", instance.Path + ":" + selection.Material)); return instance; }
            var look = selection.AppearanceOverride ?? material.Appearance;
            if (!catalog.Appearances.TryGetValue(look, out var appearance))
            { diagnostics.Add(new("assembly-appearance-unknown-appearance", instance.Path + ":" + look)); return instance; }
            return instance with { Appearance = new(material.Name, material.Identity, look, appearance.Preview,
                selection.AppearanceOverride is null ? "physical-material-default" : "with-appearance-override") };
        }).ToArray() };
    }

    private static double Number(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
}
