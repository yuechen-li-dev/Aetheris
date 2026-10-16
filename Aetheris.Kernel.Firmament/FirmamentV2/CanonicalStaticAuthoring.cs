using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Finite static authoring owner. Pattern headers/mappings and finite feature
/// Template output bind AST nodes; the remaining Record/Set catalog and source adapters
/// are explicit migration boundaries before domain AIR.</summary>
internal static class CanonicalStaticAuthoring
{
    internal const string Prefix = "firmament-v2-static-";
    private const int MaxPatternExpansion = 1024;
    internal sealed record Result(string Source, FirmamentV2StaticAuthoringDocument? Document);
    private sealed record Template(string Name, string Type, string Parameter, string Body, FirmamentV2SourceSpan Span);

    /// <summary>Assembly consumes the same checked Record/Set values, but yields product-tree declarations.</summary>
    internal static Result? ExpandAssemblyPatterns(string source, List<string> diagnostics, bool spatial = false)
    {
        var binding = new FirmamentDataBinding(source);
        diagnostics.AddRange(binding.Diagnostics.Select(d => d.Code + ": " + d.Message));
        if (binding.Diagnostics.Count > 0) return null;
        source = binding.Source;
        // Assembly frames consume the same checked Record leaves as mechanical
        // geometry. Alpha-bound identities preserve lexical scope; the existing
        // substitution owner transports their nominal values to domain readers.
        var scalars = new FirmamentCompileTimeValues(source);
        scalars.UseDataBinding(binding);
        _ = FirmamentV2TemplateExpansion.ReadDataCatalog(source, diagnostics, scalars, initialize: true);
        if (diagnostics.Count > 0) return null;
        var inputs = scalars.MaterialInputs(source);
        if (inputs.Any(v => v.Tolerance is not null))
        { diagnostics.Add(Prefix + "assembly-tolerance-consumer-unsupported: Assembly frame values cannot project a manufacturing tolerance; use the owning Part and explicit PMI."); return null; }
        source = FirmamentValueSubstitution.Apply(source, inputs.ToDictionary(v => v.Name, v => scalars.NominalExpression(v.Name), StringComparer.Ordinal));
        var points = ConceptPointAuthoring.Expand(source, diagnostics);
        if (points is null) return null;
        source = points.Source;
        var sites = AssemblySiteAuthoring.Expand(source, diagnostics);
        if (sites is null) return null;
        source = sites.Source;
        var syntax = FirmamentSyntaxParser.Parse(source);
        var headers = FirmamentSyntaxTraversal.Declarations(syntax.Document)
            .Where(d => d.Type.Name.Text == "Pattern" && d.Clauses.Any(c => c.Kind == ClauseKind.Over)).ToArray();
        if (headers.Length == 0)
        {
            source = points.Math.Normalize(source, diagnostics);
            return diagnostics.Count > 0 ? null : new(source, points.Points.Count == 0 ? null : new([],[],[],[],[], ConceptPoints: points.Points));
        }
        var catalog = source.Select(c => c is '\r' or '\n' ? c : ' ').ToArray();
        foreach (var declaration in FirmamentSyntaxTraversal.Declarations(syntax.Document)
            .Where(d => d.Type.Name.Text is "Record" or "Static" || d.Type.Name.Text == "Table" && d.Modifiers.Any(m => m.Name.Text == "Static")))
            source.CopyTo(declaration.Span.Start, catalog, declaration.Span.Start, declaration.Span.Length);
        var catalogSource = new string(catalog);
        if (spatial)
        {
            // Scene-scale Set data normalizes to the existing typed mm catalog.
            // Strings and authored identifiers are never unit-converted.
            var tokens = FirmamentLanguageAnalysisService.Lex(catalogSource);
            for (var i = tokens.Count - 2; i >= 0; i--)
                if (tokens[i].Shape == "number" && tokens[i+1].Text == "m" && tokens[i].Start+tokens[i].Length == tokens[i+1].Start
                    && double.TryParse(tokens[i].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var metres))
                    catalogSource = catalogSource.Remove(tokens[i].Start,tokens[i].Length+1)
                        .Insert(tokens[i].Start,(metres*1000).ToString("R",CultureInfo.InvariantCulture)+"mm");
        }
        var data=Expand(catalogSource,diagnostics);
        if (data?.Document is null || diagnostics.Count>0) return null;
        var sets=(data.Document.Sets ?? []).ToDictionary(s=>s.Name,StringComparer.Ordinal);
        var changes=new List<(int Start,int Length,string Text)>();
        var hoisted = new List<string>();
        var occupied = syntax.Document.Tokens.Where(t => t.Kind == SyntaxTokenKind.Identifier).Select(t => t.Text).ToHashSet(StringComparer.Ordinal);
        var patterns=new List<FirmamentV2CanonicalPatternDecl>(); var names=new HashSet<string>(); var total=0;
        foreach (var header in headers)
        {
            var name = header.Name?.Text ?? "<unnamed>";
            if (!names.Add(name)) { diagnostics.Add(Prefix + "assembly-pattern-duplicate:" + name); continue; }
            var sourceName = header.Clauses.Count == 1 ? FirmamentSyntaxValues.Reference(header.Clauses[0].Value) : null;
            if (sourceName is null || !sets.TryGetValue(sourceName, out var set))
            { diagnostics.Add(Prefix + "pattern-source-invalid:" + name); continue; }
            if (header.Body?.Statements.FirstOrDefault() is not MappingSyntax mapping)
            { diagnostics.Add(Prefix + "assembly-pattern-body-invalid:" + name); continue; }
            var mappedStatements = (mapping.Value is StatementBlockSyntax block ? block.Body.Statements : [mapping.Value]).Concat(header.Body.Statements.Skip(1)).ToArray();
            if (mappedStatements.Any(s => s is not DeclarationSyntax d || !(d.Occurrence && d.Type.Name.Text is "Part" or "Assembly"
                    || !d.Occurrence && (d.Type.Name.Text is "Interface" or "Mate" || d.Type.Name.Text == "Struct" && d.Modifiers.Any(m => m.Name.Text == "Concept"))))
                || header.Parameters.Count > 0 || header.Modifiers.Count > 0 || header.Initializer is not null || header.Annotation is not null || header.ResultType is not null
                || header.Type.Span.Length > header.Type.Name.Span.Length || header.Occurrence || header.HasParameterList)
            { diagnostics.Add(Prefix + "assembly-pattern-body-invalid:" + name); continue; }
            if (mappedStatements.OfType<DeclarationSyntax>().Where(d => d.Name is not null).GroupBy(d => d.Name!.Text).Any(g => g.Count() > 1))
            { diagnostics.Add(Prefix + "assembly-pattern-duplicate-declaration:" + name); continue; }
            if (mapping.Value is not StatementBlockSyntax && mappedStatements.OfType<DeclarationSyntax>().Any(d => !d.Occurrence))
            { diagnostics.Add(Prefix + "assembly-pattern-body-invalid:" + name); continue; }
            total+=set.Entries.Count;
            if (set.Entries.Count>MaxPatternExpansion || total>4096) { diagnostics.Add(Prefix+"pattern-expansion-limit:"+name); continue; }
            var output=new List<string>(); var associations=new List<FirmamentV2PatternAssociation>();
            const string identity="Placement LegacyExplicit = [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];";
            foreach (var entry in set.Entries)
            {
                var substitution = BindSetElement(mapping.Key.Text, entry, diagnostics);
                if (substitution is null) continue;
                var bound = mappedStatements.Select(substitution.Statement).ToArray();
                if (mapping.Value is StatementBlockSyntax)
                {
                    string PrivateName(string local)
                    {
                        var identity = name + "\0" + entry.Name + "\0" + local;
                        var stem = "__Pattern_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)))[..20] + "_" + local;
                        var candidate = stem; var suffix = 0;
                        while (!occupied.Add(candidate)) candidate = stem + "_" + ++suffix;
                        return candidate;
                    }
                    var renames = bound.OfType<DeclarationSyntax>().Where(d => !d.Occurrence && d.Name is not null)
                        .ToDictionary(d => d.Name!.Text, d => PrivateName(d.Name!.Text), StringComparer.Ordinal);
                    var parent = OccurrenceOwner(syntax.Document.Statements, header, []);
                    if (parent is null || parent.Count == 0)
                    { diagnostics.Add(Prefix + "assembly-pattern-owner-required:" + name); continue; }
                    var paths = bound.OfType<DeclarationSyntax>().Where(d => d.Occurrence && d.Name is not null)
                        .ToDictionary(d => d.Name!.Text, d => FirmamentSyntaxParser.ParseExpression(string.Join(".", parent.Append(name).Append(entry.Name).Append(d.Name!.Text))).Expression!, StringComparer.Ordinal);
                    var scope = new FirmamentSyntaxSubstitution(paths, renames);
                    bound = bound.Select(scope.Statement).ToArray();
                    foreach (var declaration in bound.OfType<DeclarationSyntax>().Where(d => !d.Occurrence && d.Type.Name.Text is "Struct" or "Interface"))
                        hoisted.Add(FirmamentSyntaxWriter.Statement(declaration));
                    bound = bound.Where(d => d is not DeclarationSyntax { Occurrence: false, Type.Name.Text: "Struct" or "Interface" }).ToArray();
                }
                var mappedText = FirmamentSyntaxWriter.Statements(bound, source);
                var mappedSyntax = new DocumentSyntax("expanded-pattern", bound, [], header.Span);
                if (FirmamentSyntaxTraversal.Expressions(mappedSyntax).OfType<MemberSyntax>()
                    .Any(m => FirmamentSyntaxValues.Reference(m.Target) == mapping.Key.Text))
                { diagnostics.Add(Prefix + "assembly-pattern-member-unresolved:" + name + "." + entry.Name); continue; }
                output.Add(spatial ? $"<Group {entry.Name}>\n{mappedText}\n</Group>" : $"<Assembly {entry.Name}> {identity}\n{mappedText}\n</Assembly>");
                associations.Add(new(name+"."+entry.Name,set.Name,entry.Name,entry.Value,entry.SourceOrder,entry.Provenance));
            }
            changes.Add((header.Span.Start,header.Span.Length,spatial ? $"<Group {name}>\n{string.Join(Environment.NewLine,output)}\n</Group>" : $"<Assembly {name}> {identity}\n{string.Join(Environment.NewLine,output)}\n</Assembly>"));
            patterns.Add(new(name,set.Name,"Assembly",set.Entries.Count,associations.Select(a=>a.GeneratedId).ToArray(),header.Span,associations,
                sites.Recipes.GetValueOrDefault(set.Name)));
        }
        foreach (var change in changes.OrderByDescending(c=>c.Start)) source=source.Remove(change.Start,change.Length).Insert(change.Start,change.Text);
        source += "\n" + string.Join("\n", hoisted);
        source = points.Math.Normalize(source, diagnostics);
        var conceptualSets = points.Points.ToDictionary(p => ConceptPointAuthoring.SetName(p.Name), p => p);
        patterns = patterns.Select(pattern => conceptualSets.TryGetValue(pattern.Source, out var collection)
            ? pattern with { Source = collection.Name, Associations = pattern.Associations?.Select(a => a with { SourceSet = collection.Name }).ToArray() } : pattern).ToList();
        return diagnostics.Count > 0 ? null : new(source,data.Document with { Patterns=patterns, ConceptPoints=points.Points });
    }

    private static IReadOnlyList<string>? OccurrenceOwner(IReadOnlyList<StatementSyntax> statements, DeclarationSyntax target, IReadOnlyList<string> path)
    {
        foreach (var statement in statements.OfType<DeclarationSyntax>())
        {
            if (ReferenceEquals(statement, target)) return path;
            if (statement.Body is not null && OccurrenceOwner(statement.Body.Statements, target,
                statement.Occurrence && statement.Name is not null ? path.Append(statement.Name.Text).ToArray() : path) is { } owner) return owner;
        }
        return null;
    }

    private static FirmamentSyntaxSubstitution? BindSetElement(string binder, FirmamentV2StaticSetEntry entry, List<string> diagnostics)
    {
        var values = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        bool Add(string name, string value)
        {
            var parsed = FirmamentSyntaxParser.ParseExpression(value);
            if (parsed.Expression is null || parsed.Diagnostics.Count > 0)
            { diagnostics.Add(Prefix + "pattern-value-invalid:" + entry.Name + ":" + name); return false; }
            values[name] = parsed.Expression; return true;
        }
        if (entry.RecordFields is not null)
        {
            if (!Add(binder, entry.Value)) return null;
            foreach (var field in entry.RecordFields)
                if (!Add(binder + "." + field.Key, field.Value)) return null;
        }
        else
        {
            if (!Add(binder, entry.Value)) return null;
            if (values[binder] is CallSyntax { Target: ReferenceSyntax { Name.Text: "Point2" or "Point3" } } point)
            {
                for (var i = 0; i < point.Arguments.Count; i++)
                {
                    values[binder + "." + "XYZ"[i]] = point.Arguments[i].Value;
                    values[binder + ".Position." + "XYZ"[i]] = point.Arguments[i].Value;
                }
                values[binder + ".Position"] = point;
            }
        }
        return new(values);
    }

    public static Result? Expand(string source, List<string> diagnostics, FirmamentCompileTimeValues? scalars = null)
    {
        var binding = new FirmamentDataBinding(source);
        diagnostics.AddRange(binding.Diagnostics.Select(d => scalars is null ? d.Code + ": " + d.Message : d.Code));
        scalars?.AddExpansionDiagnostics(binding.Diagnostics);
        if (binding.Diagnostics.Count > 0) return null;
        scalars?.UseDataBinding(binding);
        source = binding.Source;
        var points = ConceptPointAuthoring.Expand(source, diagnostics);
        if (points is null) return null;
        source = points.Source;
        var linear = LinearFeatureAuthoring.Expand(source, diagnostics);
        if (linear is null) return null;
        source = linear.Source;
        var symmetry = SemanticSymmetryAuthoring.Expand(source, diagnostics);
        if (symmetry is null) return null;
        source = symmetry.Source;
        var canonicalRoot = Regex.IsMatch(source, @"^\s*Model\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant);
        var booleanRequires = FirmamentConcepts.Requirements(FirmamentSyntaxParser.Parse(source).Document).ToArray();
        var staticDeclaration = Regex.IsMatch(source, @"\b(?:Record|Static|Template\s*(?:<|[A-Za-z_]\w*\s*\()|Pattern\s+\w+\s+Over)\b", RegexOptions.CultureInvariant)
            || symmetry.Mirrors.Count > 0 || symmetry.RadialPatterns.Count > 0 || linear.Patterns.Count > 0 || linear.Axes.Count > 0;
        if (!staticDeclaration && booleanRequires.Length == 0 && !(canonicalRoot && Regex.IsMatch(source, @"\b(?:Require\s+[A-Za-z_]\w*\s*(?:=>|\{)|Pmi\s*\{[\s\S]*?\bFrom\s*:)", RegexOptions.CultureInvariant))) {
            source = points.Math.Normalize(source, diagnostics);
            return diagnostics.Any(d => d.StartsWith(ConceptPointAuthoring.Prefix, StringComparison.Ordinal)) ? null : new(source, null);
        }
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

        var catalog = FirmamentV2TemplateExpansion.ReadDataCatalog(source, diagnostics, scalars);
        foreach (var record in catalog.Types.Values)
        {
            if (record.Fields.Count == 0) diagnostics.Add(Prefix + "record-empty:" + record.Name);
            recordTypes.Add(new(record.Name, record.Fields, record.SourceSpan));
            changes.Add((record.SourceSpan.Start, record.SourceSpan.Length, string.Empty));
            names.Add(record.Name);
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
            var entries = ParseSetEntries(source, open + 1, close, name, type, recordByName, diagnostics, scalars);
            sets.Add(new(name, type, entries, new(header.Index, close - header.Index + 1)));
            changes.Add((header.Index, close - header.Index + 1, string.Empty));
        }
        var setByName = sets.ToDictionary(x => x.Name, StringComparer.Ordinal);

        // Tables share Record typing with Static values, but intentionally preserve their
        // columnar spelling for source inspection. The template binder has already checked
        // cells and lookups before this phase erases the compile-time declaration.
        foreach (var table in catalog.Tables.Values)
        {
            var columns = table.Columns.ToDictionary(c => c.Key, c => (IReadOnlyList<string>)c.Value, StringComparer.Ordinal);
            tables.Add(new(table.Name, table.RowType, table.KeyField, columns, columns.Count == 0 ? 0 : columns.First().Value.Count, table.SourceSpan));
            changes.Add((table.SourceSpan.Start, table.SourceSpan.Length, string.Empty));
        }
        foreach (var record in catalog.Records.Values)
        {
            staticRecords.Add(new(record.Name, record.TypeName, record.Fields, record.SourceSpan));
            changes.Add((record.SourceSpan.Start, record.SourceSpan.Length, string.Empty));
        }

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

        var patternSyntax = FirmamentSyntaxParser.Parse(source);
        foreach (var pattern in FirmamentSyntaxTraversal.Declarations(patternSyntax.Document)
            .Where(d => d.Type.Name.Text == "Pattern" && d.Clauses.Any(c => c.Kind == ClauseKind.Over)))
        {
            var patternName = pattern.Name?.Text ?? "<unnamed>";
            var sourceName = pattern.Clauses.Count == 1 ? FirmamentSyntaxValues.Reference(pattern.Clauses[0].Value) : null;
            if (pattern.Body is null || sourceName is null || pattern.Name is null || pattern.Parameters.Count > 0 || pattern.Annotation is not null
                || pattern.Initializer is not null || pattern.ResultType is not null || pattern.Type.Span.Length > pattern.Type.Name.Span.Length || pattern.Modifiers.Count > 0 || pattern.Occurrence || pattern.HasParameterList)
            { diagnostics.Add(Prefix + "pattern-source-invalid:" + patternName); continue; }
            if (setByName.TryGetValue(sourceName, out var set))
            {
                var parent = FirmamentSyntaxTraversal.Declarations(patternSyntax.Document).FirstOrDefault(d => d.Body?.Statements.Contains(pattern) == true);
                string? target = null;
                if (parent?.Type.Name.Text is "Model" or "Struct")
                {
                    var bodies = parent.Body!.Statements.OfType<DeclarationSyntax>().Where(d => FirmamentV2Parser.IsPrimitiveDeclaration(d) || d.Type.Name.Text is "Compose" or "Extrude").ToArray();
                    if (bodies.Length != 1) { diagnostics.Add(Prefix + "pattern-target-ambiguous:" + patternName + ":Model-scope features require exactly one material body; use Modify target"); continue; }
                    target = bodies[0].Name?.Text;
                }
                ExpandSetPattern(pattern, set, templateByName, changes, patterns, diagnostics, target);
                continue;
            }
            if (!arrayByName.TryGetValue(sourceName, out var array))
            { diagnostics.Add(Prefix + "pattern-source-invalid:" + patternName); continue; }
            var invocation = pattern.Body.Statements.Count == 1 ? TemplateInvocation(pattern.Body.Statements[0]) : null;
            if (invocation is null || !templateByName.TryGetValue(invocation, out var template) || template.Type != array.ElementType)
            { diagnostics.Add(Prefix + "pattern-body-invalid:" + patternName); continue; }
            if (array.Elements.Count > MaxPatternExpansion)
            { diagnostics.Add(Prefix + "pattern-expansion-limit:" + patternName); continue; }
            var generated = new List<string>(); var output = new List<string>();
            for (var index = 0; index < array.Elements.Count; index++)
            {
                var id = patternName + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                var declaration = Instantiate(template, array.Elements[index], id, true, diagnostics);
                if (declaration is not null) { output.Add(declaration); generated.Add(id); }
            }
            patterns.Add(new(patternName, array.Name, template.Name, generated.Count, generated, pattern.Span));
            changes.Add((pattern.Span.Start, pattern.Span.Length, string.Join(Environment.NewLine, output)));
            foreach (var target in FirmamentSyntaxTraversal.Statements(patternSyntax.Document).OfType<FieldSyntax>()
                .Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Target")))
            {
                var id = FirmamentSyntaxWriter.Expression(target.Value);
                if (generated.Contains(id, StringComparer.Ordinal))
                    changes.Add((target.Value.Span.Start, target.Value.Span.Length, id.Replace("[", "_", StringComparison.Ordinal).Replace("]", "", StringComparison.Ordinal)));
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

        foreach (var (require, admittedScope) in booleanRequires)
        {
            var name = require.Name?.Text ?? "<unnamed>";
            if (!admittedScope)
            {
                const string code = "firmament-concept-requirement-scope-unsupported";
                const string message = "Place a Boolean Require in the Model/module or a construction Struct, not inside a primitive or feature field block.";
                diagnostics.Add(Prefix + "require-invalid-scope:" + name);
                diagnostics.Add(code + ": " + message);
                var original = scalars?.OriginalRequirement(name);
                scalars?.AddConceptDiagnostics([new(code, "error", message) { SourceSpan = original, Stage = "Concept binding" }], original: original is not null);
                continue;
            }
            if (require.Initializer is null || require.Name is null || require.Parameters.Count > 0 || require.Annotation is not null
                || require.Modifiers.Count > 0 || require.Type.Arguments.Count > 0 || require.Clauses.Count > 0 || require.ResultType is not null)
            { diagnostics.Add(Prefix + "require-non-bool:" + name); continue; }
            var expression = source.Substring(require.Initializer.Span.Start, require.Initializer.Span.Length);
            var concepts = new FirmamentConcepts(source, scalars);
            var location = scalars?.OriginalRequirement(name);
            var value = concepts.Check(require.Initializer, name, location);
            diagnostics.AddRange(concepts.Diagnostics.Select(d => d.Code + ": " + d.Message));
            scalars?.AddConceptDiagnostics(concepts.Diagnostics, original: location is not null);
            requires.Add(new(name, expression, value, require.Span));
            if (!value) diagnostics.Add(Prefix + "require-failed:" + name + ":" + expression);
            changes.Add((require.Span.Start, require.Span.Length, string.Empty));
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
        source = points.Math.Normalize(source, diagnostics);
        if (diagnostics.Any(d => d.StartsWith(ConceptPointAuthoring.Prefix, StringComparison.Ordinal))) return null;
        return new(source, new(recordTypes, arrays, templates.Select(t => new FirmamentV2CanonicalTemplateDecl(t.Name, t.Type, t.Parameter, t.Body, t.Span)).ToArray(), patterns, requires, semanticConstraints, projections, staticRecords, tables, sets, symmetry.Mirrors, symmetry.RadialPatterns, linear.Patterns, linear.Axes, points.Points));
    }

    private static IReadOnlyList<FirmamentV2StaticSetEntry> ParseSetEntries(string source, int start, int end, string setName, string elementType,
        IReadOnlyDictionary<string, FirmamentV2RecordTypeDecl> recordTypes, List<string> diagnostics, FirmamentCompileTimeValues? scalars = null)
    {
        var parsed = FirmamentSyntaxParser.Parse(source[start..end]);
        var result = new List<FirmamentV2StaticSetEntry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (!parsed.IsSuccess) { diagnostics.Add(Prefix + "set-entry-malformed:" + setName); return result; }
        foreach (var statement in parsed.Document.Statements)
        {
            if (statement is not MappingSyntax mapping)
            { diagnostics.Add(Prefix + "set-entry-malformed:" + setName + ":use-name-arrow-value"); continue; }
            var entryName = mapping.Key.Text;
            if (!names.Add(entryName)) { diagnostics.Add(Prefix + "set-duplicate-entry:" + setName + ":" + entryName); continue; }
            var value = source.Substring(start + mapping.Value.Span.Start, mapping.Value.Span.Length).Trim().TrimEnd(';');
            var span = new FirmamentV2SourceSpan(start + mapping.Span.Start, mapping.Span.Length);
            IReadOnlyDictionary<string, string>? fields = null;
            if (recordTypes.ContainsKey(elementType))
            {
                fields = FirmamentV2TemplateExpansion.BindRecordExpression(source, elementType, value,
                    "__SetValue_" + setName + "_" + entryName, diagnostics);
                if (fields is null) continue;
            }
            else
            {
                var expression = FirmamentSyntaxParser.ParseExpression(value).Expression;
                if (expression is not null && scalars is not null) expression = scalars.NominalExpression(expression);
                if (elementType is "Point2" or "Point3" or "Vector2"
                    && (expression is SequenceSyntax { Kind: SequenceKind.List } || expression is CallSyntax { Target: ReferenceSyntax constructor } && constructor.Name.Text == elementType)
                    && FirmamentSyntaxValues.Vector(expression, elementType == "Vector2" ? "" : "mm", out var coordinates)
                    && coordinates.Length == (elementType == "Point3" ? 3 : 2))
                    value = elementType + "(" + string.Join(", ", coordinates.Select(n => n.ToString("R", CultureInfo.InvariantCulture) + (elementType == "Vector2" ? "" : "mm"))) + ")";
                else if (expression is not null) value = FirmamentSyntaxWriter.Expression(expression);
            }
            if (fields is null && !ValueMatchesType(value, elementType))
            { diagnostics.Add(Prefix + "set-entry-type-mismatch:" + setName + ":" + entryName + ":expected-" + elementType); continue; }
            result.Add(new(entryName, value, result.Count, span, fields));
        }
        return result;
    }

    private static string? TemplateInvocation(StatementSyntax statement)
    {
        var expression = (statement as ExpressionStatementSyntax)?.Value;
        var (target, args) = expression switch
        {
            CallSyntax call => (call.Target as ReferenceSyntax, call.Arguments),
            SpecializationSyntax specialization => (specialization.Target as ReferenceSyntax, specialization.Arguments),
            _ => ((ReferenceSyntax?)null, (IReadOnlyList<ArgumentSyntax>)[])
        };
        return target is not null && args.Count == 1 && args[0].Name is null && args[0].Value is ReferenceSyntax { Name.Text: "Current" }
            ? target.Name.Text : null;
    }

    private static void ExpandSetPattern(DeclarationSyntax pattern, FirmamentV2StaticSetDecl set,
        IReadOnlyDictionary<string, Template> templates, List<(int Start, int Length, string Text)> changes,
        List<FirmamentV2CanonicalPatternDecl> patterns, List<string> diagnostics, string? target = null)
    {
        var patternName = pattern.Name!.Text;
        if (set.Entries.Count > MaxPatternExpansion) { diagnostics.Add(Prefix + "pattern-expansion-limit:" + patternName + ":" + set.Entries.Count); return; }
        if (pattern.Body!.Statements.Count != 1 || pattern.Body.Statements[0] is not MappingSyntax mapping)
        { diagnostics.Add(Prefix + "pattern-body-invalid:" + patternName); return; }
        var output = new List<string>(); var generated = new List<string>(); var associations = new List<FirmamentV2PatternAssociation>();
        var invocation = TemplateInvocation(mapping.Value);
        foreach (var entry in set.Entries)
        {
            var semanticId = patternName + "." + entry.Name;
            var materializedId = patternName + "_" + entry.Name;
            string? text;
            if (invocation is not null && templates.TryGetValue(invocation, out var template) && entry.RecordFields is not null && template.Type == set.ElementType)
                text = Instantiate(template, entry.RecordFields, materializedId, true, diagnostics);
            else
            {
                if (mapping.Value is not DeclarationSyntax declaration || declaration.Body is null || declaration.Name is null
                    || declaration.Type.Name.Text is not ("Hole" or "Slot" or "Boss" or "Pocket" or "EdgeFinish" or "Panel" or "Section"))
                { diagnostics.Add(Prefix + "pattern-body-invalid:" + patternName); return; }
                var substitution = BindSetElement(mapping.Key.Text, entry, diagnostics);
                if (substitution is null) continue;
                var bound = (DeclarationSyntax)substitution.Statement(declaration);
                bound = bound with { Name = bound.Name! with { Text = materializedId } };
                if (bound.Type.Name.Text != "Section")
                {
                    var value = FirmamentSyntaxParser.ParseExpression(semanticId).Expression!;
                    bound = bound with { Body = bound.Body! with { Statements = new StatementSyntax[] { new FieldSyntax(new("PatternIdentity", pattern.Span), value, pattern.Span) }.Concat(bound.Body.Statements).ToArray() } };
                }
                text = FirmamentSyntaxWriter.Statement(bound);
            }
            if (text is null) continue;
            output.Add(text); generated.Add(semanticId);
            associations.Add(new(semanticId, set.Name, entry.Name, entry.Value, entry.SourceOrder, entry.Provenance));
        }
        patterns.Add(new(patternName, set.Name, invocation ?? (mapping.Value as DeclarationSyntax)?.Type.Name.Text ?? "mapping",
            generated.Count, generated, pattern.Span, associations));
        var emitted = string.Join(Environment.NewLine, output);
        changes.Add((pattern.Span.Start, pattern.Span.Length, target is null ? emitted : "Modify " + target + " {\n" + emitted + "\n}"));
    }

    private static bool IsSupportedSetElementType(string type, IReadOnlyDictionary<string, FirmamentV2RecordTypeDecl> records) =>
        type is "Point2" or "Point3" or "Vector2" or "Length" or "Angle" or "Time" or "Int" or "Float" or "Bool" || records.ContainsKey(type);

    private static bool ValueMatchesType(string value, string type)
    {
        value = value.Trim();
        if (type is "Point2" or "Point3" or "Vector2")
        {
            var expression = FirmamentSyntaxParser.ParseExpression(value).Expression;
            return (expression is SequenceSyntax { Kind: SequenceKind.List } || expression is CallSyntax { Target: ReferenceSyntax constructor } && constructor.Name.Text == type)
                && FirmamentSyntaxValues.Vector(expression, type == "Vector2" ? "" : "mm", out var components)
                && components.Length == (type == "Point3" ? 3 : 2);
        }
        return type switch
        {
            "Time" => FirmamentNumericEvaluation.TryEvaluate(value, out var duration) && duration.Unit == "s",
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
        var syntax = FirmamentSyntaxParser.Parse(template.Body);
        if (!syntax.IsSuccess || syntax.Document.Statements.Count != 1 || syntax.Document.Statements[0] is not DeclarationSyntax declaration
            || declaration.Body is null || declaration.Name is null || declaration.Type.Name.Text is not ("Hole" or "Slot" or "Profile" or "StandardPart"))
        { diagnostics.Add(Prefix + "template-output-unsupported:" + template.Name); return null; }
        if (patterned && declaration.Type.Name.Text == "Profile")
        { diagnostics.Add(Prefix + "pattern-output-unsupported:Profile"); return null; }
        var bound = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var parsed = FirmamentSyntaxParser.ParseExpression(value.Value);
            if (parsed.Expression is null || parsed.Diagnostics.Count > 0)
            { diagnostics.Add(Prefix + "template-value-invalid:" + template.Name + ":" + value.Key); return null; }
            bound[template.Parameter + "." + value.Key] = parsed.Expression;
        }
        declaration = (DeclarationSyntax)new FirmamentSyntaxSubstitution(bound).Statement(declaration);
        if (declaration.Type.Name.Text != "Profile") declaration = declaration with
        { Name = declaration.Name! with { Text = id.Replace("[", "_", StringComparison.Ordinal).Replace("]", "", StringComparison.Ordinal) } };
        return FirmamentSyntaxWriter.Statement(declaration);
    }
    private static Dictionary<string, string> Fields(string body) => Regex.Matches(body, "\\b(?<name>[A-Za-z_]\\w*)\\s*:\\s*(?<value>\"[^\"]*\"|(?:Point2|Point3|Vector2|PlusMinus)\\s*\\([^)]*\\)|\\d+\\.\\d+\\.\\d+|\\d{4}-\\d{2}-\\d{2}|[A-Za-z_]\\w*|[-+]?\\d+(?:\\.\\d+)?(?:mm|deg)?)", RegexOptions.CultureInvariant)
        .Cast<Match>().ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value, StringComparer.Ordinal);
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
    private static int MatchPair(string text, int open, char left, char right) { var depth = 0; for (var i = open; i < text.Length; i++) { if (text[i] == left) depth++; else if (text[i] == right && --depth == 0) return i; } return -1; }
}
