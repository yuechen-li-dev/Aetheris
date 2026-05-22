using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum TieredTrimRepresentationKind
{
    AnalyticLine,
    AnalyticCircle,
    NumericalOnly,
    Deferred,
    Unsupported,
}

internal enum TieredTrimExportCapability
{
    ElementaryCurveCandidate,
    NumericalOnlyNotExportable,
    Deferred,
    Unsupported,
}

internal sealed record TrimSurfaceIntersectionProvenance(
    string? SourceSurfaceKey,
    SurfacePatchFamily? SourceSurfaceFamily,
    string? OppositeFieldKind,
    string? OppositeOperandRole,
    IReadOnlyList<string> RestrictedFieldDiagnostics,
    int? ChainId,
    RestrictedContourSnapRouteKind SnapRoute,
    IReadOnlyList<string> Diagnostics);

internal sealed record AnalyticCircleTrimData(
    double CenterU,
    double CenterV,
    double RadiusUV,
    double MaxError,
    double MeanError,
    int SampleCount);

internal sealed record AnalyticLineTrimData(
    double PointU,
    double PointV,
    double DirectionU,
    double DirectionV,
    double MaxError,
    double MeanError,
    int SampleCount);

internal sealed record NumericalTrimContourData(
    int ChainId,
    IReadOnlyList<SurfaceTrimContourChainPoint2D> PointsUV,
    bool Closed,
    SurfaceTrimContourChainStatus Status,
    IReadOnlyList<string> Diagnostics);

internal sealed record TieredTrimCurveRepresentation(
    TieredTrimRepresentationKind Kind,
    TieredTrimExportCapability ExportCapability,
    AnalyticCircleTrimData? Circle,
    AnalyticLineTrimData? Line,
    NumericalTrimContourData? NumericalContour,
    TrimSurfaceIntersectionProvenance SurfaceIntersectionProvenance,
    bool AcceptedInternalAnalyticCandidate,
    bool ExactStepExported,
    bool BRepTopologyEmitted,
    IReadOnlyList<string> Diagnostics);

internal sealed record TieredTrimRepresentationBuildResult(
    bool Success,
    TieredTrimCurveRepresentation Representation,
    IReadOnlyList<string> Diagnostics);

internal static class TieredTrimRepresentationBuilder
{
    internal static TieredTrimRepresentationBuildResult Build(
        RestrictedContourSnapSelectionResult selection,
        SurfaceTrimContourStitchResult stitchResult,
        SurfaceRestrictedField? field)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(stitchResult);

        var diagnostics = new List<string> { "tiered-trim-build-started" };

        var chainId = selection.SelectedCandidate?.ChainId;
        var chain = chainId.HasValue ? stitchResult.Chains.FirstOrDefault(c => c.ChainId == chainId.Value) : stitchResult.Chains.FirstOrDefault();
        var num = chain is null
            ? null
            : new NumericalTrimContourData(chain.ChainId, chain.Points, chain.Closed, chain.Status, chain.Diagnostics);

        if (num is not null) diagnostics.Add("tiered-trim-numerical-contour-preserved");

        var provenanceDiagnostics = new List<string>();
        var provenance = BuildProvenance(field, chainId, selection.SelectedRoute, provenanceDiagnostics);
        diagnostics.AddRange(provenanceDiagnostics);

        TieredTrimCurveRepresentation rep;
        switch (selection.SelectedRoute)
        {
            case RestrictedContourSnapRouteKind.AnalyticCircle when selection.SelectedCandidate?.Parameters is CircleSnapParameters2D circle:
                rep = new TieredTrimCurveRepresentation(
                    TieredTrimRepresentationKind.AnalyticCircle,
                    TieredTrimExportCapability.ElementaryCurveCandidate,
                    new AnalyticCircleTrimData(circle.CenterU, circle.CenterV, circle.Radius, selection.SelectedCandidate.MaxError, selection.SelectedCandidate.MeanError, selection.SelectedCandidate.SampleCount),
                    null,
                    num,
                    provenance,
                    true,
                    false,
                    false,
                    ["tiered-trim-analytic-circle-representation-built", "tiered-trim-export-capability-candidate-only", "tiered-trim-step-export-not-performed", "tiered-trim-brep-topology-not-emitted", "tiered-trim-torus-generic-exactness-not-claimed"]);
                break;
            case RestrictedContourSnapRouteKind.AnalyticLine when selection.SelectedCandidate?.Parameters is LineSnapParameters2D line:
                rep = new TieredTrimCurveRepresentation(
                    TieredTrimRepresentationKind.AnalyticLine,
                    TieredTrimExportCapability.ElementaryCurveCandidate,
                    null,
                    new AnalyticLineTrimData(line.PointU, line.PointV, line.DirectionU, line.DirectionV, selection.SelectedCandidate.MaxError, selection.SelectedCandidate.MeanError, selection.SelectedCandidate.SampleCount),
                    num,
                    provenance,
                    true,
                    false,
                    false,
                    ["tiered-trim-analytic-line-representation-built", "tiered-trim-export-capability-candidate-only", "tiered-trim-step-export-not-performed", "tiered-trim-brep-topology-not-emitted"]);
                break;
            case RestrictedContourSnapRouteKind.NumericalOnly:
                rep = new TieredTrimCurveRepresentation(TieredTrimRepresentationKind.NumericalOnly, TieredTrimExportCapability.NumericalOnlyNotExportable, null, null, num, provenance, false, false, false,
                    ["tiered-trim-numerical-only-not-exportable", "tiered-trim-step-export-not-performed", "tiered-trim-brep-topology-not-emitted", "tiered-trim-torus-generic-exactness-not-claimed"]);
                break;
            case RestrictedContourSnapRouteKind.Deferred:
                rep = new TieredTrimCurveRepresentation(TieredTrimRepresentationKind.Deferred, TieredTrimExportCapability.Deferred, null, null, num, provenance, false, false, false,
                    ["tiered-trim-deferred", "tiered-trim-step-export-not-performed", "tiered-trim-brep-topology-not-emitted", "tiered-trim-torus-generic-exactness-not-claimed"]);
                break;
            default:
                rep = new TieredTrimCurveRepresentation(TieredTrimRepresentationKind.Unsupported, TieredTrimExportCapability.Unsupported, null, null, num, provenance, false, false, false,
                    ["tiered-trim-unsupported", "tiered-trim-step-export-not-performed", "tiered-trim-brep-topology-not-emitted", "tiered-trim-torus-generic-exactness-not-claimed"]);
                break;
        }

        diagnostics.AddRange(rep.Diagnostics);
        return new TieredTrimRepresentationBuildResult(selection.Success && stitchResult.Success, rep, diagnostics);
    }

    private static TrimSurfaceIntersectionProvenance BuildProvenance(SurfaceRestrictedField? field, int? chainId, RestrictedContourSnapRouteKind route, List<string> diagnostics)
    {
        var d = new List<string>();
        var sourceKey = field?.SourceSurface.ParameterPayloadReference;
        var sourceFamily = field?.SourceSurface.Family;
        if (string.IsNullOrWhiteSpace(sourceKey)) d.Add("tiered-trim-provenance-missing-source-surface-key");
        var oppositeKind = field?.OppositeTape is null ? null : "CirTape";
        if (oppositeKind is null) d.Add("tiered-trim-provenance-missing-opposite-field-kind");

        var side = field?.Diagnostics.FirstOrDefault(x => x.StartsWith("opposite-operand-selected:", StringComparison.Ordinal));
        var role = side?.Split(':').LastOrDefault();
        if (role is null) d.Add("tiered-trim-provenance-missing-opposite-operand-role");

        diagnostics.Add(d.Count == 0 ? "tiered-trim-surface-intersection-provenance-preserved" : "tiered-trim-surface-intersection-provenance-partial");

        return new TrimSurfaceIntersectionProvenance(sourceKey, sourceFamily, oppositeKind, role, field?.Diagnostics ?? [], chainId, route, d);
    }
}
