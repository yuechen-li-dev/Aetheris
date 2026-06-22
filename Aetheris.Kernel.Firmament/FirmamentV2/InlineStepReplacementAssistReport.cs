using System.Globalization;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record InlineStepReplacementAssistReport(
    IReadOnlyList<FirmamentV2ReplacementAssistReport> Assists,
    int ReadyCount,
    int BlockedCount);

public sealed record FirmamentV2ReplacementAssistReport(
    string BodyName,
    string RegionName,
    string ProposalKind,
    bool ReplacementReady,
    string? SuggestedReplacementText,
    FirmamentV2ReplacementAssistModel? SuggestedReplacementModel,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Diagnostics);

public sealed record FirmamentV2ReplacementAssistModel(
    string Target,
    string ReplacementKind,
    string FeatureName,
    string PlacementTarget,
    FirmamentV2FaceLocalPoint2D Center,
    double Radius,
    string EndCondition);

public static class InlineStepReplacementAssistReportBuilder
{
    public const string UnsupportedProposalKind = "inline-step-replacement-assist-unsupported-proposal-kind";
    public const string RegionProposalKindMismatch = "inline-step-replacement-assist-region-proposal-kind-mismatch";
    public const string MissingPlacementTarget = "inline-step-replacement-assist-missing-placement-target";
    public const string UnresolvedPlacementTarget = "inline-step-replacement-assist-unresolved-placement-target";
    public const string InvalidRadius = "inline-step-replacement-assist-invalid-radius";
    public const string UnsupportedEndCondition = "inline-step-replacement-assist-unsupported-end-condition";
    public const string MissingCenter = "inline-step-replacement-assist-missing-center";
    public const string EvidenceRadiusMismatch = "inline-step-replacement-assist-evidence-radius-mismatch";
    public const string EvidenceThroughFalse = "inline-step-replacement-assist-evidence-through-false";
    public const string EvidenceSurfaceNotCylindrical = "inline-step-replacement-assist-evidence-surface-not-cylindrical";

    public static InlineStepReplacementAssistReport Build(FirmamentV2Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var assists = new List<FirmamentV2ReplacementAssistReport>();
        foreach (var region in document.RecognizedRegions ?? [])
        {
            if (region.Proposal is null) continue;
            assists.Add(BuildForRegion(document, region));
        }
        return new InlineStepReplacementAssistReport(assists, assists.Count(a => a.ReplacementReady), assists.Count(a => !a.ReplacementReady));
    }

    private static FirmamentV2ReplacementAssistReport BuildForRegion(FirmamentV2Document document, FirmamentV2RecognizedRegion region)
    {
        var proposal = region.Proposal!;
        var reasons = new List<string>();
        var diagnostics = new List<string>();
        if (region.Kind != proposal.ProposalKind) reasons.Add(RegionProposalKindMismatch);
        if (proposal.ProposalKind != "holeShaft") reasons.Add(UnsupportedProposalKind);
        if (string.IsNullOrWhiteSpace(proposal.PlacementTarget)) reasons.Add(MissingPlacementTarget);
        else if (!PlacementTargetResolves(document, proposal.PlacementTarget!)) reasons.Add(UnresolvedPlacementTarget);
        if (proposal.Radius is not > 0d || double.IsNaN(proposal.Radius.Value) || double.IsInfinity(proposal.Radius.Value)) reasons.Add(InvalidRadius);
        if (proposal.EndCondition != "throughAll") reasons.Add(UnsupportedEndCondition);
        if (proposal.Center is null) reasons.Add(MissingCenter);
        if (region.Evidence?.Through == false) reasons.Add(EvidenceThroughFalse);
        if (region.Evidence?.SurfaceFamilies is { Count: > 0 } families && !families.Contains("cylindrical", StringComparer.Ordinal)) reasons.Add(EvidenceSurfaceNotCylindrical);
        if (region.Evidence?.Radius is double evidenceRadius && proposal.Radius is double proposalRadius && Math.Abs(evidenceRadius - proposalRadius) > 1e-6) reasons.Add(EvidenceRadiusMismatch);

        var ready = reasons.Count == 0;
        FirmamentV2ReplacementAssistModel? model = null;
        string? text = null;
        if (ready)
        {
            model = new FirmamentV2ReplacementAssistModel(region.TargetSource, "holeShaft", proposal.FeatureName, proposal.PlacementTarget!, proposal.Center!, proposal.Radius!.Value, proposal.EndCondition!);
            text = FormatReplacement(region, proposal);
        }
        else
        {
            diagnostics.Add("inline-step-replacement-assist-blocked");
        }

        return new FirmamentV2ReplacementAssistReport(region.BodyName, region.RegionName, proposal.ProposalKind, ready, text, model, reasons.Distinct(StringComparer.Ordinal).ToArray(), diagnostics.ToArray());
    }

    private static bool PlacementTargetResolves(FirmamentV2Document document, string target)
    {
        foreach (var solid in document.Solids)
        {
            if (solid.InlineStep is null) continue;
            var prefix = solid.Name + ".face(\"";
            if (target.StartsWith(prefix, StringComparison.Ordinal) && target.EndsWith("\")", StringComparison.Ordinal))
            {
                var entity = target[prefix.Length..^2];
                return solid.InlineStep.TopologyMap.TryResolveFaceEntity(entity, out _);
            }
        }
        return false;
    }

    private static string FormatReplacement(FirmamentV2RecognizedRegion region, FirmamentV2SemanticProposal proposal) =>
        string.Join(Environment.NewLine,
        [
            $"replace {region.TargetSource} with hole<shaft> {proposal.FeatureName} {{",
            $"    on: {proposal.PlacementTarget}",
            $"    center: [{FormatMm(proposal.Center!.U)}, {FormatMm(proposal.Center.V)}]",
            $"    radius: {FormatMm(proposal.Radius!.Value)}",
            "    end: throughAll",
            "}"
        ]);

    private static string FormatMm(double value) => value.ToString("0.##########", CultureInfo.InvariantCulture) + "mm";
}
