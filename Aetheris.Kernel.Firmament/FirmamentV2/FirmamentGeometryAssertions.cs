using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentGeometryAssertion(string Id, string Kind, string TargetBodyId,
    double[]? Minimum, double[]? Maximum, double ToleranceMm, IReadOnlyDictionary<string, int>? Counts,
    string? Note, FirmamentV2SourceSpan SourceSpan);
public sealed record FirmamentGeometryAssertionResult(string AssertionId, string Kind, string TargetBodyId,
    bool Passed, string MeasurementMethod, string? Diagnostic, FirmamentV2SourceSpan SourceSpan);

internal static class FirmamentAssertionSyntax
{
    internal static IReadOnlyList<DeclarationSyntax> Declarations(string source) =>
        FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements)
            .Where(d => d.Modifiers.Any(m => m.Name.Text == "Assert")).ToArray();

    internal static (IReadOnlyList<FirmamentV2VolumeAssertion> Volume, IReadOnlyList<FirmamentGeometryAssertion> Geometry) Parse(
        string source, List<string> diagnostics)
    {
        var volume = new List<FirmamentV2VolumeAssertion>();
        var geometry = new List<FirmamentGeometryAssertion>();
        foreach (var declaration in Declarations(source))
        {
            var kind = declaration.Type.Name.Text;
            var target = declaration.Name?.Text ?? "";
            if (kind is not ("Volume" or "Bounds" or "Topology"))
            { diagnostics.Add($"{FirmamentV2Parser.AssertVolumeKindUnsupported}:{kind}:{target}"); continue; }
            if (declaration.Body is null || target.Length == 0)
            { diagnostics.Add($"{FirmamentV2Parser.AssertVolumeMalformed}:{target}"); continue; }
            var fields = new FirmamentSyntaxFields(declaration, diagnostics);
            var noteExpression = fields.Take("Note");
            var note = noteExpression is LiteralSyntax { Kind: LiteralKind.String } ? FirmamentSyntaxValues.Reference(noteExpression) : null;
            if (noteExpression is not null && note is null) diagnostics.Add($"{FirmamentV2Parser.AssertVolumeMalformed}:{target}:Note");
            var id = kind.ToLowerInvariant() + ":" + target + ":" + declaration.Span.Start;
            if (kind == "Volume")
            {
                var expectedOk = Volume(fields.Take("Expected"), source, out var expected);
                var toleranceOk = Volume(fields.Take("Tolerance"), source, out var tolerance);
                if (!expectedOk) diagnostics.Add($"{FirmamentV2Parser.AssertVolumeExpectedDimensionMismatch}:{target}:Volume");
                if (!toleranceOk) diagnostics.Add($"{FirmamentV2Parser.AssertVolumeToleranceDimensionMismatch}:{target}:Volume");
                if (tolerance < 0) diagnostics.Add($"{FirmamentV2Parser.AssertVolumeToleranceNegative}:{target}");
                if (expected < 0) diagnostics.Add($"{FirmamentV2Parser.AssertVolumeMalformed}:{target}:negative-expected");
                if (expectedOk && toleranceOk && expected >= 0 && tolerance >= 0)
                    volume.Add(new(id, target, expected, tolerance, note, declaration.Span));
            }
            else if (kind == "Bounds")
            {
                var minimum = fields.Take("Min"); var maximum = fields.Take("Max"); var tolerance = fields.Number("Tolerance", "mm", 0);
                var valid = FirmamentSyntaxValues.Vector(minimum, "mm", out var min) && min.Length == 3;
                valid &= FirmamentSyntaxValues.Vector(maximum, "mm", out var max) && max.Length == 3;
                valid &= double.IsFinite(tolerance) && tolerance >= 0;
                if (valid && Enumerable.Range(0, 3).Any(i => min[i] > max[i])) valid = false;
                if (!valid) diagnostics.Add($"firmament-v2-assert-bounds-malformed:{target}:expected-Min-Max-length-vectors-and-nonnegative-Tolerance");
                else geometry.Add(new(id, kind, target, min, max, tolerance, null, note, declaration.Span));
            }
            else
            {
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var name in new[] { "Faces", "Edges", "Vertices", "Shells" })
                {
                    var value = fields.Take(name); if (value is null) continue;
                    if (!FirmamentSyntaxValues.TryMeasure(value, "", out var number) || !double.IsFinite(number) || number < 0 || number > int.MaxValue || Math.Truncate(number) != number)
                        diagnostics.Add($"firmament-v2-assert-topology-malformed:{target}:{name}:expected-nonnegative-integer");
                    else counts.Add(name, (int)number);
                }
                if (counts.Count == 0) diagnostics.Add($"firmament-v2-assert-topology-malformed:{target}:specify-at-least-one-count");
                else geometry.Add(new(id, kind, target, null, null, 0, counts, note, declaration.Span));
            }
            fields.Complete();
        }
        return (volume, geometry);
    }

    private static bool Volume(ExpressionSyntax? expression, string source, out double value)
    {
        value = double.NaN;
        if (expression is null) return false;
        var raw = source.Substring(expression.Span.Start, expression.Span.Length).Trim();
        return raw.EndsWith("mm^3", StringComparison.Ordinal)
            && double.TryParse(raw[..^4].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    internal static string Mask(string source)
    {
        var chars = source.ToCharArray();
        foreach (var declaration in Declarations(source))
            for (var i = declaration.Span.Start; i < declaration.Span.Start + declaration.Span.Length; i++)
                if (chars[i] is not ('\n' or '\r')) chars[i] = ' ';
        return new(chars);
    }
}
