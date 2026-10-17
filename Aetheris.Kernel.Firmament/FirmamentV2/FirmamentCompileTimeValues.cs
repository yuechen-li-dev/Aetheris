using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using System.Collections.Immutable;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Per-compilation scalar environment shared by Static, Record, Template and Feature binding.
/// Checked values travel through existing expansion as opaque identifiers, never as stripped
/// nominal strings. The mechanical binder consumes their nominal AST and PMI consumes their IR.
/// This is not an evaluator: all scalar semantics belong to the existing typed dependency binder.
/// </summary>
internal sealed partial class FirmamentCompileTimeValues(string source)
{
    private readonly Dictionary<string, FirmamentV2BoundLet> values = new(StringComparer.Ordinal);
    private readonly HashSet<string> identifiers = FirmamentLanguageAnalysisService.Lex(source)
        .Where(t => t.Shape == "identifier").Select(t => t.Text).ToHashSet(StringComparer.Ordinal);
    private readonly List<FirmamentV2ValidationDiagnostic> diagnostics = [];
    private readonly Dictionary<string, DeclarationSyntax> definitions = new(StringComparer.Ordinal);
    private int nextIdentity;
    private IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes = new Dictionary<string, TemplateRecordTypeIr>();
    private readonly Dictionary<string, TemplateStaticRecordIr> recordValues = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, FirmamentV2SourceSpan> originalScopes = OriginalScopes(source);
    private FirmamentDataBinding? dataBinding;

    internal void UseDataBinding(FirmamentDataBinding binding)
    {
        dataBinding ??= binding;
        if (!binding.IsChanged) return;
        dataBinding = binding;
        source = binding.Source;
        originalSyntax = null;
        originalScopes = OriginalScopes(source).ToDictionary(p => p.Key, p => binding.OriginalSpan(p.Value), StringComparer.Ordinal);
        identifiers.UnionWith(FirmamentLanguageAnalysisService.Lex(source).Where(t => t.Shape == "identifier").Select(t => t.Text));
    }

    internal IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics => diagnostics;
    internal IReadOnlyList<FirmamentV2DataSymbolBinding> DataBindings => dataBinding?.Symbols.Select(s =>
        new FirmamentV2DataSymbolBinding(s.Name, s.Declaration.Type.Name.Text, s.Scope, s.Identity, s.Declaration.Span)).ToArray() ?? [];

    internal void AddExpansionDiagnostics(IEnumerable<FirmamentV2ValidationDiagnostic> reported) => diagnostics.AddRange(reported);

    internal void AddConceptDiagnostics(IEnumerable<FirmamentV2ValidationDiagnostic> reported, bool original = true, string? producerSource = null)
    {
        var producer = producerSource is not null && producerSource != source ? FirmamentSyntaxParser.Parse(producerSource).Document : null;
        foreach (var diagnostic in reported)
        {
            if (producer is null) { diagnostics.Add(diagnostic with { IsOriginalSource = original }); continue; }
            var owner = FirmamentSyntaxTraversal.Declarations(producer).Where(d => d.Name is not null
                    && (d.Type.Name.Text is "Concept" or "Require" || d.Modifiers.Any(m => m.Name.Text == "Template"))
                    && diagnostic.SourceSpan is { } span && d.Span.Start <= span.Start && d.Span.Start + d.Span.Length >= span.Start + span.Length)
                .OrderBy(d => d.Span.Length).FirstOrDefault();
            var anchor = owner is null ? null : OriginalDeclaration(owner.Type.Name.Text, owner.Name!.Text);
            diagnostics.Add(diagnostic with { SourceSpan = anchor, IsOriginalSource = anchor is not null,
                LocationNote = anchor is null ? "Binding failed after expansion; original-source provenance is unavailable."
                    : "Location identifies the original enclosing declaration; the check evaluated expanded values." });
        }
    }

    private DocumentSyntax? originalSyntax;
    internal FirmamentV2SourceSpan? OriginalRequirement(string name) => OriginalDeclaration("Require", name);

    internal FirmamentV2SourceSpan? OriginalDeclaration(string type, string name, string? modifier = null)
    {
        originalSyntax ??= FirmamentSyntaxParser.Parse(source).Document;
        var matches = FirmamentSyntaxTraversal.Declarations(originalSyntax)
            .Where(d => d.Type.Name.Text == type && d.Name?.Text == name && (modifier is null || d.Modifiers.Any(m => m.Name.Text == modifier))).ToArray();
        return matches.Length == 1 ? dataBinding?.OriginalSpan(matches[0].Span) ?? matches[0].Span : null;
    }

    internal void BindExpandedScalars(string expanded, List<string> codes)
    {
        var expandedChoices = new FirmamentChoiceBinding(expanded);
        choices = new(Choices.Types.Values.Concat(expandedChoices.Types.Values).GroupBy(t => t.Name).Select(g => g.Last()));
        var choiceErrorStart = diagnostics.Count;
        foreach (var error in expandedChoices.Diagnostics) ChoiceError(error, codes);
        recordTypes = recordTypes.Concat(FirmamentV2TemplateExpansion.ParseRecordTypes(expanded, codes, templateLocals: true))
            .GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
        InitializeChoices(recordTypes, codes, expanded);
        LocateChoiceErrors(choiceErrorStart, null, "Binding checked specialized Static values; an exact original-source span is unavailable.");
        var declarations = Scope(FirmamentSyntaxParser.Parse(expanded).Document.Statements)
            .Where(d => d.Type.Name.Text == "Static" && d.Name is not null && d.Initializer is not null
                && !definitions.ContainsKey(d.Name.Text)
                && d.Body is null && d.Initializer is not (ObjectSyntax or WithSyntax or IndexSyntax)
                && (d.Annotation is null || IsScalar(FirmamentSyntaxValues.Reference(d.Annotation) ?? ""))).ToArray();
        if (declarations.Length == 0) return;
        var projected = declarations.Select(d => d with { Initializer = ResolveChoices(d.Initializer!, codes,
            expected: FirmamentSyntaxValues.Reference(d.Annotation)) }).ToArray();
        var bound = FirmamentV2Parser.BindCompileTimeScalars(projected, expanded, "inline", codes, values.Values.ToArray());
        foreach (var diagnostic in bound.Diagnostics)
        {
            var original = originalScopes.GetValueOrDefault("Static " + diagnostic.Target);
            diagnostics.Add(diagnostic with { SourceSpan = original, IsOriginalSource = original is not null,
                LocationNote = original is null ? "Expanded scalar binding; original expression location is unavailable." : "Location identifies the original Static declaration." });
        }
        foreach (var value in bound.Values.Where(v => declarations.Any(d => d.Name!.Text == v.Name)))
        {
            var original = originalScopes.GetValueOrDefault("Static " + value.Name);
            values[value.Name] = value with { Origins = original is null ? value.Origins
                : value.Origins.Append(new("inline", original, "Static " + value.Name)).ToArray() };
        }
    }

    internal void Initialize(IReadOnlyDictionary<string, TemplateRecordTypeIr> records, List<string> codes)
    {
        recordTypes = FirmamentV2TemplateExpansion.ParseRecordTypes(source, codes, templateLocals: true);
        var syntax = FirmamentSyntaxParser.Parse(source);
        var declarations = Scope(syntax.Document.Statements).ToArray();
        foreach (var d in declarations.Where(d => d.Type.Name.Text == "Static" && d.Name is not null))
        {
            var type = FirmamentSyntaxValues.Reference(d.Annotation);
            if (d.Initializer is not null && d.Body is null && d.Initializer is not (ObjectSyntax or WithSyntax or IndexSyntax)
                && (d.Annotation is null || type is not null && FirmamentV2Parser.TryParseLetType(type, out _)))
                Add(d.Name!.Text, d);
        }
        var statics = declarations.Where(d => d.Type.Name.Text == "Static" && d.Name is not null).ToArray();
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var recordTypeNames = new Dictionary<string, string>(StringComparer.Ordinal);
        void Record(DeclarationSyntax d, int depth = 0)
        {
            if (depth > 128)
            {
                var code = FirmamentV2TemplateExpansion.Prefix + "record-depth-limit:" + d.Name!.Text;
                codes.Add(code);
                diagnostics.Add(new(code, "error", "Compile-time Record nesting exceeds the supported depth of 128.", Target: d.Name.Text)
                    { SourceSpan = d.Span, IsOriginalSource = true, Stage = "Binding" });
                return;
            }
            if (!visiting.Add(d.Name!.Text)) return; // The Record owner diagnoses derived-record cycles.
            var type = FirmamentSyntaxValues.Reference(d.Annotation);
            BlockSyntax? body = d.Body ?? (d.Initializer as ObjectSyntax)?.Body;
            if (d.Initializer is WithSyntax w && FirmamentSyntaxValues.Reference(w.Target) is { } parent)
            {
                var original = statics.FirstOrDefault(s => s.Name!.Text == parent);
                if (original is not null)
                {
                    Record(original, depth + 1);
                    type ??= recordTypeNames.GetValueOrDefault(parent) ?? FirmamentSyntaxValues.Reference(original.Annotation);
                    foreach (var field in definitions.Where(p => p.Key.StartsWith(parent + ".", StringComparison.Ordinal)).ToArray())
                        Add(d.Name.Text + field.Key[parent.Length..], field.Value);
                }
                body = w.Body;
            }
            if (type is null || !records.TryGetValue(type, out var schema) || body is null) return;
            recordTypeNames[d.Name.Text] = type;
            foreach (var field in body.Statements.OfType<FieldSyntax>())
                if (schema.Fields.TryGetValue(field.Name.Text, out var fieldType)
                    && FirmamentV2Parser.TryParseLetType(fieldType, out _))
                {
                    var name = d.Name.Text + "." + field.Name.Text;
                    if (LegacyWideInteger(fieldType, Slice(field.Value))) definitions.Remove(name);
                    else Add(name, Scalar(name, fieldType, field.Value, field.Span));
                }
                else if (schema.Fields.TryGetValue(field.Name.Text, out fieldType) && records.ContainsKey(fieldType)
                    && FirmamentSyntaxValues.Reference(field.Value) is { } nested)
                {
                    var original = statics.FirstOrDefault(s => s.Name!.Text == nested);
                    if (original is null) continue;
                    Record(original, depth + 1);
                    foreach (var child in definitions.Where(p => p.Key.StartsWith(nested + ".", StringComparison.Ordinal)).ToArray())
                        Add(d.Name.Text + "." + field.Name.Text + child.Key[nested.Length..], child.Value);
                }
        }
        foreach (var d in statics) Record(d);
        InitializeChoices(recordTypes, codes);
        // A legacy collection reader may retain only a literal prefix. Every tol
        // in a Static value must belong to a checked scalar initializer before
        // that reader can erase the containing declaration.
        var admittedTolerances = definitions.Values.Where(d => d.Initializer is not null)
            .SelectMany(d => FirmamentSyntaxTraversal.Expressions(d.Initializer!)).OfType<ToleranceSyntax>()
            .Select(t => t.ToleranceSpan).ToHashSet();
        foreach (var declaration in statics)
            foreach (var tolerance in FirmamentSyntaxTraversal.Expressions(new DocumentSyntax("inline", [declaration], [], declaration.Span))
                .OfType<ToleranceSyntax>().Where(t => !admittedTolerances.Contains(t.ToleranceSpan)))
            {
                codes.Add(FirmamentV2Parser.ToleranceUnsupported);
                diagnostics.Add(new(FirmamentV2Parser.ToleranceUnsupported, "error",
                    $"Tolerance in Static '{declaration.Name!.Text}' is not admitted by its collection or value owner. Declare a scalar Static or typed Record requirement; collection/Table tolerance propagation is not yet supported.", Target: declaration.Name.Text)
                    { SourceSpan = tolerance.ToleranceSpan, IsOriginalSource = true, Stage = "Binding" });
            }
        if (definitions.Count == 0) return;
        var bound = FirmamentV2Parser.BindCompileTimeScalars(definitions.Select(p => p.Value with
        { Name = new(p.Key, p.Value.Name!.Span), Initializer = ResolveChoices(p.Value.Initializer!, codes,
            expected: FirmamentSyntaxValues.Reference(p.Value.Annotation)) }).ToArray(), source, "inline", codes);
        diagnostics.AddRange(bound.Diagnostics.Select(d => d with { IsOriginalSource = true,
            SourceSpan = d.SourceSpan is { } span ? dataBinding?.OriginalSpan(span) ?? span : null }));
        foreach (var value in bound.Values)
        {
            var definition = definitions[value.Name];
            var origins = new List<FirmamentV2ValueOrigin>();
            foreach (var dependency in value.Dependencies ?? new HashSet<string>())
                if (definitions.TryGetValue(dependency, out var origin)) origins.Add(new("inline", origin.Initializer?.Span ?? origin.Span, dependency));
            origins.Add(new("inline", definition.Initializer?.Span ?? definition.Span, definition.Name!.Text));
            if (definition.Name.Text != value.Name) origins.Add(new("inline", definition.Span, value.Name));
            if (definition.Initializer is ToleranceSyntax)
                origins = origins.OrderByDescending(o => o.Binding == definition.Name.Text).ToList();
            values[value.Name] = value with { Origins = origins.Distinct().Select(o => dataBinding is null ? o
                : o with { SourceSpan = dataBinding.OriginalSpan(o.SourceSpan), Binding = dataBinding.DisplayName(o.Binding) }).ToArray() };
        }
        void Add(string name, DeclarationSyntax declaration) => definitions[name] = declaration;
    }

    internal bool IsScalar(string type) => FirmamentV2Parser.TryParseLetType(type, out _);

    internal bool TryTransfer(string expression, string type, string binding, FirmamentV2SourceSpan span,
        List<string> codes, out string transported)
    {
        var scopeName = binding.Split('.')[0];
        var originalConsumer = originalScopes.TryGetValue(scopeName, out var originalSpan);
        if (originalConsumer) span = originalSpan!;
        transported = expression;
        if (!FirmamentV2Parser.TryParseLetType(type, out _)) return false;
        // Existing Template/Record Int literals admit Int64, whereas Model Static's
        // graph currently uses Int32. Do not narrow unrelated admitted literal data.
        if (LegacyWideInteger(type, expression)) return false;
        FirmamentV2BoundLet? value;
        if (!values.TryGetValue(expression.Trim(), out value))
        {
            var parsed = FirmamentSyntaxParser.ParseExpression(expression);
            if (parsed.Expression is null || parsed.Diagnostics.Any(d => d.Severity is "error" or "fatal")) return false;
            var name = FreshName();
            var localCodes = new List<string>();
            var resolved = ResolveChoices(parsed.Expression, codes, expected: type);
            var bound = FirmamentV2Parser.BindCompileTimeScalars([Scalar(name, type, resolved, parsed.Expression.Span)],
                expression, "inline", localCodes, values.Values.ToArray());
            codes.AddRange(localCodes);
            diagnostics.AddRange(bound.Diagnostics.Select(d => d with { SourceSpan = originalConsumer ? span : null, IsOriginalSource = originalConsumer,
                Target = binding, LocationNote = originalConsumer ? "Compile-time argument binding; location identifies the original enclosing declaration."
                    : "Expanded compile-time argument; original consumer location is unavailable." }));
            value = bound.Values.FirstOrDefault(v => v.Name == name);
            if (value is not null && parsed.Expression is ToleranceSyntax && value.Tolerance is { } freshTolerance)
            {
                var originSpan = originalConsumer ? span : new FirmamentV2SourceSpan(0, source.Length);
                value = value with { Tolerance = freshTolerance with { SourceSpan = originSpan },
                    Origins = new[] { new FirmamentV2ValueOrigin("inline", originSpan, binding,
                        originalConsumer ? "Location identifies the enclosing declaration of the new requirement." : "Original consumer location is unavailable; location identifies the source module.") }.Concat(value.Origins).ToArray() };
            }
            if (value is not null && value.Origins.Count == 0)
                value = value with { SourceSpan = originalConsumer ? span : new(0, source.Length),
                    Origins = [new("inline", originalConsumer ? span : new(0, source.Length), binding,
                        originalConsumer ? "Location identifies the enclosing declaration." : "Original consumer location is unavailable; location identifies the source module.")],
                    Tolerance = value.Tolerance is { } tolerance ? tolerance with { SourceSpan = originalConsumer ? span : new(0, source.Length) } : null };
        }
        if (value is null || !FirmamentV2Parser.TryParseLetType(type, out var expected)) return false;
        if (value.Type != expected)
        {
            TypeError(binding, type, value.Type.ToString(), codes);
            return false;
        }
        // Ordinary literal specialization keeps its established spelling and geometry identities.
        if (value.Tolerance is null)
        {
            transported = IsLiteral(expression) ? expression : Nominal(value);
            return true;
        }
        var identity = FreshName();
        values[identity] = value with { Name = identity,
            Origins = value.Origins.LastOrDefault()?.Binding == binding ? value.Origins
                : value.Origins.Append(new("inline", originalConsumer ? span : value.Origins.FirstOrDefault()?.SourceSpan ?? new(0, source.Length), binding,
                    originalConsumer ? "Location identifies the enclosing declaration." : "Original consumer location is unavailable; location identifies the requirement or source module.")).ToArray() };
        transported = identity;
        return true;
    }

    internal string Field(string record, string field, string expression, string type, FirmamentV2SourceSpan span, List<string> codes)
    {
        if (IsChoice(type)) return TryChoiceTransfer(expression, type, codes, out var tag) ? tag : expression;
        if (!IsScalar(type)) return expression;
        if (values.TryGetValue(record + "." + field, out var value) && value.Tolerance is null && IsLiteral(expression)) return expression;
        return TryTransfer(values.ContainsKey(record + "." + field) ? record + "." + field : expression,
            type, record + "." + field, span, codes, out var result) ? result : expression;
    }

    internal IReadOnlyList<FirmamentV2BoundLet> MaterialInputs(string expanded)
    {
        var names = FirmamentLanguageAnalysisService.Lex(expanded).Select(t => t.Text).ToHashSet(StringComparer.Ordinal);
        foreach (var member in FirmamentSyntaxTraversal.Expressions(FirmamentSyntaxParser.Parse(expanded).Document).OfType<MemberSyntax>())
            if (FirmamentSyntaxValues.Reference(member) is { } name) names.Add(name);
        return values.Values.Where(v => names.Contains(v.Name)).ToArray();
    }

    internal void SetRecords(IReadOnlyDictionary<string, TemplateRecordTypeIr> types, IReadOnlyDictionary<string, TemplateStaticRecordIr> records)
    {
        recordTypes = recordTypes.Concat(types).GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
        foreach (var record in records) recordValues[record.Key] = record.Value;
        foreach (var record in records.Values)
            foreach (var field in FirmamentV2TemplateExpansion.FlattenRecordFields(record, types, recordValues, []))
                if (RecordFieldType(record.TypeName, field.Key) is { } type && IsChoice(type))
                    choiceInputs[record.Name + "." + field.Key] = new(type, FirmamentSyntaxParser.ParseExpression(field.Value).Expression!);
        // Table rows and aliases may not have authored scalar field declarations.
        // Admit their leaves through the same typed graph used by literal Records.
        foreach (var record in records.Values)
        {
            if (!types.ContainsKey(record.TypeName)) continue;
            var flattenCodes = new List<string>();
            var leaves = FirmamentV2TemplateExpansion.FlattenRecordFields(record, types, recordValues, flattenCodes);
            foreach (var field in leaves)
            {
                var name = record.Name + "." + field.Key;
                var type = RecordFieldType(record.TypeName, field.Key);
                if (values.ContainsKey(name) || type is null || !IsScalar(type)) continue;
                if (values.TryGetValue(field.Value, out var existing)) { values[name] = existing with { Name = name }; continue; }
                var parsed = FirmamentSyntaxParser.ParseExpression(field.Value);
                if (parsed.Expression is not { } expression || parsed.Diagnostics.Any(d => d.Severity is "error" or "fatal") || LegacyWideInteger(type, field.Value)) continue;
                var codes = new List<string>();
                var bound = FirmamentV2Parser.BindCompileTimeScalars([Scalar(name, type, expression, expression.Span)],
                    field.Value, "inline", codes, values.Values.ToArray());
                var span = OriginalDeclaration("Static", record.Name);
                diagnostics.AddRange(bound.Diagnostics.Select(d => d with { SourceSpan = span, IsOriginalSource = span is not null,
                    LocationNote = "Location identifies the enclosing Static value selected from Record/Table data." }));
                if (bound.Values.FirstOrDefault(v => v.Name == name) is { } value)
                    values[name] = value with { SourceSpan = span ?? record.SourceSpan,
                        Origins = [new("inline", span ?? record.SourceSpan, DisplayDataName(name), record.Provenance)] };
            }
        }
    }

    internal string DisplayDataName(string name)
    {
        var dot = name.IndexOf('.');
        return dot < 0 ? dataBinding?.DisplayName(name) ?? name
            : (dataBinding?.DisplayName(name[..dot]) ?? name[..dot]) + name[dot..];
    }

    internal string? RecordFieldType(string type, string path)
    {
        string? fieldType = null;
        foreach (var part in path.Split('.'))
        {
            if (!recordTypes.TryGetValue(type, out var schema) || !schema.Fields.TryGetValue(part, out fieldType)) return null;
            type = fieldType;
        }
        return fieldType;
    }

    internal bool IsRecordType(string type) => recordTypes.ContainsKey(type);
    internal bool TryRecord(string reference, out TemplateStaticRecordIr record) => recordValues.TryGetValue(reference, out record!);

    internal string RecordReference(TemplateStaticRecordIr record, ImmutableDictionary<string, string> fields)
    {
        if (!fields.Values.Any(field => values.TryGetValue(field, out var value) && value.Tolerance is not null)) return record.Name;
        var name = FreshName();
        recordValues[name] = record with { Name = name, Fields = fields, AuthoredName = record.AuthoredName ?? record.Name };
        return name;
    }

    internal bool TryRecordTransfer(string expression, string type, string binding, FirmamentV2SourceSpan span,
        List<string> codes, out IReadOnlyDictionary<string, string> fields)
    {
        fields = new Dictionary<string, string>();
        if (!recordValues.TryGetValue(expression, out var record) || record.TypeName != type)
        {
            TypeError(binding, type, record?.TypeName ?? expression, codes);
            return false;
        }
        var before = codes.Count;
        var result = FirmamentV2TemplateExpansion.FlattenRecordFields(record, recordTypes, recordValues, codes).ToBuilder();
        if (codes.Count > before)
        {
            foreach (var code in codes.Skip(before).Distinct()) RecordError(record.AuthoredName ?? record.Name, "", code,
                $"Record '{record.AuthoredName ?? record.Name}' contains a cyclic reference or exceeds the supported nesting depth of 128. Use finite, acyclic Record data.");
            return false;
        }
        foreach (var field in result.ToArray())
        {
            var fieldType = RecordFieldType(type, field.Key);
            if (fieldType is not null && IsScalar(fieldType)
                && TryTransfer(field.Value, fieldType, binding + "." + field.Key, span, codes, out var transported))
                result[field.Key] = transported;
        }
        fields = result.ToImmutable();
        return true;
    }

    internal bool Contains(string expression) => values.ContainsKey(expression.Trim());
    internal bool TryValue(string expression, out FirmamentV2BoundLet value) => values.TryGetValue(expression.Trim(), out value!);

    internal void RecordError(string record, string field, string code, string message)
    {
        var span = definitions.GetValueOrDefault(record + "." + field)?.Span
            ?? originalScopes.GetValueOrDefault("Static " + record);
        diagnostics.Add(new(code, "error", message, field, record) { SourceSpan = span,
            IsOriginalSource = span is not null, Stage = "Binding" });
    }

    private void TypeError(string binding, string expected, string actual, List<string> codes)
    {
        var original = originalScopes.GetValueOrDefault(binding.Split('.')[0]);
        codes.Add(FirmamentV2Parser.ExpressionTypeMismatch);
        diagnostics.Add(new(FirmamentV2Parser.ExpressionTypeMismatch, "error",
            $"Compile-time input '{binding}' requires {expected}; supplied value has type {actual}.", Target: binding)
            { SourceSpan = original, IsOriginalSource = original is not null, Stage = "Binding",
                LocationNote = original is null ? "Expanded input binding; original consumer location is unavailable."
                    : "Location identifies the original parameter's enclosing declaration." });
    }
    internal bool HasType(string expression, string type) => values.TryGetValue(expression.Trim(), out var value)
        && FirmamentV2Parser.TryParseLetType(type, out var expected) && value.Type == expected;
    internal string NominalExpression(string expression) => values.TryGetValue(expression.Trim(), out var value) ? Nominal(value)
        : recordValues.TryGetValue(expression.Trim(), out var record) ? record.AuthoredName ?? record.Name : expression;

    internal ExpressionSyntax NominalExpression(ExpressionSyntax expression)
    {
        var replacements = FirmamentSyntaxTraversal.Expressions(expression)
            .Select(FirmamentSyntaxValues.Reference).OfType<string>().Distinct(StringComparer.Ordinal)
            .Where(values.ContainsKey).ToDictionary(name => name,
                name => FirmamentSyntaxParser.ParseExpression(NominalExpression(name)).Expression!, StringComparer.Ordinal);
        return new FirmamentSyntaxSubstitution(replacements).Expression(expression);
    }

    private static bool IsLiteral(string expression) => FirmamentSyntaxParser.ParseExpression(expression).Expression is
        LiteralSyntax or QuantitySyntax or UnarySyntax { Operand: LiteralSyntax or QuantitySyntax };

    private string Slice(ExpressionSyntax expression) => source.Substring(expression.Span.Start, expression.Span.Length);
    private static bool LegacyWideInteger(string type, string expression) => type is "Int" or "int"
        && long.TryParse(expression, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
        && (value < int.MinValue || value > int.MaxValue);

    private string FreshName()
    {
        string name;
        do name = "__firmamentValue" + nextIdentity++; while (!identifiers.Add(name));
        return name;
    }

    private static string Nominal(FirmamentV2BoundLet value) => value.Type switch
    {
        FirmamentV2PrimitiveType.Bool => (bool)value.Value.Value ? "true" : "false",
        FirmamentV2PrimitiveType.String => System.Text.Json.JsonSerializer.Serialize(value.Value.Value),
        _ => value.Value.NumericValue!.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + value.Value.Unit
    };

    private static DeclarationSyntax Scalar(string name, string type, ExpressionSyntax expression, FirmamentV2SourceSpan span) =>
        new(new(new("Static", span), [], span), new(name, span), [], [], new ReferenceSyntax(new(type, span), span),
            null, [], expression, null, false, span);

    private static IEnumerable<DeclarationSyntax> Scope(IEnumerable<StatementSyntax> statements)
    {
        foreach (var d in statements.OfType<DeclarationSyntax>())
        {
            if (d.Modifiers.Any(m => m.Name.Text is "Template" or "Concept") || d.Type.Name.Text is "Feature" or "Template") continue;
            yield return d;
            if (d.Type.Name.Text is not ("Record" or "Static" or "Enum" or "Table") && d.Body is not null)
                foreach (var child in Scope(d.Body.Statements)) yield return child;
        }
    }

    private static IReadOnlyDictionary<string, FirmamentV2SourceSpan> OriginalScopes(string authored)
    {
        var result = new Dictionary<string, FirmamentV2SourceSpan>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        void Add(string name, FirmamentV2SourceSpan span)
        {
            if (ambiguous.Contains(name)) return;
            if (!result.TryAdd(name, span)) { result.Remove(name); ambiguous.Add(name); }
        }
        void Visit(IEnumerable<StatementSyntax> statements)
        {
            foreach (var d in statements.OfType<DeclarationSyntax>())
            {
                if (d.Type.Name.Text == "Feature" && d.Name is not null) Add("Feature " + d.Name.Text, d.Span);
                if (d.Modifiers.Any(m => m.Name.Text == "Template") && d.Name is not null) Add("Template " + d.Name.Text, d.Span);
                if (d.Type.Name.Text == "Static" && d.Name is not null) Add("Static " + d.Name.Text, d.Span);
                if (d.Body is not null) Visit(d.Body.Statements);
            }
        }
        Visit(FirmamentSyntaxParser.Parse(authored).Document.Statements);
        return result;
    }
}
