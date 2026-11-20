using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private static KernelResult<FirmamentStepExportResult> ExportImportedEdgeFinishes(FirmamentV2Document document)
    {
        var blocks = document.ModifyBlocks!;
        var finishes = blocks.SelectMany(block => block.EdgeFinishes ?? []).ToArray();
        if (document.Solids.Count != 1 || document.Solids[0].InlineStep is not { } imported
            || blocks.Any(block => block.SemanticHoles.Count != 0 || block.Regions.Count != 0 || block.Perforations is { Count: > 0 })
            || document.Replacements is { Count: > 0 } || document.Removals.Count != 0 || document.Threads is { Count: > 0 }
            || finishes.Any(finish => finish.ImportedTarget is null))
            return Fail("combination", "Use imported edge finishes on one STEP solid in a separate revision from other material operations.");
        if (imported.SourceHashPin is null)
            return Fail("source-pin", "Pin the STEP with firmament.lock.toml, sha256, or textSha256 before editing it.");
        if (!imported.GeometryOnly)
            return Fail("source-pmi", "Use geometryOnly: true to exclude source annotations before this topology edit.");
        if (imported.RuntimeBody is not { ImportQualification.GeometryStatus: ImportQualificationStatus.Qualified } body)
            return Fail("import-unqualified", "The imported solid must qualify at the requested importTolerance.");

        var solid = document.Solids[0];
        var tolerance = imported.ImportToleranceMillimetres ?? .001;
        var plans = new List<string>(finishes.Length);
        foreach (var finish in finishes)
        {
            var target = finish.ImportedTarget!;
            var kind = finish.Kind == "Chamfer" ? ImportedEdgeFinishKind.Chamfer : ImportedEdgeFinishKind.Fillet;
            KernelResult<ImportedEdgeFinishWitness> edit;
            if (target.IsBoundary)
            {
                var matches = body.Bindings.FaceBindings.Where(binding => binding.SourceStepEntityId == target.StepEntity).ToArray();
                if (matches.Length != 1)
                    return Fail("face-unresolved", $"STEP face #{target.StepEntity} must resolve to exactly one current face.", finish);
                edit = BrepImportedEdgeFinish.TryApplyBoundary(body, matches[0].FaceId, kind, finish.Distance, tolerance, target.Clearance);
            }
            else
            {
                var matches = body.Bindings.EdgeBindings.Where(binding => binding.SourceStepEntityId == target.StepEntity).ToArray();
                if (matches.Length != 1)
                    return Fail("edge-unresolved", $"STEP edge #{target.StepEntity} must resolve to exactly one current edge. An earlier finish may have replaced it.", finish);
                edit = BrepImportedEdgeFinish.TryApply(body, matches[0].EdgeId, kind, finish.Distance, tolerance, target.Clearance);
            }

            if (!edit.IsSuccess)
                return KernelResult<FirmamentStepExportResult>.Failure(edit.Diagnostics.Select(diagnostic => diagnostic with
                    { SourceSpan = new(finish.SourceSpan.Start, finish.SourceSpan.Length) }));
            body = edit.Value.Body;
            plans.Add(edit.Value.Strategy);
        }

        var admitted = document with { Solids = [solid with { Primitive = imported with { RuntimeBody = body } }] };
        var pmiAdmission = ValidateV2PmiExportSupport(admitted);
        if (!pmiAdmission.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(pmiAdmission.Diagnostics);
        var pmi = BuildV2SemanticPmi(admitted, [], solid.Name, body: body);
        var pmiTargets = ValidateImportedPmiTargets(pmi, body);
        if (!pmiTargets.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(pmiTargets.Diagnostics);
        var step = Step242Exporter.ExportBody(body, pmi, new Step242ExportOptions
        {
            ProductName = document.ModelName,
            ApplicationName = "Aetheris.Firmament.ImportedEdgeFinish",
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            DistanceAccuracyMillimetres = double.Max(imported.RuntimeBody.ImportQualification?.SourceDistanceAccuracyMillimetres ?? 0, tolerance),
            ImportedRecoveryToleranceMillimetres = tolerance
        });
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reopened = Step242Importer.ImportBody(step.Value, new ImportPolicy(PcurveQualificationToleranceMillimetres: tolerance));
        if (!reopened.IsSuccess || reopened.Value.ImportQualification?.GeometryStatus != ImportQualificationStatus.Qualified)
            return Fail("roundtrip", "The finished body failed qualified STEP reimport.");

        var strategy = string.Join(",", plans);
        return KernelResult<FirmamentStepExportResult>.Success(new(step.Value, solid.Name, 0, "inline-step-edge-finish", strategy,
            InlineStepMigration: InlineStepMigrationReportBuilder.Build(admitted, solid, emissionStrategy: strategy, residualSurgery: true))
        {
            RuntimeBody = body,
            RuntimeBodySourceName = solid.Name,
            RuntimeAdmissionDocument = admitted,
            DocumentName = document.ModelName,
            MaterializedFeatures = FirmamentFeatureAccounting.Receipts(solid.Name, "EdgeFinish", finishes.Select(finish => finish.Name).ToArray())
        }, [new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning,
            "firmament-inline-step-source-semantics-excluded: geometryOnly explicitly excludes source annotations.", "FirmamentV2.InlineStep")]);

        KernelResult<FirmamentStepExportResult> Fail(string code, string message, FirmamentV2EdgeFinishDecl? finish = null) =>
            KernelResult<FirmamentStepExportResult>.Failure([new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                "firmament-imported-finish-" + code + ": " + message, "FirmamentV2.ImportedEdgeFinish")
                { SourceSpan = (finish ?? finishes.FirstOrDefault())?.SourceSpan is { } span ? new(span.Start, span.Length) : null }]);
    }
}
