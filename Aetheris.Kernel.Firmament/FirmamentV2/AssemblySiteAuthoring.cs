using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Finite, named mounting-site recipes. Coordinates belong to the consumer's
/// target frame; this produces checked Set data, never reflected occurrence matrices.</summary>
internal static class AssemblySiteAuthoring
{
    private sealed record Site(string Key, double[] Point);
    internal sealed record Result(string Source, IReadOnlyDictionary<string, IReadOnlyList<string>> Recipes);
    internal static Result? Expand(string source, List<string> diagnostics)
    {
        var declarations = Regex.Matches(source,
            @"\b(?<kind>Linear|Mirrored)\s+Sites\s+(?<name>[A-Za-z_]\w*)(?:\s+From\s+(?<from>[A-Za-z_]\w*))?\s*\{").Cast<Match>().ToArray();
        if (declarations.Length == 0) return new(source, new Dictionary<string, IReadOnlyList<string>>());
        var values = new Dictionary<string, Site[]>(StringComparer.Ordinal);
        var recipes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var changes = new List<(int Start, int Length, string Text)>();
        foreach (var declaration in declarations)
        {
            var name = declaration.Groups["name"].Value;
            var open = declaration.Index + declaration.Length - 1;
            var close = source.IndexOf('}', open);
            void Error(string detail) => diagnostics.Add(CanonicalStaticAuthoring.Prefix + "assembly-sites-invalid:" + name + ":" + detail);
            if (close < 0) { Error("unclosed"); return null; }
            var body = source[(open + 1)..close];
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var split = field.Split(':', 2, StringSplitOptions.TrimEntries);
                if (split.Length != 2 || !fields.TryAdd(split[0], split[1])) Error("malformed-or-duplicate-field");
            }
            if (values.ContainsKey(name)) { Error("duplicate-name"); continue; }
            Site[]? sites = null;
            if (declaration.Groups["kind"].Value == "Linear")
            {
                if (declaration.Groups["from"].Success || fields.Count != 3 ||
                    !fields.TryGetValue("Keys", out var keysText) || !fields.TryGetValue("Start", out var startText) ||
                    !fields.TryGetValue("Step", out var stepText)) { Error("expected-Keys-Start-Step"); continue; }
                var keysMatch = Regex.Match(keysText, @"^\[(?<keys>[^\[\]]*)\]$");
                var keys = keysMatch.Groups["keys"].Value.Split(',', StringSplitOptions.TrimEntries);
                if (!keysMatch.Success || keys.Length > 1024 || keys.Any(k => !Regex.IsMatch(k, @"^[A-Za-z_]\w*$")) ||
                    keys.Distinct(StringComparer.Ordinal).Count() != keys.Length) { Error("invalid-keys"); continue; }
                var start = Vector(startText); var step = Vector(stepText);
                if (start is null || step is null || step.All(v => v == 0)) { Error("finite-mm-vectors-and-nonzero-step-required"); continue; }
                sites = keys.Select((key, i) => new Site(key, start.Zip(step, (s, d) => s + i * d).ToArray())).ToArray();
                if (sites.Any(s => s.Point.Any(p => !double.IsFinite(p)))) { Error("coordinate-overflow"); continue; }
            }
            else
            {
                if (!declaration.Groups["from"].Success || !values.TryGetValue(declaration.Groups["from"].Value, out var parent))
                { Error("source-must-be-an-earlier-site-recipe"); continue; }
                if (fields.Count != 1 || !fields.TryGetValue("Across", out var plane) || plane is not ("YZ" or "XZ" or "XY"))
                { Error("Across-must-be-local-YZ-XZ-or-XY"); continue; }
                var axis = plane == "YZ" ? 0 : plane == "XZ" ? 1 : 2;
                sites = parent.Select(s => new Site(s.Key, s.Point.Select((p, i) => i == axis ? -p : p).ToArray())).ToArray();
            }
            values.Add(name, sites);
            var recipe = source[declaration.Index..(close + 1)];
            recipes.Add(name, declaration.Groups["from"].Success
                ? [.. recipes[declaration.Groups["from"].Value], recipe] : [recipe]);
            var type = "__AssemblySite_" + name;
            string Mm(double number) => number.ToString("R", CultureInfo.InvariantCulture) + "mm";
            var rows = sites.Select(s => $"{s.Key} => {type} {{ X: {Mm(s.Point[0])}; Y: {Mm(s.Point[1])}; Z: {Mm(s.Point[2])} }}");
            changes.Add((declaration.Index, close - declaration.Index + 1,
                $"Record {type} {{ X: Length; Y: Length; Z: Length }}\nStatic {name}: Set<{type}> {{\n{string.Join("\n", rows)}\n}}"));
        }
        if (diagnostics.Count > 0) return null;
        foreach (var change in changes.OrderByDescending(c => c.Start))
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return new(source, recipes);
    }

    private static double[]? Vector(string text)
    {
        var match = Regex.Match(text, @"^\[(?<v>[^\[\]]*)\]$");
        if (!match.Success) return null;
        var terms = match.Groups["v"].Value.Split(',', StringSplitOptions.TrimEntries);
        if (terms.Length != 3) return null;
        var result = new double[3];
        for (var i = 0; i < 3; i++)
            if (!terms[i].EndsWith("mm", StringComparison.Ordinal) ||
                !double.TryParse(terms[i][..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]) || !double.IsFinite(result[i])) return null;
        return result;
    }
}
