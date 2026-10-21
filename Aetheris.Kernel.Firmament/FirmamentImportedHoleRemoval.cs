using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentImportedHoleRemovalReport(string Feature, string SourceHash, string SourceHashKind, string? SourceByteHash,
    string Strategy, bool SourceSemanticsExcluded, double ImportToleranceMillimetres, IReadOnlyList<int> RetiredWallFaces,
    int EntryFace, int ExitFace, double Diameter, double Depth, ThroughHoleEditBounds Within, int PreservedFaces, int PreservedEdges,
    IReadOnlyDictionary<int, int> FaceCorrespondence);

public static partial class FirmamentBuildAndExport
{
    static KernelResult<FirmamentStepExportResult> ExportImportedHoleRemoval(FirmamentV2Document document,
        FirmamentV2SolidBinding solid, FirmamentV2RemovalDecl removal)
    {
        KernelResult<FirmamentStepExportResult> Failure(string message) => KernelResult<FirmamentStepExportResult>.Failure([
            new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, "FirmamentV2.ImportedHoleRemoval")
            { SourceSpan = new(removal.SourceSpan.Start, removal.SourceSpan.Length) }]);
        var imported = solid.InlineStep!;
        var tolerance = imported.ImportToleranceMillimetres ?? .001;
        if (!imported.GeometryOnly) return Failure("firmament-imported-hole-source-semantics-unsupported: Remove requires explicit geometryOnly: true; imported annotation migration is not qualified.");
        if (imported.RuntimeBody is not { } body || body.ImportQualification?.GeometryStatus != ImportQualificationStatus.Qualified)
            return Failure("firmament-imported-hole-import-unqualified: Remove requires the pinned, qualified source body.");
        if (document.Pmi is { Count: > 0 } || document.PmiBlock?.Records.Count > 0)
            return Failure("firmament-imported-hole-removal-pmi-unsupported: Explicit PMI rebinding across removal is not qualified. Annotate the successor separately; source PMI is excluded only by explicit geometryOnly permission.");
        var region = document.RecognizedRegions!.Single(r => r.BodyName == solid.Name && r.RegionName == removal.RecognizedRegionName);
        FaceId? Resolve(string entity) => imported.TopologyMap.TryResolveFaceEntity(entity, out var id)
            && int.TryParse(id.AsSpan("face-".Length), out var number) ? new(number) : null;
        var selected = region.FaceRefs.Select(Resolve).ToArray();
        if (selected.Any(f => f is null)) return Failure("firmament-imported-hole-selection-unresolved: Remove requires complete resolved wall selection.");
        var edit = BrepThroughHoleRevision.Remove(body, new(removal.RecognizedRegionName, selected.Select(f => f!.Value).ToArray(), removal.Within, tolerance));
        if (!edit.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(edit.Diagnostics.Select(d => d with { SourceSpan = new(removal.SourceSpan.Start, removal.SourceSpan.Length) }));
        var revised = edit.Value;
        if (!ImportedHoleEvidenceAgrees(region, body, selected.Select(f => f!.Value), revised.Diameter))
            return Failure("firmament-imported-hole-evidence-mismatch: Authored evidence disagrees with the proved through-hole geometry.");
        var exported = Step242Exporter.ExportBody(revised.Body, new Step242ExportOptions
        {
            ProductName = document.ModelName,
            ApplicationName = "Aetheris.Firmament.ImportedHoleRemoval",
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            ImportedRecoveryToleranceMillimetres = tolerance,
            DistanceAccuracyMillimetres = System.Math.Max(body.ImportQualification.SourceDistanceAccuracyMillimetres ?? 0, tolerance)
        });
        if (!exported.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(exported.Diagnostics);
        var roundtrip = Step242Importer.ImportBody(exported.Value, new ImportPolicy(PcurveQualificationToleranceMillimetres: tolerance));
        if (!roundtrip.IsSuccess || roundtrip.Value.ImportQualification?.GeometryStatus != ImportQualificationStatus.Qualified)
            return Failure("firmament-imported-hole-roundtrip-unqualified: The removal successor failed qualified STEP roundtrip.");
        var map = imported.TopologyMap.FaceEntityToFaceId.Where(p => Resolve(p.Key) is { } id && revised.FaceCorrespondence.ContainsKey(id)).ToDictionary(p => p.Key, p => p.Value);
        var bound = document with { Solids = [solid with { Primitive = imported with { RuntimeBody = revised.Body, TopologyMap = new(map, map.ToDictionary(p => p.Value, p => p.Key)) } }] };
        var migration = InlineStepMigrationReportBuilder.Build(document, solid, emissionStrategy: "through-hole-suppression", residualSurgery: true) with
        {
            Removals = new(1, 1, 1, revised.RetiredWallFaces.Count),
            Residual = new(revised.PreservedFaces, revised.PreservedFaces),
            ReplacementStates = []
        };
        return KernelResult<FirmamentStepExportResult>.Success(new(exported.Value, solid.Name, 0, "inline-step-removal", "through-hole-suppression", InlineStepMigration: migration)
        {
            DocumentName = document.ModelName,
            RuntimeBody = revised.Body,
            RuntimeBodySourceName = solid.Name,
            RuntimeAdmissionDocument = bound,
            ImportedHoleRemoval = new(removal.RecognizedRegionName, imported.SourceHashPin!, imported.TextHashPin ? "sha256-utf8-lf" : "sha256-bytes", imported.SourceByteHash,
                "ThroughHoleSuppression", true, tolerance, revised.RetiredWallFaces.Select(f => f.Value).ToArray(), revised.EntryFace.Value, revised.ExitFace.Value, revised.Diameter, revised.Depth,
                removal.Within, revised.PreservedFaces, revised.PreservedEdges, revised.FaceCorrespondence.ToDictionary(p => p.Key.Value, p => p.Value.Value))
        },
            [new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning, "firmament-inline-step-source-semantics-excluded: geometryOnly explicitly excludes source annotations. The removed hole has no successor diameter PMI.", "FirmamentV2.InlineStep")
            { SourceSpan = solid.SourceSpan is {} span ? new(span.Start, span.Length) : null }]);
    }
}
