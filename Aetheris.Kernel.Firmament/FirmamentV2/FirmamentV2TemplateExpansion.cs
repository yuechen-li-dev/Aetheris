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
internal sealed record TemplateStaticRecordIr(string Name, string TypeName, ImmutableDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan, string Provenance = "StaticRecord");
internal sealed record TemplateStaticTableIr(string Name, string RowType, string? KeyField, ImmutableDictionary<string, ImmutableArray<string>> Columns, FirmamentV2SourceSpan SourceSpan);
internal sealed record BoundTemplateRecordIr(string Parameter, string TypeName, string StaticName, ImmutableDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan, string Provenance);
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
    internal const string RecordMissingField = Prefix + "record-missing-field";
    internal const string RecordExtraField = Prefix + "record-extra-field";
    internal const string RecordFieldTypeMismatch = Prefix + "record-field-type-mismatch";
    internal const string MaterializedRecordArgument = Prefix + "materialized-value-not-compile-time-record";
    internal const string UnsupportedRecordValue = Prefix + "unsupported-record-value-form";
    internal const string WithBaseNotRecord = Prefix + "with-base-not-record";
    internal const string WithUnknownField = Prefix + "with-unknown-field";
    internal const string WithDuplicateField = Prefix + "with-duplicate-field";
    internal const string WithTypeMismatch = Prefix + "with-field-type-mismatch";
    internal const string TableUnknownRowType = Prefix + "table-unknown-row-type";
    internal const string TableMissingColumn = Prefix + "table-missing-column";
    internal const string TableUnknownColumn = Prefix + "table-unknown-column";
    internal const string TableColumnTypeMismatch = Prefix + "table-column-type-mismatch";
    internal const string TableUnequalColumns = Prefix + "table-unequal-column-length";
    internal const string TableDuplicateKey = Prefix + "table-duplicate-key";
    internal const string TableMissingKey = Prefix + "table-missing-key-field";
    internal const string TableInvalidKey = Prefix + "table-invalid-key-type";
    internal const string TableLookupTypeMismatch = Prefix + "table-lookup-key-type-mismatch";
    internal const string TableKeyNotFound = Prefix + "table-key-not-found";
    internal const string TableIndexOutOfRange = Prefix + "table-index-out-of-range";

    internal sealed record Result(string Source, IReadOnlyList<ConceptIrTemplateInstantiation> Instantiations);

    internal sealed record HostArgument(
        string Expression,
        string? RecordType = null,
        IReadOnlyDictionary<string, string>? RecordFields = null);

    internal sealed record HostParameter(
        string Name,
        string Kind,
        string TypeName,
        string? DefaultExpression,
        string? ConstraintConcept);

    internal sealed record HostTemplate(
        string Name,
        string TargetKind,
        IReadOnlyList<HostParameter> Parameters,
        IReadOnlyList<HostConstraint> Constraints);

    internal sealed record HostConstraint(string Name, string Expression);

    internal sealed record HostRecord(string Name, IReadOnlyDictionary<string, string> Fields);

    internal sealed record HostStaticRecord(
        string Name,
        string TypeName,
        IReadOnlyDictionary<string, string> Fields,
        string Provenance);

    internal static IReadOnlyList<HostTemplate> Inspect(string source, List<string> diagnostics) =>
        ParseDeclarations(source, diagnostics)
            .Select(template => new HostTemplate(
                template.Name,
                template.TargetKind,
                template.Parameters.Select(parameter => parameter switch
                {
                    TemplateTypeParameterIr type => new HostParameter(type.Name, "Type", type.Name, null, type.ConstraintConcept),
                    TemplateValueParameterIr value => new HostParameter(value.Name, "Value", value.TypeName, value.DefaultExpression, null),
                    _ => throw new InvalidOperationException($"Unknown Template parameter IR '{parameter.GetType().Name}'."),
                }).ToArray(),
                ParseRequires(template.Body)))
            .OrderBy(template => template.Name, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<HostConstraint> ParseRequires(string body) =>
        Regex.Matches(body, @"\bRequire\s+(?<name>[A-Za-z_]\w*)\s*=>\s*(?<expression>[^\r\n}]+)", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => new HostConstraint(match.Groups["name"].Value, match.Groups["expression"].Value.Trim()))
            .ToArray();

    internal static IReadOnlyList<HostRecord> InspectRecords(string source, List<string> diagnostics) =>
        ParseRecordTypes(source, diagnostics).Values
            .OrderBy(record => record.Name, StringComparer.Ordinal)
            .Select(record => new HostRecord(record.Name,
                record.Fields.OrderBy(field => field.Key, StringComparer.Ordinal)
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal)))
            .ToArray();

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> InspectEnums(string source) =>
        ParseEnums(source).OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key,
                item => (IReadOnlyList<string>)item.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

    internal static IReadOnlyList<HostStaticRecord> InspectStaticRecords(string source, List<string> diagnostics)
    {
        var enums = ParseEnums(source);
        var recordTypes = ParseRecordTypes(source, diagnostics);
        var tables = ParseStaticTables(source, recordTypes, enums, diagnostics);
        var staticRecords = ParseStaticRecords(source, recordTypes, enums, tables, diagnostics);
        return staticRecords.Values
            .OrderBy(record => record.Name, StringComparer.Ordinal)
            .Select(record => new HostStaticRecord(
                record.Name,
                record.TypeName,
                FlattenRecordFields(record, recordTypes, staticRecords),
                record.Provenance))
            .ToArray();
    }

    /// <summary>
    /// Expands one host-supplied Template invocation from typed arguments. The invocation is
    /// admitted directly as binder IR; callers never need to manufacture Firmament application
    /// source, and host Record values are supplied as immutable synthetic static records.
    /// </summary>
    internal static Result? ExpandHostInvocation(
        string source,
        string templateName,
        string instanceName,
        IReadOnlyDictionary<string, HostArgument> hostArguments,
        List<string> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(hostArguments);

        var declarations = ParseDeclarations(source, diagnostics);
        var byName = declarations.ToDictionary(declaration => declaration.Name, StringComparer.Ordinal);
        DetectTemplateCycles(declarations, byName, diagnostics);
        if (!byName.TryGetValue(templateName, out var template))
        {
            diagnostics.Add(Prefix + "not-found:" + templateName);
            return null;
        }

        if (!Regex.IsMatch(instanceName, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
        {
            diagnostics.Add(Prefix + "invalid-instance-name:" + instanceName);
            return null;
        }

        var enums = ParseEnums(source);
        var recordTypes = ParseRecordTypes(source, diagnostics);
        var tables = ParseStaticTables(source, recordTypes, enums, diagnostics);
        var staticRecords = ParseStaticRecords(source, recordTypes, enums, tables, diagnostics).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var arguments = ImmutableArray.CreateBuilder<TemplateArgumentIr>();
        foreach (var pair in hostArguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var expression = pair.Value.Expression;
            if (pair.Value.RecordFields is not null)
            {
                var recordName = $"__forge_{instanceName}_{pair.Key}";
                var recordType = pair.Value.RecordType ?? string.Empty;
                if (!recordTypes.TryGetValue(recordType, out var recordDefinition))
                {
                    diagnostics.Add(UnknownRecordType + $":{pair.Key}:{recordType}");
                    continue;
                }
                ValidateSyntheticRecord(recordName, recordDefinition, pair.Value.RecordFields, string.Empty);
                staticRecords[recordName] = new TemplateStaticRecordIr(
                    recordName,
                    recordType,
                    pair.Value.RecordFields.ToImmutableDictionary(StringComparer.Ordinal),
                    new FirmamentV2SourceSpan(source.Length, 0),
                    "ForgeHostRecord");
                expression = recordName;
            }
            arguments.Add(new TemplateArgumentIr(pair.Key, expression, new FirmamentV2SourceSpan(source.Length, 0)));
        }
        if (HasErrors(diagnostics)) return null;

        var application = new TemplateApplicationIr(
            template.TargetKind,
            instanceName,
            templateName,
            arguments.ToImmutable(),
            new FirmamentV2SourceSpan(source.Length, 0));
        var bound = Bind(template, application, source, enums, recordTypes, staticRecords, diagnostics);
        if (bound is null || HasErrors(diagnostics)) return null;

        if (!ValidateRecordMembers(template.Body, bound, diagnostics)) return null;
        if (!EvaluateRequires(Substitute(template.Body, bound), application.InstanceName,
                DisplaySignature(template), diagnostics, out var requireResults)) return null;
        var body = ResolveTemplateMatches(template.Body, bound, diagnostics, out var selectedMatches);
        if (body is null) return null;
        body = Substitute(body, bound);
        body = RemoveRequires(body);

        var specialization = new TemplateSpecializationIr(
            template,
            application,
            bound,
            Identity(template, application, bound),
            GeneratedPaths(template.Body, application.InstanceName));
        var recordArguments = bound.RecordArguments.ToDictionary(
            pair => pair.Key,
            pair => new ConceptIrTemplateRecordArgument(
                pair.Value.TypeName,
                pair.Value.StaticName,
                pair.Value.Fields,
                pair.Value.SourceSpan,
                pair.Value.Provenance),
            StringComparer.Ordinal);
        var instantiation = new ConceptIrTemplateInstantiation(
            template.Name,
            application.InstanceName,
            bound.TypeArguments,
            bound.ValueArguments,
            bound.DefaultedArguments,
            specialization.SpecializationIdentity,
            specialization.GeneratedDeclarationPaths,
            template.SourceSpan,
            application.SourceSpan,
            SelectedMatchArms: selectedMatches,
            RecordArguments: recordArguments,
            RequireResults: requireResults);
        var nestedInstantiations = new List<ConceptIrTemplateInstantiation>();
        body = ExpandNestedApplications(body, byName, source, enums, recordTypes, staticRecords,
            diagnostics, nestedInstantiations, [template.Name]);
        if (HasErrors(diagnostics)) return null;
        var liftedPmi = LiftPmi(body, out body);

        var changes = declarations
            .Select(declaration => (
                Start: declaration.SourceSpan.Start,
                Length: declaration.SourceSpan.Length,
                Text: string.Equals(declaration.Name, template.Name, StringComparison.Ordinal)
                    ? $"{template.TargetKind} {instanceName}{template.HeaderTail} {{{body}}}{liftedPmi}"
                    : string.Empty))
            .OrderByDescending(change => change.Start)
            .ToArray();
        foreach (var change in changes)
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return HasErrors(diagnostics) ? null : new Result(source, new[] { instantiation }.Concat(nestedInstantiations).ToArray());

        void ValidateSyntheticRecord(string recordName, TemplateRecordTypeIr definition,
            IReadOnlyDictionary<string, string> fields, string prefix)
        {
            foreach (var expected in definition.Fields)
            {
                var path = prefix + expected.Key;
                if (!fields.TryGetValue(path, out var value))
                {
                    diagnostics.Add(RecordMissingField + ":" + recordName + ":" + path);
                    continue;
                }
                if (recordTypes.TryGetValue(expected.Value, out var nested))
                {
                    if (!string.Equals(value, expected.Value, StringComparison.Ordinal))
                        diagnostics.Add(RecordFieldTypeMismatch + $":{recordName}.{path}:expected-{expected.Value}:actual-{value}");
                    else ValidateSyntheticRecord(recordName, nested, fields, path + ".");
                }
                else if (!TypeMatches(value, expected.Value, enums, recordTypes, staticRecords))
                    diagnostics.Add(RecordFieldTypeMismatch + $":{recordName}.{path}:expected-{expected.Value}:actual-{value}");
            }
            var admitted = definition.Fields.Keys.Select(field => prefix + field).ToArray();
            foreach (var extra in fields.Keys.Where(field => field.StartsWith(prefix, StringComparison.Ordinal)
                         && !admitted.Contains(field, StringComparer.Ordinal)
                         && !admitted.Any(candidate => field.StartsWith(candidate + ".", StringComparison.Ordinal))))
                diagnostics.Add(RecordExtraField + ":" + recordName + ":" + extra);
        }
    }

    public static Result? Expand(string source, List<string> diagnostics)
    {
        var declarations = ParseDeclarations(source, diagnostics);
        var byName = declarations.ToDictionary(d => d.Name, StringComparer.Ordinal);
        DetectTemplateCycles(declarations, byName, diagnostics);
        var applications = ParseApplications(source, byName, diagnostics)
            .Where(application => !declarations.Any(declaration =>
                application.SourceSpan.Start > declaration.SourceSpan.Start
                && application.SourceSpan.Start < declaration.SourceSpan.Start + declaration.SourceSpan.Length))
            .ToImmutableArray();
        if (HasErrors(diagnostics)) return null;

        var enums = ParseEnums(source);
        var recordTypes = ParseRecordTypes(source, diagnostics);
        var tables = ParseStaticTables(source, recordTypes, enums, diagnostics);
        var staticRecords = ParseStaticRecords(source, recordTypes, enums, tables, diagnostics);
        var changes = declarations.Select(d => (d.SourceSpan.Start, d.SourceSpan.Length, Text: string.Empty)).ToList();
        var instantiations = new List<ConceptIrTemplateInstantiation>();
        foreach (var application in applications)
        {
            var template = byName[application.TemplateName];
            if (!string.Equals(application.TargetKind, template.TargetKind, StringComparison.Ordinal)) { diagnostics.Add(FirmamentV2Parser.UnsupportedConstruct); continue; }
            var bound = Bind(template, application, source, enums, recordTypes, staticRecords, diagnostics);
            if (bound is null) continue;
            var specialization = new TemplateSpecializationIr(template, application, bound, Identity(template, application, bound), GeneratedPaths(template.Body, application.InstanceName));
            if (!ValidateRecordMembers(template.Body, bound, diagnostics)) continue;
            if (!EvaluateRequires(Substitute(template.Body, bound), application.InstanceName,
                    DisplaySignature(template), diagnostics, out var requireResults)) continue;
            var body = ResolveTemplateMatches(template.Body, bound, diagnostics, out var selectedMatches);
            if (body is null) continue;
            body = Substitute(body, bound);
            body = RemoveRequires(body);
            var recordArguments = bound.RecordArguments.ToDictionary(pair => pair.Key, pair => new ConceptIrTemplateRecordArgument(
                pair.Value.TypeName, pair.Value.StaticName, pair.Value.Fields, pair.Value.SourceSpan, pair.Value.Provenance), StringComparer.Ordinal);
            instantiations.Add(new(template.Name, application.InstanceName, bound.TypeArguments, bound.ValueArguments, bound.DefaultedArguments,
                specialization.SpecializationIdentity, specialization.GeneratedDeclarationPaths, template.SourceSpan, application.SourceSpan,
                SelectedMatchArms: selectedMatches, RecordArguments: recordArguments, RequireResults: requireResults));
            body = ExpandNestedApplications(body, byName, source, enums, recordTypes, staticRecords,
                diagnostics, instantiations, [template.Name]);
            var liftedPmi = LiftPmi(body, out body);
            changes.Add((application.SourceSpan.Start, application.SourceSpan.Length, $"{template.TargetKind} {application.InstanceName}{template.HeaderTail} {{{body}}}{liftedPmi}"));
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
            var header = close < 0 ? Match.Empty : Regex.Match(source[(close + 1)..], @"^\s*(?<kind>Concept\s+Struct|Struct|Model|Panel|SheetMetal|ProfileDelta)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<tail>\s*:\s*[A-Za-z_][A-Za-z0-9_]*)?\s*\{", RegexOptions.CultureInvariant);
            if (close >= 0 && !header.Success
                && Regex.IsMatch(source[(close + 1)..], @"^\s*[A-Za-z_][A-Za-z0-9_]*\s*\{", RegexOptions.CultureInvariant))
                continue; // finite feature Template; CanonicalStaticAuthoring owns this bounded form.
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
        foreach (Match start in Regex.Matches(source, @"\b(?<kind>Concept\s+Struct|Struct|Model|Panel|SheetMetal|ProfileDelta)\s+(?<instance>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<template>[A-Za-z_][A-Za-z0-9_]*)\s*<", RegexOptions.CultureInvariant))
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
        var expectedSignature = DisplaySignature(template);
        foreach (var unknown in supplied.Keys.Where(name => template.Parameters.All(p => p.Name != name))) diagnostics.Add(UnknownArgument + ":" + unknown + ":expected-signature:" + expectedSignature);
        var types = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal); var values = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var records = ImmutableDictionary.CreateBuilder<string, BoundTemplateRecordIr>(StringComparer.Ordinal); var defaults = ImmutableArray.CreateBuilder<string>();
        var resolving = new List<string>();
        string? Resolve(TemplateValueParameterIr p)
        {
            if (values.TryGetValue(p.Name, out var existing)) return existing;
            if (resolving.Contains(p.Name, StringComparer.Ordinal)) { diagnostics.Add(DefaultCycle + ":" + string.Join(" -> ", resolving.Append(p.Name))); return null; }
            var expression = supplied.TryGetValue(p.Name, out var suppliedValue) ? suppliedValue : p.DefaultExpression;
            if (expression is null) { diagnostics.Add(MissingArgument + ":" + p.Name + ":expected-signature:" + expectedSignature); return null; }
            resolving.Add(p.Name);
            var referenced = template.Parameters.OfType<TemplateValueParameterIr>().SingleOrDefault(x => x.Name == expression);
            var value = referenced is null ? expression : Resolve(referenced);
            resolving.RemoveAt(resolving.Count - 1);
            if (value is null) return null;
            if (!TypeMatches(value, p.TypeName, enums)) { diagnostics.Add((supplied.ContainsKey(p.Name) ? TypeMismatch : BadDefault) + $":{p.Name}:expected-{p.TypeName}:actual-{value}:expected-signature:{expectedSignature}"); return null; }
            values[p.Name] = value;
            if (!supplied.ContainsKey(p.Name)) defaults.Add(p.Name);
            return value;
        }
        foreach (var parameter in template.Parameters)
        {
            if (parameter is TemplateTypeParameterIr type)
            {
                if (!supplied.TryGetValue(type.Name, out var value)) { diagnostics.Add(MissingArgument + ":" + type.Name + ":expected-signature:" + expectedSignature); continue; }
                if (!ConceptExists(type.ConstraintConcept, source)) diagnostics.Add(UnknownConstraint + ":" + type.ConstraintConcept);
                else if (!Satisfies(value, type.ConstraintConcept, source)) diagnostics.Add(ConstraintFailure + $":{type.Name}:{value}:{type.ConstraintConcept}:expected-signature:{expectedSignature}");
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
            if (expression is null) { diagnostics.Add(MissingArgument + ":" + parameter.Name + ":expected-signature:" + expectedSignature); return; }
            if (!supplied.ContainsKey(parameter.Name)) defaults.Add(parameter.Name);
            if (Regex.IsMatch(source, $@"\bStatic\s+{Regex.Escape(expression)}\s*:\s*{Regex.Escape(recordType.Name)}\s*\[", RegexOptions.CultureInvariant))
            { diagnostics.Add(RecordCollectionMismatch + $":{parameter.Name}:expected-{recordType.Name}:actual-collection"); return; }
            if (staticRecords.TryGetValue(expression, out var staticRecord))
            {
                if (!string.Equals(staticRecord.TypeName, recordType.Name, StringComparison.Ordinal))
                { diagnostics.Add(WrongRecordType + $":{parameter.Name}:expected-{recordType.Name}:actual-{staticRecord.TypeName}"); return; }
                records[parameter.Name] = new(parameter.Name, recordType.Name, staticRecord.Name, FlattenRecordFields(staticRecord, recordTypes, staticRecords), staticRecord.SourceSpan, staticRecord.Provenance);
                values[parameter.Name] = staticRecord.Name;
                return;
            }
            if (Regex.IsMatch(source, $@"\b(?:Struct|Model)\s+{Regex.Escape(expression)}\b", RegexOptions.CultureInvariant))
            { diagnostics.Add(MaterializedRecordArgument + $":{parameter.Name}:{expression}"); return; }
            if (Regex.IsMatch(expression, @"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant)) diagnostics.Add(UnknownStaticRecord + $":{parameter.Name}:{expression}");
            else diagnostics.Add(UnsupportedRecordValue + $":{parameter.Name}:{expression}");
        }
    }

    private static ImmutableDictionary<string, string> FlattenRecordFields(TemplateStaticRecordIr record,
        IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes, IReadOnlyDictionary<string, TemplateStaticRecordIr> staticRecords)
    {
        var result = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        void Add(TemplateStaticRecordIr value, string prefix)
        {
            foreach (var field in value.Fields)
            {
                var path = prefix + field.Key;
                result[path] = field.Value;
                if (recordTypes[value.TypeName].Fields.TryGetValue(field.Key, out var type)
                    && recordTypes.ContainsKey(type) && staticRecords.TryGetValue(field.Value, out var nested)) Add(nested, path + ".");
            }
        }
        Add(record, string.Empty);
        return result.ToImmutable();
    }

    private static IReadOnlyDictionary<string, ImmutableHashSet<string>> ParseEnums(string source) => Regex.Matches(source, @"\bEnum\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant).Cast<Match>().ToDictionary(
        m => m.Groups["name"].Value,
        m => Regex.Matches(m.Groups["body"].Value, @"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant).Select(v => v.Value).ToImmutableHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    private static bool TypeMatches(string value, string type, IReadOnlyDictionary<string, ImmutableHashSet<string>> enums,
        IReadOnlyDictionary<string, TemplateRecordTypeIr>? recordTypes = null, IReadOnlyDictionary<string, TemplateStaticRecordIr>? staticRecords = null) => type switch
    {
        "Length" => Regex.IsMatch(value, @"^[-+]?[0-9]+(?:\.[0-9]+)?mm$", RegexOptions.CultureInvariant),
        "Angle" => Regex.IsMatch(value, @"^[-+]?[0-9]+(?:\.[0-9]+)?deg$", RegexOptions.CultureInvariant),
        "String" => Regex.IsMatch(value, "^\"[^\"]*\"$", RegexOptions.CultureInvariant),
        "Version" => Regex.IsMatch(value, @"^(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)$", RegexOptions.CultureInvariant),
        "Date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        "ImportedStep" => Regex.IsMatch(value, @"^\$[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant),
        "ProfilePath" => Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+$", RegexOptions.CultureInvariant),
        "Int" or "int" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        "Float" or "float" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        "Bool" or "bool" => value is "true" or "false",
        _ when enums.TryGetValue(type, out var variants) => variants.Contains(value),
        _ when recordTypes?.ContainsKey(type) == true && staticRecords?.TryGetValue(value, out var record) == true => record.TypeName == type,
        _ => false
    };
    private static bool IsBuiltInValueType(string type, IReadOnlyDictionary<string, ImmutableHashSet<string>> enums) =>
        type is "Length" or "Angle" or "String" or "Version" or "Date" or "ImportedStep" or "ProfilePath" or "Int" or "Float" or "Bool" or "int" or "float" or "bool" || enums.ContainsKey(type);

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

    private static IReadOnlyDictionary<string, TemplateStaticTableIr> ParseStaticTables(string source, IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes,
        IReadOnlyDictionary<string, ImmutableHashSet<string>> enums, List<string> diagnostics)
    {
        var tables = new Dictionary<string, TemplateStaticTableIr>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(source, @"\bStatic\s+Table\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)(?:\s+Key\s*:\s*(?<key>[A-Za-z_]\w*))?\s*\{", RegexOptions.CultureInvariant))
        {
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open, '{', '}');
            var name = header.Groups["name"].Value; var type = header.Groups["type"].Value; var key = header.Groups["key"].Success ? header.Groups["key"].Value : null;
            if (close < 0 || !recordTypes.TryGetValue(type, out var rowType)) { diagnostics.Add(TableUnknownRowType + ":" + name + ":" + type); continue; }
            var columns = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(StringComparer.Ordinal);
            var body = source[(open + 1)..close];
            foreach (Match column in Regex.Matches(body, @"\b(?<name>[A-Za-z_]\w*)\s*:\s*\[", RegexOptions.CultureInvariant))
            {
                var columnOpen = open + 1 + column.Index + column.Value.LastIndexOf('['); var columnClose = Matching(source, columnOpen, '[', ']');
                if (columnClose < 0 || columnClose > close) { diagnostics.Add(TableUnequalColumns + ":" + name + ":malformed-" + column.Groups["name"].Value); continue; }
                var columnName = column.Groups["name"].Value;
                if (columns.ContainsKey(columnName)) { diagnostics.Add(TableDuplicateKey + ":" + name + ":duplicate-column-" + columnName); continue; }
                columns[columnName] = SplitValues(source[(columnOpen + 1)..columnClose]).ToImmutableArray();
            }
            foreach (var field in rowType.Fields.Keys.Where(field => !columns.ContainsKey(field))) diagnostics.Add(TableMissingColumn + ":" + name + ":" + field);
            foreach (var column in columns.Keys.Where(column => !rowType.Fields.ContainsKey(column))) diagnostics.Add(TableUnknownColumn + ":" + name + ":" + column);
            foreach (var column in columns)
                if (rowType.Fields.TryGetValue(column.Key, out var expected))
                    foreach (var value in column.Value.Where(value => !TypeMatches(value, expected, enums, recordTypes, null))) diagnostics.Add(TableColumnTypeMismatch + ":" + name + "." + column.Key + ":expected-" + expected + ":actual-" + value);
            var lengths = columns.Values.Select(values => values.Length).Distinct().ToArray();
            if (lengths.Length > 1) diagnostics.Add(TableUnequalColumns + ":" + name);
            if (key is not null)
            {
                if (!rowType.Fields.TryGetValue(key, out var keyType) || !columns.ContainsKey(key)) diagnostics.Add(TableMissingKey + ":" + name + ":" + key);
                else if (keyType is not ("String" or "int") && !enums.ContainsKey(keyType)) diagnostics.Add(TableInvalidKey + ":" + name + ":" + keyType);
                else if (columns[key].Distinct(StringComparer.Ordinal).Count() != columns[key].Length) diagnostics.Add(TableDuplicateKey + ":" + name + ":" + key);
            }
            tables[name] = new(name, type, key, columns.ToImmutable(), new(header.Index, close - header.Index + 1));
        }
        return tables;
    }

    private static IReadOnlyDictionary<string, TemplateStaticRecordIr> ParseStaticRecords(string source, IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes,
        IReadOnlyDictionary<string, ImmutableHashSet<string>> enums, IReadOnlyDictionary<string, TemplateStaticTableIr> tables, List<string> diagnostics)
    {
        var result = new Dictionary<string, TemplateStaticRecordIr>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*=\s*(?<literal>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var name = header.Groups["name"].Value; var typeName = header.Groups["type"].Value; var literalType = header.Groups["literal"].Value;
            var open = source.IndexOf('{', header.Index); var close = Matching(source, open, '{', '}');
            if (close < 0 || !recordTypes.TryGetValue(typeName, out var recordType)) continue;
            if (!string.Equals(typeName, literalType, StringComparison.Ordinal)) { diagnostics.Add(WrongRecordType + $":{name}:expected-{typeName}:actual-{literalType}"); continue; }
            var fields = ParseRecordFields(source[(open + 1)..close]);
            foreach (var missing in recordType.Fields.Keys.Where(field => !fields.ContainsKey(field))) diagnostics.Add(RecordMissingField + ":" + name + ":" + missing);
            foreach (var extra in fields.Keys.Where(field => !recordType.Fields.ContainsKey(field))) diagnostics.Add(RecordExtraField + ":" + name + ":" + extra);
            foreach (var field in fields.Where(field => recordType.Fields.TryGetValue(field.Key, out var expected) && !recordTypes.ContainsKey(expected) && !TypeMatches(field.Value, expected, enums, recordTypes, result)))
                diagnostics.Add(RecordFieldTypeMismatch + ":" + name + "." + field.Key + ":expected-" + recordType.Fields[field.Key] + ":actual-" + field.Value);
            result[name] = new(name, typeName, fields.ToImmutableDictionary(StringComparer.Ordinal), new(header.Index, close - header.Index + 1));
        }
        foreach (Match declaration in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<type>[A-Za-z_]\w*))?\s*=\s*(?<table>[A-Za-z_]\w*)\s*\[\s*(?<lookup>[^\]]+)\s*\]", RegexOptions.CultureInvariant))
        {
            if (!tables.TryGetValue(declaration.Groups["table"].Value, out var table)) continue;
            var lookup = declaration.Groups["lookup"].Value.Trim(); var index = -1;
            if (int.TryParse(lookup, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)) index = numeric;
            else if (table.KeyField is null) diagnostics.Add(TableLookupTypeMismatch + ":" + table.Name + ":index-required");
            else
            {
                var keyType = recordTypes[table.RowType].Fields[table.KeyField];
                if (!TypeMatches(lookup, keyType, enums, recordTypes, result)) diagnostics.Add(TableLookupTypeMismatch + ":" + table.Name + ":expected-" + keyType + ":actual-" + lookup);
                else index = table.Columns[table.KeyField].IndexOf(lookup);
                if (index < 0) diagnostics.Add(TableKeyNotFound + ":" + table.Name + ":" + lookup);
            }
            var rowCount = table.Columns.Count == 0 ? 0 : table.Columns.First().Value.Length;
            if (index < 0 || index >= rowCount) { if (int.TryParse(lookup, out _)) diagnostics.Add(TableIndexOutOfRange + ":" + table.Name + ":" + lookup); continue; }
            var requestedType = declaration.Groups["type"].Success ? declaration.Groups["type"].Value : table.RowType;
            if (!string.Equals(requestedType, table.RowType, StringComparison.Ordinal)) { diagnostics.Add(WrongRecordType + ":" + declaration.Groups["name"].Value + ":expected-" + requestedType + ":actual-" + table.RowType); continue; }
            var fields = table.Columns.ToImmutableDictionary(column => column.Key, column => column.Value[index], StringComparer.Ordinal);
            var keyDetail = table.KeyField is null ? string.Empty : " key:" + table.Columns[table.KeyField][index];
            result[declaration.Groups["name"].Value] = new(declaration.Groups["name"].Value, table.RowType, fields, new(declaration.Index, declaration.Length), "Table:" + table.Name + " row:" + index + keyDetail);
        }
        // Derived records are resolved in source order. This gives Static declarations the same
        // acyclic dependency discipline as existing template defaults without runtime values.
        var pending = Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<type>[A-Za-z_]\w*))?\s*=\s*(?<base>[A-Za-z_]\w*)\s+with\s*\{", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        while (pending.Length > 0)
        {
            var progressed = false;
            var remaining = new List<Match>();
            foreach (var declaration in pending)
            {
                var open = source.IndexOf('{', declaration.Index); var close = Matching(source, open, '{', '}');
                var name = declaration.Groups["name"].Value; var baseName = declaration.Groups["base"].Value;
                if (close < 0) { diagnostics.Add(WithBaseNotRecord + ":" + name + ":malformed"); continue; }
                if (!result.TryGetValue(baseName, out var baseRecord)) { remaining.Add(declaration); continue; }
                var type = declaration.Groups["type"].Success ? declaration.Groups["type"].Value : baseRecord.TypeName;
                if (!string.Equals(type, baseRecord.TypeName, StringComparison.Ordinal)) { diagnostics.Add(WrongRecordType + ":" + name + ":expected-" + type + ":actual-" + baseRecord.TypeName); continue; }
                var overrides = ParseRecordFields(source[(open + 1)..close]);
                var overrideNames = Regex.Matches(source[(open + 1)..close], @"\b(?<name>[A-Za-z_]\w*)\s*:", RegexOptions.CultureInvariant).Cast<Match>().Select(match => match.Groups["name"].Value).ToArray();
                foreach (var duplicate in overrideNames.GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1)) diagnostics.Add(WithDuplicateField + ":" + name + ":" + duplicate.Key);
                foreach (var field in overrides.Keys.Where(field => !recordTypes[baseRecord.TypeName].Fields.ContainsKey(field))) diagnostics.Add(WithUnknownField + ":" + name + ":" + field);
                foreach (var field in overrideNames.Where(field => recordTypes[baseRecord.TypeName].Fields.ContainsKey(field) && !overrides.ContainsKey(field))) diagnostics.Add(WithTypeMismatch + ":" + name + "." + field + ":collection-or-unsupported-value");
                foreach (var field in overrides.Where(field => recordTypes[baseRecord.TypeName].Fields.TryGetValue(field.Key, out var expected) && !TypeMatches(field.Value, expected, enums, recordTypes, result))) diagnostics.Add(WithTypeMismatch + ":" + name + "." + field.Key + ":expected-" + recordTypes[baseRecord.TypeName].Fields[field.Key] + ":actual-" + field.Value);
                var fields = baseRecord.Fields.SetItems(overrides);
                var inherited = baseRecord.Fields.Values.Concat(overrides.Values).Where(result.ContainsKey).Select(value => result[value].Provenance).Distinct(StringComparer.Ordinal).ToArray();
                result[name] = new(name, type, fields, new(declaration.Index, close - declaration.Index + 1), baseRecord.Provenance + "; derivedFrom:" + baseName + (inherited.Length == 0 ? string.Empty : "; origin:" + string.Join("|", inherited)) + "; overrides:" + string.Join(",", overrides.Keys.OrderBy(value => value, StringComparer.Ordinal)));
                progressed = true;
            }
            if (!progressed)
            {
                foreach (var declaration in remaining) diagnostics.Add(WithBaseNotRecord + ":" + declaration.Groups["name"].Value + ":" + declaration.Groups["base"].Value);
                break;
            }
            pending = remaining.ToArray();
        }
        foreach (Match declaration in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<type>[A-Za-z_]\w*))?\s*=\s*(?<table>[A-Za-z_]\w*)\s*\[\s*(?<lookup>[^\]]+)\s*\]", RegexOptions.CultureInvariant))
        {
            if (!tables.TryGetValue(declaration.Groups["table"].Value, out var table)) continue;
            var lookup = declaration.Groups["lookup"].Value.Trim(); var index = -1;
            if (int.TryParse(lookup, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)) index = numeric;
            else if (table.KeyField is null) diagnostics.Add(TableLookupTypeMismatch + ":" + table.Name + ":index-required");
            else
            {
                var keyType = recordTypes[table.RowType].Fields[table.KeyField];
                if (!TypeMatches(lookup, keyType, enums, recordTypes, result)) diagnostics.Add(TableLookupTypeMismatch + ":" + table.Name + ":expected-" + keyType + ":actual-" + lookup);
                else index = table.Columns[table.KeyField].IndexOf(lookup);
                if (index < 0) diagnostics.Add(TableKeyNotFound + ":" + table.Name + ":" + lookup);
            }
            var rowCount = table.Columns.Count == 0 ? 0 : table.Columns.First().Value.Length;
            if (index < 0 || index >= rowCount) { if (int.TryParse(lookup, out _)) diagnostics.Add(TableIndexOutOfRange + ":" + table.Name + ":" + lookup); continue; }
            var requestedType = declaration.Groups["type"].Success ? declaration.Groups["type"].Value : table.RowType;
            if (!string.Equals(requestedType, table.RowType, StringComparison.Ordinal)) { diagnostics.Add(WrongRecordType + ":" + declaration.Groups["name"].Value + ":expected-" + requestedType + ":actual-" + table.RowType); continue; }
            var fields = table.Columns.ToImmutableDictionary(column => column.Key, column => column.Value[index], StringComparer.Ordinal);
            var keyDetail = table.KeyField is null ? string.Empty : " key:" + table.Columns[table.KeyField][index];
            result[declaration.Groups["name"].Value] = new(declaration.Groups["name"].Value, table.RowType, fields, new(declaration.Index, declaration.Length), "Table:" + table.Name + " row:" + index + keyDetail);
        }
        foreach (var record in result.Values.ToArray())
        {
            var origins = record.Fields.Values.Where(result.ContainsKey).Select(value => result[value].Provenance).Where(value => value != record.Provenance).Distinct(StringComparer.Ordinal).ToArray();
            if (origins.Length > 0 && !record.Provenance.Contains("origin:", StringComparison.Ordinal)) result[record.Name] = record with { Provenance = record.Provenance + "; origin:" + string.Join("|", origins) };
        }
        foreach (var record in result.Values)
            foreach (var field in record.Fields.Where(field => recordTypes[record.TypeName].Fields.TryGetValue(field.Key, out var expected) && !TypeMatches(field.Value, expected, enums, recordTypes, result)))
                diagnostics.Add(Prefix + "record-field-type-mismatch:" + record.Name + "." + field.Key + ":expected-" + recordTypes[record.TypeName].Fields[field.Key] + ":actual-" + field.Value);
        return result;
    }

    private static IReadOnlyList<string> SplitValues(string text) => Regex.Matches(text, "\"[^\"]*\"|\\d+\\.\\d+\\.\\d+|\\d{4}-\\d{2}-\\d{2}|[-+]?\\d+(?:\\.\\d+)?(?:mm|deg)?|[A-Za-z_]\\w*", RegexOptions.CultureInvariant).Cast<Match>().Select(match => match.Value).ToArray();

    private static Dictionary<string, string> ParseRecordFields(string body) => Regex.Matches(body,
        "\\b(?<name>[A-Za-z_]\\w*)\\s*:\\s*(?<value>\"[^\"]*\"|(?:Point2|Point3|Vector2|Vector3|Axis)\\s*\\([^)]*\\)|\\d+\\.\\d+\\.\\d+|\\d{4}-\\d{2}-\\d{2}|[A-Za-z_]\\w*|[-+]?\\d+(?:\\.\\d+)?(?:mm|deg)?)",
        RegexOptions.CultureInvariant).Cast<Match>().GroupBy(m => m.Groups["name"].Value, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().Groups["value"].Value, StringComparer.Ordinal);
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
            foreach (var field in record.Fields.OrderByDescending(field => field.Key.Length))
                body = Regex.Replace(body, $@"\b{Regex.Escape(record.Parameter)}\s*\.\s*{Regex.Escape(field.Key).Replace("\\.", @"\s*\.\s*", StringComparison.Ordinal)}\b", _ => field.Value, RegexOptions.CultureInvariant);
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
            var match = Regex.Match(body, @"\bMatch\s+(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*)?)\s*\{", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!match.Success) { selectedMatches = selected; return body; }
            var name = Regex.Replace(match.Groups["name"].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant);
            string? selectedArm = null;
            if (!bound.ValueArguments.TryGetValue(name, out selectedArm))
            {
                var dot = name.IndexOf('.');
                if (dot > 0 && bound.RecordArguments.TryGetValue(name[..dot], out var record))
                    record.Fields.TryGetValue(name[(dot + 1)..], out selectedArm);
            }
            if (selectedArm is null) { selectedMatches = selected; return body; }
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
    private static bool EvaluateRequires(string body, string instance, string signature, List<string> diagnostics, out IReadOnlyDictionary<string, string> results)
    {
        var evidence = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match require in Regex.Matches(body, @"\bRequire\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=>\s*(?<expr>[^\r\n}]+)", RegexOptions.CultureInvariant))
        {
            var name = require.Groups["name"].Value; var expression = require.Groups["expr"].Value.Trim();
            if (!TryBool(expression, out var value)) { diagnostics.Add(RequireNonBool + ":" + name + ":template-signature:" + signature); evidence[name] = "Invalid:" + expression; }
            else if (!value) { diagnostics.Add(RequireFailed + $":{instance}.{name}:{expression}:template-signature:{signature}"); evidence[name] = "Failed:" + expression; }
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
    private static string DisplaySignature(TemplateDeclarationIr template) => template.Name + "<" + string.Join(", ", template.Parameters.Select(parameter => parameter switch
    {
        TemplateTypeParameterIr type => $"type {type.Name} satisfies {type.ConstraintConcept}",
        TemplateValueParameterIr value => $"{value.Name}: {value.TypeName}" + (value.DefaultExpression is null ? string.Empty : $" = {value.DefaultExpression}"),
        _ => parameter.Name,
    })) + ">";
    private static ImmutableArray<string> GeneratedPaths(string body, string instance) => Regex.Matches(body, @"\b(?:Concept\s+Struct|Box|Modify|EdgeFinish)\s+(?<n>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Select(m => instance + "::" + m.Groups["n"].Value).Distinct().ToImmutableArray();
    private static string RemoveRequires(string body) => Regex.Replace(body, @"(?m)^\s*Require\s+[A-Za-z_][A-Za-z0-9_]*\s*=>\s*[^\r\n}]+\s*$", string.Empty);
    private static string LiftPmi(string body, out string withoutPmi)
    {
        var blocks = new List<(int Start, int Length, string Text)>();
        foreach (Match header in Regex.Matches(body, @"\bPmi\s*\{", RegexOptions.CultureInvariant))
        {
            if (blocks.Any(block => header.Index >= block.Start && header.Index < block.Start + block.Length)) continue;
            var open = body.IndexOf('{', header.Index);
            var close = Matching(body, open, '{', '}');
            if (close >= 0) blocks.Add((header.Index, close - header.Index + 1, body[header.Index..(close + 1)]));
        }
        withoutPmi = body;
        foreach (var block in blocks.OrderByDescending(block => block.Start))
            withoutPmi = withoutPmi.Remove(block.Start, block.Length);
        return blocks.Count == 0 ? string.Empty : Environment.NewLine + string.Join(Environment.NewLine, blocks.Select(block => block.Text));
    }

    private static string ExpandNestedApplications(
        string body,
        IReadOnlyDictionary<string, TemplateDeclarationIr> templates,
        string moduleSource,
        IReadOnlyDictionary<string, ImmutableHashSet<string>> enums,
        IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes,
        IReadOnlyDictionary<string, TemplateStaticRecordIr> staticRecords,
        List<string> diagnostics,
        ICollection<ConceptIrTemplateInstantiation> instantiations,
        IReadOnlyList<string> stack)
    {
        var applications = ParseApplications(body, templates, diagnostics);
        var changes = new List<(int Start, int Length, string Text)>();
        foreach (var application in applications)
        {
            var template = templates[application.TemplateName];
            if (stack.Contains(template.Name, StringComparer.Ordinal))
            {
                diagnostics.Add(Recursive + ":" + string.Join(" -> ", stack.Append(template.Name)));
                continue;
            }
            var bound = Bind(template, application, moduleSource, enums, recordTypes, staticRecords, diagnostics);
            if (bound is null) continue;
            if (!ValidateRecordMembers(template.Body, bound, diagnostics)) continue;
            if (!EvaluateRequires(Substitute(template.Body, bound), application.InstanceName,
                    DisplaySignature(template), diagnostics, out var requireResults)) continue;
            var specializedBody = ResolveTemplateMatches(template.Body, bound, diagnostics, out var selectedMatches);
            if (specializedBody is null) continue;
            specializedBody = Substitute(specializedBody, bound);
            specializedBody = RemoveRequires(specializedBody);
            var specialization = new TemplateSpecializationIr(template, application, bound,
                Identity(template, application, bound), GeneratedPaths(template.Body, application.InstanceName));
            var recordArguments = bound.RecordArguments.ToDictionary(pair => pair.Key, pair => new ConceptIrTemplateRecordArgument(
                pair.Value.TypeName, pair.Value.StaticName, pair.Value.Fields, pair.Value.SourceSpan, pair.Value.Provenance), StringComparer.Ordinal);
            instantiations.Add(new(template.Name, application.InstanceName, bound.TypeArguments, bound.ValueArguments,
                bound.DefaultedArguments, specialization.SpecializationIdentity, specialization.GeneratedDeclarationPaths,
                template.SourceSpan, application.SourceSpan, SelectedMatchArms: selectedMatches,
                RecordArguments: recordArguments, RequireResults: requireResults));
            specializedBody = ExpandNestedApplications(specializedBody, templates, moduleSource, enums, recordTypes,
                staticRecords, diagnostics, instantiations, stack.Append(template.Name).ToArray());
            var liftedPmi = LiftPmi(specializedBody, out specializedBody);
            // Nested finite specializations flatten into the containing declaration before
            // Feature AIR. The instantiation record above preserves the semantic boundary;
            // material grammar receives concrete declarations without unsupported nested Structs.
            changes.Add((application.SourceSpan.Start, application.SourceSpan.Length,
                specializedBody + liftedPmi));
        }
        foreach (var change in changes.OrderByDescending(change => change.Start))
            body = body.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return body;
    }
    private static bool HasErrors(IEnumerable<string> diagnostics) => diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal));
    private static int Matching(string source, int open, char begin, char end) { var depth = 0; for (var i = open; i < source.Length; i++) { if (source[i] == begin) depth++; else if (source[i] == end && --depth == 0) return i; } return -1; }
}
