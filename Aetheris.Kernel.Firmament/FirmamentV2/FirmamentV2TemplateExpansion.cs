using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

// AST-owned declaration/application binding. Body slices remain an explicit transport
// boundary to the existing Record/Match/PMI specialization lowerers.
internal sealed record TemplateDeclarationIr(string Name, string TargetKind, string HeaderTail, ImmutableArray<TemplateParameterIr> Parameters, string Body, FirmamentV2SourceSpan SourceSpan)
{
    internal FirmamentTemplateSyntax.Scope? DefinitionScope { get; init; }
    internal FirmamentTemplateSyntax.Scope? BodyScope { get; init; }
    internal List<TemplateApplicationIr> BodyApplications { get; } = [];
    internal string? IdentityNamespace { get; set; }
}
internal abstract record TemplateParameterIr(string Name, FirmamentV2SourceSpan SourceSpan);
internal sealed record TemplateTypeParameterIr(string Name, string ConstraintConcept, FirmamentV2SourceSpan SourceSpan) : TemplateParameterIr(Name, SourceSpan);
internal sealed record TemplateValueParameterIr(string Name, string TypeName, string? DefaultExpression, FirmamentV2SourceSpan SourceSpan) : TemplateParameterIr(Name, SourceSpan);
internal sealed record TemplateApplicationIr(string TargetKind, string InstanceName, string TemplateName, ImmutableArray<TemplateArgumentIr> Arguments, FirmamentV2SourceSpan SourceSpan)
{
    internal TemplateDeclarationIr? BoundTemplate { get; init; }
    internal string? BindingScope { get; init; }
}
internal sealed record TemplateArgumentIr(string Name, string Expression, FirmamentV2SourceSpan SourceSpan);
internal sealed record TemplateRecordTypeIr(string Name, ImmutableDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan);
internal sealed record TemplateStaticRecordIr(string Name, string TypeName, ImmutableDictionary<string, string> Fields, FirmamentV2SourceSpan SourceSpan, string Provenance = "StaticRecord")
{
    internal string? AuthoredName { get; init; }
}
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
        ExportedDeclarations(source, diagnostics)
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
        FirmamentSyntaxParser.Parse(body).Document.Statements.OfType<DeclarationSyntax>()
            .Where(d => d.Type.Name.Text == "Require" && d.Name is not null && d.Initializer is not null && d.Body is null)
            .Select(d => new HostConstraint(d.Name!.Text, body.Substring(d.Initializer!.Span.Start, d.Initializer.Span.Length)))
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
                FlattenRecordFields(record, recordTypes, staticRecords, diagnostics),
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

        var dataBinding = new FirmamentDataBinding(source);
        diagnostics.AddRange(dataBinding.Diagnostics.Select(d => d.Code + ": " + d.Message));
        if (dataBinding.Diagnostics.Count > 0) return null;
        source = dataBinding.Source;
        var syntax = new FirmamentTemplateSyntax(source, diagnostics);
        var declarations = syntax.RemovableDeclarations;
        DetectTemplateCycles(syntax.Declarations, diagnostics);
        var template = syntax.ExportScope.Resolve(templateName);
        if (template is null)
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
            var expression = dataBinding.ResolveIdentity("value", pair.Value.Expression, dataBinding.OriginalSpan(template.SourceSpan).Start);
            if (pair.Value.RecordFields is not null)
            {
                var recordName = $"__forge_{instanceName}_{pair.Key}";
                var recordType = dataBinding.ResolveIdentity("type", pair.Value.RecordType ?? string.Empty, dataBinding.OriginalSpan(template.SourceSpan).Start);
                if (!recordTypes.TryGetValue(recordType, out var recordDefinition))
                {
                    diagnostics.Add(UnknownRecordType + $":{pair.Key}:{recordType}");
                    continue;
                }
                var recordFields = pair.Value.RecordFields.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                ValidateSyntheticRecord(recordName, recordDefinition, recordFields, string.Empty);
                // Reify the checked host view as ordinary nested Records. Whole
                // nested values can then forward through the same Template binder;
                // a flattened type marker is never mistaken for a Static name.
                void AddRecord(string name, TemplateRecordTypeIr schema, string prefix)
                {
                    var fields = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
                    foreach (var field in schema.Fields)
                    {
                        var path = prefix + field.Key;
                        if (!recordFields.TryGetValue(path, out var value)) continue;
                        if (recordTypes.TryGetValue(field.Value, out var nested))
                        {
                            value = name + "_" + field.Key;
                            AddRecord(value, nested, path + ".");
                        }
                        fields[field.Key] = value;
                    }
                    staticRecords[name] = new(name, schema.Name, fields.ToImmutable(), new(source.Length, 0), "HostRecord");
                }
                if (!HasErrors(diagnostics)) AddRecord(recordName, recordDefinition, "");
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
                DisplaySignature(template), diagnostics, out var requireResults, source)) return null;
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
            RequireResults: requireResults)
        { DefinitionScope = template.DefinitionScope?.Identity, BindingScope = "host/document" };
        var nestedInstantiations = new List<ConceptIrTemplateInstantiation>();
        body = ExpandNestedApplications(body, template, source, enums, recordTypes, staticRecords,
            diagnostics, nestedInstantiations, [template]);
        if (HasErrors(diagnostics)) return null;
        var liftedPmi = LiftPmi(body, out body);

        var changes = declarations
            .Select(declaration => (
                Start: declaration.SourceSpan.Start,
                Length: declaration.SourceSpan.Length,
                Text: ReferenceEquals(declaration, template)
                    ? RenderOutput(template.TargetKind, instanceName, template.HeaderTail, body) + liftedPmi
                    : string.Empty))
            .OrderByDescending(change => change.Start)
            .ToArray();
        foreach (var change in changes)
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return HasErrors(diagnostics) ? null : new Result(source, new[] { instantiation }.Concat(nestedInstantiations).ToArray());

        void ValidateSyntheticRecord(string recordName, TemplateRecordTypeIr definition,
            IDictionary<string, string> fields, string prefix)
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
                    if (value == dataBinding.DisplayName(expected.Value)) { fields[path] = expected.Value; value = expected.Value; }
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

    public static Result? Expand(string source, List<string> diagnostics, FirmamentCompileTimeValues? scalars = null, string? originalSource = null)
    {
        var dataBinding = new FirmamentDataBinding(source);
        diagnostics.AddRange(dataBinding.Diagnostics.Select(d => scalars is null ? d.Code + ": " + d.Message : d.Code));
        scalars?.AddExpansionDiagnostics(dataBinding.Diagnostics);
        if (dataBinding.Diagnostics.Count > 0) return null;
        scalars?.UseDataBinding(dataBinding);
        source = dataBinding.Source;
        var syntax = new FirmamentTemplateSyntax(source, diagnostics);
        if (scalars is not null && syntax.Diagnostics.Count > 0)
        {
            var origins = new FirmamentSyntaxOrigins(FirmamentSyntaxParser.Parse(originalSource ?? source).Document,
                FirmamentSyntaxParser.Parse(source).Document);
            scalars.AddExpansionDiagnostics(syntax.Diagnostics.Select(d =>
            {
                var anchor = d.SourceSpan is { } span ? origins.Find(span) : null;
                return d with { SourceSpan = anchor ?? d.SourceSpan, IsOriginalSource = anchor is not null,
                    SourceFile = anchor is null ? d.SourceFile : "inline",
                    LocationNote = anchor is null ? d.LocationNote : "Location identifies the original enclosing Template declaration or application." };
            }));
        }
        var declarations = syntax.RemovableDeclarations;
        DetectTemplateCycles(syntax.Declarations, diagnostics);
        var applications = syntax.Applications;
        if (HasErrors(diagnostics)) return null;

        var catalog = ReadDataCatalog(source, diagnostics, scalars, initialize: true);
        var enums = catalog.Enums;
        var recordTypes = catalog.Types;
        var tables = catalog.Tables;
        var staticRecords = catalog.Records;
        var concepts = new FirmamentConcepts(source, scalars);
        diagnostics.AddRange(concepts.Diagnostics.Select(d => d.Code + ": " + d.Message));
        scalars?.AddConceptDiagnostics(concepts.Diagnostics, producerSource: source);
        if (HasErrors(diagnostics)) return null;
        var changes = declarations.Select(d => (d.SourceSpan.Start, d.SourceSpan.Length, Text: string.Empty)).ToList();
        var instantiations = new List<ConceptIrTemplateInstantiation>();
        foreach (var application in applications)
        {
            var template = application.BoundTemplate!;
            if (!string.Equals(application.TargetKind, template.TargetKind, StringComparison.Ordinal)) { diagnostics.Add(FirmamentV2Parser.UnsupportedConstruct); continue; }
            var bound = Bind(template, application, source, enums, recordTypes, staticRecords, diagnostics, scalars);
            if (bound is null) continue;
            var specialization = new TemplateSpecializationIr(template, application, bound, Identity(template, application, bound, scalars), GeneratedPaths(template.Body, application.InstanceName));
            if (!ValidateRecordMembers(template.Body, bound, diagnostics)) continue;
            if (!EvaluateRequires(Substitute(template.Body, bound, scalars), application.InstanceName,
                    DisplaySignature(template), diagnostics, out var requireResults, source, scalars)) continue;
            var body = ResolveTemplateMatches(template.Body, bound, diagnostics, out var selectedMatches);
            if (body is null) continue;
            body = Substitute(body, bound);
            body = RemoveRequires(body);
            var recordArguments = bound.RecordArguments.ToDictionary(pair => pair.Key, pair => new ConceptIrTemplateRecordArgument(
                pair.Value.TypeName, pair.Value.StaticName, pair.Value.Fields, pair.Value.SourceSpan, pair.Value.Provenance), StringComparer.Ordinal);
            instantiations.Add(new(template.Name, application.InstanceName, bound.TypeArguments, bound.ValueArguments, bound.DefaultedArguments,
                specialization.SpecializationIdentity, specialization.GeneratedDeclarationPaths, template.SourceSpan, application.SourceSpan,
                SelectedMatchArms: selectedMatches, RecordArguments: recordArguments, RequireResults: requireResults)
            { DefinitionScope = template.DefinitionScope?.Identity, BindingScope = application.BindingScope });
            body = ExpandNestedApplications(body, template, source, enums, recordTypes, staticRecords,
                diagnostics, instantiations, [template], scalars);
            var liftedPmi = LiftPmi(body, out body);
            changes.Add((application.SourceSpan.Start, application.SourceSpan.Length, RenderOutput(template.TargetKind, application.InstanceName, template.HeaderTail, body) + liftedPmi));
        }
        if (HasErrors(diagnostics)) return null;
        foreach (var change in changes.OrderByDescending(c => c.Start)) source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        if (instantiations.Count > 0)
        {
            // Concrete instances introduce their own Record/Static/Table scopes.
            // Rebind symbol identities before Feature/default admission, never by
            // sharing the unspecialized definition's value environment.
            var concrete = new FirmamentDataBinding(source);
            diagnostics.AddRange(concrete.Diagnostics.Select(d => scalars is null ? d.Code + ": " + d.Message : d.Code));
            scalars?.AddExpansionDiagnostics(concrete.Diagnostics.Select(d => d with { SourceSpan = null, IsOriginalSource = false,
                LocationNote = "Binding failed in specialized source; an original instance location is unavailable." }));
            if (concrete.Diagnostics.Count > 0) return null;
            source = concrete.Source;
            scalars?.BindExpandedScalars(source, diagnostics);
            _ = ReadDataCatalog(source, diagnostics, scalars);
        }
        if (HasErrors(diagnostics)) return null;
        return new(source, instantiations);
    }

    internal static IReadOnlyList<FirmamentV2SourceSpan> DeclarationSpans(string source, List<string> diagnostics)
        // Boundary owners only need shielding spans. Binding here would discard
        // structured errors before the specialization owner can present them.
        => FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document)
            .Where(d => d.Modifiers.Any(m => m.Name.Text == "Template")).Select(d => d.Span).ToArray();

    private static IEnumerable<TemplateDeclarationIr> ExportedDeclarations(string source, List<string> diagnostics)
    {
        var syntax = new FirmamentTemplateSyntax(source, diagnostics);
        return syntax.Declarations.Where(d => ReferenceEquals(d.DefinitionScope, syntax.ExportScope));
    }

    private static string RenderOutput(string kind, string instance, string tail, string body)
    {
        // Profile guides belong to each specialization; span identities stay shared.
        if (kind == "Profile")
        {
            var names = Regex.Matches(body, @"\b(?:Concept\s+Struct|Curve2|Point2|Line2|CubicBezier2|Circle2|Arc2|Rect2|RoundedRect2|Ellipse2|Path2)\s+(?<name>[A-Za-z_]\w*)\s*\{")
                .Select(m => m.Groups["name"].Value).Distinct(StringComparer.Ordinal).ToArray();
            foreach (var name in names)
                body = Regex.Replace(body, $@"\b{Regex.Escape(name)}\b", instance + "_" + name);
        }
        var prelude = "";
        if (kind == "Profile")
        {
            for (var offset = 0; offset < body.Length;)
            {
                var match = Regex.Match(body[offset..], @"\b(?:Concept\s+Struct|Point2|Line2|CubicBezier2|Circle2|Arc2|Rect2|RoundedRect2|Ellipse2|Path2)\s+[A-Za-z_]\w*\s*\{");
                if (!match.Success) break;
                var start = offset + match.Index; var open = body.IndexOf('{', start); var close = Matching(body, open, '{', '}');
                if (close < 0) break;
                prelude += body[start..(close + 1)] + "\n";
                body = body.Remove(start, close - start + 1); offset = start;
            }
        }
        return prelude + $"{kind} {instance}{tail} {{{body}}}";
    }

    private static BoundTemplateArguments? Bind(TemplateDeclarationIr template, TemplateApplicationIr application, string source, IReadOnlyDictionary<string, ImmutableHashSet<string>> enums,
        IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes, IReadOnlyDictionary<string, TemplateStaticRecordIr> staticRecords, List<string> diagnostics,
        FirmamentCompileTimeValues? scalars = null)
    {
        var supplied = application.Arguments.ToDictionary(a => a.Name, a => a.Expression, StringComparer.Ordinal);
        var concepts = new FirmamentConcepts(source, scalars);
        diagnostics.AddRange(concepts.Diagnostics.Select(d => d.Code + ": " + d.Message));
        scalars?.AddConceptDiagnostics(concepts.Diagnostics, producerSource: source);
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
            if (scalars?.IsScalar(p.TypeName) == true && scalars.TryTransfer(value, p.TypeName,
                    $"Template {template.Name}.{p.Name}", application.SourceSpan, diagnostics, out var checkedValue)) value = checkedValue;
            if (!TypeMatches(value, p.TypeName, enums, scalars: scalars)) { diagnostics.Add((supplied.ContainsKey(p.Name) ? TypeMismatch : BadDefault) + $":{p.Name}:expected-{p.TypeName}:actual-{value}:expected-signature:{expectedSignature}"); return null; }
            values[p.Name] = value;
            if (!supplied.ContainsKey(p.Name)) defaults.Add(p.Name);
            return value;
        }
        foreach (var parameter in template.Parameters)
        {
            if (parameter is TemplateTypeParameterIr type)
            {
                if (!supplied.TryGetValue(type.Name, out var value)) { diagnostics.Add(MissingArgument + ":" + type.Name + ":expected-signature:" + expectedSignature); continue; }
                if (!concepts.HasContract(type.ConstraintConcept)) diagnostics.Add(UnknownConstraint + ":" + type.ConstraintConcept);
                else if (!concepts.Satisfies(value, type.ConstraintConcept))
                {
                    diagnostics.AddRange(concepts.BindingDiagnostics);
                    diagnostics.Add(ConstraintFailure + $":{type.Name}:{value}:{type.ConstraintConcept}:expected-signature:{expectedSignature}");
                    var members = concepts.Contracts.Single(c => c.Name == type.ConstraintConcept).Members.Values;
                    var diagnostic = new FirmamentV2ValidationDiagnostic("firmament-concept-structural-conformance-failed", "error",
                        $"Type '{value}' does not provide Concept '{type.ConstraintConcept}'. Required semantic members: {string.Join(", ", members.Select(m => m.Name + ": " + m.Type))}. A claimed contract must also match the Concept Struct's actual members.", type.Name, value)
                        { SourceSpan = type.SourceSpan, Stage = "Concept binding", LocationNote = "Location identifies the Template type parameter whose supplied type failed conformance." };
                    diagnostics.Add(diagnostic.Code + ": " + diagnostic.Message);
                    scalars?.AddConceptDiagnostics([diagnostic], producerSource: source);
                }
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
            if (staticRecords.TryGetValue(expression, out var staticRecord) || scalars?.TryRecord(expression, out staticRecord) == true)
            {
                if (!string.Equals(staticRecord.TypeName, recordType.Name, StringComparison.Ordinal))
                { diagnostics.Add(WrongRecordType + $":{parameter.Name}:expected-{recordType.Name}:actual-{staticRecord.TypeName}"); return; }
                ImmutableDictionary<string, string> fields;
                if (scalars is not null)
                {
                    if (!scalars.TryRecordTransfer(expression, recordType.Name,
                        $"Template {template.Name}.{parameter.Name}", application.SourceSpan, diagnostics, out var checkedFields)) return;
                    fields = checkedFields.ToImmutableDictionary(StringComparer.Ordinal);
                }
                else fields = FlattenRecordFields(staticRecord, recordTypes, staticRecords, diagnostics);
                records[parameter.Name] = new(parameter.Name, scalars?.DisplayDataName(recordType.Name) ?? recordType.Name,
                    scalars?.DisplayDataName(staticRecord.AuthoredName ?? staticRecord.Name) ?? staticRecord.AuthoredName ?? staticRecord.Name,
                    fields, staticRecord.SourceSpan, staticRecord.Provenance);
                values[parameter.Name] = scalars?.RecordReference(staticRecord, fields) ?? staticRecord.Name;
                return;
            }
            if (Regex.IsMatch(source, $@"\b(?:Struct|Model)\s+{Regex.Escape(expression)}\b", RegexOptions.CultureInvariant))
            { diagnostics.Add(MaterializedRecordArgument + $":{parameter.Name}:{expression}"); return; }
            if (Regex.IsMatch(expression, @"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant)) diagnostics.Add(UnknownStaticRecord + $":{parameter.Name}:{expression}");
            else diagnostics.Add(UnsupportedRecordValue + $":{parameter.Name}:{expression}");
        }
    }

    internal static ImmutableDictionary<string, string> FlattenRecordFields(TemplateStaticRecordIr record,
        IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes, IReadOnlyDictionary<string, TemplateStaticRecordIr> staticRecords,
        List<string> diagnostics)
    {
        // A checked transport view is already flat. Re-expanding its source Record
        // would overwrite the parameter receipts on its nested leaf values.
        if (record.AuthoredName is not null) return record.Fields;
        var result = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        void Add(TemplateStaticRecordIr value, string prefix)
        {
            if (!visiting.Add(value.Name)) { diagnostics.Add(Prefix + "record-cycle:" + value.Name); return; }
            if (visiting.Count > 128) { diagnostics.Add(Prefix + "record-depth-limit:" + value.Name); visiting.Remove(value.Name); return; }
            foreach (var field in value.Fields)
            {
                var path = prefix + field.Key;
                result[path] = field.Value;
                if (recordTypes[value.TypeName].Fields.TryGetValue(field.Key, out var type)
                    && recordTypes.ContainsKey(type) && staticRecords.TryGetValue(field.Value, out var nested)) Add(nested, path + ".");
            }
            visiting.Remove(value.Name);
        }
        Add(record, string.Empty);
        return result.ToImmutable();
    }

    internal static IReadOnlyDictionary<string, string>? BindRecordExpression(string source, string type,
        string expression, string name, List<string> diagnostics, bool hostView = false)
    {
        // The synthetic declaration is a transport into the existing Record owner,
        // not a second schema or expression evaluator. Authored environments remain lexical.
        var before = diagnostics.Count;
        var input = source + "\nStatic " + name + ": " + type + " = " + expression + ";";
        var scalars = new FirmamentCompileTimeValues(input);
        var catalog = ReadDataCatalog(input, diagnostics, scalars, initialize: true);
        if (diagnostics.Count > before) return null;
        if (!catalog.Records.TryGetValue(name, out var record))
        { diagnostics.Add(UnknownStaticRecord + ":" + name + ":" + type); return null; }
        if (!scalars.TryRecordTransfer(name, type, name, record.SourceSpan, diagnostics, out var fields)) return null;
        if (!hostView) return fields;
        var transported = fields.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var field in fields)
            if (catalog.Records.TryGetValue(field.Value, out var nested)) transported[field.Key] = nested.TypeName;
        return transported;
    }

    private static IReadOnlyDictionary<string, ImmutableHashSet<string>> ParseEnums(string source) => Regex.Matches(source, @"\bEnum\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant).Cast<Match>().ToDictionary(
        m => m.Groups["name"].Value,
        m => Regex.Matches(m.Groups["body"].Value, @"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant).Select(v => v.Value).ToImmutableHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    private static bool MatchesCoordinates(string value, string type)
    {
        if (FirmamentSyntaxParser.ParseExpression(value).Expression is not CallSyntax { Target: ReferenceSyntax target } call
            || target.Name.Text != type || call.Arguments.Count != (type == "Point3" ? 3 : 2)) return false;
        return call.Arguments.All(a => a.Name is null && FirmamentNumericEvaluation.TryEvaluate(a.Value, out var coordinate)
            && coordinate.Unit == (type == "Vector2" ? "" : "mm") && double.IsFinite(coordinate.Number));
    }

    private static bool TypeMatches(string value, string type, IReadOnlyDictionary<string, ImmutableHashSet<string>> enums,
        IReadOnlyDictionary<string, TemplateRecordTypeIr>? recordTypes = null, IReadOnlyDictionary<string, TemplateStaticRecordIr>? staticRecords = null,
        FirmamentCompileTimeValues? scalars = null) => scalars?.Contains(value) == true ? scalars.HasType(value, type) : type switch
    {
        "Point2" or "Point3" or "Vector2" => MatchesCoordinates(value, type),
        "Time" => FirmamentNumericEvaluation.TryEvaluate(value, out var duration) && duration.Unit == "s",
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
        type is "Point3" or "Length" or "Angle" or "String" or "Version" or "Date" or "ImportedStep" or "ProfilePath" or "Int" or "Float" or "Bool" or "int" or "float" or "bool" || enums.ContainsKey(type);

    internal sealed record DataCatalog(IReadOnlyDictionary<string, TemplateRecordTypeIr> Types,
        IReadOnlyDictionary<string, TemplateStaticTableIr> Tables, IReadOnlyDictionary<string, TemplateStaticRecordIr> Records,
        IReadOnlyDictionary<string, ImmutableHashSet<string>> Enums);

    internal static DataCatalog ReadDataCatalog(string source, List<string> diagnostics, FirmamentCompileTimeValues? scalars = null, bool initialize = false)
    {
        var enums = ParseEnums(source);
        var types = ParseRecordTypes(source, diagnostics);
        if (initialize) scalars?.Initialize(types, diagnostics);
        var tables = ParseStaticTables(source, types, enums, diagnostics, scalars);
        if (HasErrors(diagnostics)) return new(types, tables, new Dictionary<string, TemplateStaticRecordIr>(), enums);
        var records = ParseStaticRecords(source, types, enums, tables, diagnostics, scalars);
        scalars?.SetRecords(types, records);
        return new(types, tables, records, enums);
    }

    internal static IEnumerable<DeclarationSyntax> DataDeclarations(string source)
    {
        IEnumerable<DeclarationSyntax> Visit(IEnumerable<StatementSyntax> statements)
        {
            foreach (var declaration in statements.OfType<DeclarationSyntax>())
            {
                if (declaration.Modifiers.Any(m => m.Name.Text == "Template") || declaration.Type.Name.Text is "Template" or "Feature") continue;
                yield return declaration;
                if (declaration.Body is not null && declaration.Type.Name.Text is not ("Record" or "Enum" or "Static" or "Table"))
                    foreach (var nested in Visit(declaration.Body.Statements)) yield return nested;
            }
        }
        return Visit(FirmamentSyntaxParser.Parse(source).Document.Statements);
    }

    private static IReadOnlyDictionary<string, TemplateRecordTypeIr> ParseRecordTypes(string source, List<string> diagnostics)
    {
        var result = new Dictionary<string, TemplateRecordTypeIr>(StringComparer.Ordinal);
        foreach (var declaration in DataDeclarations(source).Where(d => d.Type.Name.Text == "Record" && d.Name is not null && d.Body is not null))
        {
            var fields = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (var statement in declaration.Body!.Statements)
            {
                if (statement is not FieldSyntax field) { diagnostics.Add(Prefix + "record-field-declaration:" + declaration.Name!.Text); continue; }
                var type = FirmamentSyntaxValues.Reference(field.Value) ?? source.Substring(field.Value.Span.Start, field.Value.Span.Length);
                if (!fields.TryAdd(field.Name.Text, type)) diagnostics.Add(Prefix + "record-duplicate-field:" + field.Name.Text);
            }
            if (!result.TryAdd(declaration.Name!.Text, new(declaration.Name.Text, fields.ToImmutable(), declaration.Span)))
                diagnostics.Add(Prefix + "record-duplicate-name:" + declaration.Name.Text);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, TemplateStaticTableIr> ParseStaticTables(string source, IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes,
        IReadOnlyDictionary<string, ImmutableHashSet<string>> enums, List<string> diagnostics, FirmamentCompileTimeValues? scalars = null)
    {
        var tables = new Dictionary<string, TemplateStaticTableIr>(StringComparer.Ordinal);
        foreach (var declaration in DataDeclarations(source).Where(d => d.Type.Name.Text == "Table" && d.Modifiers.Any(m => m.Name.Text == "Static") && d.Name is not null))
        {
            var name = declaration.Name!.Text;
            var type = FirmamentSyntaxValues.Reference(declaration.Annotation) ?? "";
            var key = declaration.Clauses.FirstOrDefault(c => c.Kind == ClauseKind.Key) is { } clause ? FirmamentSyntaxValues.Reference(clause.Value) : null;
            if (declaration.Body is null || !recordTypes.TryGetValue(type, out var rowType)) { diagnostics.Add(TableUnknownRowType + ":" + name + ":" + type); continue; }
            var columns = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(StringComparer.Ordinal);
            foreach (var statement in declaration.Body.Statements)
            {
                if (statement is not FieldSyntax { Value: SequenceSyntax { Kind: SequenceKind.List } values } column)
                { diagnostics.Add(TableUnknownColumn + ":" + name + ":invalid-column"); continue; }
                if (columns.ContainsKey(column.Name.Text)) { diagnostics.Add(TableDuplicateKey + ":" + name + ":duplicate-column-" + column.Name.Text); continue; }
                if (values.Items.Any(a => FirmamentSyntaxTraversal.Expressions(a.Value).OfType<ToleranceSyntax>().Any()))
                    diagnostics.Add(FirmamentV2Parser.ToleranceUnsupported + ":Table:" + name + "." + column.Name.Text);
                columns[column.Name.Text] = values.Items.Select(a => source.Substring(a.Value.Span.Start, a.Value.Span.Length)).ToImmutableArray();
            }
            foreach (var field in rowType.Fields.Keys.Where(field => !columns.ContainsKey(field))) diagnostics.Add(TableMissingColumn + ":" + name + ":" + field);
            foreach (var column in columns.Keys.Where(column => !rowType.Fields.ContainsKey(column))) diagnostics.Add(TableUnknownColumn + ":" + name + ":" + column);
            foreach (var column in columns)
                if (rowType.Fields.TryGetValue(column.Key, out var expected))
                    foreach (var value in column.Value.Where(value => !TypeMatches(value, expected, enums, recordTypes, null, scalars))) diagnostics.Add(TableColumnTypeMismatch + ":" + name + "." + column.Key + ":expected-" + expected + ":actual-" + value);
            var lengths = columns.Values.Select(values => values.Length).Distinct().ToArray();
            if (lengths.Length > 1) diagnostics.Add(TableUnequalColumns + ":" + name);
            if (key is not null)
            {
                if (!rowType.Fields.TryGetValue(key, out var keyType) || !columns.ContainsKey(key)) diagnostics.Add(TableMissingKey + ":" + name + ":" + key);
                else if (keyType is not ("String" or "int") && !enums.ContainsKey(keyType)) diagnostics.Add(TableInvalidKey + ":" + name + ":" + keyType);
                else if (columns[key].Distinct(StringComparer.Ordinal).Count() != columns[key].Length) diagnostics.Add(TableDuplicateKey + ":" + name + ":" + key);
            }
            tables[name] = new(name, type, key, columns.ToImmutable(), declaration.Span);
        }
        return tables;
    }

    private static IReadOnlyDictionary<string, TemplateStaticRecordIr> ParseStaticRecords(string source, IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes,
        IReadOnlyDictionary<string, ImmutableHashSet<string>> enums, IReadOnlyDictionary<string, TemplateStaticTableIr> tables, List<string> diagnostics,
        FirmamentCompileTimeValues? scalars = null)
    {
        var result = new Dictionary<string, TemplateStaticRecordIr>(StringComparer.Ordinal);
        var pending = DataDeclarations(source).Where(d => d.Type.Name.Text == "Static" && d.Name is not null
            && (d.Body is not null && recordTypes.ContainsKey(FirmamentSyntaxValues.Reference(d.Annotation) ?? "")
                || d.Initializer is ObjectSyntax or WithSyntax
                || d.Initializer is IndexSyntax { Target: ReferenceSyntax target } && tables.ContainsKey(target.Name.Text)
                || d.Initializer is ReferenceSyntax && recordTypes.ContainsKey(FirmamentSyntaxValues.Reference(d.Annotation) ?? ""))).ToList();
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var declaration in pending.ToArray())
            {
                var name = declaration.Name!.Text;
                var annotatedType = FirmamentSyntaxValues.Reference(declaration.Annotation);
                if (declaration.Initializer is WithSyntax with)
                {
                    var baseName = FirmamentSyntaxValues.Reference(with.Target) ?? "";
                    if (!result.TryGetValue(baseName, out var parent)) continue;
                    var type = annotatedType ?? parent.TypeName;
                    if (type != parent.TypeName) diagnostics.Add(WrongRecordType + ":" + name + ":expected-" + type + ":actual-" + parent.TypeName);
                    else
                    {
                        var schema = recordTypes[type];
                        foreach (var duplicate in with.Body.Statements.OfType<FieldSyntax>().GroupBy(f => f.Name.Text).Where(g => g.Count() > 1))
                            diagnostics.Add(WithDuplicateField + ":" + name + ":" + duplicate.Key);
                        var fields = Fields(with.Body, schema, name);
                        foreach (var extra in fields.Keys.Where(f => !schema.Fields.ContainsKey(f))) diagnostics.Add(WithUnknownField + ":" + name + ":" + extra);
                        Check(fields, schema, name, WithTypeMismatch);
                        result[name] = new(name, type, parent.Fields.SetItems(fields), declaration.Span,
                            parent.Provenance + "; derivedFrom:" + baseName + "; overrides:" + string.Join(",", fields.Keys.OrderBy(k => k, StringComparer.Ordinal)));
                    }
                }
                else if (declaration.Initializer is IndexSyntax index)
                {
                    var tableName = FirmamentSyntaxValues.Reference(index.Target) ?? "";
                    if (!tables.TryGetValue(tableName, out var table)) continue;
                    var lookup = index.Index is null ? "" : Text(index.Index.Span);
                    if (scalars is not null && scalars.TryValue(lookup, out var lookupValue)) lookup = scalars.NominalExpression(lookupValue.Name);
                    var row = -1;
                    if (int.TryParse(lookup, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) row = number;
                    else if (table.KeyField is null) diagnostics.Add(TableLookupTypeMismatch + ":" + table.Name + ":index-required");
                    else
                    {
                        var keyType = recordTypes[table.RowType].Fields[table.KeyField];
                        if (!TypeMatches(lookup, keyType, enums, recordTypes, result, scalars)) diagnostics.Add(TableLookupTypeMismatch + ":" + table.Name + ":expected-" + keyType + ":actual-" + lookup);
                        else row = table.Columns[table.KeyField].IndexOf(lookup);
                        if (row < 0) diagnostics.Add(TableKeyNotFound + ":" + table.Name + ":" + lookup);
                    }
                    var rowCount = table.Columns.Count == 0 ? 0 : table.Columns.First().Value.Length;
                    if (row < 0 || row >= rowCount)
                    {
                        if (int.TryParse(lookup, out _)) diagnostics.Add(TableIndexOutOfRange + ":" + table.Name + ":" + lookup);
                    }
                    else if (annotatedType is not null && annotatedType != table.RowType)
                        diagnostics.Add(WrongRecordType + ":" + name + ":expected-" + annotatedType + ":actual-" + table.RowType);
                    else
                    {
                        var fields = table.Columns.ToDictionary(c => c.Key, c => c.Value[row], StringComparer.Ordinal);
                        foreach (var field in fields.ToArray())
                            if (scalars is not null && recordTypes[table.RowType].Fields.TryGetValue(field.Key, out var fieldType))
                                fields[field.Key] = scalars.Field(name, field.Key, field.Value, fieldType, declaration.Span, diagnostics);
                        var keyDetail = table.KeyField is null ? "" : " key:" + table.Columns[table.KeyField][row];
                        result[name] = new(name, table.RowType, fields.ToImmutableDictionary(StringComparer.Ordinal), declaration.Span, "Table:" + table.Name + " row:" + row + keyDetail);
                    }
                }
                else if (declaration.Initializer is ReferenceSyntax reference)
                {
                    if (!result.TryGetValue(reference.Name.Text, out var parent)) continue;
                    if (annotatedType != parent.TypeName) diagnostics.Add(WrongRecordType + ":" + name + ":expected-" + annotatedType + ":actual-" + parent.TypeName);
                    else result[name] = parent with { Name = name, SourceSpan = declaration.Span, Provenance = parent.Provenance + "; derivedFrom:" + parent.Name };
                }
                else
                {
                    var literal = declaration.Initializer as ObjectSyntax;
                    var type = annotatedType ?? FirmamentSyntaxValues.Reference(literal?.Type);
                    if (type is null || !recordTypes.TryGetValue(type, out var schema)) { pending.Remove(declaration); progressed = true; continue; }
                    var literalType = FirmamentSyntaxValues.Reference(literal?.Type) ?? type;
                    if (literalType != type) diagnostics.Add(WrongRecordType + ":" + name + ":expected-" + type + ":actual-" + literalType);
                    var body = literal?.Body ?? declaration.Body!;
                    var fields = Fields(body, schema, name);
                    foreach (var missing in schema.Fields.Keys.Where(f => !fields.ContainsKey(f))) diagnostics.Add(RecordMissingField + ":" + name + ":" + missing);
                    foreach (var extra in fields.Keys.Where(f => !schema.Fields.ContainsKey(f))) diagnostics.Add(RecordExtraField + ":" + name + ":" + extra);
                    Check(fields, schema, name, RecordFieldTypeMismatch, scalarOnly: true);
                    result[name] = new(name, type, fields.ToImmutableDictionary(StringComparer.Ordinal), declaration.Span);
                }
                pending.Remove(declaration); progressed = true;
            }
            if (progressed) continue;
            foreach (var declaration in pending)
                diagnostics.Add(WithBaseNotRecord + ":" + declaration.Name!.Text + ":" + (declaration.Initializer is WithSyntax w ? FirmamentSyntaxValues.Reference(w.Target) : FirmamentSyntaxValues.Reference(declaration.Initializer)));
            break;
        }
        foreach (var record in result.Values.ToArray())
        {
            var origins = record.Fields.Values.Where(result.ContainsKey).Select(v => result[v].Provenance).Where(v => v != record.Provenance).Distinct(StringComparer.Ordinal).ToArray();
            if (origins.Length > 0 && !record.Provenance.Contains("origin:", StringComparison.Ordinal)) result[record.Name] = record with { Provenance = record.Provenance + "; origin:" + string.Join("|", origins) };
        }
        foreach (var record in result.Values)
        {
            Check(record.Fields, recordTypes[record.TypeName], record.Name, RecordFieldTypeMismatch);
            var before = diagnostics.Count;
            _ = FlattenRecordFields(record, recordTypes, result, diagnostics);
            foreach (var code in diagnostics.Skip(before).ToArray()) scalars?.RecordError(record.Name, "", code,
                $"Record '{record.Name}' contains a cyclic reference or exceeds the supported nesting depth of 128. Use finite, acyclic Record data.");
        }
        return result;

        string Text(FirmamentV2SourceSpan span) => source.Substring(span.Start, span.Length);
        Dictionary<string, string> Fields(BlockSyntax body, TemplateRecordTypeIr schema, string name)
        {
            var fields = ParseRecordFields(Text(body.ContentSpan), schema.Fields, diagnostics, scalars, name);
            foreach (var field in fields.ToArray())
                if (schema.Fields.TryGetValue(field.Key, out var type) && scalars is not null)
                    fields[field.Key] = scalars.Field(name, field.Key, field.Value, type, body.Span, diagnostics);
            return fields;
        }
        void Check(IReadOnlyDictionary<string, string> fields, TemplateRecordTypeIr schema, string name, string code, bool scalarOnly = false)
        {
            foreach (var field in fields)
                if (schema.Fields.TryGetValue(field.Key, out var type) && (!scalarOnly || !recordTypes.ContainsKey(type))
                    && !TypeMatches(field.Value, type, enums, recordTypes, result, scalars))
                    diagnostics.Add(code + ":" + name + "." + field.Key + ":expected-" + type + ":actual-" + field.Value);
        }
    }

    private static Dictionary<string, string> ParseRecordFields(string body, IReadOnlyDictionary<string, string> schema, List<string> diagnostics,
        FirmamentCompileTimeValues? scalars, string recordName)
    {
        const string prefix = "Struct Fields {\n";
        var text = prefix + body + "\n}";
        var syntax = FirmamentSyntaxParser.Parse(text);
        var fields = syntax.Document.Statements.OfType<DeclarationSyntax>().SelectMany(d => d.Body?.Statements ?? []).OfType<FieldSyntax>().ToArray();
        if (syntax.Document.Statements.OfType<DeclarationSyntax>().SelectMany(d => d.Body?.Statements ?? []).Any(s => s is not FieldSyntax))
            diagnostics.Add(Prefix + "record-field-declaration:" + recordName);
        foreach (var duplicate in fields.GroupBy(f => f.Name.Text, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var code = Prefix + "record-duplicate-field:" + duplicate.Key;
            diagnostics.Add(code);
            scalars?.RecordError(recordName, duplicate.Key, code,
                $"Record '{recordName}' supplies field '{duplicate.Key}' more than once. Supply it once, or derive a new Record with a checked with override.");
        }
        var result = fields.GroupBy(f => f.Name.Text, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => text.Substring(g.Last().Value.Span.Start, g.Last().Value.Span.Length), StringComparer.Ordinal);
        // Version/Date are established domain symbols, not decimal arithmetic. Keep their
        // complete token spelling using the Record owner's existing field type metadata.
        var tokens = FirmamentLanguageAnalysisService.Lex(body).Where(t => t.Shape != "comment").ToArray();
        var depth = 0;
        for (var i = 0; i + 2 < tokens.Length; i++)
        {
            if (tokens[i].Text is "(" or "[" or "{") depth++;
            if (tokens[i].Text is ")" or "]" or "}") depth--;
            if (depth != 0 || tokens[i + 1].Text != ":" || !schema.TryGetValue(tokens[i].Text, out var type) || type is not ("Version" or "Date")) continue;
            var end = i + 2;
            while (end < tokens.Length && tokens[end].Text != ";" && !(end + 1 < tokens.Length && tokens[end + 1].Text == ":")) end++;
            var finish = end == tokens.Length ? body.Length : tokens[end].Start;
            result[tokens[i].Text] = body[tokens[i + 2].Start..finish].Trim();
        }
        return result;
    }
    private static string Substitute(string body, BoundTemplateArguments bound, FirmamentCompileTimeValues? nominalScalars = null)
    {
        var values = bound.TypeArguments.Concat(bound.ValueArguments)
            .ToDictionary(p => p.Key, p => nominalScalars?.NominalExpression(p.Value) ?? p.Value, StringComparer.Ordinal);
        foreach (var record in bound.RecordArguments.Values)
            foreach (var field in record.Fields) values[record.Parameter + "." + field.Key] = nominalScalars?.NominalExpression(field.Value) ?? field.Value;
        return FirmamentValueSubstitution.Apply(body, values);
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
    private static bool EvaluateRequires(string body, string instance, string signature, List<string> diagnostics,
        out IReadOnlyDictionary<string, string> results, string moduleSource, FirmamentCompileTimeValues? scalars = null)
    {
        var evidence = new Dictionary<string, string>(StringComparer.Ordinal);
        var concepts = new FirmamentConcepts(moduleSource, scalars);
        var templateName = signature.Split('<')[0];
        var original = FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(moduleSource).Document)
            .FirstOrDefault(d => d.Name?.Text == templateName && d.Modifiers.Any(m => m.Name.Text == "Template"));
        foreach (var require in FirmamentSyntaxParser.Parse(body).Document.Statements.OfType<DeclarationSyntax>()
            .Where(d => d.Type.Name.Text == "Require" && d.Body is null))
        {
            var name = require.Name?.Text ?? "<unnamed>";
            var expression = require.Initializer is null ? "" : body.Substring(require.Initializer.Span.Start, require.Initializer.Span.Length);
            var location = original?.Body?.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Type.Name.Text == "Require" && d.Name?.Text == name)?.Span;
            if (require.Initializer is null || require.Body is not null || require.Parameters.Count > 0)
                diagnostics.Add(RequireNonBool + ":" + name + ":template-signature:" + signature);
            else if (!concepts.Check(require.Initializer, instance + "." + name, location))
            {
                var failed = concepts.Diagnostics.LastOrDefault()?.Code == "firmament-concept-requirement-failed";
                diagnostics.Add(failed ? RequireFailed + $":{instance}.{name}:{expression}:template-signature:{signature}"
                    : RequireNonBool + ":" + name + ":template-signature:" + signature);
                evidence[name] = (failed ? "Failed:" : "Invalid:") + expression;
            }
            else evidence[name] = "Passed:" + expression;
        }
        diagnostics.AddRange(concepts.Diagnostics.Select(d => d.Code + ": " + d.Message));
        scalars?.AddConceptDiagnostics(concepts.Diagnostics, producerSource: moduleSource);
        results = evidence;
        return !HasErrors(diagnostics);
    }
    private static void DetectTemplateCycles(IEnumerable<TemplateDeclarationIr> templates, List<string> diagnostics)
    {
        var complete = new HashSet<TemplateDeclarationIr>(ReferenceEqualityComparer.Instance);
        var stack = new List<TemplateDeclarationIr>();
        void Visit(TemplateDeclarationIr template)
        {
            var at = stack.FindIndex(item => ReferenceEquals(item, template));
            if (at >= 0)
            {
                diagnostics.Add(Recursive + ":" + string.Join(" -> ", stack.Skip(at).Append(template).Select(item => item.Name)));
                return;
            }
            if (complete.Contains(template)) return;
            if (stack.Count >= 64) { diagnostics.Add(Prefix + "depth-limit:" + template.Name); return; }
            stack.Add(template);
            foreach (var application in template.BodyApplications) Visit(application.BoundTemplate!);
            stack.RemoveAt(stack.Count - 1);
            complete.Add(template);
        }
        foreach (var template in templates) Visit(template);
    }
    private static string Identity(TemplateDeclarationIr template, TemplateApplicationIr application, BoundTemplateArguments args, FirmamentCompileTimeValues? scalars = null)
    {
        string Value(string value) => scalars?.NominalExpression(value) ?? value;
        var recordInput = args.RecordArguments.OrderBy(x => x.Key).SelectMany(x => x.Value.Fields.OrderBy(f => f.Key).Select(f => x.Key + "." + f.Key + "=" + Value(f.Value)));
        var input = (template.IdentityNamespace is null ? template.Name : template.IdentityNamespace + "/" + template.Name) + "|" + application.InstanceName + "|" + string.Join("|", args.TypeArguments.Concat(args.ValueArguments).OrderBy(x => x.Key).Select(x => x.Key + "=" + Value(x.Value)).Concat(recordInput));
        return "template:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..16];
    }
    private static string DisplaySignature(TemplateDeclarationIr template) => template.Name + "<" + string.Join(", ", template.Parameters.Select(parameter => parameter switch
    {
        TemplateTypeParameterIr type => $"type {type.Name} satisfies {type.ConstraintConcept}",
        TemplateValueParameterIr value => $"{value.Name}: {value.TypeName}" + (value.DefaultExpression is null ? string.Empty : $" = {value.DefaultExpression}"),
        _ => parameter.Name,
    })) + ">";
    private static ImmutableArray<string> GeneratedPaths(string body, string instance) => Regex.Matches(body, @"\b(?:Concept\s+Struct|Box|Modify|EdgeFinish)\s+(?<n>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Select(m => instance + "::" + m.Groups["n"].Value).Distinct().ToImmutableArray();
    private static string RemoveRequires(string body)
    {
        var spans = FirmamentSyntaxParser.Parse(body).Document.Statements.OfType<DeclarationSyntax>()
            .Where(d => d.Type.Name.Text == "Require" && d.Body is null).Select(d => d.Span).OrderByDescending(s => s.Start);
        foreach (var span in spans) body = body.Remove(span.Start, span.Length);
        return body;
    }
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
        TemplateDeclarationIr enclosingTemplate,
        string moduleSource,
        IReadOnlyDictionary<string, ImmutableHashSet<string>> enums,
        IReadOnlyDictionary<string, TemplateRecordTypeIr> recordTypes,
        IReadOnlyDictionary<string, TemplateStaticRecordIr> staticRecords,
        List<string> diagnostics,
        ICollection<ConceptIrTemplateInstantiation> instantiations,
        IReadOnlyList<TemplateDeclarationIr> stack, FirmamentCompileTimeValues? scalars = null)
    {
        if (stack.Count > 64) { diagnostics.Add(Prefix + "depth-limit:" + enclosingTemplate.Name); return body; }
        var syntax = new FirmamentTemplateSyntax(body, diagnostics, enclosingTemplate.BodyScope);
        var applications = syntax.Applications;
        var changes = syntax.RemovableDeclarations.Select(d => (Start: d.SourceSpan.Start, Length: d.SourceSpan.Length, Text: string.Empty)).ToList();
        foreach (var application in applications)
        {
            var template = application.BoundTemplate!;
            if (stack.Any(item => ReferenceEquals(item, template)))
            {
                diagnostics.Add(Recursive + ":" + string.Join(" -> ", stack.Append(template).Select(item => item.Name)));
                continue;
            }
            var bound = Bind(template, application, moduleSource, enums, recordTypes, staticRecords, diagnostics, scalars);
            if (bound is null) continue;
            if (!ValidateRecordMembers(template.Body, bound, diagnostics)) continue;
            if (!EvaluateRequires(Substitute(template.Body, bound, scalars), application.InstanceName,
                    DisplaySignature(template), diagnostics, out var requireResults, moduleSource, scalars)) continue;
            var specializedBody = ResolveTemplateMatches(template.Body, bound, diagnostics, out var selectedMatches);
            if (specializedBody is null) continue;
            specializedBody = Substitute(specializedBody, bound);
            specializedBody = RemoveRequires(specializedBody);
            var specialization = new TemplateSpecializationIr(template, application, bound,
                Identity(template, application, bound, scalars), GeneratedPaths(template.Body, application.InstanceName));
            var recordArguments = bound.RecordArguments.ToDictionary(pair => pair.Key, pair => new ConceptIrTemplateRecordArgument(
                pair.Value.TypeName, pair.Value.StaticName, pair.Value.Fields, pair.Value.SourceSpan, pair.Value.Provenance), StringComparer.Ordinal);
            instantiations.Add(new(template.Name, application.InstanceName, bound.TypeArguments, bound.ValueArguments,
                bound.DefaultedArguments, specialization.SpecializationIdentity, specialization.GeneratedDeclarationPaths,
                template.SourceSpan, application.SourceSpan, SelectedMatchArms: selectedMatches,
                RecordArguments: recordArguments, RequireResults: requireResults)
            { DefinitionScope = template.DefinitionScope?.Identity, BindingScope = application.BindingScope });
            specializedBody = ExpandNestedApplications(specializedBody, template, moduleSource, enums, recordTypes,
                staticRecords, diagnostics, instantiations, stack.Append(template).ToArray(), scalars);
            var liftedPmi = LiftPmi(specializedBody, out specializedBody);
            // Nested finite specializations flatten into the containing declaration before
            // Feature AIR. The instantiation record above preserves the semantic boundary;
            // material grammar receives concrete declarations without unsupported nested Structs.
            changes.Add((application.SourceSpan.Start, application.SourceSpan.Length,
                (template.TargetKind == "Profile" ? RenderOutput("Profile", application.InstanceName, template.HeaderTail, specializedBody) : specializedBody) + liftedPmi));
        }
        foreach (var change in changes.OrderByDescending(change => change.Start))
            body = body.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return body;
    }
    private static bool HasErrors(IEnumerable<string> diagnostics) => diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal));
    private static int Matching(string source, int open, char begin, char end) { var depth = 0; for (var i = open; i < source.Length; i++) { if (source[i] == begin) depth++; else if (source[i] == end && --depth == 0) return i; } return -1; }
}
