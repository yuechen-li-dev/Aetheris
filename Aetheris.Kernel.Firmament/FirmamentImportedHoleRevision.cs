using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentImportedHoleRevisionReport(string Feature, string SourceHash,
    string SourceHashKind, string? SourceByteHash,
    string Strategy, bool SourceSemanticsExcluded, double ImportToleranceMillimetres,
    IReadOnlyList<int> RetiredWallFaces, IReadOnlyList<int> WallFaces, int EntryFace, int ExitFace,
    double Diameter, double Depth, ThroughHoleEditBounds Within, int PreservedFaces, int PreservedEdges,
    IReadOnlyDictionary<int, int> FaceCorrespondence)
{
    public ThroughHoleEditBounds? DestinationWithin { get; init; }
}

public static partial class FirmamentBuildAndExport
{
    private static KernelResult<FirmamentStepExportResult> ExportPinnedImportedRevision(FirmamentV2Document document)
    {
        var solid = document.Solids.First(s => s.InlineStep?.SourceHashPin is not null);
        if (document.Solids.Count != 1 || document.ModifyBlocks is { Count: > 0 }
            || document.Panels is { Count: > 0 } || document.LatticeFills is { Count: > 0 }
            || document.StandaloneLatticeFills is { Count: > 0 } || document.Threads is { Count: > 0 }
            || document.Replacements is not { Count: 1 } || document.Replacements[0].Within is null)
            return KernelResult<FirmamentStepExportResult>.Failure([new(KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                "firmament-inline-step-revision-required: Pinned imports currently admit one bounded Replace with a within envelope and no additional Modify operations. No imported geometry was emitted.", "FirmamentV2.InlineStep")
                { SourceSpan = solid.SourceSpan is { } span ? new(span.Start, span.Length) : null }]);
        return ExportImportedHoleRevision(document, solid, document.Replacements[0]);
    }

    private static KernelResult<FirmamentStepExportResult> ExportImportedHoleRevision(FirmamentV2Document document,
        FirmamentV2SolidBinding solid, FirmamentV2ReplacementDecl replacement)
    {
        KernelResult<FirmamentStepExportResult> Failure(string message) => KernelResult<FirmamentStepExportResult>.Failure([
            new(KernelDiagnosticCode.ValidationFailed,KernelDiagnosticSeverity.Error,message,"FirmamentV2.ImportedHoleRevision")
            { SourceSpan=replacement.SourceSpan is {} span ? new(span.Start,span.Length) : null }]);
        var imported = solid.InlineStep!;
        if (imported.SourceHashPin is null || replacement.WorldCenter is null || replacement.Within is null)
            return Failure("firmament-imported-hole-binding-incomplete: A pinned source, Point3 center and within envelope are required.");
        // Source annotation migration is outside this first release. Requiring explicit
        // exclusion also covers vendor graphs unknown to the bounded PMI inspector.
        if (!imported.GeometryOnly)
            return Failure("firmament-imported-hole-source-semantics-unsupported: Imported annotations cannot yet be safely migrated. Use an explicitly authored geometryOnly: true import to exclude them, or retain the original model without this edit.");
        var tolerance = imported.ImportToleranceMillimetres ?? 1e-3;
        var body = imported.RuntimeBody;
        if (body is null) return Failure("firmament-imported-hole-import-unavailable: The admitted immutable imported body is missing.");
        if (body.ImportQualification?.GeometryStatus != ImportQualificationStatus.Qualified)
            return Failure("firmament-imported-hole-import-unqualified: Required imported trims are not qualified at importTolerance. Inspect the import diagnostics; the tolerance is not widened automatically.");
        var region = document.RecognizedRegions!.Single(r => r.BodyName == solid.Name && r.RegionName == replacement.RecognizedRegionName);
        FaceId? Resolve(string entity) => imported.TopologyMap.TryResolveFaceEntity(entity, out var id) &&
            int.TryParse(id.AsSpan("face-".Length), out var number) ? new(number) : null;
        var selected = region.FaceRefs.Select(Resolve).ToArray();
        var marker = replacement.PlacementTarget.IndexOf("\"", StringComparison.Ordinal);
        var end = replacement.PlacementTarget.LastIndexOf('"');
        var entry = marker >= 0 && end > marker ? Resolve(replacement.PlacementTarget[(marker + 1)..end]) : null;
        if (selected.Any(id => id is null) || entry is null) return Failure("firmament-imported-hole-selection-unresolved: Resolve every wall and the entry support in the pinned source.");
        var edit = BrepThroughHoleRevision.Execute(body, new(replacement.ReplacementFeatureName, selected.Select(id => id!.Value).ToArray(), entry.Value,
            replacement.WorldCenter.Value, replacement.Radius * 2, replacement.Within, tolerance) { DestinationWithin = replacement.DestinationWithin });
        if (!edit.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(edit.Diagnostics.Select(d => d with
        {
            SourceSpan = replacement.SourceSpan is { } span ? new(span.Start, span.Length) : null
        }));
        var revised = edit.Value;
        if (region.Evidence is { } evidence && (
            evidence.Radius is { } radius && System.Math.Abs(radius * 2 - revised.Diameter) > 1e-6 || evidence.Through == false
            || evidence.SurfaceFamilies.Any(f => f != "cylindrical")
            || evidence.Axis is { } axis && axis is not ("Z" or "+Z" or "-Z")
            || evidence.Center is { } oldCenter && selected.Any(id => !body.TryGetFaceSurfaceGeometry(id!.Value, out var s) || s?.Cylinder is not { } c
                || System.Math.Abs(c.Origin.X - oldCenter.U) > 1e-6 || System.Math.Abs(c.Origin.Y - oldCenter.V) > 1e-6)))
            return Failure("firmament-imported-hole-evidence-mismatch: Authored evidence disagrees with the proved through-hole geometry.");
        var pmiAdmission = ValidateV2PmiExportSupport(document);
        if (!pmiAdmission.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(pmiAdmission.Diagnostics);
        var mapped = imported.TopologyMap.FaceEntityToFaceId.ToDictionary(p => p.Key, p =>
        {
            var old = Resolve(p.Key)!.Value; return "face-" + revised.FaceCorrespondence[old].Value;
        });
        var updatedSolid = solid with { Primitive = imported with { TopologyMap = new(mapped, mapped.ToDictionary(p => p.Value, p => p.Key)), RuntimeBody = revised.Body } };
        var bound = document with { Solids = document.Solids.Select(s => s.Name == solid.Name ? updatedSolid : s).ToArray() };
        var pmi = BuildV2SemanticPmi(bound, [], solid.Name, body: revised.Body).ToList();
        var generatedHolePmi = new Step242SemanticPmiHole(solid.Name + "." + replacement.ReplacementFeatureName, revised.Diameter, revised.Depth, "Shaft", null, null)
        {
            Name = replacement.ReplacementFeatureName,
            GeometricFaceIds = revised.WallFaces.Select(id => id.Value).ToArray()
        };
        pmi.Add(generatedHolePmi);
        var targetAdmission = ValidateImportedPmiTargets(pmi, revised.Body);
        if (!targetAdmission.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(targetAdmission.Diagnostics);
        var exported = Step242Exporter.ExportBody(revised.Body, pmi, new Step242ExportOptions
        {
            ProductName = document.ModelName,
            ApplicationName = "Aetheris.Firmament.ImportedHoleRevision",
            DistanceAccuracyMillimetres = System.Math.Max(body.ImportQualification?.SourceDistanceAccuracyMillimetres ?? 0, tolerance),
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            ImportedRecoveryToleranceMillimetres = tolerance
        });
        if (!exported.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(exported.Diagnostics);
        var reimport = Step242Importer.ImportBody(exported.Value, new ImportPolicy(PcurveQualificationToleranceMillimetres: tolerance));
        if (!reimport.IsSuccess || reimport.Value.ImportQualification?.GeometryStatus != ImportQualificationStatus.Qualified)
            return Failure("firmament-imported-hole-roundtrip-unqualified: The successor failed qualified STEP roundtrip.");
        IReadOnlyList<KernelDiagnostic> warnings = imported.GeometryOnly ? [new(KernelDiagnosticCode.ValidationFailed,KernelDiagnosticSeverity.Warning,
            "firmament-inline-step-source-semantics-excluded: geometryOnly explicitly excludes the imported annotation layer. The original source file remains unchanged; replacement-hole PMI is generated from the authored feature.","FirmamentV2.InlineStep")
            {SourceSpan=solid.SourceSpan is {} span?new(span.Start,span.Length):null}] : [];
        return KernelResult<FirmamentStepExportResult>.Success(new(exported.Value, solid.Name, 0, "inline-step-revision", "through-hole-suppression-reinsertion",
            DatumInspection: pmi.OfType<Step242SemanticPmiDatum>().Select(d => new FirmamentPmiInspectionDatum(d.Label, "planar", d.Target)).ToArray(),
            DimensionInspection: pmi.OfType<Step242SemanticPmiHole>().Select(h => new FirmamentPmiInspectionDimension(
                "Diameter", h.FeatureId, null, h.Diameter, "imported-hole-revision-pmi", h.Name)).ToArray(),
            InlineStepMigration: InlineStepMigrationReportBuilder.Build(document, solid, true, true, "through-hole-suppression-reinsertion", true))
        {
            GeneratedSemanticPmi = [generatedHolePmi],
            DocumentName = document.ModelName,
            RuntimeBody = revised.Body,
            RuntimeBodySourceName = solid.Name,
            RuntimeAdmissionDocument = bound,
            ImportedHoleRevision = new(replacement.ReplacementFeatureName, imported.SourceHashPin, imported.TextHashPin ? "sha256-utf8-lf" : "sha256-bytes", imported.SourceByteHash, "ThroughHoleSuppressionReinsertion", imported.GeometryOnly, tolerance,
                revised.RetiredWallFaces.Select(id => id.Value).ToArray(), revised.WallFaces.Select(id => id.Value).ToArray(), revised.EntryFace.Value, revised.ExitFace.Value,
                revised.Diameter, revised.Depth, revised.Within, revised.PreservedFaces, revised.PreservedEdges, revised.FaceCorrespondence.ToDictionary(p => p.Key.Value, p => p.Value.Value)) { DestinationWithin = revised.DestinationWithin }
        }, warnings);
    }
}
