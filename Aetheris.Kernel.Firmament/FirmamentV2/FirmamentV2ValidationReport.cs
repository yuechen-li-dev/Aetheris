using System.Globalization;
using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2ValidationReport(
    string Source,
    string Status,
    FirmamentV2ForgeRuntimeMetadata ForgeRuntime,
    IReadOnlyList<FirmamentV2ValidationLet> Lets,
    IReadOnlyList<FirmamentV2ValidationConcept> Concepts,
    IReadOnlyList<FirmamentV2ValidationPmiRecord> Pmi,
    IReadOnlyList<FirmamentV2ValidationConceptPmiObligation> ConceptPmiObligations,
    FirmamentV2ValidationExportSupport ExportSupport,
    IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics,
    FirmamentV2ValidationSummary Summary);

public sealed record FirmamentV2ValidationSummary(int LetCount, int TolerancedLetCount, int ConceptCount, int ValidConceptCount, int PmiRecordCount, int ExportSupportedPmiCount, int ExportDeferredPmiCount, int PmiObligationCount, int SatisfiedPmiObligationCount, int MissingPmiObligationCount, int FatalDiagnosticCount, int WarningDiagnosticCount);
public sealed record FirmamentV2ValidationDiagnostic(string Code, string Severity, string Message, string? FieldName = null, string? Target = null);
public sealed record FirmamentV2ValidationExportSupport(int SupportedPmiCount, int DeferredPmiCount, IReadOnlyDictionary<string, string> Matrix);
public sealed record FirmamentV2ValidationLet(string Name, string Type, string Nominal, FirmamentV2ValidationTolerance? Tolerance, string Source, IReadOnlyList<string> Dependencies, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);
public sealed record FirmamentV2ValidationTolerance(string Kind, string Plus, string Minus);
public sealed record FirmamentV2ValidationConceptRuntimeValidation(string Provider, string Status, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);
public sealed record FirmamentV2ValidationConcept(string Kind, string? Name, string Family, string Concept, string Status, string DfmStatus, IReadOnlyList<FirmamentV2ValidationConceptField> Fields, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics, FirmamentV2ValidationConceptRuntimeValidation? RuntimeValidation = null);
public sealed record FirmamentV2ValidationConceptField(string Name, string Type, bool HasTolerance, string Source);
public sealed record FirmamentV2ValidationPmiRecord(string Kind, string Name, string Status, string ExportSupport, string? Target, IReadOnlyList<string> Targets, IReadOnlyList<string> DatumRefs, FirmamentV2ValidationDimension? Dimension, string? Reason, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics, string? ProjectionSource = null, string? SourceConstraintKind = null, string? SourceSubject = null, string? ValidationStatus = null, string? Provenance = null);
public sealed record FirmamentV2ValidationDimension(string Nominal, FirmamentV2ValidationTolerance? Tolerance);
public sealed record FirmamentV2ValidationConceptPmiObligation(string Kind, string SourceConcept, string? SourceName, string? Target, string? ExpectedDimensionField, string Status, string Severity, string? MatchedPmi = null, string? DiagnosticCode = null);

public static class FirmamentV2ValidationReportBuilder
{
    public static FirmamentV2ValidationReport Build(
        FirmamentV2ParseResult parse,
        string source = "inline",
        FirmamentV2RuntimeConceptValidationResult? runtimeValidation = null,
        FirmamentV2ForgeConceptCatalog? conceptCatalog = null)
    {
        runtimeValidation ??= FirmamentV2RuntimeConceptValidation.Validate(parse.Document);
        conceptCatalog ??= FirmamentV2ForgeConceptRegistry.Catalog;
        var diagnostics = parse.Diagnostics
            .Distinct(StringComparer.Ordinal)
            .Where(code => !IsParserTraceDiagnostic(code))
            .Select(ToParserDiagnostic)
            .Concat(runtimeValidation.Diagnostics.Select(ToRuntimeDiagnostic))
            .Distinct()
            .ToArray();

        var fatalCount = diagnostics.Count(diagnostic => diagnostic.Severity == "fatal");
        var warningCount = diagnostics.Count(diagnostic => diagnostic.Severity == "warning");
        var document = parse.Document;
        var lets = document is null ? [] : BuildLets(document);
        var concepts = document is null ? [] : BuildConcepts(document, runtimeValidation, conceptCatalog);
        var pmi = document is null ? [] : BuildPmi(document, diagnostics);
        var conceptPmiEvaluation = document is null
            ? new FirmamentV2ConceptPmiObligationEvaluation([], [])
            : BuildConceptPmiObligations(document, runtimeValidation);
        diagnostics = diagnostics
            .Concat(conceptPmiEvaluation.Diagnostics)
            .Distinct()
            .ToArray();
        fatalCount = diagnostics.Count(diagnostic => diagnostic.Severity == "fatal");
        warningCount = diagnostics.Count(diagnostic => diagnostic.Severity == "warning");
        var supported = pmi.Count(record => record.ExportSupport is "supported" or "supported-when-target-resolves");
        var deferred = pmi.Count(record => record.ExportSupport == "deferred");
        var status = fatalCount > 0 ? "invalid" : deferred > 0 ? "valid-with-deferred-export" : "valid";
        var summary = new FirmamentV2ValidationSummary(
            lets.Count,
            lets.Count(row => row.Tolerance is not null),
            concepts.Count,
            concepts.Count(row => row.Status == "valid"),
            pmi.Count,
            supported,
            deferred,
            conceptPmiEvaluation.Obligations.Count,
            conceptPmiEvaluation.Obligations.Count(row => row.Status == "satisfied"),
            conceptPmiEvaluation.Obligations.Count(row => row.Status == "missing"),
            fatalCount,
            warningCount);

        return new FirmamentV2ValidationReport(
            source,
            status,
            runtimeValidation.ForgeRuntime,
            lets,
            concepts,
            pmi,
            conceptPmiEvaluation.Obligations,
            new FirmamentV2ValidationExportSupport(supported, deferred, ExportMatrix),
            diagnostics,
            summary);
    }

    private static IReadOnlyList<FirmamentV2ValidationLet> BuildLets(FirmamentV2Document document)
    {
        var rows = new List<FirmamentV2ValidationLet>();
        foreach (var boundLet in document.BoundLets ?? [])
        {
            rows.Add(Let(boundLet.Name, boundLet, "let"));
        }

        foreach (var record in document.BoundLetRecords ?? [])
        {
            foreach (var field in record.Fields.Values)
            {
                rows.Add(Let($"{record.Name}.{field.Name}", field, "let-record"));
            }
        }

        return rows.OrderBy(row => row.Name, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> Dependencies(FirmamentV2BoundLet boundLet) =>
        boundLet.Dependencies is null ? [] : boundLet.Dependencies.Order(StringComparer.Ordinal).ToArray();

    private static FirmamentV2ValidationLet Let(string name, FirmamentV2BoundLet boundLet, string source) =>
        new(name, TypeName(boundLet.Type), FormatValue(boundLet.Value), ToTolerance(boundLet.Tolerance), source, Dependencies(boundLet), []);

    private static IReadOnlyList<FirmamentV2ValidationConcept> BuildConcepts(
        FirmamentV2Document document,
        FirmamentV2RuntimeConceptValidationResult runtimeValidation,
        FirmamentV2ForgeConceptCatalog conceptCatalog)
    {
        var runtimeByKey = runtimeValidation.Concepts.ToDictionary(
            result => ConceptKey(result.Kind, result.Name, result.Family, result.Concept),
            StringComparer.Ordinal);

        var rows = new List<FirmamentV2ValidationConcept>();
        foreach (var declaration in document.ManufacturingConcepts ?? [])
        {
            rows.Add(Concept("manufacturing", null, declaration.Application, declaration.BoundFields ?? [], runtimeByKey, conceptCatalog));
        }

        foreach (var declaration in document.FeatureConcepts ?? [])
        {
            rows.Add(Concept("feature", declaration.Name, declaration.Application, declaration.BoundFields ?? [], runtimeByKey, conceptCatalog));
        }

        return rows;
    }

    private static FirmamentV2ValidationConcept Concept(
        string kind,
        string? name,
        FirmamentV2ConceptApplication application,
        IReadOnlyList<FirmamentV2BoundConceptField> fields,
        IReadOnlyDictionary<string, FirmamentV2RuntimeConceptValidationEntry> runtimeByKey,
        FirmamentV2ForgeConceptCatalog conceptCatalog)
    {
        var reportFields = fields
            .Select(field => new FirmamentV2ValidationConceptField(
                field.Name,
                field.TargetSource is not null ? "target" : field.BoundValue is null ? "unknown" : TypeName(field.BoundValue.InferredType),
                field.BoundValue?.AliasTolerance is not null,
                field.TargetSource ?? field.Field.Source))
            .ToArray();

        var parserValid = conceptCatalog.TryGet(application.FamilyName, application.ConceptName, out var descriptor)
            && descriptor.Fields.Values.Where(field => field.Required).All(field => reportFields.Any(reportField => reportField.Name == field.Name))
            && reportFields.All(reportField => reportField.Type != "unknown");

        runtimeByKey.TryGetValue(ConceptKey(kind, name, application.FamilyName, application.ConceptName), out var runtime);
        var runtimeDiagnostics = runtime?.Diagnostics.Select(ToRuntimeDiagnostic).ToArray() ?? [];
        var runtimeStatus = runtime is null
            ? null
            : new FirmamentV2ValidationConceptRuntimeValidation(runtime.Provider, runtime.Status, runtimeDiagnostics);

        var status = !parserValid || runtime?.Status == "invalid" ? "invalid" : "valid";

        return new FirmamentV2ValidationConcept(
            kind,
            name,
            application.FamilyName,
            application.ConceptName,
            status,
            "not-run",
            reportFields,
            runtimeDiagnostics,
            runtimeStatus);
    }

    private static string ConceptKey(FirmamentConceptApplicationKind kind, string? name, string family, string concept) =>
        ConceptKey(kind == FirmamentConceptApplicationKind.Manufacturing ? "manufacturing" : "feature", name, family, concept);

    private static string ConceptKey(string kind, string? name, string family, string concept) =>
        string.Join("|", kind, name ?? string.Empty, family, concept);

    private static IReadOnlyList<FirmamentV2ValidationPmiRecord> BuildPmi(
        FirmamentV2Document document,
        IReadOnlyList<FirmamentV2ValidationDiagnostic> diagnostics)
    {
        var bound = (document.BoundPmi?.Datums ?? [])
            .Concat(document.BoundPmi?.Dimensions ?? [])
            .Concat(document.BoundPmi?.Controls ?? [])
            .ToDictionary(record => record.Name, StringComparer.Ordinal);

        return (document.PmiBlock?.Records ?? []).Select(record =>
        {
            bound.TryGetValue(record.Name, out var boundRecord);
            var exportSupport = ExportSupport(record.Kind, boundRecord);
            var itemDiagnostics = boundRecord is null
                ? diagnostics.Where(diagnostic => diagnostic.Code.StartsWith("firmament-v2-pmi-", StringComparison.Ordinal)).ToArray()
                : [];
            var status = itemDiagnostics.Any(diagnostic => diagnostic.Severity == "fatal")
                ? "invalid"
                : exportSupport == "deferred"
                    ? "export-deferred"
                    : "valid";

            var constraint = record.Projection is null ? null : (document.StaticAuthoring?.SemanticConstraints ?? []).SingleOrDefault(item => item.Id == record.Projection.SourceRequireId);
            return new FirmamentV2ValidationPmiRecord(
                KindName(record.Kind),
                record.Name,
                status,
                exportSupport,
                boundRecord?.Targets.FirstOrDefault() ?? Field(record, "target"),
                boundRecord?.Targets ?? [],
                boundRecord?.DatumRefs ?? [],
                Dimension(boundRecord),
                exportSupport == "deferred" ? DeferredReason(record.Kind) : null,
                itemDiagnostics,
                record.Projection?.SourceRequireId,
                constraint is null ? null : "dimensional-equality",
                constraint is null ? null : constraint.Subject + "." + constraint.Property,
                constraint is null ? null : constraint.ValidationSucceeded ? "succeeded" : "failed",
                constraint?.ExpectedProvenance);
        }).ToArray();
    }

    private static FirmamentV2ConceptPmiObligationEvaluation BuildConceptPmiObligations(
        FirmamentV2Document document,
        FirmamentV2RuntimeConceptValidationResult runtimeValidation)
    {
        var boundPmiByName = (document.BoundPmi?.Datums ?? [])
            .Concat(document.BoundPmi?.Dimensions ?? [])
            .Concat(document.BoundPmi?.Controls ?? [])
            .ToDictionary(record => record.Name, StringComparer.Ordinal);

        var obligations = new List<FirmamentV2ValidationConceptPmiObligation>();
        var diagnostics = new List<FirmamentV2ValidationDiagnostic>();

        foreach (var obligation in runtimeValidation.PmiObligations)
        {
            var matchedPmi = MatchPmi(obligation, boundPmiByName.Values);
            var status = matchedPmi is null ? "missing" : "satisfied";
            var severity = obligation.Severity switch
            {
                FirmamentDiagnosticSeverity.Fatal => "fatal",
                FirmamentDiagnosticSeverity.Warning => "warning",
                _ => "info"
            };

            obligations.Add(new FirmamentV2ValidationConceptPmiObligation(
                obligation.Kind,
                obligation.SourceConcept.ToString(),
                obligation.SourceName,
                obligation.TargetSource,
                obligation.ExpectedDimensionField,
                status,
                severity,
                matchedPmi?.Name,
                status == "missing" ? "forge.pmi.obligation.missing" : null));

            if (status == "missing")
            {
                diagnostics.Add(new FirmamentV2ValidationDiagnostic(
                    "forge.pmi.obligation.missing",
                    severity,
                    MissingObligationMessage(obligation),
                    obligation.ExpectedDimensionField,
                    obligation.TargetSource));
            }
        }

        return new FirmamentV2ConceptPmiObligationEvaluation(obligations, diagnostics);
    }

    private static FirmamentV2BoundPmiRecord? MatchPmi(PmiObligation obligation, IEnumerable<FirmamentV2BoundPmiRecord> records) =>
        records.FirstOrDefault(record =>
            string.Equals(KindName(record.Kind), obligation.Kind, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(obligation.TargetSource)
            && record.Targets.Any(target => string.Equals(target, obligation.TargetSource, StringComparison.Ordinal)));

    private static string MissingObligationMessage(PmiObligation obligation) =>
        $"{obligation.SourceConcept} feature {obligation.SourceName ?? "<unnamed>"} has no matching {obligation.Kind} PMI record.";

    private static FirmamentV2ValidationDimension? Dimension(FirmamentV2BoundPmiRecord? boundRecord)
    {
        if (boundRecord is null)
        {
            return null;
        }

        if (boundRecord.DimensionValue is not null)
        {
            return new FirmamentV2ValidationDimension(FormatValue(boundRecord.DimensionValue), ToTolerance(boundRecord.DimensionTolerance));
        }

        if (boundRecord.ControlTolerance is not null)
        {
            return new FirmamentV2ValidationDimension(FormatValue(boundRecord.ControlTolerance), null);
        }

        return null;
    }

    private static FirmamentV2ValidationDiagnostic ToParserDiagnostic(string code) =>
        new(code, FirmamentV2Parser.IsFatalDiagnosticCode(code) ? "fatal" : "warning", Message(code));

    private static bool IsParserTraceDiagnostic(string code) =>
        code is "firmament-v2-parser-invoked" or "firmament-v2-parse-succeeded"
        || code.EndsWith("-parsed", StringComparison.Ordinal)
        || code.EndsWith("-adapted", StringComparison.Ordinal)
        || code.EndsWith("-symbols-bound", StringComparison.Ordinal);

    private static FirmamentV2ValidationDiagnostic ToRuntimeDiagnostic(FirmamentDiagnostic diagnostic) =>
        new(
            diagnostic.Code,
            diagnostic.Severity switch
            {
                FirmamentDiagnosticSeverity.Fatal => "fatal",
                FirmamentDiagnosticSeverity.Warning => "warning",
                _ => "info"
            },
            diagnostic.Message,
            diagnostic.FieldName,
            diagnostic.Target);

    private static string Message(string code) => code switch
    {
        FirmamentV2Parser.PmiDimensionMissingTolerance => "PMI dimension resolves to a value without tolerance evidence.",
        FirmamentV2Parser.ConceptMissingRequiredField => "Forge concept application is missing a required descriptor field.",
        FirmamentV2Parser.PmiUnknownDatum => "PMI relation references an unknown datum.",
        FirmamentV2Parser.ToleranceDroppedThroughArithmetic => "A toleranced value was used in arithmetic and only the nominal value was preserved.",
        _ => code
    };

    private static FirmamentV2ValidationTolerance? ToTolerance(FirmamentV2Tolerance? tolerance) =>
        tolerance is null
            ? null
            : new(
                tolerance.Kind == FirmamentV2ToleranceKind.Bilateral ? "bilateral" : "asymmetric",
                FormatNumber(tolerance.Plus) + tolerance.Unit,
                FormatNumber(tolerance.Minus) + tolerance.Unit);

    private static string FormatValue(FirmamentV2LiteralValue value) =>
        value.Unit is null
            ? Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty
            : FormatNumber(value.NumericValue ?? 0) + value.Unit;

    private static string FormatNumber(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);

    private static string TypeName(FirmamentV2PrimitiveType type) => type.ToString().ToLowerInvariant();

    private static string KindName(FirmamentV2PmiKind kind) => kind switch
    {
        FirmamentV2PmiKind.HoleDiameter => "diameter",
        FirmamentV2PmiKind.DatumPlane => "datum",
        _ => kind.ToString().ToLowerInvariant()
    };

    private static string? Field(FirmamentV2PmiRecord record, string name) =>
        record.Fields.TryGetValue(name, out var field) ? field.Source : null;

    private static string ExportSupport(FirmamentV2PmiKind kind, FirmamentV2BoundPmiRecord? boundRecord) => kind switch
    {
        FirmamentV2PmiKind.DatumPlane or FirmamentV2PmiKind.HoleDiameter => boundRecord is null ? "supported-when-target-resolves" : "supported",
        _ => "deferred"
    };

    private static string DeferredReason(FirmamentV2PmiKind kind) =>
        $"AP242 lowering for {KindName(kind)} is not implemented in Phase 1 P1.";

    private static readonly IReadOnlyDictionary<string, string> ExportMatrix = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["datum"] = "supported-when-target-resolves",
        ["diameter"] = "supported-when-target-resolves",
        ["distance"] = "deferred",
        ["flatness"] = "deferred",
        ["parallel"] = "deferred",
        ["perpendicular"] = "deferred",
        ["coplanar"] = "deferred"
    };

    private sealed record FirmamentV2ConceptPmiObligationEvaluation(
        IReadOnlyList<FirmamentV2ValidationConceptPmiObligation> Obligations,
        IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);
}
