using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

// Immutable parser/binder IR. Source text is payload for the already-established declaration parser;
// it is never an authority for parameter meaning after TemplateIrParser has built these nodes.
internal sealed record TemplateDeclarationIr(string Name, string TargetKind, string HeaderTail, ImmutableArray<TemplateParameterIr> Parameters, string Body, FirmamentV2SourceSpan SourceSpan);
internal abstract record TemplateParameterIr(string Name, FirmamentV2SourceSpan SourceSpan);
internal sealed record TemplateTypeParameterIr(string Name, string ConstraintConcept, FirmamentV2SourceSpan SourceSpan) : TemplateParameterIr(Name, SourceSpan);
internal sealed record TemplateValueParameterIr(string Name, string TypeName, string? DefaultExpression, FirmamentV2SourceSpan SourceSpan) : TemplateParameterIr(Name, SourceSpan);
internal sealed record TemplateApplicationIr(string TargetKind, string InstanceName, string TemplateName, ImmutableArray<TemplateArgumentIr> Arguments, FirmamentV2SourceSpan SourceSpan);
internal sealed record TemplateArgumentIr(string Name, string Expression, FirmamentV2SourceSpan SourceSpan);
internal sealed record TemplateRecordTypeIr(string Name, ImmutableDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan);
internal sealed record TemplateStaticRecordIr(string Name, string TypeName, ImmutableDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan);
internal sealed record BoundTemplateRecordIr(string Parameter, string TypeName, string StaticName, ImmutableDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan);
internal sealed record BoundTemplateArguments(ImmutableDictionary<string, string> TypeArguments, ImmutableDictionary<string, string> ValueArguments, ImmutableDictionary<string, BoundTemplateRecordIr> RecordArguments, ImmutableArray<string> DefaultedArguments);
internal sealed record TemplateSpecializationIr(TemplateDeclarationIr Template, TemplateApplicationIr Application, BoundTemplateArguments Arguments, string SpecializationIdentity, ImmutableArray<string> GeneratedDeclarationPaths);

/// <summary>Authoritative, finite template specialization phase. It produces concrete declarations only.</summary>
internal static class FirmamentV2TemplateExpansion
{
    internal const string Prefix = "firmament-template-";
    internal const string DuplicateName = Prefix + "duplicate-name";
    internal const string DuplicateParameter = Prefix + "duplicate-parameter";
    internal const string InvalidParameter = Prefix + "invalid-parameter-name";
    internal const string MissingArgument = Prefix + "missing-required-argument";
    internal const string UnknownArgument = Prefix + "unknown-argument";
    internal const string DuplicateArgument = Prefix + "duplicate-argument";
    internal const string TypeMismatch = Prefix + "value-argument-type-mismatch";
    internal const string BadDefault = Prefix + "default-value-type-mismatch";
    internal const string DefaultCycle = Prefix + "default-dependency-cycle";
    internal const string UnknownConstraint = Prefix + "unknown-concept-constraint";
    internal const string ConstraintFailure = Prefix + "type-argument-does-not-satisfy-concept";
    internal const string RequireFailed = Prefix + "require-failed";
    internal const string RequireNonBool = Prefix + "require-non-bool";
    internal const string Recursive = Prefix + "recursive-specialization";
    internal const string ApplicationCycle = Prefix + "application-cycle";
    internal const string UnknownRecordType = Prefix + "unknown-record-parameter-type";
    internal const string UnknownStaticRecord = Prefix + "unknown-static-record-value";
    internal const string WrongRecordType = Prefix + "record-argument-type-mismatch";
    internal const string RecordCollectionMismatch = Prefix + "record-collection-scalar-mismatch";
    internal const string UnknownRecordMember = Prefix + "unknown-record-member";
    internal const string MaterializedRecordArgument = Prefix + "materialized-value-not-compile-time-record";
    internal const string UnsupportedRecordValue = Prefix + "unsupported-record-value-form";

    internal sealed record Result(string Source, IReadOnlyList<ConceptIrTemplateInstantiation> Instantiations);

    public static Result? Expand(string source, List<string> diagnostics)
    {
        var declarations = ParseDeclarations(source, diagnostics);
        if (declarations.Length == 0) return new(source, []);
        var byName = declarations.ToDictionary(d => d.Name, StringComparer.Ordinal);
        DetectTemplateCycles(declarations, byName, diagnostics);
        var applications = ParseApplications(source, byName, diagnostics);
        if (HasErrors(diagnostics)) return null;

        var enums = ParseEnums(source);
        var recordTypes = ParseRecordTypes(source, diagnostics);
        var staticRecords = ParseStaticRecords(source, recordTypes, enums, diagnostics);
        var changes = declarations.Select(d => (d.SourceSpan.Start, d.SourceSpan.Length, Text: string.Empty)).ToList();
        var instantiations = new List<ConceptIrTemplateInstantiation>();
        foreach (var application in applications)
        {
            var template = byName[application.TemplateName];
            if (!string.Equals(application.TargetKind, template.TargetKind, StringComparison.Ordinal)) { diagnostics.Add(FirmamentV2Parser.UnsupportedConstruct); continue; }
            var bound = Bind(template, application, source, enums, recordTypes, staticRecords, diagnostics);
            if (bound is null) continue;
            var specialization = new TemplateSpecializationIr(template, application, bound, Identity(template, application, bound), GeneratedPaths(template.Body, application.InstanceName));
            var body = ResolveTemplateMatches(template.Body, bound, diagnostics, out var selectedMatches);
            if (body is null) continue;
            if (!ValidateRecordMembers(body, bound, diagnostics)) continue;
            body = Substitute(body, bound);
            if (!EvaluateRequires(body, application.InstanceName, diagnostics, out var requireResults)) continue;
            body = RemoveRequires(body);
            changes.Add((application.SourceSpan.Start, application.SourceSpan.Length, $"{template.TargetKind} {application.InstanceName}{template.HeaderTail} {{{body}}}"));
            var recordArguments = bound.RecordArguments.ToDictionary(pair => pair.Key, pair => new ConceptIrTemplateRecordArgument(
                pair.Value.TypeName, pair.Value.StaticName, pair.Value.Fields, pair.Value.SourceSpan, "StaticRecord"), StringComparer.Ordinal);
            instantiations.Add(new(template.Name, application.InstanceName, bound.TypeArguments, bound.ValueArguments, bound.DefaultedArguments,
                specialization.SpecializationIdentity, specialization.GeneratedDeclarationPaths, template.SourceSpan, application.SourceSpan,
                SelectedMatchArms: selectedMatches, RecordArguments: recordArguments, RequireResults: requireResults));
        }
        if (HasErrors(diagnostics)) return null;
        foreach (var change in changes.OrderByDescending(c => c.Start)) source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return new(source, instantiations);
    }

    private static ImmutableArray<TemplateDeclarationIr> ParseDeclarations(string source, List<string> diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<TemplateDeclarationIr>(); var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match start in Regex.Matches(source, @"\bTemplate\s*<", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('<', start.Index); var close = Matching(source, open, '<', '>');
            var header = close < 0 ? Match.Empty : Regex.Match(source[(close + 1)..], @"^\s*(?<kind>Concept\s+Struct|Struct|Model)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<tail>\s*:\s*[A-Za-z_][A-Za-z0-9_]*)?\s*\{", RegexOptions.CultureInvariant);
            if (close < 0 || !header.Success) { diagnostics.Add(FirmamentV2Parser.UnsupportedConstruct); continue; }
            var brace = close + 1 + header.Index + header.Value.LastIndexOf('{'); var end = Matching(source, brace, '{', '}');
            if (end < 0) { diagnostics.Add(FirmamentV2Parser.UnsupportedConstruct); continue; }
            var name = header.Groups["name"].Value;
            if (!names.Add(name)) { diagnostics.Add(DuplicateName + ":" + name); continue; }
            var parameters = ParseParameters(source[(open + 1)..close], open + 1, diagnostics);
            result.Add(new(name, header.Groups["kind"].Value, header.Groups["tail"].Value, parameters, source[(brace + 1)..end], new(start.Index, end - start.Index + 1)));
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<TemplateParameterIr> ParseParameters(string text, int offset, List<string> diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<TemplateParameterIr>(); var names = new HashSet<string>(StringComparer.Ordinal);
        var cursor = 0;
        foreach (var raw in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var span = new FirmamentV2SourceSpan(offset + cursor, raw.Length); cursor += raw.Length + 1;
            var type = Regex.Match(raw, @"^type\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+satisfies\s+(?<concept>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant);
            var value = Regex.Match(raw, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)(?:\s*=\s*(?<default>.+))?$", RegexOptions.CultureInvariant);
            if (!type.Success && !value.Success) { diagnostics.Add(InvalidParameter + ":" + raw); continue; }
            var name = type.Success ? type.Groups["name"].Value : value.Groups["name"].Value;
            if (!names.Add(name)) { diagnostics.Add(DuplicateParameter + ":" + name); continue; }
            result.Add(type.Success ? new TemplateTypeParameterIr(name, type.Groups["concept"].Value, span) : new TemplateValueParameterIr(name, value.Groups["type"].Value, value.Groups["default"].Success ? value.Groups["default"].Value.Trim() : null, span));
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<TemplateApplicationIr> ParseApplications(string source, IReadOnlyDictionary<string, TemplateDeclarationIr> templates, List<string> diagnostics)
    {
        var result = ImmutableArray.CreateBuilder<TemplateApplicationIr>();
        foreach (Match start in Regex.Matches(source, @"\b(?<kind>Concept\s+Struct|Struct|Model)\s+(?<instance>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<template>[A-Za-z_][A-Za-z0-9_]*)\s*<", RegexOptions.CultureInvariant))
        {
            if (!templates.ContainsKey(start.Groups["template"].Value)) continue;
            var open = source.IndexOf('<', start.Index); var close = Matching(source, open, '<', '>');
            if (close < 0) { diagnostics.Add(MissingArgument); continue; }
            var args = ImmutableArray.CreateBuilder<TemplateArgumentIr>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in source[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var parsed = Regex.Match(raw, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.+)$", RegexOptions.CultureInvariant);
                if (!parsed.Success) { diagnostics.Add(UnknownArgument + ":" + raw); continue; }
                var name = parsed.Groups["name"].Value;
                if (!seen.Add(name)) { diagnostics.Add(DuplicateArgument + ":" + name); continue; }
                args.Add(new(name, parsed.Groups["value"].Value.Trim(), new(start.Index, close - start.Index + 1)));
            }
            result.Add(new(start.Groups["kind"].Value, start.Groups["instance"].Value, start.Groups["template"].Value, args.ToImmutable(), new(start.Index, close - start.Index + 1)));
        }
        return result.ToImmutable();
    }

    private static BoundTemplateArguments? Bind(TemplateDeclarationIr template, TemplateApplicationIr application, string source, IReadOnlyDictionary<string, ImmutableHashSet<string>> enums,
        IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes, IReadOnlyDictionary<string, TemplateStaticRecordIr> staticRecords, List<string> diagnostics)
    {
        var supplied = application.Arguments.ToDictionary(a => a.Name, a => a.Expression, StringComparer.Ordinal);
        foreach (var unknown in supplied.Keys.Where(name => template.Parameters.All(p => p.Name != name))) diagnostics.Add(UnknownArgument + ":" + unknown);
        var types = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal); var values = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var records = ImmutableDictionary.CreateBuilder<string, BoundTemplateRecordIr>(StringComparer.Ordinal); var defaults = ImmutableArray.CreateBuilder<string>();
        var resolving = new List<string>();
        string? Resolve(TemplateValueParameterIr p)
        {
            if (values.TryGetValue(p.Name, out var existing)) return existing;
            if (resolving.Contains(p.Name, StringComparer.Ordinal)) { diagnostics.Add(DefaultCycle + ":" + string.Join(" -> ", resolving.Append(p.Name))); return null; }
            var expression = supplied.TryGetValue(p.Name, out var suppliedValue) ? suppliedValue : p.DefaultExpression;
            if (expression is null) { diagnostics.Add(MissingArgument + ":" + p.Name); return null; }
            resolving.Add(p.Name);
            var referenced = template.Parameters.OfType<TemplateValueParameterIr>().SingleOrDefault(x => x.Name == expression);
            var value = referenced is null ? expression : Resolve(referenced);
            resolving.RemoveAt(resolving.Count - 1);
            if (value is null) return null;
            if (!TypeMatches(value, p.TypeName, enums)) { diagnostics.Add((supplied.ContainsKey(p.Name) ? TypeMismatch : BadDefault) + $":{p.Name}:expected-{p.TypeName}:actual-{value}"); return null; }
            values[p.Name] = value;
            if (!supplied.ContainsKey(p.Name)) defaults.Add(p.Name);
            return value;
        }
        foreach (var parameter in template.Parameters)
        {
            if (parameter is TemplateTypeParameterIr type)
            {
                if (!supplied.TryGetValue(type.Name, out var value)) { diagnostics.Add(MissingArgument + ":" + type.Name); continue; }
                if (!ConceptExists(type.ConstraintConcept, source)) diagnostics.Add(UnknownConstraint + ":" + type.ConstraintConcept);
                else if (!Satisfies(value, type.ConstraintConcept, source)) diagnostics.Add(ConstraintFailure + $":{type.Name}:{value}:{type.ConstraintConcept}");
                types[type.Name] = value;
            }
            else
            {
                var valueParameter = (TemplateValueParameterIr)parameter;
                if (recordTypes.TryGetValue(valueParameter.TypeName, out var recordType)) BindRecord(valueParameter, recordType);
                else if (IsBuiltInValueType(valueParameter.TypeName, enums)) _ = Resolve(valueParameter);
                else diagnostics.Add(UnknownRecordType + $":{valueParameter.Name}:{valueParameter.TypeName}");
            }
        }
        return HasErrors(diagnostics) ? null : new(types.ToImmutable(), values.ToImmutable(), records.ToImmutable(), defaults.ToImmutable());

        void BindRecord(TemplateValueParameterIr parameter, TemplateRecordTypeIr recordType)
        {
            var expression = supplied.TryGetValue(parameter.Name, out var suppliedValue) ? suppliedValue : parameter.DefaultExpression;
            if (expression is null) { diagnostics.Add(MissingArgument + ":" + parameter.Name); return; }
            if (!supplied.ContainsKey(parameter.Name)) defaults.Add(parameter.Name);
            if (Regex.IsMatch(source, $@"\bStatic\s+{Regex.Escape(expression)}\s*:\s*{Regex.Escape(recordType.Name)}\s*\[", RegexOptions.CultureInvariant))
            { diagnostics.Add(RecordCollectionMismatch + $":{parameter.Name}:expected-{recordType.Name}:actual-collection"); return; }
            if (staticRecords.TryGetValue(expression, out var staticRecord))
            {
                if (!string.Equals(staticRecord.TypeName, recordType.Name, StringComparison.Ordinal))
                { diagnostics.Add(WrongRecordType + $":{parameter.Name}:expected-{recordType.Name}:actual-{staticRecord.TypeName}"); return; }
                records[parameter.Name] = new(parameter.Name, recordType.Name, staticRecord.Name, staticRecord.Fields, staticRecord.SourceSpan);
                values[parameter.Name] = staticRecord.Name;
                return;
            }
            if (Regex.IsMatch(source, $@"\b(?:Struct|Model)\s+{Regex.Escape(expression)}\b", RegexOptions.CultureInvariant))
            { diagnostics.Add(MaterializedRecordArgument + $":{parameter.Name}:{expression}"); return; }
            if (Regex.IsMatch(expression, @"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant)) diagnostics.Add(UnknownStaticRecord + $":{parameter.Name}:{expression}");
            else diagnostics.Add(UnsupportedRecordValue + $":{parameter.Name}:{expression}");
        }
    }

    private static IReadOnlyDictionary<string, ImmutableHashSet<string>> ParseEnums(string source) => Regex.Matches(source, @"\bEnum\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant).Cast<Match>().ToDictionary(
        m => m.Groups["name"].Value,
        m => Regex.Matches(m.Groups["body"].Value, @"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant).Select(v => v.Value).ToImmutableHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    private static bool TypeMatches(string value, string type, IReadOnlyDictionary<string, ImmutableHashSet<string>> enums) => type switch
    {
        "Length" => Regex.IsMatch(value, @"^[-+]?[0-9]+(?:\.[0-9]+)?mm$", RegexOptions.CultureInvariant),
        "Angle" => Regex.IsMatch(value, @"^[-+]?[0-9]+(?:\.[0-9]+)?deg$", RegexOptions.CultureInvariant),
        "String" => Regex.IsMatch(value, "^\"[^\"]*\"$", RegexOptions.CultureInvariant),
        "int" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _), "float" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _), "bool" => value is "true" or "false",
        _ when enums.TryGetValue(type, out var variants) => variants.Contains(value),
        _ => false
    };
    private static bool IsBuiltInValueType(string type, IReadOnlyDictionary<string, ImmutableHashSet<string>> enums) =>
        type is "Length" or "Angle" or "String" or "int" or "float" or "bool" || enums.ContainsKey(type);

    private static IReadOnlyDictionary<string, TemplateRecordTypeIr> ParseRecordTypes(string source, List<string> diagnostics)
    {
        var result = new Dictionary<string, TemplateRecordTypeIr>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(source, @"\bRecord\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open, '{', '}');
            if (close < 0) continue;
            var fields = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (Match field in Regex.Matches(source[(open + 1)..close], @"\b(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)", RegexOptions.CultureInvariant))
                fields[field.Groups["name"].Value] = field.Groups["type"].Value;
            result[header.Groups["name"].Value] = new(header.Groups["name"].Value, fields.ToImmutable(), new(header.Index, close - header.Index + 1));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, TemplateStaticRecordIr> ParseStaticRecords(string source, IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes,
        IReadOnlyDictionary<string, ImmutableHashSet<string>> enums, List<string> diagnostics)
    {
        var result = new Dictionary<string, TemplateStaticRecordIr>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*=\s*(?<literal>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var name = header.Groups["name"].Value; var typeName = header.Groups["type"].Value; var literalType = header.Groups["literal"].Value;
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open, '{', '}');
            if (close < 0 || !recordTypes.TryGetValue(typeName, out var recordType)) continue;
            if (!string.Equals(typeName, literalType, StringComparison.Ordinal)) { diagnostics.Add(WrongRecordType + $":{name}:expected-{typeName}:actual-{literalType}"); continue; }
            var fields = ParseRecordFields(source[(open + 1)..close]);
            foreach (var missing in recordType.Fields.Keys.Where(field => !fields.ContainsKey(field))) diagnostics.Add(Prefix + "record-missing-field:" + name + ":" + missing);
            foreach (var extra in fields.Keys.Where(field => !recordType.Fields.ContainsKey(field))) diagnostics.Add(Prefix + "record-extra-field:" + name + ":" + extra);
            foreach (var field in fields.Where(field => recordType.Fields.TryGetValue(field.Key, out var expected) && !TypeMatches(field.Value, expected, enums)))
                diagnostics.Add(Prefix + "record-field-type-mismatch:" + name + "." + field.Key + ":expected-" + recordType.Fields[field.Key] + ":actual-" + field.Value);
            result[name] = new(name, typeName, fields.ToImmutableDictionary(StringComparer.Ordinal), new(header.Index, close - header.Index + 1));
        }
        return result;
    }

    private static Dictionary<string, string> ParseRecordFields(string body) => Regex.Matches(body,
        "\\b(?<name>[A-Za-z_]\\w*)\\s*:\\s*(?<value>\"[^\"]*\"|(?:Point2|Point3|Vector2|Vector3|Axis)\\s*\\([^)]*\\)|[A-Za-z_]\\w*|[-+]?\\d+(?:\\.\\d+)?(?:mm|deg)?)",
        RegexOptions.CultureInvariant).Cast<Match>().ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value, StringComparer.Ordinal);
    private static bool ConceptExists(string concept, string source) => Regex.IsMatch(source, $@"\bConcept\s+{Regex.Escape(concept)}\s*\{{", RegexOptions.CultureInvariant);
    private static bool Satisfies(string type, string concept, string source)
    {
        if (type != "Box") return Regex.IsMatch(source, $@"\bConcept\s+Struct\s+{Regex.Escape(type)}\s*:\s*{Regex.Escape(concept)}\s*\{{", RegexOptions.CultureInvariant);
        var definition = Regex.Match(source, $@"\bConcept\s+{Regex.Escape(concept)}\s*\{{(?<body>.*?)\}}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var capabilities = new Dictionary<string, string>(StringComparer.Ordinal) { ["Bounds"] = "Box3", ["TopPlane"] = "Plane", ["CenterAxis"] = "Axis" };
        return definition.Success && Regex.Matches(definition.Groups["body"].Value, @"(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<kind>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant).Cast<Match>().All(m => capabilities.TryGetValue(m.Groups["name"].Value, out var actual) && actual == m.Groups["kind"].Value);
    }
    private static string Substitute(string body, BoundTemplateArguments bound)
    {
        foreach (var record in bound.RecordArguments.Values)
        {
            foreach (var field in record.Fields)
                body = Regex.Replace(body, $@"\b{Regex.Escape(record.Parameter)}\s*\.\s*{Regex.Escape(field.Key)}\b", _ => field.Value, RegexOptions.CultureInvariant);
        }
        return bound.TypeArguments.Concat(bound.ValueArguments.Where(pair => !bound.RecordArguments.ContainsKey(pair.Key)))
            .Aggregate(body, (result, pair) => Regex.Replace(result, $@"\b{Regex.Escape(pair.Key)}\b", pair.Value));
    }
    private static bool ValidateRecordMembers(string body, BoundTemplateArguments bound, List<string> diagnostics)
    {
        foreach (var record in bound.RecordArguments.Values)
            foreach (var member in Regex.Matches(body, $@"\b{Regex.Escape(record.Parameter)}\s*\.\s*(?<member>[A-Za-z_]\w*)", RegexOptions.CultureInvariant)
                         .Cast<Match>().Select(match => match.Groups["member"].Value).Distinct(StringComparer.Ordinal))
                if (!record.Fields.ContainsKey(member)) diagnostics.Add(UnknownRecordMember + $":{record.Parameter}.{member}:record-{record.TypeName}");
        return !HasErrors(diagnostics);
    }
    private static string? ResolveTemplateMatches(string body, BoundTemplateArguments bound, List<string> diagnostics, out IReadOnlyDictionary<string, string> selectedMatches)
    {
        // Enum parameters are resolved here so M3's downstream Match evaluator receives only the selected value.
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            var match = Regex.Match(body, @"\bMatch\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!match.Success) { selectedMatches = selected; return body; }
            var name = match.Groups["name"].Value;
            if (!bound.ValueArguments.TryGetValue(name, out var selectedArm)) { selectedMatches = selected; return body; }
            var open = match.Index + match.Value.LastIndexOf('{'); var close = Matching(body, open, '{', '}');
            if (close < 0) { diagnostics.Add(FirmamentV2Parser.UnsupportedConstruct); selectedMatches = selected; return null; }
            var arm = Regex.Match(body[(open + 1)..close], $@"(?m)^\s*{Regex.Escape(selectedArm)}\s*=>\s*", RegexOptions.CultureInvariant);
            if (!arm.Success) { diagnostics.Add(FirmamentV2Parser.UnsupportedConstruct); selectedMatches = selected; return null; }
            var armValueStart = open + 1 + arm.Index + arm.Length;
            var value = StaticMatchArmValue(body, armValueStart, close);
            if (value is null) { diagnostics.Add(FirmamentV2Parser.UnsupportedConstruct); selectedMatches = selected; return null; }
            selected[name] = selectedArm;
            body = body.Remove(match.Index, close - match.Index + 1).Insert(match.Index, value);
        }
    }

    // A Match arm may be a scalar static expression or a balanced spatial initializer such as Grid { ... }.
    // Preserve the whole selected initializer; truncating at its first brace would leak malformed source downstream.
    private static string? StaticMatchArmValue(string body, int start, int enclosingClose)
    {
        while (start < enclosingClose && char.IsWhiteSpace(body[start])) start++;
        if (start >= enclosingClose) return null;
        var initializer = Regex.Match(body[start..enclosingClose], @"^[A-Za-z_][A-Za-z0-9_]*\s*\{", RegexOptions.CultureInvariant);
        if (initializer.Success)
        {
            var open = start + initializer.Value.LastIndexOf('{');
            var close = Matching(body, open, '{', '}');
            return close < 0 || close > enclosingClose ? null : body[start..(close + 1)].Trim();
        }
        var end = body.IndexOfAny(['\r', '\n'], start);
        if (end < 0 || end > enclosingClose) end = enclosingClose;
        return body[start..end].Trim();
    }
    private static bool EvaluateRequires(string body, string instance, List<string> diagnostics, out IReadOnlyDictionary<string, string> results)
    {
        var evidence = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match require in Regex.Matches(body, @"\bRequire\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=>\s*(?<expr>[^\r\n}]+)", RegexOptions.CultureInvariant))
        {
            var name = require.Groups["name"].Value; var expression = require.Groups["expr"].Value.Trim();
            if (!TryBool(expression, out var value)) { diagnostics.Add(RequireNonBool + ":" + name); evidence[name] = "Invalid:" + expression; }
            else if (!value) { diagnostics.Add(RequireFailed + $":{instance}.{name}:{expression}"); evidence[name] = "Failed:" + expression; }
            else evidence[name] = "Passed:" + expression;
        }
        results = evidence;
        return !HasErrors(diagnostics);
    }
    private static bool TryBool(string expression, out bool result)
    {
        result = true;
        foreach (var clause in expression.Split("&&", StringSplitOptions.TrimEntries))
        {
            var m = Regex.Match(clause, @"^(?<a>[-+]?[0-9]+(?:\.[0-9]+)?)(?<u>mm|deg)?\s*(?<op>>=|<=|>|<|==)\s*(?<b>[-+]?[0-9]+(?:\.[0-9]+)?)(?<u2>mm|deg)?$", RegexOptions.CultureInvariant);
            if (!m.Success || m.Groups["u"].Value != m.Groups["u2"].Value) return false;
            var a = double.Parse(m.Groups["a"].Value, CultureInfo.InvariantCulture); var b = double.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture);
            result &= m.Groups["op"].Value switch { ">" => a > b, "<" => a < b, ">=" => a >= b, "<=" => a <= b, _ => a == b };
        }
        return true;
    }
    private static void DetectTemplateCycles(IEnumerable<TemplateDeclarationIr> templates, IReadOnlyDictionary<string, TemplateDeclarationIr> byName, List<string> diagnostics)
    {
        foreach (var template in templates)
        {
            var stack = new List<string>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(string name)
            {
                var at = stack.IndexOf(name); if (at >= 0) { diagnostics.Add(Recursive + ":" + string.Join(" -> ", stack.Skip(at).Append(name))); return true; }
                if (!seen.Add(name)) return false; stack.Add(name);
                foreach (Match call in Regex.Matches(byName[name].Body, @"=\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*<", RegexOptions.CultureInvariant)) if (byName.ContainsKey(call.Groups["name"].Value) && Visit(call.Groups["name"].Value)) return true;
                stack.RemoveAt(stack.Count - 1); return false;
            }
            _ = Visit(template.Name);
        }
    }
    private static string Identity(TemplateDeclarationIr template, TemplateApplicationIr application, BoundTemplateArguments args)
    {
        var recordInput = args.RecordArguments.OrderBy(x => x.Key).SelectMany(x => x.Value.Fields.OrderBy(f => f.Key).Select(f => x.Key + "." + f.Key + "=" + f.Value));
        var input = template.Name + "|" + application.InstanceName + "|" + string.Join("|", args.TypeArguments.Concat(args.ValueArguments).OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value).Concat(recordInput));
        return "template:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..16];
    }
    private static ImmutableArray<string> GeneratedPaths(string body, string instance) => Regex.Matches(body, @"\b(?:Concept\s+Struct|Box|Modify|EdgeFinish)\s+(?<n>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Select(m => instance + "::" + m.Groups["n"].Value).Distinct().ToImmutableArray();
    private static string RemoveRequires(string body) => Regex.Replace(body, @"(?m)^\s*Require\s+[A-Za-z_][A-Za-z0-9_]*\s*=>\s*[^\r\n}]+\s*$", string.Empty);
    private static bool HasErrors(IEnumerable<string> diagnostics) => diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal));
    private static int Matching(string source, int open, char begin, char end) { var depth = 0; for (var i = open; i < source.Length; i++) { if (source[i] == begin) depth++; else if (source[i] == end && --depth == 0) return i; } return -1; }
}
