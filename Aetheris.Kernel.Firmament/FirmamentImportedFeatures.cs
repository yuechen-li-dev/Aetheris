using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament;

public sealed class ImportedFeatureInspection
{
    internal ImportedFeatureInspection(string text, BrepBody body, double tolerance)
    {
        SourceText = text; Body = body;
        SourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')))).ToLowerInvariant();
        Discovery = BrepThroughHoleRevision.Discover(body, tolerance);
    }
    internal string SourceText { get; }
    internal BrepBody Body { get; }
    public string SourceHash { get; }
    public string SourceHashKind => "sha256-utf8-lf";
    public double? SourceDistanceAccuracyMillimetres => Body.ImportQualification?.SourceDistanceAccuracyMillimetres;
    public ThroughHoleDiscovery Discovery { get; }
}

public sealed record ImportedFeatureMovePlan(string Source, string Candidate, string Alias,
    string SourceHash, string Admission, FirmamentImportedHoleRevisionReport Revision)
{
    public double AuthoringIncrementMillimetres => FirmamentImportedFeatures.AuthoringIncrementMillimetres;
    [JsonIgnore] public FirmamentStepExportResult Export { get; init; } = null!;
}
public sealed record ImportedFeatureRemovalPlan(string Source, string Candidate, string Alias, string SourceHash, string Admission,
    FirmamentImportedHoleRemovalReport Removal)
{
    [JsonIgnore] public FirmamentStepExportResult Export { get; init; } = null!;
}

/// <summary>Agent-facing recovery assistance. Selection is explicit; geometric
/// admission belongs exclusively to the existing production kernel/compiler.</summary>
public static class FirmamentImportedFeatures
{
    public const double AuthoringIncrementMillimetres = .1;

    static double Nominal(double value)
    {
        var rounded = double.Round(value, 1, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0 : rounded;
    }

    static double Outward(double value, bool minimum)
    {
        // Remove arithmetic jitter at a grid boundary before directing the round.
        // Final compiler admission checks the actual emitted bounds against BRep.
        var grid = double.Round(value / AuthoringIncrementMillimetres, 8);
        return (minimum ? double.Floor(grid) : double.Ceiling(grid)) * AuthoringIncrementMillimetres;
    }

    public static KernelResult<ImportedFeatureInspection> Inspect(string stepText, double importToleranceMillimetres = .001)
    {
        ArgumentNullException.ThrowIfNull(stepText);
        if (!double.IsFinite(importToleranceMillimetres) || importToleranceMillimetres <= 0 || importToleranceMillimetres > .1)
            return Failure<ImportedFeatureInspection>("Import tolerance must be positive, finite and at most .1 mm.");
        var import = Step242Importer.ImportBody(stepText, new ImportPolicy(PcurveQualificationToleranceMillimetres: importToleranceMillimetres));
        return import.IsSuccess ? KernelResult<ImportedFeatureInspection>.Success(new(stepText, import.Value, importToleranceMillimetres), import.Diagnostics)
            : KernelResult<ImportedFeatureInspection>.Failure(import.Diagnostics);
    }

    public static KernelResult<ImportedFeatureMovePlan> PlanMove(ImportedFeatureInspection inspection,
        string candidateId, string alias, Point3D destination, bool geometryOnly,
        string sourceReference = "input.step", string? sourceDirectory = null, double clearanceMillimetres = .1)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (!geometryOnly) return Failure<ImportedFeatureMovePlan>("Explicit geometry-only approval is required; source PMI migration is not admitted.");
        var name = FirmamentSyntaxParser.ParseExpression(alias);
        if (name.Diagnostics.Count > 0 || name.Expression is not ReferenceSyntax identifier || identifier.Name.Text != alias)
            return Failure<ImportedFeatureMovePlan>("Alias must be one Firmament identifier.");
        if (string.IsNullOrWhiteSpace(sourceReference) || Path.IsPathRooted(sourceReference)) return Failure<ImportedFeatureMovePlan>("The generated InlineStep path must be relative to its source document.");
        string? resourcePath = null;
        if (sourceDirectory is null)
        {
            try { resourcePath = Assembly.FirmamentProjectSnapshot.NormalizePath(sourceReference); }
            catch (ArgumentException) { return Failure<ImportedFeatureMovePlan>("Snapshot source reference must remain inside the resource project."); }
        }
        var candidate = inspection.Discovery.Candidates.SingleOrDefault(c => c.Id == candidateId);
        if (candidate?.AxisIndex is not { } axis || candidate.Center is not { } original)
            return Failure<ImportedFeatureMovePlan>("Select a proved principal-axis round through-hole before planning.");
        double Component(Point3D p) => axis switch { 0 => p.X, 1 => p.Y, _ => p.Z };
        if (System.Math.Abs(Component(destination) - Component(original)) > 1e-6)
            return Failure<ImportedFeatureMovePlan>("The destination must remain on the proved entry plane; only in-plane positions are rounded.");
        var authoredDestination = axis switch
        {
            0 => new Point3D(original.X, Nominal(destination.Y), Nominal(destination.Z)),
            1 => new(Nominal(destination.X), original.Y, Nominal(destination.Z)),
            _ => new(Nominal(destination.X), Nominal(destination.Y), original.Z)
        };
        var planned = BrepThroughHoleRevision.PlanMove(inspection.Body, candidateId, alias, authoredDestination,
            inspection.Discovery.ImportToleranceMillimetres, clearanceMillimetres);
        if (!planned.IsSuccess) return KernelResult<ImportedFeatureMovePlan>.Failure(planned.Diagnostics);
        var request = planned.Value.Request;
        var entryEntity = inspection.Body.Bindings.GetFaceBinding(request.EntryFace).SourceStepEntityId;
        if (entryEntity is null) return Failure<ImportedFeatureMovePlan>("Entry support has no imported source entity binding.");
        var source = axis == 2 && System.Math.Abs(Nominal(request.Diameter) - request.Diameter) < 1e-6
            && System.Math.Abs(Nominal(request.Within.Min.Z) - request.Within.Min.Z) < 1e-6 && System.Math.Abs(Nominal(request.Within.Max.Z) - request.Within.Max.Z) < 1e-6
            ? Emit(inspection, candidateId, alias, sourceReference, entryEntity.Value, request)
            : EmitMeasured(inspection, candidate, alias, sourceReference, request.Within, request.DestinationWithin, request.Center, entryEntity.Value);
        // Resource snapshots use exactly the inspected immutable text. File-backed
        // callers re-enter the ordinary loader, which rechecks the persisted pin.
        IReadOnlyDictionary<string, string>? resources = sourceDirectory is null
            ? new Dictionary<string, string> { [resourcePath!] = inspection.SourceText } : null;
        var build = FirmamentBuildAndExport.CompileSource(source, sourceDirectory ?? "", resources);
        if (!build.IsSuccess) return KernelResult<ImportedFeatureMovePlan>.Failure(build.Diagnostics);
        return KernelResult<ImportedFeatureMovePlan>.Success(new(source, candidateId, alias, inspection.SourceHash,
            "Built", build.Value.ImportedHoleRevision!)
        { Export = build.Value }, build.Diagnostics);
    }

    public static KernelResult<ImportedFeatureRemovalPlan> PlanRemove(ImportedFeatureInspection inspection, string candidateId, string alias,
        bool geometryOnly, string sourceReference = "input.step", string? sourceDirectory = null, double clearanceMillimetres = .1)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (!geometryOnly) return Failure<ImportedFeatureRemovalPlan>("Explicit geometry-only approval is required; source PMI migration is not admitted.");
        var name = FirmamentSyntaxParser.ParseExpression(alias);
        if (name.Diagnostics.Count > 0 || name.Expression is not ReferenceSyntax r || r.Name.Text != alias)
            return Failure<ImportedFeatureRemovalPlan>("Alias must be one Firmament identifier.");
        if (string.IsNullOrWhiteSpace(sourceReference) || Path.IsPathRooted(sourceReference)) return Failure<ImportedFeatureRemovalPlan>("The STEP path must be relative to its source document.");
        string? resourcePath = null;
        if (sourceDirectory is null)
        {
            try { resourcePath = Assembly.FirmamentProjectSnapshot.NormalizePath(sourceReference); }
            catch (ArgumentException) { return Failure<ImportedFeatureRemovalPlan>("Snapshot source reference must remain inside the resource project."); }
        }
        var planned = BrepThroughHoleRevision.PlanRemove(inspection.Body, candidateId, alias, inspection.Discovery.ImportToleranceMillimetres, clearanceMillimetres);
        if (!planned.IsSuccess) return KernelResult<ImportedFeatureRemovalPlan>.Failure(planned.Diagnostics);
        var candidate = inspection.Discovery.Candidates.Single(c => c.Id == candidateId);
        var source = EmitMeasured(inspection, candidate, alias, sourceReference, planned.Value.Request.Within, null, null, null);
        IReadOnlyDictionary<string, string>? resources = sourceDirectory is null ? new Dictionary<string, string> { [resourcePath!] = inspection.SourceText } : null;
        var build = FirmamentBuildAndExport.CompileSource(source, sourceDirectory ?? "", resources);
        return build.IsSuccess ? KernelResult<ImportedFeatureRemovalPlan>.Success(new(source, candidateId, alias, inspection.SourceHash, "Built", build.Value.ImportedHoleRemoval!) { Export = build.Value }, build.Diagnostics)
            : KernelResult<ImportedFeatureRemovalPlan>.Failure(build.Diagnostics);
    }

    static string EmitMeasured(ImportedFeatureInspection inspection, ThroughHoleCandidate candidate, string alias, string path,
        ThroughHoleEditBounds within, ThroughHoleEditBounds? destination, Point3D? center, int? entryEntity)
    {
        var strings = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var axis = candidate.AxisIndex!.Value; var prefix = "Source." + alias; var axisName = new[] { "X", "Y", "Z" }[axis];
        static double Component(Point3D p, int i) => i switch { 0 => p.X, 1 => p.Y, _ => p.Z };
        string Point(Point3D p, string? axial = null, bool? minimum = null) => "Point3(" + string.Join(", ", Enumerable.Range(0, 3).Select(i => i == axis
            ? prefix + "." + (axial ?? "center") + "." + axisName
            : (minimum is { } min ? Outward(Component(p, i), min) : Nominal(Component(p, i))).ToString("0.#", CultureInfo.InvariantCulture) + "mm")) + ")";
        string Bounds(ThroughHoleEditBounds b) => "{ min: " + Point(b.Min, "exit", true) + "; max: " + Point(b.Max, "entry", false) + " }";
        var operation = destination is null ? "Remove " + prefix + " {\n        within: " + Bounds(within) + "\n    }"
            : $"Replace {prefix} with Hole<Shaft> {alias} {{\n        on: Source.Face(\"#{entryEntity}\")\n        center: {Point(center!.Value)}\n        diameter: {prefix}.diameter\n        end: ThroughAll\n        within: [{Bounds(within)}, {Bounds(destination)}]\n    }}";
        return $$"""
        // New in-plane positions use a 0.1mm grid. Source size/supports remain exact.
        Model ImportedRevision {
            units: mm
            InlineStep Source {
                path: {{JsonSerializer.Serialize(path)}}
                textSha256: "{{inspection.SourceHash}}"
                importTolerance: {{inspection.Discovery.ImportToleranceMillimetres.ToString("R", CultureInfo.InvariantCulture)}}mm
                geometryOnly: true
            }
            Recognize Source {
                Region {{alias}} { kind: HoleShaft; candidate: {{JsonSerializer.Serialize(candidate.Id, strings)}}; }
            }
            {{operation}}
        }
        """;
    }

    static string Emit(ImportedFeatureInspection inspection, string candidate, string alias,
        string path, int entryEntity, ThroughHoleRevisionRequest request)
    {
        static string N(double value) => Nominal(value).ToString("0.#", CultureInfo.InvariantCulture);
        static string P(Point3D point) => $"Point3({N(point.X)}mm, {N(point.Y)}mm, {N(point.Z)}mm)";
        static string B(ThroughHoleEditBounds bounds) => $"{{ min: {P(new(Outward(bounds.Min.X, true), Outward(bounds.Min.Y, true), bounds.Min.Z))}; max: {P(new(Outward(bounds.Max.X, false), Outward(bounds.Max.Y, false), bounds.Max.Z))} }}";
        var target = "RevisionLayout.Targets." + alias;
        var pad = N(Outward(request.DestinationWithin!.Max.X - request.Center.X, false));
        var destinationBounds = $"{{ min: Point3({target}.X - {pad}mm, {target}.Y - {pad}mm, {N(request.Within.Min.Z)}mm); max: Point3({target}.X + {pad}mm, {target}.Y + {pad}mm, {N(request.Within.Max.Z)}mm) }}";
        var strings = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        return $$"""
        // Recovery proves the admitted feature family, not the original modeling history.
        // Authored lengths use a 0.1mm grid; numerical import accuracy is separate.
        Model ImportedRevision {
            units: mm
            Concept Struct RevisionLayout {
                Points<Point3> Targets { {{alias}}: {{P(request.Center)}}; }
            }
            InlineStep Source {
                path: {{JsonSerializer.Serialize(path, strings)}}
                textSha256: "{{inspection.SourceHash}}"
                importTolerance: {{request.ImportToleranceMillimetres.ToString("R", CultureInfo.InvariantCulture)}}mm
                geometryOnly: true
            }
            Recognize Source {
                Region {{alias}} {
                    kind: HoleShaft
                    candidate: {{JsonSerializer.Serialize(candidate, strings)}}
                }
            }
            Replace Source.{{alias}} with Hole<Shaft> {{alias}} {
                on: Source.Face("#{{entryEntity}}")
                center: RevisionLayout.Targets.{{alias}}
                diameter: {{N(request.Diameter)}}mm
                end: ThroughAll
                within: [
                    {{B(request.Within)}},
                    {{destinationBounds}}
                ]
            }
        }
        """;
    }

    static KernelResult<T> Failure<T>(string message) => KernelResult<T>.Failure([
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
            "firmament-imported-feature-assistance-rejected: " + message, "Firmament.ImportedFeatures")]);
}
