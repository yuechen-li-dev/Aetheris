using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.PlasticShell;

/// <summary>Module-owned manufacturing-first Firmament surface. It lowers to PlasticShellIr before any BRep operation.</summary>
public static class PlasticShellFirmament
{
    private static readonly RegexOptions Rx = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    public static bool LooksLikePlasticShell(string? source) => source is not null && Regex.IsMatch(Strip(source), @"\bPlasticShell\s+[A-Za-z_]\w*\s*\{", Rx);
    public static PlasticShellCompileResult CompileFile(string path) => Compile(File.ReadAllText(path), path);

    public static PlasticShellCompileResult Compile(string source, string sourcePath = "authored.firmament")
    {
        ArgumentNullException.ThrowIfNull(source);
        var clean = Strip(source); var diagnostics = new List<PlasticDiagnostic>();
        var model = Regex.Match(clean, @"\bModel\s+(?<name>[A-Za-z_]\w*)\s*\{", Rx);
        if (!model.Success) return Fail("<unknown>", "PlasticShell source requires 'Model Name { ... }'.");
        var modelName = model.Groups["name"].Value;
        if (!Regex.IsMatch(clean, @"\bUnits\s*:\s*mm\b", Rx)) diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "PlasticShell X0 requires Units: mm."));
        var blocks = Blocks(clean, "PlasticShell").ToArray();
        if (blocks.Length != 1) diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "A bounded X0 source requires exactly one PlasticShell block."));
        if (diagnostics.Count > 0 || blocks.Length != 1) return new(false, modelName, null, null, diagnostics);
        var shell = blocks[0];
        try
        {
            var exteriorKind = Scalar(shell.Body, "Exterior") ?? "Frustum";
            if (!exteriorKind.Equals("Frustum", StringComparison.OrdinalIgnoreCase)) diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "X0 realizes only the exact analytic Frustum exterior family; the IR remains extensible to accepted SURF authority."));
            var bottomRadius = Length(shell.Body, "BottomRadius", diagnostics); var topRadius = Length(shell.Body, "TopRadius", diagnostics); var height = Length(shell.Body, "Height", diagnostics);
            var material = Scalar(shell.Body, "Material") ?? "UnspecifiedPlastic";
            var tooling = Direction(shell.Body, "ToolingDirection", diagnostics);
            var minimumDraft = Angle(shell.Body, "MinimumDraftAngle", diagnostics);
            var wallBlock = Blocks(shell.Body, "WallPolicy").SingleOrDefault();
            if (wallBlock is null) diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "PlasticShell requires WallPolicy."));
            var nominal = wallBlock is null ? null : Length(wallBlock.Body, "NominalThickness", diagnostics);
            var minimum = wallBlock is null ? null : Length(wallBlock.Body, "MinimumThickness", diagnostics);
            var maximum = wallBlock is null ? null : Length(wallBlock.Body, "MaximumThickness", diagnostics);
            var tolerance = wallBlock is null ? null : Length(wallBlock.Body, "ThicknessTolerance", diagnostics, allowZero: true);
            var partingBlock = Blocks(shell.Body, "PartingPlane").SingleOrDefault();
            if (partingBlock is null) diagnostics.Add(new(PlasticDiagnosticCodes.InvalidParting, PlasticDiagnosticSeverity.Error, "PlasticShell X0 requires an explicit PartingPlane."));
            var partingOrigin = partingBlock is null ? null : Point(partingBlock.Body, "Origin", diagnostics);
            var partingNormal = partingBlock is null ? null : Direction(partingBlock.Body, "Normal", diagnostics);
            var preserved = List(shell.Body, "Preserve");
            var gates = Blocks(shell.Body, "Gate").Select(b => ParseGate(b, diagnostics)).Where(x => x is not null).Select(x => x!).ToArray();
            var standoffs = Blocks(shell.Body, "Standoff").Select(b => ParseStandoff(b, diagnostics)).Where(x => x is not null).Select(x => x!).ToArray();
            var ejectors = Blocks(shell.Body, "EjectorPin").Select(b => ParseEjector(b, diagnostics)).Where(x => x is not null).Select(x => x!).ToArray();
            var autoBlock = Blocks(shell.Body, "AutoRib").SingleOrDefault();
            var autoRib = autoBlock is null ? null : ParseAutoRib(autoBlock, diagnostics);
            if (gates.Length == 0) diagnostics.Add(new(PlasticDiagnosticCodes.InvalidGate, PlasticDiagnosticSeverity.Error, "PlasticShell X0 requires at least one explicit Gate."));
            if (ejectors.Length == 0) diagnostics.Add(new(PlasticDiagnosticCodes.EjectorNotCoreAccessible, PlasticDiagnosticSeverity.Error, "PlasticShell X0 policy requires at least one explicit EjectorPin."));
            if (diagnostics.Any(d => d.Severity == PlasticDiagnosticSeverity.Error) || bottomRadius is null || topRadius is null || height is null || tooling is null || minimumDraft is null || nominal is null || minimum is null || maximum is null || tolerance is null || partingOrigin is null || partingNormal is null)
                return new(false, modelName, null, null, diagnostics);
            var exteriorToken = preserved.FirstOrDefault(p => p is "ExteriorDesignSurface" or "ExteriorCrown");
            var exteriorId = $"{shell.Name}.{exteriorToken ?? "ExteriorDesignSurface"}";
            var ir = new PlasticShellIr(shell.Name,
                new(exteriorId, exteriorKind, bottomRadius.Value, topRadius.Value, height.Value, exteriorToken is not null),
                material, new(nominal.Value, minimum.Value, maximum.Value, tolerance.Value), tooling.Value,
                new(partingBlock?.Name ?? $"{shell.Name}.PartingPlane", partingOrigin.Value, partingNormal.Value), minimumDraft.Value,
                gates, standoffs, ejectors, autoRib, preserved.Select(p => p is "ExteriorDesignSurface" or "ExteriorCrown" ? $"{shell.Name}.{p}" : p).ToArray());
            var compiled = PlasticShellCompiler.Compile(ir, modelName);
            return compiled with { Diagnostics = diagnostics.Concat(compiled.Diagnostics).ToArray() };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, ex.Message));
            return new(false, modelName, null, null, diagnostics);
        }
    }

    private static PlasticGate? ParseGate(Block b, ICollection<PlasticDiagnostic> d)
    {
        var position = Point(b.Body, "Position", d); var target = Scalar(b.Body, "Target") ?? "TopAnnularRim"; var size = OptionalLength(b.Body, "Size", d);
        var kindText = Scalar(b.Body, "Kind") ?? "Point";
        if (!Enum.TryParse<GateKind>(kindText, true, out var kind)) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, $"Unknown Gate Kind '{kindText}'.", b.Name)); return null; }
        return position is null ? null : new(b.Name, position.Value, target, kind, size);
    }

    private static PlasticStandoff? ParseStandoff(Block b, ICollection<PlasticDiagnostic> d)
    {
        var p = Point(b.Body, "Position", d); var h = Length(b.Body, "Height", d); var od = Length(b.Body, "OuterDiameter", d); var hole = OptionalLength(b.Body, "HoleDiameter", d);
        var intentText = Scalar(b.Body, "SupportIntent") ?? "Pcb";
        if (!Enum.TryParse<StandoffSupportIntent>(intentText, true, out var intent)) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, $"Unknown Standoff SupportIntent '{intentText}'.", b.Name)); return null; }
        return p is null || h is null || od is null ? null : new(b.Name, p.Value, h.Value, od.Value, hole, intent);
    }

    private static PlasticEjectorPin? ParseEjector(Block b, ICollection<PlasticDiagnostic> d)
    {
        var p = Point(b.Body, "Position", d); var diameter = Length(b.Body, "Diameter", d); var target = Scalar(b.Body, "Target") ?? "InnerBottom";
        return p is null || diameter is null ? null : new(b.Name, p.Value, diameter.Value, target);
    }

    private static PlasticAutoRibRequest? ParseAutoRib(Block b, ICollection<PlasticDiagnostic> d)
    {
        var supports = List(b.Body, "Support"); var gate = Scalar(b.Body, "Gate") ?? string.Empty; var keepouts = List(b.Body, "KeepOut");
        var policyBlock = Blocks(b.Body, "RibPolicy").SingleOrDefault();
        if (policyBlock is null) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "AutoRib requires RibPolicy.", b.Name)); return null; }
        var ratio = Number(policyBlock.Body, "ThicknessRatio", d); var min = Length(policyBlock.Body, "MinimumHeight", d); var max = Length(policyBlock.Body, "MaximumHeight", d); var spacing = Length(policyBlock.Body, "MinimumSpacing", d); var draft = Angle(policyBlock.Body, "DraftAngle", d); var blend = Length(policyBlock.Body, "BaseBlendRadius", d, allowZero: true);
        return ratio is null || min is null || max is null || spacing is null || draft is null || blend is null ? null : new(b.Name, supports, gate, keepouts, new(ratio.Value, min.Value, max.Value, spacing.Value, draft.Value, blend.Value));
    }

    private static string Strip(string source) => Regex.Replace(source, @"//.*?$|#.*?$", string.Empty, RegexOptions.Multiline);
    private static string? Scalar(string body, string key) => Regex.Match(body, $@"\b{Regex.Escape(key)}\s*:\s*(?<v>[A-Za-z_][\w.:-]*)", Rx) is { Success: true } m ? m.Groups["v"].Value : null;
    private static double? Number(string body, string key, ICollection<PlasticDiagnostic> d) => Quantity(body, key, null, d, false);
    private static double? Length(string body, string key, ICollection<PlasticDiagnostic> d, bool allowZero = false) => Quantity(body, key, "mm", d, allowZero);
    private static double? Angle(string body, string key, ICollection<PlasticDiagnostic> d) => Quantity(body, key, "deg", d, true);
    private static double? OptionalLength(string body, string key, ICollection<PlasticDiagnostic> d) => Regex.IsMatch(body, $@"\b{Regex.Escape(key)}\s*:", Rx) ? Length(body, key, d) : null;
    private static double? Quantity(string body, string key, string? unit, ICollection<PlasticDiagnostic> d, bool allowZero)
    {
        var suffix = unit is null ? string.Empty : $@"\s*{unit}"; var m = Regex.Match(body, $@"\b{Regex.Escape(key)}\s*:\s*(?<v>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)" + suffix + @"\s*;?", Rx);
        if (!m.Success || !double.TryParse(m.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v) || (allowZero ? v < 0 : v <= 0)) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, $"{key} requires a finite {(allowZero ? "non-negative" : "positive")} value{(unit is null ? string.Empty : " in " + unit)}.")); return null; }
        return v;
    }
    private static Point3D? Point(string body, string key, ICollection<PlasticDiagnostic> d) { var v = Vector(body, key, d); return v is null ? null : new(v[0], v[1], v[2]); }
    private static Direction3D? Direction(string body, string key, ICollection<PlasticDiagnostic> d)
    {
        var v = Vector(body, key, d); if (v is null) return null;
        try { return Direction3D.Create(new Vector3D(v[0], v[1], v[2])); } catch (ArgumentException) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, $"{key} must be a non-zero direction.")); return null; }
    }
    private static double[]? Vector(string body, string key, ICollection<PlasticDiagnostic> d)
    {
        var m = Regex.Match(body, $@"\b{Regex.Escape(key)}\s*:\s*\[(?<v>[^\]]+)\]", Rx); if (!m.Success) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, $"{key} requires [x, y, z].")); return null; }
        var parts = m.Groups["v"].Value.Split(',').Select(x => double.TryParse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? (double?)v : null).ToArray();
        if (parts.Length != 3 || parts.Any(x => x is null || !double.IsFinite(x.Value))) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, $"{key} requires three finite numbers.")); return null; }
        return parts.Select(x => x!.Value).ToArray();
    }
    private static IReadOnlyList<string> List(string body, string key)
    {
        var m = Regex.Match(body, $@"\b{Regex.Escape(key)}\s*:\s*\[(?<v>[^\]]*)\]", Rx); return !m.Success ? [] : m.Groups["v"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
    private static IEnumerable<Block> Blocks(string source, string keyword)
    {
        var matches = Regex.Matches(source, $@"\b{Regex.Escape(keyword)}(?:\s+(?<name>[A-Za-z_]\w*))?\s*\{{", Rx);
        foreach (Match match in matches)
        {
            var depth = 1; var i = match.Index + match.Length;
            for (; i < source.Length && depth > 0; i++) { if (source[i] == '{') depth++; else if (source[i] == '}') depth--; }
            if (depth == 0) yield return new(match.Groups["name"].Success ? match.Groups["name"].Value : keyword, source[(match.Index + match.Length)..(i - 1)]);
        }
    }
    private static PlasticShellCompileResult Fail(string model, string message) => new(false, model, null, null, [new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, message)]);
    private sealed record Block(string Name, string Body);
}
