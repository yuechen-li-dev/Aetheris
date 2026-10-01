using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Named compile-time boundary values. Geometry transforms preserve the
/// existing resolved Profile representation, validation and materializer authority.</summary>
internal static class ConceptProfileDerivation
{
    internal static bool TryResolve(string source, string name, HashSet<string> visiting,
        List<string> diagnostics, out ResolvedProfile2D? result)
    {
        result = null;
        var profile = Regex.Match(source, $@"\bProfile\s+{Regex.Escape(name)}(?:\s+Using\s+(?<owner>\w+))?\s*\{{");
        if (!profile.Success) return false;
        var body = Body(source, profile.Index + profile.Length - 1);
        if (body is null) return false;
        var trace = Regex.Match(body, @"^\s*Loop\s+Outer\s*\{\s*(?<curve>\w+(?:\.\w+)?)\s*\|>\s*TraceLoop\s*\}\s*$");
        if (!trace.Success) return false;
        var reference = trace.Groups["curve"].Value;
        var qualified = reference.Contains('.') ? reference : profile.Groups["owner"].Value + "." + reference;
        var parts = qualified.Split('.');
        if (parts.Length != 2) return false;
        var layout = Regex.Match(source, $@"\bConcept\s+Struct\s+{Regex.Escape(parts[0])}(?:\s+On\s+\w+)?\s*\{{");
        if (!layout.Success) return false;
        var layoutBody = Body(source, layout.Index + layout.Length - 1);
        if (layoutBody is null) return false;
        var curves = Regex.Matches(layoutBody, $@"\bCurve2\s+{Regex.Escape(parts[1])}\s*\{{");
        if (curves.Count == 0) return false;
        if (curves.Count != 1) { diagnostics.Add($"concept-profile-duplicate-curve:{qualified}"); return true; }
        var fields = Body(layoutBody, curves[0].Index + curves[0].Length - 1)!;
        string? Field(string key)
        {
            var matches = Regex.Matches(fields, $@"\b{key}\s*:\s*(?<v>\[[^]]*\]|[^;\r\n}}]+)\s*;?");
            if (matches.Count > 1) diagnostics.Add($"concept-profile-duplicate-field:{qualified}:{key}");
            return matches.Count == 0 ? null : matches[0].Groups["v"].Value.Trim();
        }
        var from = Field("From"); var on = Field("On");
        var translate = Vector(Field("Translate"), "Translate", [0,0]);
        var pivot = Vector(Field("Pivot"), "Pivot", [0,0]);
        var rotate = Number(Field("Rotate"), "deg", "Rotate", 0);
        var scale = Number(Field("Scale"), "", "Scale", 1);
        if (scale <= 0) diagnostics.Add($"concept-profile-scale-invalid:{qualified}:positive-uniform-scale-required");
        var remaining = Regex.Replace(fields, @"\b(?:From|On|Translate|Pivot|Rotate|Scale)\s*:\s*(?:\[[^]]*\]|[^;\r\n}]+)\s*;?", "");
        if (!string.IsNullOrWhiteSpace(remaining.Replace(";", ""))) diagnostics.Add($"concept-profile-fields-invalid:{qualified}");
        if (from is null || !Regex.IsMatch(from, @"^\w+$") || on is null || !Regex.IsMatch(on, @"^\w+(?:\.\w+)?$"))
        { diagnostics.Add($"concept-profile-source-invalid:{qualified}:named-profile-and-plane-required"); return true; }
        var inherited = ProfileAuthoringParser.ResolveNamedProfileCore(source, from, visiting, out var sourceErrors);
        diagnostics.AddRange(sourceErrors);
        if (inherited is null) return true;
        if (inherited.Loops.Count != 1)
        { diagnostics.Add($"concept-profile-loop-count-invalid:{qualified}:single-boundary-required"); return true; }
        var planeName = on == "XY" || on.Contains('.') ? on : parts[0] + "." + on;
        ConstructionPlane? plane = ConstructionPlane.WorldXY;
        if (planeName != "XY")
        {
            if (!ConceptIrResolver.TryResolvePlane(source, planeName, out var conceptPlane, out var planeError) || conceptPlane is null)
            { diagnostics.Add(planeError ?? $"concept-profile-plane-unresolved:{planeName}"); return true; }
            if (!ConstructionPlane.TryTrace("concept-profile:" + name, conceptPlane, $"offset:{profile.Index}", out plane, out var traceError))
            { diagnostics.Add(traceError!); return true; }
        }
        var radians = rotate * Math.PI / 180;
        (double X, double Y) Point((double X, double Y) p)
        {
            var x = (p.X - pivot[0]) * scale; var y = (p.Y - pivot[1]) * scale;
            return (x*Math.Cos(radians)-y*Math.Sin(radians)+pivot[0]+translate[0],
                x*Math.Sin(radians)+y*Math.Cos(radians)+pivot[1]+translate[1]);
        }
        LineArcProfileCurve2D? Transform(LineArcProfileCurve2D curve) => curve switch
        {
            LineArcLineSegment2D line => new LineArcLineSegment2D(Point(line.Start), Point(line.End)),
            LineArcCubicBezier2D cubic => new LineArcCubicBezier2D(Point(cubic.Start),Point(cubic.Control1),Point(cubic.Control2),Point(cubic.End)),
            LineArcCircularArc2D arc => new LineArcCircularArc2D(Point(arc.Center),arc.Radius*scale,arc.StartAngleRadians+radians,arc.SweepAngleRadians),
            LineArcFullCircle2D circle => new LineArcFullCircle2D(Point(circle.Center),circle.Radius*scale),
            LineArcFullEllipse2D ellipse => new LineArcFullEllipse2D(Point(ellipse.Center),ellipse.MajorRadius*scale,ellipse.MinorRadius*scale,ellipse.RotationRadians+radians),
            _ => null
        };
        var segments = new List<ResolvedProfileSegment2D>();
        foreach (var segment in inherited.Loops[0].Segments)
        {
            var geometry = Transform(segment.Geometry);
            if (geometry is null) { diagnostics.Add($"concept-profile-curve-unsupported:{qualified}:{segment.Name}"); continue; }
            segments.Add(new(segment.Name, geometry, segment.Provenance with {
                StableId = $"profile:{name}:segment:{segment.Name}", ConceptStableId = "concept-curve:" + qualified,
                SourceSpan = $"offset:{profile.Index}", Derivation = FormattableString.Invariant($"ConceptBoundaryPlacement:source={from};translate={translate[0]:R},{translate[1]:R};rotate={rotate:R};scale={scale:R};pivot={pivot[0]:R},{pivot[1]:R}"),
                SourceFrame = planeName, TracedFrom = segment.Provenance.StableId }));
        }
        result = new(name, planeName, [new("Outer", true, segments)], plane, BoundaryEdits: inherited.BoundaryEdits);
        var validation = ResolvedProfile2DValidator.Validate(result);
        diagnostics.AddRange(validation.Diagnostics);
        if (diagnostics.Count != 0) result = null;
        return true;

        double Number(string? text, string unit, string key, double fallback)
        {
            if (text is null) return fallback;
            if (unit.Length > 0 && !text.EndsWith(unit, StringComparison.Ordinal))
            { diagnostics.Add($"concept-profile-unit-invalid:{qualified}:{key}:{unit}"); return fallback; }
            if (unit.Length > 0) text = text[..^unit.Length];
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            { diagnostics.Add($"concept-profile-number-invalid:{qualified}:{key}"); return fallback; }
            return value;
        }
        double[] Vector(string? text, string key, double[] fallback)
        {
            if (text is null) return fallback;
            var components = text.Trim('[',']').Split(',', StringSplitOptions.TrimEntries);
            if (components.Length != 2) { diagnostics.Add($"concept-profile-vector-invalid:{qualified}:{key}"); return fallback; }
            return components.Select(c => Number(c, "mm", key, 0)).ToArray();
        }
    }

    private static string? Body(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            if (source[i] == '}' && --depth == 0) return source[(open + 1)..i];
        }
        return null;
    }
}
