using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Small, bounded canonical static frontend. It normalizes records, arrays, templates,
/// patterns, and Require into erased concrete declarations before the material frontend runs.</summary>
internal static class CanonicalStaticAuthoring
{
    internal const string Prefix = "firmament-v2-static-";
    internal sealed record Result(string Source, FirmamentV2StaticAuthoringDocument? Document);
    private sealed record Template(string Name, string Type, string Parameter, string Body, FirmamentV2SourceSpan Span);

    public static Result? Expand(string source, List<string> diagnostics)
    {
        var canonicalRoot = Regex.IsMatch(source, @"^\s*Model\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant);
        var staticDeclaration = Regex.IsMatch(source, @"\b(?:Record|Static|Template\s+[A-Za-z_]\w*\s*\(|Pattern\s+\w+\s+Over)\b", RegexOptions.CultureInvariant);
        if (!staticDeclaration && !(canonicalRoot && Regex.IsMatch(source, @"\bRequire\s+[A-Za-z_]\w*\s*=>", RegexOptions.CultureInvariant))) return new(source, null);
        var changes = new List<(int Start, int Length, string Text)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var recordTypes = new List<FirmamentV2RecordTypeDecl>();
        var arrays = new List<FirmamentV2StaticArrayDecl>();
        var templates = new List<Template>();
        var patterns = new List<FirmamentV2CanonicalPatternDecl>();
        var requires = new List<FirmamentV2RequireDecl>();

        foreach (Match header in Regex.Matches(source, @"\bRecord\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var close = MatchPair(source, source.IndexOf('{', header.Index), '{', '}');
            if (close < 0) { diagnostics.Add(Prefix + "record-malformed"); continue; }
            var name = header.Groups["name"].Value; if (!names.Add(name)) { diagnostics.Add(FirmamentV2Parser.DuplicateName + ":Record:" + name); continue; }
            var fields = Regex.Matches(source[(source.IndexOf('{', header.Index) + 1)..close], @"\b(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)", RegexOptions.CultureInvariant)
                .Cast<Match>().ToDictionary(m => m.Groups["name"].Value, m => m.Groups["type"].Value, StringComparer.Ordinal);
            if (fields.Count == 0) diagnostics.Add(Prefix + "record-empty:" + name);
            recordTypes.Add(new(name, fields, new(header.Index, close - header.Index + 1)));
            changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }
        var recordByName = recordTypes.ToDictionary(x => x.Name, StringComparer.Ordinal);

        foreach (Match header in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\[\]\s*=\s*\[", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('[', header.Index + header.Value.LastIndexOf('[')); var close = MatchPair(source, open, '[', ']');
            var name = header.Groups["name"].Value; var type = header.Groups["type"].Value;
            if (close < 0 || !recordByName.TryGetValue(type, out var record)) { diagnostics.Add(Prefix + "array-type-invalid:" + name); continue; }
            if (!names.Add(name)) { diagnostics.Add(FirmamentV2Parser.DuplicateName + ":Static:" + name); continue; }
            var elements = new List<IReadOnlyDictionary<string, string>>();
            foreach (Match literal in Regex.Matches(source[(open + 1)..close], $@"\b{Regex.Escape(type)}\s*\{{", RegexOptions.CultureInvariant))
            {
                var literalOpen = open + 1 + literal.Index + literal.Value.LastIndexOf('{'); var literalClose = MatchPair(source, literalOpen, '{', '}');
                if (literalClose < 0) { diagnostics.Add(Prefix + "record-literal-malformed:" + name); continue; }
                var values = Fields(source[(literalOpen + 1)..literalClose]);
                var missing = record.Fields.Keys.Where(field => !values.ContainsKey(field)).ToArray();
                var extra = values.Keys.Where(field => !record.Fields.ContainsKey(field)).ToArray();
                if (missing.Length > 0) diagnostics.Add(Prefix + "record-missing-field:" + name + ":" + string.Join(",", missing));
                if (extra.Length > 0) diagnostics.Add(Prefix + "record-extra-field:" + name + ":" + string.Join(",", extra));
                if (missing.Length == 0 && extra.Length == 0) elements.Add(values);
            }
            arrays.Add(new(name, type, elements, new(header.Index, close - header.Index + 1)));
            changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }
        var arrayByName = arrays.ToDictionary(x => x.Name, StringComparer.Ordinal);

        foreach (Match header in Regex.Matches(source, @"\bTemplate\s+(?<name>[A-Za-z_]\w*)\s*\(\s*(?<type>[A-Za-z_]\w*)\s+(?<param>[A-Za-z_]\w*)\s*\)\s*\{", RegexOptions.CultureInvariant))
        {
            var close = MatchPair(source, source.IndexOf('{', header.Index), '{', '}');
            var name = header.Groups["name"].Value;
            if (close < 0 || !recordByName.ContainsKey(header.Groups["type"].Value)) { diagnostics.Add(Prefix + "template-malformed:" + name); continue; }
            if (!names.Add(name)) { diagnostics.Add(FirmamentV2Parser.DuplicateName + ":Template:" + name); continue; }
            var template = new Template(name, header.Groups["type"].Value, header.Groups["param"].Value, source[(source.IndexOf('{', header.Index) + 1)..close], new(header.Index, close - header.Index + 1));
            templates.Add(template); changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }
        var templateByName = templates.ToDictionary(x => x.Name, StringComparer.Ordinal);

        // Static members are substituted through the common expression spelling before
        // Profile/Compose adapters consume their already-resolved guide declarations.
        foreach (var array in arrays)
        {
            for (var index = 0; index < array.Elements.Count; index++)
            {
                foreach (var field in array.Elements[index])
                {
                    var reference = $@"\b{Regex.Escape(array.Name)}\s*\[\s*{index.ToString(CultureInfo.InvariantCulture)}\s*\]\s*\.\s*{Regex.Escape(field.Key)}\b";
                    foreach (Match referenceMatch in Regex.Matches(source, reference, RegexOptions.CultureInvariant))
                        changes.Add((referenceMatch.Index, referenceMatch.Length, field.Value));
                }
            }
        }

        foreach (Match pattern in Regex.Matches(source, @"\bPattern\s+(?<name>[A-Za-z_]\w*)\s+Over\s+(?<array>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', pattern.Index); var close = MatchPair(source, open, '{', '}');
            if (close < 0 || !arrayByName.TryGetValue(pattern.Groups["array"].Value, out var array)) { diagnostics.Add(Prefix + "pattern-source-invalid:" + pattern.Groups["name"].Value); continue; }
            var invocation = Regex.Match(source[(open + 1)..close], @"^(?:\s)*(?<template>[A-Za-z_]\w*)\s*\(\s*Current\s*\)\s*$", RegexOptions.CultureInvariant);
            if (!invocation.Success || !templateByName.TryGetValue(invocation.Groups["template"].Value, out var template) || template.Type != array.ElementType) { diagnostics.Add(Prefix + "pattern-body-invalid:" + pattern.Groups["name"].Value); continue; }
            var generated = new List<string>(); var output = new List<string>();
            for (var index = 0; index < array.Elements.Count; index++)
            {
                var id = pattern.Groups["name"].Value + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                var declaration = Instantiate(template, array.Elements[index], id, true, diagnostics);
                if (declaration is not null) { output.Add(declaration); generated.Add(id); }
            }
            patterns.Add(new(pattern.Groups["name"].Value, array.Name, template.Name, generated.Count, generated, new(pattern.Index, close - pattern.Index + 1)));
            changes.Add((pattern.Index, close - pattern.Index + 1, string.Join(Environment.NewLine, output)));
        }

        // A direct invocation is the same static expansion route as Pattern, with an
        // explicit bounded element index. The declaration body is still erased before
        // material AIR; it is not a runtime call.
        foreach (Match invocation in Regex.Matches(source, @"\b(?<template>[A-Za-z_]\w*)\s*\(\s*(?<array>[A-Za-z_]\w*)\s*\[\s*(?<index>\d+)\s*\]\s*\)", RegexOptions.CultureInvariant))
        {
            if (!templateByName.TryGetValue(invocation.Groups["template"].Value, out var template)
                || !arrayByName.TryGetValue(invocation.Groups["array"].Value, out var array)
                || template.Type != array.ElementType) continue;
            var index = int.Parse(invocation.Groups["index"].Value, CultureInfo.InvariantCulture);
            if (index >= array.Elements.Count) { diagnostics.Add(Prefix + "array-index-out-of-range:" + array.Name); continue; }
            var generatedId = template.Name + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            var declaration = Instantiate(template, array.Elements[index], generatedId, false, diagnostics);
            if (declaration is not null) changes.Add((invocation.Index, invocation.Length, declaration));
        }

        foreach (Match require in Regex.Matches(source, @"\bRequire\s+(?<name>[A-Za-z_]\w*)\s*=>\s*(?<expr>[^\r\n}]+)", RegexOptions.CultureInvariant))
        {
            var expression = require.Groups["expr"].Value.Trim();
            if (!TryRequire(expression, out var value)) { diagnostics.Add(Prefix + "require-non-bool:" + require.Groups["name"].Value); continue; }
            requires.Add(new(require.Groups["name"].Value, expression, value, new(require.Index, require.Length)));
            if (!value) diagnostics.Add(Prefix + "require-failed:" + require.Groups["name"].Value + ":" + expression);
            changes.Add((require.Index, require.Length, string.Empty));
        }
        if (diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal) || d.StartsWith(FirmamentV2Parser.DuplicateName, StringComparison.Ordinal))) return null;
        foreach (var change in changes.OrderByDescending(x => x.Start)) source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return new(source, new(recordTypes, arrays, templates.Select(t => new FirmamentV2CanonicalTemplateDecl(t.Name, t.Type, t.Parameter, t.Body, t.Span)).ToArray(), patterns, requires));
    }

    private static string? Instantiate(Template template, IReadOnlyDictionary<string, string> values, string id, bool patterned, List<string> diagnostics)
    {
        var declaration = Regex.Match(template.Body, @"\b(?<kind>Hole\s*<\s*Shaft\s*>|Slot\s*<\s*(?:Capsule|RoundedRectangle)\s*>|Profile)\s+(?<name>[A-Za-z_]\w*)(?<tail>\s+Using\s+[A-Za-z_]\w*)?\s*\{", RegexOptions.CultureInvariant);
        if (!declaration.Success) { diagnostics.Add(Prefix + "template-output-unsupported:" + template.Name); return null; }
        var kind = declaration.Groups["kind"].Value;
        if (patterned && string.Equals(kind, "Profile", StringComparison.Ordinal))
        {
            diagnostics.Add(Prefix + "pattern-output-unsupported:Profile");
            return null;
        }
        var open = template.Body.IndexOf('{', declaration.Index); var close = MatchPair(template.Body, open, '{', '}');
        if (close < 0) { diagnostics.Add(Prefix + "template-output-unsupported:" + template.Name); return null; }
        var body = template.Body[(open + 1)..close];
        foreach (var value in values) body = Regex.Replace(body, $@"\b{Regex.Escape(template.Parameter)}\.{Regex.Escape(value.Key)}\b", value.Value);
        var outputName = string.Equals(kind, "Profile", StringComparison.Ordinal)
            ? declaration.Groups["name"].Value
            : id.Replace("[", "_", StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal);
        return $"{kind} {outputName}{declaration.Groups["tail"].Value} {{{body}}}";
    }
    private static Dictionary<string, string> Fields(string body) => Regex.Matches(body, @"\b(?<name>[A-Za-z_]\w*)\s*:\s*(?<value>(?:Point2|Vector2)\s*\([^)]*\)|[-+]?\d+(?:\.\d+)?(?:mm|deg)?)", RegexOptions.CultureInvariant)
        .Cast<Match>().ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value, StringComparer.Ordinal);
    private static bool TryRequire(string expression, out bool value)
    {
        value = false; var m = Regex.Match(expression, @"^(?<a>[-+]?\d+(?:\.\d+)?)(?<u>mm|deg)?\s*(?<op>>=|<=|==|>|<)\s*(?<b>[-+]?\d+(?:\.\d+)?)(?<v>mm|deg)?$", RegexOptions.CultureInvariant);
        if (!m.Success || m.Groups["u"].Value != m.Groups["v"].Value) return false;
        var a = double.Parse(m.Groups["a"].Value, CultureInfo.InvariantCulture); var b = double.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture);
        value = m.Groups["op"].Value switch { ">" => a > b, ">=" => a >= b, "<" => a < b, "<=" => a <= b, "==" => a == b, _ => false }; return true;
    }
    private static int MatchPair(string text, int open, char left, char right) { var depth = 0; for (var i = open; i < text.Length; i++) { if (text[i] == left) depth++; else if (text[i] == right && --depth == 0) return i; } return -1; }
}
