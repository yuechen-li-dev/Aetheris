using System.Globalization;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2ValidationReport(
    string Source,
    string Status,
    IReadOnlyList<FirmamentV2ValidationLet> Lets,
    IReadOnlyList<FirmamentV2ValidationConcept> Concepts,
    IReadOnlyList<FirmamentV2ValidationPmiRecord> Pmi,
    FirmamentV2ValidationExportSupport ExportSupport,
    IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics,
    FirmamentV2ValidationSummary Summary);

public sealed record FirmamentV2ValidationSummary(int LetCount, int TolerancedLetCount, int ConceptCount, int ValidConceptCount, int PmiRecordCount, int ExportSupportedPmiCount, int ExportDeferredPmiCount, int FatalDiagnosticCount, int WarningDiagnosticCount);
public sealed record FirmamentV2ValidationDiagnostic(string Code, string Severity, string Message);
public sealed record FirmamentV2ValidationExportSupport(int SupportedPmiCount, int DeferredPmiCount, IReadOnlyDictionary<string, string> Matrix);
public sealed record FirmamentV2ValidationLet(string Name, string Type, string Nominal, FirmamentV2ValidationTolerance? Tolerance, string Source, IReadOnlyList<string> Dependencies, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);
public sealed record FirmamentV2ValidationTolerance(string Kind, string Plus, string Minus);
public sealed record FirmamentV2ValidationConcept(string Kind, string? Name, string Family, string Concept, string Status, string DfmStatus, IReadOnlyList<FirmamentV2ValidationConceptField> Fields, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);
public sealed record FirmamentV2ValidationConceptField(string Name, string Type, bool HasTolerance, string Source);
public sealed record FirmamentV2ValidationPmiRecord(string Kind, string Name, string Status, string ExportSupport, string? Target, IReadOnlyList<string> Targets, IReadOnlyList<string> DatumRefs, FirmamentV2ValidationDimension? Dimension, string? Reason, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);
public sealed record FirmamentV2ValidationDimension(string Nominal, FirmamentV2ValidationTolerance? Tolerance);

public static class FirmamentV2ValidationReportBuilder
{
    public static FirmamentV2ValidationReport Build(FirmamentV2ParseResult parse, string source = "inline")
    {
        var diagnostics = parse.Diagnostics.Distinct(StringComparer.Ordinal).Select(ToDiagnostic).ToArray();
        var fatalCount = diagnostics.Count(d => d.Severity == "fatal");
        var warningCount = diagnostics.Length - fatalCount;
        var document = parse.Document;
        var lets = document is null ? [] : BuildLets(document);
        var concepts = document is null ? [] : BuildConcepts(document);
        var pmi = document is null ? [] : BuildPmi(document, diagnostics);
        var supported = pmi.Count(p => p.ExportSupport is "supported" or "supported-when-target-resolves");
        var deferred = pmi.Count(p => p.ExportSupport == "deferred");
        var status = fatalCount > 0 ? "invalid" : deferred > 0 ? "valid-with-deferred-export" : "valid";
        var summary = new FirmamentV2ValidationSummary(lets.Count, lets.Count(l => l.Tolerance is not null), concepts.Count, concepts.Count(c => c.Status == "valid"), pmi.Count, supported, deferred, fatalCount, warningCount);
        return new(source, status, lets, concepts, pmi, new(supported, deferred, ExportMatrix), diagnostics, summary);
    }

    private static IReadOnlyList<FirmamentV2ValidationLet> BuildLets(FirmamentV2Document d)
    {
        var rows = new List<FirmamentV2ValidationLet>();
        foreach (var l in d.BoundLets ?? []) rows.Add(Let(l.Name, l, "let"));
        foreach (var r in d.BoundLetRecords ?? []) foreach (var f in r.Fields.Values) rows.Add(Let($"{r.Name}.{f.Name}", f, "let-record"));
        return rows.OrderBy(l => l.Name, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> Dependencies(FirmamentV2BoundLet l) => l.Dependencies is null ? [] : l.Dependencies.Order(StringComparer.Ordinal).ToArray();

    private static FirmamentV2ValidationLet Let(string name, FirmamentV2BoundLet l, string source) => new(name, TypeName(l.Type), FormatValue(l.Value), ToTolerance(l.Tolerance), source, Dependencies(l), []);

    private static IReadOnlyList<FirmamentV2ValidationConcept> BuildConcepts(FirmamentV2Document d)
    {
        var rows = new List<FirmamentV2ValidationConcept>();
        foreach (var c in d.ManufacturingConcepts ?? []) rows.Add(Concept("manufacturing", null, c.Application, c.BoundFields ?? []));
        foreach (var c in d.FeatureConcepts ?? []) rows.Add(Concept("feature", c.Name, c.Application, c.BoundFields ?? []));
        return rows;
    }

    private static FirmamentV2ValidationConcept Concept(string kind, string? name, FirmamentV2ConceptApplication app, IReadOnlyList<FirmamentV2BoundConceptField> fields)
    {
        var reportFields = fields.Select(f => new FirmamentV2ValidationConceptField(f.Name, f.TargetSource is not null ? "target" : f.BoundValue is null ? "unknown" : TypeName(f.BoundValue.InferredType), f.BoundValue?.AliasTolerance is not null, f.TargetSource ?? f.Field.Source)).ToArray();
        var valid = FirmamentV2ForgeConceptRegistry.TryGet(app.FamilyName, app.ConceptName, out var descriptor) && descriptor.Fields.Values.Where(f => f.Required).All(f => reportFields.Any(r => r.Name == f.Name)) && reportFields.All(f => f.Type != "unknown");
        return new(kind, name, app.FamilyName, app.ConceptName, valid ? "valid" : "invalid", "not-run", reportFields, []);
    }

    private static IReadOnlyList<FirmamentV2ValidationPmiRecord> BuildPmi(FirmamentV2Document d, IReadOnlyList<FirmamentV2ValidationDiagnostic> diagnostics)
    {
        var bound = (d.BoundPmi?.Datums ?? []).Concat(d.BoundPmi?.Dimensions ?? []).Concat(d.BoundPmi?.Controls ?? []).ToDictionary(r => r.Name, StringComparer.Ordinal);
        return (d.PmiBlock?.Records ?? []).Select(r =>
        {
            bound.TryGetValue(r.Name, out var b);
            var export = ExportSupport(r.Kind, b);
            var itemDiagnostics = b is null ? diagnostics.Where(x => x.Code.StartsWith("firmament-v2-pmi-", StringComparison.Ordinal)).ToArray() : [];
            var status = itemDiagnostics.Any(x => x.Severity == "fatal") ? "invalid" : export == "deferred" ? "export-deferred" : "valid";
            return new FirmamentV2ValidationPmiRecord(KindName(r.Kind), r.Name, status, export, b?.Targets.FirstOrDefault() ?? Field(r, "target"), b?.Targets ?? [], b?.DatumRefs ?? [], Dimension(b), export == "deferred" ? DeferredReason(r.Kind) : null, itemDiagnostics);
        }).ToArray();
    }

    private static FirmamentV2ValidationDimension? Dimension(FirmamentV2BoundPmiRecord? b)
    {
        if (b is null) return null;
        if (b.DimensionValue is not null) return new(FormatValue(b.DimensionValue), ToTolerance(b.DimensionTolerance));
        if (b.ControlTolerance is not null) return new(FormatValue(b.ControlTolerance), null);
        return null;
    }

    private static FirmamentV2ValidationDiagnostic ToDiagnostic(string code) => new(code, FirmamentV2Parser.IsFatalDiagnosticCode(code) ? "fatal" : "warning", Message(code));
    private static string Message(string code) => code switch { FirmamentV2Parser.PmiDimensionMissingTolerance => "PMI dimension resolves to a value without tolerance evidence.", FirmamentV2Parser.ConceptMissingRequiredField => "Forge concept application is missing a required descriptor field.", FirmamentV2Parser.PmiUnknownDatum => "PMI relation references an unknown datum.", FirmamentV2Parser.ToleranceDroppedThroughArithmetic => "A toleranced value was used in arithmetic and only the nominal value was preserved.", _ => code };
    private static FirmamentV2ValidationTolerance? ToTolerance(FirmamentV2Tolerance? t) => t is null ? null : new(t.Kind == FirmamentV2ToleranceKind.Bilateral ? "bilateral" : "asymmetric", FormatNumber(t.Plus) + t.Unit, FormatNumber(t.Minus) + t.Unit);
    private static string FormatValue(FirmamentV2LiteralValue v) => v.Unit is null ? Convert.ToString(v.Value, CultureInfo.InvariantCulture) ?? string.Empty : FormatNumber(v.NumericValue ?? 0) + v.Unit;
    private static string FormatNumber(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);
    private static string TypeName(FirmamentV2PrimitiveType t) => t.ToString().ToLowerInvariant();
    private static string KindName(FirmamentV2PmiKind k) => k switch { FirmamentV2PmiKind.HoleDiameter => "diameter", FirmamentV2PmiKind.DatumPlane => "datum", _ => k.ToString().ToLowerInvariant() };
    private static string? Field(FirmamentV2PmiRecord r, string name) => r.Fields.TryGetValue(name, out var f) ? f.Source : null;
    private static string ExportSupport(FirmamentV2PmiKind kind, FirmamentV2BoundPmiRecord? bound) => kind switch { FirmamentV2PmiKind.DatumPlane or FirmamentV2PmiKind.HoleDiameter => bound is null ? "supported-when-target-resolves" : "supported", _ => "deferred" };
    private static string DeferredReason(FirmamentV2PmiKind kind) => $"AP242 lowering for {KindName(kind)} is not implemented in Phase 1 P1.";
    private static readonly IReadOnlyDictionary<string, string> ExportMatrix = new Dictionary<string, string>(StringComparer.Ordinal) { ["datum"] = "supported-when-target-resolves", ["diameter"] = "supported-when-target-resolves", ["distance"] = "deferred", ["flatness"] = "deferred", ["parallel"] = "deferred", ["perpendicular"] = "deferred", ["coplanar"] = "deferred" };
}
