using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private static IEnumerable<ResolvedProfile2D> RimSupportProfiles(string source,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles, PrismaticSectionStackConstruction stack)
    {
        var stock = stack.Feature.Operations.Where(o => o.Intent == PrismaticProfileIntent.Base).ToArray();
        if (ProfileBoundaryChamferSourceBinder.HasHostBoundaryFinish(source) && stock.Length == 1)
            return profiles.Where(p => p.Key == stock[0].ProfileReference).Select(p => p.Value);
        return profiles.Values.OrderBy(p => p.Name, StringComparer.Ordinal);
    }

    private static KernelResult<FirmamentStepExportResult> ExportComposedProfileRim(string source,
        PrismaticSectionStackConstruction stack, ResolvedProfile2D profile, ProfileBoundaryChamferTarget target,
        double amount, bool fillet, double clearance, PrismaticSectionStackEmissionResult emitted, FirmamentV2Document? document)
    {
        var declaration = ProfileBoundaryChamferSourceBinder.FinishDeclarations(source)
            .Single(d => $"edgefinish:{d.Name?.Text}" == target.StableId);
        KernelResult<FirmamentStepExportResult> Fail(IEnumerable<string> messages) => KernelResult<FirmamentStepExportResult>.Failure(
            messages.Select(message => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                message, "Firmament.ProfileRimFinish") { SourceSpan = new(declaration.Span.Start, declaration.Span.Length) }).ToArray());
        var planned = ProfileRimCavityRetention.TryPlan(stack, profile, target, amount, fillet, clearance, emitted);
        if (!planned.Succeeded || planned.Body is null || planned.Correspondence is null) return Fail(planned.Diagnostics);
        if (!FirmamentManifoldChecker.IsManifold(planned.Body)) return Fail(["ProfileRimFinishNonManifold"]);
        var pmi = Array.Empty<Step242SemanticPmi>();
        if (document is not null)
        {
            var support = ValidateV2PmiExportSupport(document);
            if (!support.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(support.Diagnostics);
            var features = (stack.Feature.ShaftHoles ?? []).ToDictionary(h => h.Name, h => h.StableId, StringComparer.Ordinal);
            foreach (var hole in stack.Feature.CounterboreHoles ?? []) features[hole.Name] = hole.StableId;
            foreach (var hole in stack.Feature.CountersinkHoles ?? []) features[hole.Name] = hole.StableId;
            pmi = BuildV2SemanticPmi(document, [], stack.Feature.Name, features, planned.Correspondence, planned.Body).ToArray();
        }
        var step = Step242Exporter.ExportBody(planned.Body, pmi, new Step242ExportOptions {
            ProductName = stack.Feature.Name, ApplicationName = "Aetheris.ProfileRimFinish", BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var imported = Step242Importer.ImportBody(step.Value);
        if (!imported.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(imported.Diagnostics);
        if (!FirmamentManifoldChecker.IsManifold(imported.Value)) return Fail(["ProfileRimFinishRoundtripNonManifold"]);
        return KernelResult<FirmamentStepExportResult>.Success(new(step.Value, stack.Feature.Name, 0,
            "profile-rim-cavities", "ProfileRimCavityRetention", Features: CompositionHoleReports(stack.Feature, planned.Body, planned.Correspondence),
            EngineeringFeatures: EngineeringFeatureReports(stack.Feature).Append(new FirmamentEngineeringFeatureReport(
                target.StableId["edgefinish:".Length..], "EdgeFinish", target.StableId, target.HostBodyId, target.Side.ToString(),
                $"{target.ProfileId}.{target.LoopId}", amount, fillet ? "Radius" : "Distance", "Remove", PolicySource: fillet ? "Fillet" : "Chamfer",
                MaterializationRoute: "ProfileRimCavityRetention")).ToArray())
        {
            RuntimeBody = planned.Body, RuntimeCorrespondence = planned.Correspondence, RuntimeBodySourceName = stack.Feature.Name,
            EdgeFinishPlanning = planned.PlanningEvidence,
            MaterializedFeatures = FirmamentFeatureAccounting.Composition(stack, planned.Correspondence)
                .Concat(FirmamentFeatureAccounting.ProfileFinish(source, target.StableId["edgefinish:".Length..], target.HostBodyId)).ToArray()
        });
    }
}
