using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Finite translation of circular Boss intent, before AIR or BRep construction.</summary>
internal static class LinearFeatureAuthoring
{
    internal sealed record Result(string Source, IReadOnlyList<FirmamentV2LinearPatternDecl> Patterns, IReadOnlyList<FirmamentV2Axis2Decl> Axes);
    internal static Result? Expand(string source, List<string> diagnostics)
    {
        var reports = new List<FirmamentV2LinearPatternDecl>();
        var axes = new Dictionary<string, FirmamentV2Axis2Decl>(StringComparer.Ordinal);
        foreach (Match axis in Regex.Matches(source, @"\bAxis2\s+(?<name>[A-Za-z_]\w*)\s*\{"))
        {
            var end = Matching(source, source.IndexOf('{', axis.Index));
            var identity = axis.Groups["name"].Value;
            foreach (Match owner in Regex.Matches(source, @"\bConcept\s+Struct\s+(?<name>[A-Za-z_]\w*)\s+On\s+XY\s*\{"))
                if (axis.Index > owner.Index && axis.Index < Matching(source, source.IndexOf('{', owner.Index)))
                    identity = owner.Groups["name"].Value + "." + axis.Groups["name"].Value;
            if (end < 0) { Error("axis-malformed", identity); continue; }
            var fields = source[(source.IndexOf('{', axis.Index) + 1)..end];
            var origin = Regex.Match(fields, @"\bOrigin\s*:\s*\[(?<x>[^,]+),(?<y>[^]]+)\]");
            var vector = Regex.Match(fields, @"\bDirection\s*:\s*\[(?<x>[^,]+),(?<y>[^]]+)\]");
            if (!origin.Success || !vector.Success
                || !FirmamentV2FeatureExpansion.TryEvaluateScalar(origin.Groups["x"].Value, out var x, out var xu) || xu != "mm"
                || !FirmamentV2FeatureExpansion.TryEvaluateScalar(origin.Groups["y"].Value, out var y, out var yu) || yu != "mm"
                || !FirmamentV2FeatureExpansion.TryEvaluateScalar(vector.Groups["x"].Value, out var vx, out var vxu) || vxu.Length != 0
                || !FirmamentV2FeatureExpansion.TryEvaluateScalar(vector.Groups["y"].Value, out var vy, out var vyu) || vyu.Length != 0
                || !double.IsFinite(vx * vx + vy * vy) || vx * vx + vy * vy < 1e-24)
            { Error("axis-invalid", identity); continue; }
            var norm = Math.Sqrt(vx * vx + vy * vy);
            if (!axes.TryAdd(identity, new(identity, x, y, vx / norm, vy / norm, new(axis.Index, end - axis.Index + 1)))) Error("axis-duplicate", identity);
        }
        var changes = new List<(int Start, int Length, string Text)>();
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var patternNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(source, @"\bLinear\s+Pattern\s+(?<name>[A-Za-z_]\w*)\s*\{"))
        {
            var close = Matching(source, source.IndexOf('{', header.Index));
            var name = header.Groups["name"].Value;
            if (close < 0) { Error("malformed", name); continue; }
            var body = source[(source.IndexOf('{', header.Index) + 1)..close];
            if (!patternNames.Add(name)) { Error("duplicate-name", name); continue; }
            if (Regex.IsMatch(body, @"\bPattern\s+")) { Error("nested-unsupported", name); continue; }
            var fields = Regex.Matches(body, @"\b(?<name>[A-Za-z_]\w*)\s*:").Cast<Match>().Select(m => m.Groups["name"].Value).ToArray();
            if (fields.Length != 4 || fields.Distinct(StringComparer.Ordinal).Count() != 4 || fields.Any(f => f is not ("Source" or "Direction" or "Count" or "Spacing")))
            { Error("fields-invalid", name); continue; }
            var seed = Field(body, "Source"); var direction = Field(body, "Direction");
            if (!int.TryParse(Field(body, "Count"), out var count) || count is < 1 or > 1024)
            { Error("count-invalid", name); continue; }
            if (!FirmamentV2FeatureExpansion.TryEvaluateScalar(Field(body, "Spacing"), out var spacing, out var unit) || unit != "mm" || spacing <= 0)
            { Error("spacing-invalid", name); continue; }
            if (direction.EndsWith(".Direction", StringComparison.Ordinal))
            {
                if (!axes.TryGetValue(direction[..^10], out var axis)) { Error("axis-unresolved", name); continue; }
                direction = $"[{F(axis.DirectionX)},{F(axis.DirectionY)}]";
            }
            var components = direction.Trim().Trim('[', ']').Split(',');
            if (components.Length != 2 || !double.TryParse(components[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var dx)
                || !double.TryParse(components[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var dy)
                || !double.IsFinite(dx) || !double.IsFinite(dy) || Math.Sqrt(dx * dx + dy * dy) < 1e-12)
            { Error("direction-invalid", name); continue; }
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(length)) { Error("direction-invalid", name); continue; }
            dx /= length; dy /= length;
            var feature = Regex.Match(source, $@"\bBoss\s+{Regex.Escape(seed)}\s*\{{");
            var featureClose = feature.Success ? Matching(source, source.IndexOf('{', feature.Index)) : -1;
            if (featureClose < 0 || !consumed.Add(seed)) { Error("source-invalid-or-reused", name); continue; }
            var featureBody = source[(source.IndexOf('{', feature.Index) + 1)..featureClose];
            var profileName = Field(featureBody, "Profile");
            var profile = ProfileAuthoringParser.ResolveNamedProfile(source, profileName, out var profileErrors);
            if (profile is null || profile.PlaneFrame != "XY" || profile.Loops.Count != 1
                || profile.Loops[0].Segments.Count != 1 || profile.Loops[0].Segments[0].Geometry is not LineArcFullCircle2D circle)
            { diagnostics.AddRange(profileErrors); Error("source-profile-not-qualified", name); continue; }
            var output = new List<string>(); var identities = new List<string>();
            for (var i = 0; i < count; i++)
            {
                var id = name + "_Instance" + i.ToString(CultureInfo.InvariantCulture);
                if (Regex.IsMatch(source, $@"\b(?:Boss|Profile|Point2|Circle2)\s+{Regex.Escape(id)}\b")) { Error("identity-collision", id); break; }
                var center = id + "__Center"; var guide = id + "__Circle"; var section = id + "__Profile";
                var x = circle.Center.X + i * spacing * dx; var y = circle.Center.Y + i * spacing * dy;
                output.Add($"Point2 {center} {{ Position: [{F(x)}mm,{F(y)}mm] }}\nCircle2 {guide} {{ Center: {center}; Radius: {F(circle.Radius)}mm }}\nProfile {section} {{ Loop Outer {{ {guide} |> TraceLoop }} }}\nBoss {id} {{ "
                    + Regex.Replace(featureBody, @"\bProfile\s*:\s*[A-Za-z_]\w*", "Profile: " + section) + " }");
                identities.Add(id);
            }
            changes.Add((feature.Index, featureClose - feature.Index + 1, ""));
            changes.Add((header.Index, close - header.Index + 1, string.Join("\n", output)));
            reports.Add(new(name, seed, Field(body, "Direction"), count, spacing, dx, dy, identities, new(header.Index, close - header.Index + 1)));
        }
        if (diagnostics.Any(d => d.StartsWith("firmament-feature-linear-", StringComparison.Ordinal))) return null;
        foreach (var change in changes.OrderByDescending(c => c.Start)) source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return new(source, reports, axes.Values.ToArray());
        void Error(string code, string detail) => diagnostics.Add("firmament-feature-linear-" + code + ":" + detail);
    }
    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Field(string body, string name) => Regex.Match(body, $@"\b{name}\s*:\s*(?<value>[^;\r\n}}]+)").Groups["value"].Value.Trim();
    private static int Matching(string source, int open)
    {
        var depth = 0;
        for (var i = open; i >= 0 && i < source.Length; i++)
        { if (source[i] == '{') depth++; else if (source[i] == '}' && --depth == 0) return i; }
        return -1;
    }
}
