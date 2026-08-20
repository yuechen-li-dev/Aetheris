using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Surfacing;

public sealed record SculptingTimingEvidence(double BasePreparationMilliseconds, double OperationConstructionMilliseconds, double LocalityAndPreservationMilliseconds, double BrepValidationMilliseconds, double StepExportMilliseconds);
public sealed record SculptingCompileResult(bool IsSuccess, string ModelName, BodyState? OutputState, IReadOnlyDictionary<string, BodyState> States, IReadOnlyList<SculptDiagnostic> Diagnostics, SculptingTimingEvidence Timings);

public static class SculptingAuthoring
{
    private static readonly Regex ModelHeader = new(@"\bModel\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);

    public static bool IsSculptingSource(string source) => Regex.IsMatch(source, @"\b(?:BodyState|SculptState)\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant);
    public static SculptingCompileResult CompileFile(string path) => Compile(File.ReadAllText(path));

    public static SculptingCompileResult Compile(string source)
    {
        var total = Stopwatch.StartNew(); var diagnostics = new List<SculptDiagnostic>(); var states = new Dictionary<string, BodyState>(StringComparer.Ordinal);
        var model = ModelHeader.Match(source); if (!model.Success) return Fail("<unknown>", "sculpt-source-malformed", "Sculpting source requires 'Model Name { ... }'.");
        if (!Regex.IsMatch(source, @"\bUnits\s*:\s*mm\b", RegexOptions.CultureInvariant)) diagnostics.Add(new("sculpt-units-invalid", "SURF-X0 authoring requires millimetres."));
        var baseClock = Stopwatch.StartNew();
        foreach (var block in Blocks(source, "BodyState"))
        {
            var size = Vector(block.Body, "Size", 3, diagnostics); var holes = ParseHoles(block.Body, diagnostics);
            if (size is null) continue;
            var created = SculptedHousingFactory.CreateBase(block.Name, size[0], size[1], size[2], holes);
            if (!created.IsSuccess || created.OutputState is null) diagnostics.AddRange(created.Diagnostics);
            else if (!states.TryAdd(block.Name, created.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"BodyState '{block.Name}' is declared more than once."));
        }
        baseClock.Stop(); var opConstruction = TimeSpan.Zero; var verification = TimeSpan.Zero; var brep = TimeSpan.Zero;
        foreach (var block in Blocks(source, "SculptState"))
        {
            var operationClock = Stopwatch.StartNew();
            var inputName = ScalarText(block.Body, "Input");
            if (inputName is null || !states.TryGetValue(inputName, out var input)) { diagnostics.Add(new("sculpt-predecessor-unresolved", $"SculptState '{block.Name}' must name one previously accepted Input state.")); continue; }
            var operationBlock = Blocks(block.Body, "OffsetRegion").SingleOrDefault();
            if (operationBlock is null) { diagnostics.Add(new("sculpt-operation-unsupported", $"SculptState '{block.Name}' requires exactly one OffsetRegion operation.")); continue; }
            var target = ScalarText(operationBlock.Body, "Target") ?? string.Empty;
            var offset = Length(operationBlock.Body, "Offset", diagnostics);
            var region = Vector(operationBlock.Body, "Region", 2, diagnostics);
            var envelope = Vector(operationBlock.Body, "InfluenceEnvelope", 6, diagnostics);
            var mayModify = List(block.Body, "MayModify"); var preserve = List(block.Body, "Preserve"); var requirements = List(block.Body, "Require");
            var boundary = ScalarText(operationBlock.Body, "Boundary") ?? "G0";
            operationClock.Stop(); opConstruction += operationClock.Elapsed;
            if (offset is null || region is null || envelope is null) continue;
            var contracts = preserve.Select(x => new PreservationContract(x, x == SculptedHousingFactory.MountingHolePattern ? PreservationMode.PatternPlacementAndDiameter : PreservationMode.ExactGeometry)).ToArray();
            var parsedRequirements = new List<SculptRequirement>();
            foreach (var requirement in requirements)
                if (Enum.TryParse<SculptRequirement>(requirement, out var parsed)) parsedRequirements.Add(parsed); else diagnostics.Add(new("sculpt-requirement-unsupported", $"Requirement '{requirement}' is not supported by SURF-X0."));
            var operation = new OffsetRegionOperation(block.Name + ".OffsetRegion", target, offset.Value, region[0], region[1], mayModify,
                new(envelope[0], envelope[1], envelope[2], envelope[3], envelope[4], envelope[5]), contracts, parsedRequirements, boundary);
            var applyClock = Stopwatch.StartNew(); var result = OffsetRegionSculptor.Apply(input, block.Name, operation); applyClock.Stop(); verification += applyClock.Elapsed;
            if (!result.IsSuccess || result.OutputState is null) diagnostics.AddRange(result.Diagnostics);
            else if (!states.TryAdd(block.Name, result.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"State '{block.Name}' is declared more than once."));
        }
        var outputName = ScalarText(source, "Output");
        BodyState? output = null;
        if (outputName is null || !states.TryGetValue(outputName, out output)) diagnostics.Add(new("sculpt-output-unresolved", "Output must name one accepted BodyState or SculptState."));
        total.Stop();
        return new(diagnostics.Count == 0 && output is not null, model.Groups["name"].Value, output, states, diagnostics,
            new(baseClock.Elapsed.TotalMilliseconds, opConstruction.TotalMilliseconds, verification.TotalMilliseconds, brep.TotalMilliseconds, 0d));

        SculptingCompileResult Fail(string name, string code, string message) => new(false, name, null, states, [new(code, message)], new(0, 0, 0, 0, 0));
    }

    private static IReadOnlyList<HousingHole> ParseHoles(string body, List<SculptDiagnostic> diagnostics)
    {
        var result = new List<HousingHole>();
        foreach (var hole in Blocks(body, "Hole"))
        {
            var center = Vector(hole.Body, "Center", 2, diagnostics); var diameter = Length(hole.Body, "Diameter", diagnostics);
            if (center is not null && diameter is not null) result.Add(new(hole.Name, center[0], center[1], diameter.Value));
        }
        if (result.Count == 0) diagnostics.Add(new("sculpt-hole-pattern-required", "The X0 housing witness requires at least one bounded mounting hole."));
        return result;
    }

    private static IReadOnlyList<Block> Blocks(string source, string keyword)
    {
        var result = new List<Block>(); var regex = new Regex($@"\b{Regex.Escape(keyword)}(?:\s+(?<name>[A-Za-z_]\w*))?\s*\{{", RegexOptions.CultureInvariant);
        for (var start = 0; start < source.Length;)
        {
            var match = regex.Match(source, start); if (!match.Success) break; var open = source.IndexOf('{', match.Index + match.Length - 1); var close = MatchingBrace(source, open);
            if (close < 0) break; result.Add(new(match.Groups["name"].Success ? match.Groups["name"].Value : keyword, source[(open + 1)..close])); start = close + 1;
        }
        return result;
    }
    private static int MatchingBrace(string source, int open) { var depth = 0; var quoted = false; for (var i = open; i < source.Length; i++) { if (source[i] == '"' && (i == 0 || source[i - 1] != '\\')) quoted = !quoted; if (quoted) continue; if (source[i] == '{') depth++; else if (source[i] == '}' && --depth == 0) return i; } return -1; }
    private static string? ScalarText(string body, string field) { var m = Regex.Match(body, $@"(?m)^\s*{Regex.Escape(field)}\s*:\s*(?<v>[A-Za-z_]\w*)\s*$", RegexOptions.CultureInvariant); return m.Success ? m.Groups["v"].Value : null; }
    private static double? Length(string body, string field, List<SculptDiagnostic> diagnostics)
    {
        var m = Regex.Match(body, $@"(?m)^\s*{Regex.Escape(field)}\s*:\s*(?<v>[-+]?\d+(?:\.\d+)?)mm\s*$", RegexOptions.CultureInvariant);
        if (m.Success) return double.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture); diagnostics.Add(new("sculpt-field-invalid", $"Field '{field}' requires a millimetre length.")); return null;
    }
    private static double[]? Vector(string body, string field, int count, List<SculptDiagnostic> diagnostics)
    {
        var m = Regex.Match(body, $@"(?m)^\s*{Regex.Escape(field)}\s*:\s*\[(?<v>[^\]]+)\]\s*$", RegexOptions.CultureInvariant);
        if (m.Success)
        {
            var values = m.Groups["v"].Value.Split(',').Select(x => Regex.Match(x.Trim(), @"^(?<v>[-+]?\d+(?:\.\d+)?)mm$", RegexOptions.CultureInvariant)).ToArray();
            if (values.Length == count && values.All(x => x.Success)) return values.Select(x => double.Parse(x.Groups["v"].Value, CultureInfo.InvariantCulture)).ToArray();
        }
        diagnostics.Add(new("sculpt-field-invalid", $"Field '{field}' requires {count} comma-separated millimetre lengths.")); return null;
    }
    private static IReadOnlyList<string> List(string body, string field)
    {
        var m = Regex.Match(body, $@"(?m)^\s*{Regex.Escape(field)}\s*:\s*\[(?<v>[^\]]*)\]\s*$", RegexOptions.CultureInvariant);
        return m.Success ? m.Groups["v"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
    }
    private sealed record Block(string Name, string Body);
}
