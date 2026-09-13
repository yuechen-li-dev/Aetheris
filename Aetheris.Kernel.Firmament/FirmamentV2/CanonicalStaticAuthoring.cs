using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Small, bounded canonical static frontend. It normalizes records, arrays, templates,
/// patterns, and Require into erased concrete declarations before the material frontend runs.</summary>
internal static class CanonicalStaticAuthoring
{
    internal const string Prefix = "firmament-v2-static-";
    private const int MaxPatternExpansion = 1024;
    internal sealed record Result(string Source, FirmamentV2StaticAuthoringDocument? Document);
    private sealed record Template(string Name, string Type, string Parameter, string Body, FirmamentV2SourceSpan Span);

    public static Result? Expand(string source, List<string> diagnostics)
    {
        var symmetry = SemanticSymmetryAuthoring.Expand(source, diagnostics);
        if (symmetry is null) return null;
        source = symmetry.Source;
        var canonicalRoot = Regex.IsMatch(source, @"^\s*Model\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant);
        var staticDeclaration = Regex.IsMatch(source, @"\b(?:Record|Static|Template\s*(?:<|[A-Za-z_]\w*\s*\()|Pattern\s+\w+\s+Over)\b", RegexOptions.CultureInvariant)
            || symmetry.Mirrors.Count > 0 || symmetry.RadialPatterns.Count > 0;
        if (!staticDeclaration && !(canonicalRoot && Regex.IsMatch(source, @"\b(?:Require\s+[A-Za-z_]\w*\s*(?:=>|\{)|Pmi\s*\{[\s\S]*?\bFrom\s*:)", RegexOptions.CultureInvariant))) return new(source, null);
        var changes = new List<(int Start, int Length, string Text)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var recordTypes = new List<FirmamentV2RecordTypeDecl>();
        var arrays = new List<FirmamentV2StaticArrayDecl>();
        var sets = new List<FirmamentV2StaticSetDecl>();
        var staticRecords = new List<FirmamentV2StaticRecordDecl>();
        var tables = new List<FirmamentV2StaticTableDecl>();
        var templates = new List<Template>();
        var patterns = new List<FirmamentV2CanonicalPatternDecl>();
        var requires = new List<FirmamentV2RequireDecl>();

        // Enums are static type declarations. Modern template binding has consumed their
        // variants already, so keep them out of the material grammar just like Records.
        foreach (Match header in Regex.Matches(source, @"\bEnum\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant))
        {
            var close = MatchPair(source, source.IndexOf('{', header.Index), '{', '}');
            if (close >= 0) changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }

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

        foreach (Match nested in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*:\s*Set\s*<\s*Set\s*<", RegexOptions.CultureInvariant))
            diagnostics.Add(Prefix + "set-element-type-unsupported:" + nested.Groups["name"].Value + ":nested-Set");

        // Set<T> is a built-in finite value form, not a general generic or runtime collection.
        // Entry order and names are retained as semantic evidence while values are erased before AIR.
        foreach (Match header in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*:\s*Set\s*<\s*(?<type>[A-Za-z_]\w*)\s*>\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index); var close = MatchPair(source, open, '{', '}');
            var name = header.Groups["name"].Value; var type = header.Groups["type"].Value;
            if (close < 0) { diagnostics.Add(Prefix + "set-malformed:" + name); continue; }
            if (!IsSupportedSetElementType(type, recordByName)) { diagnostics.Add(Prefix + "set-element-type-unsupported:" + name + ":" + type); continue; }
            if (!names.Add(name)) { diagnostics.Add(FirmamentV2Parser.DuplicateName + ":Static:" + name); continue; }
            var entries = ParseSetEntries(source, open + 1, close, name, type, recordByName, diagnostics);
            sets.Add(new(name, type, entries, new(header.Index, close - header.Index + 1)));
            changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }
        var setByName = sets.ToDictionary(x => x.Name, StringComparer.Ordinal);

        // Tables share Record typing with Static values, but intentionally preserve their
        // columnar spelling for source inspection. The template binder has already checked
        // cells and lookups before this phase erases the compile-time declaration.
        foreach (Match header in Regex.Matches(source, @"\bStatic\s+Table\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)(?:\s+Key\s*:\s*(?<key>[A-Za-z_]\w*))?\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index); var close = MatchPair(source, open, '{', '}');
            if (close < 0) { diagnostics.Add(Prefix + "table-malformed:" + header.Groups["name"].Value); continue; }
            var columns = TableColumns(source, open + 1, close);
            var rowCount = columns.Count == 0 ? 0 : columns.First().Value.Count;
            tables.Add(new(header.Groups["name"].Value, header.Groups["type"].Value, header.Groups["key"].Success ? header.Groups["key"].Value : null, columns, rowCount, new(header.Index, close - header.Index + 1)));
            changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }

        foreach (Match header in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*=\s*(?<literal>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index); var close = MatchPair(source, open, '{', '}');
            var name = header.Groups["name"].Value; var type = header.Groups["type"].Value; var literalType = header.Groups["literal"].Value;
            if (close < 0 || !recordByName.TryGetValue(type, out var record)) { diagnostics.Add(Prefix + "record-type-invalid:" + name); continue; }
            if (!string.Equals(type, literalType, StringComparison.Ordinal)) { diagnostics.Add(Prefix + "record-literal-type-mismatch:" + name); continue; }
            if (!names.Add(name)) { diagnostics.Add(FirmamentV2Parser.DuplicateName + ":Static:" + name); continue; }
            var values = Fields(source[(open + 1)..close]);
            var missing = record.Fields.Keys.Where(field => !values.ContainsKey(field)).ToArray();
            var extra = values.Keys.Where(field => !record.Fields.ContainsKey(field)).ToArray();
            if (missing.Length > 0) diagnostics.Add(Prefix + "record-missing-field:" + name + ":" + string.Join(",", missing));
            if (extra.Length > 0) diagnostics.Add(Prefix + "record-extra-field:" + name + ":" + string.Join(",", extra));
            staticRecords.Add(new(name, type, values, new(header.Index, close - header.Index + 1)));
            changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }

        // `with` and Table row selection are compile-time Record expressions. Their actual
        // member maps are owned by the common template binder; this phase retains no runtime
        // representation and simply ensures source is erased before material parsing.
        foreach (Match declaration in Regex.Matches(source, @"\bStatic\s+[A-Za-z_]\w*\s*(?::\s*[A-Za-z_]\w*)?\s*=\s*[A-Za-z_]\w*\s+with\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', declaration.Index); var close = MatchPair(source, open, '{', '}');
            if (close >= 0) changes.Add((declaration.Index, close - declaration.Index + 1, string.Empty));
        }
        foreach (Match declaration in Regex.Matches(source, @"\bStatic\s+[A-Za-z_]\w*\s*(?::\s*[A-Za-z_]\w*)?\s*=\s*[A-Za-z_]\w*\s*\[[^\]]+\]", RegexOptions.CultureInvariant))
            changes.Add((declaration.Index, declaration.Length, string.Empty));

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

        // Named access is canonical. It substitutes the authored typed value (or a Record
        // member) before Profile/Compose parsing; no public index operation is introduced.
        foreach (var set in sets)
        {
            AddPointConsumerDeclarations(source, changes, set);
            foreach (var entry in set.Entries)
            {
                if (entry.RecordFields is null)
                    AddSetValueSubstitutions(source, changes, set, entry);
                else
                    foreach (var field in entry.RecordFields)
                        AddSubstitutions(source, changes, $@"\b{Regex.Escape(set.Name)}\s*\.\s*{Regex.Escape(entry.Name)}\s*\.\s*{Regex.Escape(field.Key)}\b", field.Value);
            }
            AddSubstitutions(source, changes, $@"\b{Regex.Escape(set.Name)}\s*\.\s*Count\b", set.Entries.Count.ToString(CultureInfo.InvariantCulture));
        }

        // Canonical finite feature Template: Template<Parameter: RecordType> Name { ... }.
        // The former Template Name(RecordType parameter) spelling remains warning-free
        // compatibility syntax for persisted Preview sources.
        var featureTemplateHeaders = Regex.Matches(source,
            @"\bTemplate\s*<\s*(?<param>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*>\s*(?<name>[A-Za-z_]\w*)\s*\{|\bTemplate\s+(?<legacyName>[A-Za-z_]\w*)\s*\(\s*(?<legacyType>[A-Za-z_]\w*)\s+(?<legacyParam>[A-Za-z_]\w*)\s*\)\s*\{",
            RegexOptions.CultureInvariant);
        foreach (Match header in featureTemplateHeaders)
        {
            var close = MatchPair(source, source.IndexOf('{', header.Index), '{', '}');
            var name = header.Groups["name"].Success ? header.Groups["name"].Value : header.Groups["legacyName"].Value;
            var type = header.Groups["type"].Success ? header.Groups["type"].Value : header.Groups["legacyType"].Value;
            var parameter = header.Groups["param"].Success ? header.Groups["param"].Value : header.Groups["legacyParam"].Value;
            if (close < 0 || !recordByName.ContainsKey(type)) { diagnostics.Add(Prefix + "template-malformed:" + name); continue; }
            if (!names.Add(name)) { diagnostics.Add(FirmamentV2Parser.DuplicateName + ":Template:" + name); continue; }
            var template = new Template(name, type, parameter, source[(source.IndexOf('{', header.Index) + 1)..close], new(header.Index, close - header.Index + 1));
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
            var sourceName = pattern.Groups["array"].Value;
            if (close >= 0 && setByName.TryGetValue(sourceName, out var set))
            {
                ExpandSetPattern(source, pattern, open, close, set, templateByName, changes, patterns, diagnostics);
                continue;
            }
            if (close < 0 || !arrayByName.TryGetValue(sourceName, out var array)) { diagnostics.Add(Prefix + "pattern-source-invalid:" + pattern.Groups["name"].Value); continue; }
            var invocation = Regex.Match(source[(open + 1)..close], @"^(?:\s)*(?<template>[A-Za-z_]\w*)\s*(?:<\s*Current\s*>|\(\s*Current\s*\))\s*$", RegexOptions.CultureInvariant);
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
            // PMI may target a semantic Pattern instance. Resolve that declaration identity
            // through the same deterministic materialization map used for generated features.
            for (var index = 0; index < generated.Count; index++)
            {
                var semanticId = generated[index];
                var materializedId = semanticId.Replace("[", "_", StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal);
                foreach (Match target in Regex.Matches(source,
                             $@"\bTarget\s*:\s*(?<id>{Regex.Escape(semanticId)})\b",
                             RegexOptions.CultureInvariant))
                    changes.Add((target.Groups["id"].Index, target.Groups["id"].Length, materializedId));
            }
        }

        // A direct invocation is the same static expansion route as Pattern, with an
        // explicit bounded element index. The declaration body is still erased before
        // material AIR; it is not a runtime call.
        foreach (Match invocation in Regex.Matches(source, @"\b(?<template>[A-Za-z_]\w*)\s*(?:<\s*(?<array>[A-Za-z_]\w*)\s*\[\s*(?<index>\d+)\s*\]\s*>|\(\s*(?<legacyArray>[A-Za-z_]\w*)\s*\[\s*(?<legacyIndex>\d+)\s*\]\s*\))", RegexOptions.CultureInvariant))
        {
            var arrayName = invocation.Groups["array"].Success ? invocation.Groups["array"].Value : invocation.Groups["legacyArray"].Value;
            var indexText = invocation.Groups["index"].Success ? invocation.Groups["index"].Value : invocation.Groups["legacyIndex"].Value;
            if (!templateByName.TryGetValue(invocation.Groups["template"].Value, out var template)
                || !arrayByName.TryGetValue(arrayName, out var array)
                || template.Type != array.ElementType) continue;
            var index = int.Parse(indexText, CultureInfo.InvariantCulture);
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
        var semanticConstraints = new List<FirmamentV2SemanticConstraint>();
        foreach (Match header in Regex.Matches(source, @"\bRequire\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index); var close = MatchPair(source, open, '{', '}');
            if (close < 0) { diagnostics.Add(FirmamentV2Parser.SemanticConstraintMalformed); continue; }
            var fields = RequireFields(source[(open + 1)..close]);
            if (!fields.TryGetValue("Actual", out var actual) || !fields.TryGetValue("Expected", out var expected))
            { diagnostics.Add(FirmamentV2Parser.SemanticConstraintMalformed); continue; }
            if (!TrySubject(actual, out var subject, out var property) || !TryLength(ResolveStaticValue(source, expected), out var nominal))
            { diagnostics.Add(FirmamentV2Parser.SemanticConstraintUnsupported); continue; }
            FirmamentV2Tolerance? tolerance = null;
            if (fields.TryGetValue("Tolerance", out var toleranceSource) && !TryTolerance(ResolveStaticValue(source, toleranceSource), out tolerance))
            { diagnostics.Add(FirmamentV2Parser.SemanticConstraintDimensionMismatch); continue; }
            var name = header.Groups["name"].Value;
            var span = new FirmamentV2SourceSpan(header.Index, close - header.Index + 1);
            requires.Add(new(name, $"{actual} == {expected}", true, span, "semantic-constraint", actual, expected, fields.GetValueOrDefault("Tolerance")));
            semanticConstraints.Add(new(name, subject, property, nominal, tolerance, false, span, expected));
            changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }
        var projections = RewriteProjectedPmi(source, semanticConstraints, changes, diagnostics);
        if (diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal) || d.StartsWith(FirmamentV2Parser.DuplicateName, StringComparison.Ordinal))) return null;
        // A static declaration / Require is erased as a whole. Do not also apply a
        // substitution nested inside that erased span: applying the nested edit first
        // changes the parent span's offsets and corrupts the following declaration.
        var erasures = changes.Where(change => change.Text.Length == 0
            || Regex.IsMatch(source.Substring(change.Start, Math.Min(change.Length, source.Length - change.Start)), @"^\s*(?:Static\s+\w+\s*:\s*Set\s*<|Pattern\s+\w+\s+Over\b)", RegexOptions.CultureInvariant)).ToArray();
        foreach (var change in changes
            .Where(change => !erasures.Any(erase => erase.Start < change.Start && change.Start < erase.Start + erase.Length))
            .OrderByDescending(change => change.Start))
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return new(source, new(recordTypes, arrays, templates.Select(t => new FirmamentV2CanonicalTemplateDecl(t.Name, t.Type, t.Parameter, t.Body, t.Span)).ToArray(), patterns, requires, semanticConstraints, projections, staticRecords, tables, sets, symmetry.Mirrors, symmetry.RadialPatterns));
    }

    private static IReadOnlyList<FirmamentV2StaticSetEntry> ParseSetEntries(string source, int start, int end, string setName, string elementType,
        IReadOnlyDictionary<string, FirmamentV2RecordTypeDecl> recordTypes, List<string> diagnostics)
    {
        var body = source[start..end];
        var headers = Regex.Matches(body, @"(?<![A-Za-z0-9_])(?<name>[A-Za-z_]\w*)\s*=>", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        var result = new List<FirmamentV2StaticSetEntry>(); var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < headers.Length; index++)
        {
            var header = headers[index]; var entryName = header.Groups["name"].Value;
            var valueStart = header.Index + header.Length; var valueEnd = index + 1 < headers.Length ? headers[index + 1].Index : body.Length;
            var value = body[valueStart..valueEnd].Trim().TrimEnd(',', ';').Trim();
            var span = new FirmamentV2SourceSpan(start + header.Index, valueEnd - header.Index);
            if (!names.Add(entryName)) { diagnostics.Add(Prefix + "set-duplicate-entry:" + setName + ":" + entryName); continue; }
            IReadOnlyDictionary<string, string>? fields = null;
            if (recordTypes.TryGetValue(elementType, out var record))
            {
                var literal = Regex.Match(value, $@"^{Regex.Escape(elementType)}\s*\{{(?<body>[\s\S]*)\}}$", RegexOptions.CultureInvariant);
                if (!literal.Success) { diagnostics.Add(Prefix + "set-entry-type-mismatch:" + setName + ":" + entryName + ":expected-" + elementType); continue; }
                fields = Fields(literal.Groups["body"].Value);
                var missing = record.Fields.Keys.Where(field => !fields.ContainsKey(field)).ToArray();
                var extra = fields.Keys.Where(field => !record.Fields.ContainsKey(field)).ToArray();
                if (missing.Length > 0) diagnostics.Add(Prefix + "record-missing-field:" + setName + "." + entryName + ":" + string.Join(",", missing));
                if (extra.Length > 0) diagnostics.Add(Prefix + "record-extra-field:" + setName + "." + entryName + ":" + string.Join(",", extra));
                foreach (var field in fields)
                    if (record.Fields.TryGetValue(field.Key, out var fieldType) && !ValueMatchesType(field.Value, fieldType))
                        diagnostics.Add(Prefix + "set-entry-field-type-mismatch:" + setName + ":" + entryName + ":" + field.Key + ":expected-" + fieldType);
            }
            else if (!ValueMatchesType(value, elementType))
            { diagnostics.Add(Prefix + "set-entry-type-mismatch:" + setName + ":" + entryName + ":expected-" + elementType); continue; }
            result.Add(new(entryName, value, index, span, fields));
        }
        return result;
    }

    private static void ExpandSetPattern(string source, Match pattern, int open, int close, FirmamentV2StaticSetDecl set,
        IReadOnlyDictionary<string, Template> templates, List<(int Start, int Length, string Text)> changes,
        List<FirmamentV2CanonicalPatternDecl> patterns, List<string> diagnostics)
    {
        var patternName = pattern.Groups["name"].Value;
        if (set.Entries.Count > MaxPatternExpansion) { diagnostics.Add(Prefix + "pattern-expansion-limit:" + patternName + ":" + set.Entries.Count); return; }
        var body = source[(open + 1)..close].Trim();
        var arrow = Regex.Match(body, @"^(?<binder>[A-Za-z_]\w*)\s*=>\s*(?<mapping>[\s\S]+)$", RegexOptions.CultureInvariant);
        if (!arrow.Success) { diagnostics.Add(Prefix + "pattern-body-invalid:" + patternName); return; }
        var binder = arrow.Groups["binder"].Value; var mapping = arrow.Groups["mapping"].Value.Trim();
        var output = new List<string>(); var generated = new List<string>(); var associations = new List<FirmamentV2PatternAssociation>();
        foreach (var entry in set.Entries)
        {
            var semanticId = patternName + "." + entry.Name;
            var materializedId = patternName + "_" + entry.Name;
            string? declaration;
            var templateCall = Regex.Match(mapping, @"^(?<template>[A-Za-z_]\w*)\s*(?:<\s*Current\s*>|\(\s*Current\s*\))$", RegexOptions.CultureInvariant);
            if (templateCall.Success && templates.TryGetValue(templateCall.Groups["template"].Value, out var template) && entry.RecordFields is not null && template.Type == set.ElementType)
                declaration = Instantiate(template, entry.RecordFields, materializedId, true, diagnostics);
            else
            {
                declaration = mapping;
                if (entry.RecordFields is not null)
                    foreach (var field in entry.RecordFields)
                        declaration = Regex.Replace(declaration, $@"\b{Regex.Escape(binder)}\s*\.\s*{Regex.Escape(field.Key)}\b", field.Value, RegexOptions.CultureInvariant);
                else declaration = Regex.Replace(declaration, $@"\b{Regex.Escape(binder)}\b", entry.Value, RegexOptions.CultureInvariant);
                var construction = Regex.Match(declaration, @"\b(?:Hole\s*<\s*(?:Shaft|Counterbore|Countersink)\s*>|Slot\s*<\s*(?:Capsule|RoundedRectangle)\s*>|Boss|Pocket|EdgeFinish)\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
                if (!construction.Success) { diagnostics.Add(Prefix + "pattern-body-invalid:" + patternName); return; }
                declaration = declaration.Remove(construction.Groups["name"].Index, construction.Groups["name"].Length).Insert(construction.Groups["name"].Index, materializedId);
                var openingBrace = declaration.IndexOf('{', construction.Index);
                declaration = declaration.Insert(openingBrace + 1, " PatternIdentity: " + semanticId + " ");
            }
            if (declaration is null) continue;
            output.Add(declaration); generated.Add(semanticId);
            associations.Add(new(semanticId, set.Name, entry.Name, entry.Value, entry.SourceOrder, entry.Provenance));
        }
        patterns.Add(new(patternName, set.Name, Regex.Match(mapping, @"^[A-Za-z_]\w*").Value, generated.Count, generated,
            new(pattern.Index, close - pattern.Index + 1), associations));
        changes.Add((pattern.Index, close - pattern.Index + 1, string.Join(Environment.NewLine, output)));
    }

    private static bool IsSupportedSetElementType(string type, IReadOnlyDictionary<string, FirmamentV2RecordTypeDecl> records) =>
        type is "Point2" or "Point3" or "Vector2" or "Length" or "Angle" or "Int" or "Float" or "Bool" || records.ContainsKey(type);

    private static bool ValueMatchesType(string value, string type)
    {
        value = value.Trim();
        return type switch
        {
            "Point2" => Regex.IsMatch(value, @"^Point2\s*\(\s*[-+]?\d+(?:\.\d+)?mm\s*,\s*[-+]?\d+(?:\.\d+)?mm\s*\)$", RegexOptions.CultureInvariant),
            "Point3" => Regex.IsMatch(value, @"^Point3\s*\(\s*[-+]?\d+(?:\.\d+)?mm\s*,\s*[-+]?\d+(?:\.\d+)?mm\s*,\s*[-+]?\d+(?:\.\d+)?mm\s*\)$", RegexOptions.CultureInvariant),
            "Vector2" => Regex.IsMatch(value, @"^Vector2\s*\(\s*[-+]?\d+(?:\.\d+)?\s*,\s*[-+]?\d+(?:\.\d+)?\s*\)$", RegexOptions.CultureInvariant),
            "Length" => Regex.IsMatch(value, @"^[-+]?\d+(?:\.\d+)?mm$", RegexOptions.CultureInvariant),
            "Angle" => Regex.IsMatch(value, @"^[-+]?\d+(?:\.\d+)?deg$", RegexOptions.CultureInvariant),
            "Int" => Regex.IsMatch(value, @"^[-+]?\d+$", RegexOptions.CultureInvariant),
            "Float" => Regex.IsMatch(value, @"^[-+]?\d+(?:\.\d+)?$", RegexOptions.CultureInvariant),
            "Bool" => value is "true" or "false",
            _ => Regex.IsMatch(value, $@"^{Regex.Escape(type)}\s*\{{", RegexOptions.CultureInvariant)
        };
    }

    private static void AddSubstitutions(string source, List<(int Start, int Length, string Text)> changes, string pattern, string replacement)
    {
        foreach (Match match in Regex.Matches(source, pattern, RegexOptions.CultureInvariant)) changes.Add((match.Index, match.Length, replacement));
    }

    private static void AddSetValueSubstitutions(string source, List<(int Start, int Length, string Text)> changes,
        FirmamentV2StaticSetDecl set, FirmamentV2StaticSetEntry entry)
    {
        var pattern = $@"\b{Regex.Escape(set.Name)}\s*\.\s*{Regex.Escape(entry.Name)}\b";
        foreach (Match match in Regex.Matches(source, pattern, RegexOptions.CultureInvariant))
        {
            var prefix = source[Math.Max(0, match.Index - 12)..match.Index];
            var needsNamedPoint = set.ElementType == "Point2" && Regex.IsMatch(prefix, @"(?:From|To)\s*:\s*$", RegexOptions.CultureInvariant);
            changes.Add((match.Index, match.Length, needsNamedPoint ? set.Name + "_" + entry.Name : entry.Value));
        }
    }

    private static string PointDeclarations(string setName, string elementType, IReadOnlyList<FirmamentV2StaticSetEntry> entries)
    {
        if (elementType != "Point2") return string.Empty;
        return string.Join(Environment.NewLine, entries.Select(entry =>
        {
            var match = Regex.Match(entry.Value, @"^Point2\s*\((?<body>[^)]+)\)$", RegexOptions.CultureInvariant);
            return match.Success ? $"Point2 {setName}_{entry.Name} {{ Position: [{match.Groups["body"].Value}] }}" : string.Empty;
        }));
    }

    private static void AddPointConsumerDeclarations(string source, List<(int Start, int Length, string Text)> changes, FirmamentV2StaticSetDecl set)
    {
        if (set.ElementType != "Point2" || !Regex.IsMatch(source,
                $@"(?:From|To)\s*:\s*{Regex.Escape(set.Name)}\s*\.", RegexOptions.CultureInvariant)) return;
        foreach (Match layout in Regex.Matches(source, @"\bConcept\s+Struct\s+[A-Za-z_]\w*(?:\s+On\s+[A-Za-z_]\w*)?\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', layout.Index); var close = MatchPair(source, open, '{', '}');
            if (close < 0 || !Regex.IsMatch(source[(open + 1)..close], $@"(?:From|To)\s*:\s*{Regex.Escape(set.Name)}\s*\.", RegexOptions.CultureInvariant)) continue;
            changes.Add((open + 1, 0, Environment.NewLine + PointDeclarations(set.Name, set.ElementType, set.Entries) + Environment.NewLine));
            return;
        }
    }

    private static string? Instantiate(Template template, IReadOnlyDictionary<string, string> values, string id, bool patterned, List<string> diagnostics)
    {
        var declaration = Regex.Match(template.Body, @"\b(?<kind>Hole\s*<\s*(?:Shaft|Counterbore)\s*>|Slot\s*<\s*(?:Capsule|RoundedRectangle)\s*>|Profile|StandardPart)\s+(?<name>[A-Za-z_]\w*)(?<tail>\s+Using\s+[A-Za-z_]\w*)?\s*\{", RegexOptions.CultureInvariant);
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
    private static Dictionary<string, string> Fields(string body) => Regex.Matches(body, "\\b(?<name>[A-Za-z_]\\w*)\\s*:\\s*(?<value>\"[^\"]*\"|(?:Point2|Vector2|PlusMinus)\\s*\\([^)]*\\)|\\d+\\.\\d+\\.\\d+|\\d{4}-\\d{2}-\\d{2}|[A-Za-z_]\\w*|[-+]?\\d+(?:\\.\\d+)?(?:mm|deg)?)", RegexOptions.CultureInvariant)
        .Cast<Match>().ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value, StringComparer.Ordinal);
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> TableColumns(string source, int start, int end)
    {
        var columns = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (Match column in Regex.Matches(source[start..end], @"\b(?<name>[A-Za-z_]\w*)\s*:\s*\[", RegexOptions.CultureInvariant))
        {
            var open = start + column.Index + column.Value.LastIndexOf('['); var close = MatchPair(source, open, '[', ']');
            if (close < 0 || close > end) continue;
            columns[column.Groups["name"].Value] = Regex.Matches(source[(open + 1)..close], "\"[^\"]*\"|[-+]?\\d+(?:\\.\\d+)?(?:mm|deg)?|[A-Za-z_]\\w*", RegexOptions.CultureInvariant).Cast<Match>().Select(match => match.Value).ToArray();
        }
        return columns;
    }
    private static Dictionary<string, string> RequireFields(string body) => Regex.Matches(body, @"(?<name>[A-Za-z_]\w*)\s*:\s*(?<value>.*?)(?=\s+[A-Za-z_]\w*\s*:|$)", RegexOptions.CultureInvariant | RegexOptions.Singleline)
        .Cast<Match>().ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value.Trim(), StringComparer.Ordinal);
    private static bool TrySubject(string text, out string subject, out string property)
    { var m = Regex.Match(text.Trim(), @"^(?<subject>[A-Za-z_]\w*)\.(?<property>Diameter)$", RegexOptions.CultureInvariant); subject = m.Groups["subject"].Value; property = m.Groups["property"].Value; return m.Success; }
    private static bool TryLength(string text, out FirmamentV2LiteralValue value)
    { var m = Regex.Match(text.Trim(), @"^(?<n>[-+]?\d+(?:\.\d+)?)mm$", RegexOptions.CultureInvariant); value = new(FirmamentV2PrimitiveType.Length, 0d); if (!m.Success || !double.TryParse(m.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || n <= 0d) return false; value = new(FirmamentV2PrimitiveType.Length, n, n, "mm", text); return true; }
    private static bool TryTolerance(string text, out FirmamentV2Tolerance? tolerance)
    { var m = Regex.Match(text.Trim(), @"^PlusMinus\(\s*(?<p>[-+]?\d+(?:\.\d+)?)mm\s*,\s*(?<m>[-+]?\d+(?:\.\d+)?)mm\s*\)$", RegexOptions.CultureInvariant); tolerance = null; if (!m.Success || !double.TryParse(m.Groups["p"].Value, CultureInfo.InvariantCulture, out var plus) || !double.TryParse(m.Groups["m"].Value, CultureInfo.InvariantCulture, out var minus) || plus < 0 || minus < 0) return false; tolerance = new(FirmamentV2ToleranceKind.Asymmetric, plus, minus, "mm", FirmamentV2PrimitiveType.Length, new(0, text.Length)); return true; }
    private static string ResolveStaticValue(string source, string expression)
    {
        var array = Regex.Match(expression.Trim(), @"^(?<array>[A-Za-z_]\w*)\s*\[\s*0\s*\]\s*\.\s*(?<field>[A-Za-z_]\w*)$", RegexOptions.CultureInvariant);
        if (array.Success)
        {
            var staticMatch = Regex.Match(source, $@"\bStatic\s+{Regex.Escape(array.Groups["array"].Value)}\s*:\s*[A-Za-z_]\w*\[\]\s*=\s*\[\s*[A-Za-z_]\w*\s*\{{(?<body>[\s\S]*?)\}}", RegexOptions.CultureInvariant);
            var field = staticMatch.Success ? Fields(staticMatch.Groups["body"].Value).GetValueOrDefault(array.Groups["field"].Value) : null;
            return field ?? expression.Trim();
        }
        var dotted = Regex.Match(expression.Trim(), @"^(?<record>[A-Za-z_]\w*)\.(?<field>[A-Za-z_]\w*)$", RegexOptions.CultureInvariant); if (!dotted.Success) return expression.Trim(); var record = Regex.Match(source, $@"\blet\s+{Regex.Escape(dotted.Groups["record"].Value)}\s*\{{(?<body>[\s\S]*?)\}}", RegexOptions.CultureInvariant); if (!record.Success) return expression.Trim(); var directField = Regex.Match(record.Groups["body"].Value, $@"(?m)^\s*{Regex.Escape(dotted.Groups["field"].Value)}\s*:\s*[A-Za-z_]\w*\s*=\s*(?<value>[^\r\n]+)", RegexOptions.CultureInvariant); return directField.Success ? directField.Groups["value"].Value.Trim() : expression.Trim(); }
    private static IReadOnlyDictionary<string, FirmamentV2PmiProjection> RewriteProjectedPmi(string source, IReadOnlyList<FirmamentV2SemanticConstraint> constraints, List<(int Start, int Length, string Text)> changes, List<string> diagnostics)
    {
        var result = new Dictionary<string, FirmamentV2PmiProjection>(StringComparer.Ordinal);
        foreach (Match pmi in Regex.Matches(source, @"\bPmi\s*\{", RegexOptions.CultureInvariant))
        {
            var outerOpen = source.IndexOf('{', pmi.Index); var outerClose = MatchPair(source, outerOpen, '{', '}'); if (outerClose < 0) continue;
            var body = source[(outerOpen + 1)..outerClose];
            foreach (Match entry in Regex.Matches(body, @"\b(?<kind>[A-Za-z_]\w*)\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
            {
                var entryOpen = outerOpen + 1 + body.IndexOf('{', entry.Index); var entryClose = MatchPair(source, entryOpen, '{', '}'); if (entryClose < 0) continue;
                var fields = RequireFields(source[(entryOpen + 1)..entryClose]);
                if (!fields.TryGetValue("From", out var from)) continue;
                if (!fields.TryGetValue("As", out var asKind) || !string.Equals(asKind, "HoleDiameter", StringComparison.Ordinal) || !string.Equals(entry.Groups["kind"].Value, "HoleDiameter", StringComparison.Ordinal)) { diagnostics.Add(FirmamentV2Parser.PmiProjectionUnsupportedKind); continue; }
                if (fields.Keys.Any(key => key is "Target" or "Value" or "Tolerance" or "Dimension" or "Diameter")) { diagnostics.Add(FirmamentV2Parser.PmiProjectedFieldMustNotOverrideSourceConstraint); continue; }
                var constraint = constraints.SingleOrDefault(c => string.Equals(c.Id, from, StringComparison.Ordinal));
                if (constraint is null) { diagnostics.Add(FirmamentV2Parser.PmiProjectionUnknownRequire); continue; }
                var value = constraint.NominalValue.NumericValue!.Value.ToString(CultureInfo.InvariantCulture) + "mm";
                var replacement = $"\n            Target: {constraint.Subject}\n            Value: {value}";
                if (constraint.Tolerance is { } tolerance) replacement += $"\n            Tolerance: PlusMinus({tolerance.Plus.ToString(CultureInfo.InvariantCulture)}mm, {tolerance.Minus.ToString(CultureInfo.InvariantCulture)}mm)";
                if (fields.TryGetValue("DatumRefs", out var datum)) replacement += $"\n            DatumRefs: {datum}";
                replacement += "\n        ";
                changes.Add((entryOpen + 1, entryClose - entryOpen - 1, replacement));
                result[entry.Groups["name"].Value] = new(from, FirmamentV2PmiKind.HoleDiameter, new(entry.Index + outerOpen + 1, entry.Length));
            }
        }
        return result;
    }
    private static bool TryRequire(string expression, out bool value)
    {
        value = false; var m = Regex.Match(expression, @"^(?<a>[-+]?\d+(?:\.\d+)?)(?<u>mm|deg)?\s*(?<op>>=|<=|==|>|<)\s*(?<b>[-+]?\d+(?:\.\d+)?)(?<v>mm|deg)?$", RegexOptions.CultureInvariant);
        if (!m.Success || m.Groups["u"].Value != m.Groups["v"].Value) return false;
        var a = double.Parse(m.Groups["a"].Value, CultureInfo.InvariantCulture); var b = double.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture);
        value = m.Groups["op"].Value switch { ">" => a > b, ">=" => a >= b, "<" => a < b, "<=" => a <= b, "==" => a == b, _ => false }; return true;
    }
    private static int MatchPair(string text, int open, char left, char right) { var depth = 0; for (var i = open; i < text.Length; i++) { if (text[i] == left) depth++; else if (text[i] == right && --depth == 0) return i; } return -1; }
}
