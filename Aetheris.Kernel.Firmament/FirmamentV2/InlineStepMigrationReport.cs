namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record InlineStepMigrationReport(
    string ImportedBodyName,
    string SourcePath,
    string SourceHash,
    InlineStepOriginalTopologyAccounting OriginalTopology,
    InlineStepRecognizedAccounting Recognized,
    InlineStepReplacementAccounting Replacements,
    InlineStepResidualAccounting Residual,
    InlineStepCoverageAccounting Coverage,
    string EmissionStrategy,
    bool ResidualSurgery,
    IReadOnlyList<string> ReplacementStates,
    IReadOnlyList<string> Diagnostics);

public sealed record InlineStepOriginalTopologyAccounting(int FaceCount, int EdgeCount, int VertexCount);
public sealed record InlineStepRecognizedAccounting(int RegionCount, int ReferencedFaceCount, int DuplicateReferencedFaceCount, int UnresolvedReferenceCount, int EvidenceCount = 0, int ProposalCount = 0, int ProposalVerifiedCount = 0, int ProposalUnverifiedCount = 0, int ProposalAssistReadyCount = 0, int ProposalAssistBlockedCount = 0);
public sealed record InlineStepReplacementAccounting(int PlannedCount, int VerifiedCount, int EmittedCount, int FailedCount, int ReplacedFaceCount);
public sealed record InlineStepResidualAccounting(int ResidualFaceCount, int UnclaimedFaceCount);
public sealed record InlineStepCoverageAccounting(double RecognizedFaceRatio, double ReplacedFaceRatio);

public static class InlineStepMigrationReportBuilder
{
    public static InlineStepMigrationReport Build(
        FirmamentV2Document document,
        FirmamentV2SolidBinding solid,
        bool replacementsVerified = false,
        bool replacementsEmitted = false,
        string emissionStrategy = "canonical-reexport",
        bool residualSurgery = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(solid);
        var inlineStep = solid.InlineStep ?? throw new ArgumentException("Solid must be an InlineStep body.", nameof(solid));
        var diagnostics = new List<string>();
        var originalFaceCount = inlineStep.TopologyMap.FaceEntityToFaceId.Count;
        if (originalFaceCount == 0) diagnostics.Add("inline-step-migration-original-face-count-zero");

        var regions = (document.RecognizedRegions ?? []).Where(r => string.Equals(r.BodyName, solid.Name, StringComparison.Ordinal)).ToArray();
        var uniqueRecognized = new HashSet<string>(StringComparer.Ordinal);
        var duplicateCount = 0;
        var unresolvedCount = 0;
        foreach (var faceRef in regions.SelectMany(r => r.FaceRefs))
        {
            if (!inlineStep.TopologyMap.TryResolveFaceEntity(faceRef, out _))
            {
                unresolvedCount++;
                diagnostics.Add($"inline-step-migration-unresolved-face:{faceRef}");
                continue;
            }

            if (!uniqueRecognized.Add(faceRef))
            {
                duplicateCount++;
                diagnostics.Add($"inline-step-migration-duplicate-face-reference:{faceRef}");
            }
        }

        var assist = InlineStepReplacementAssistReportBuilder.Build(document);
        var replacements = (document.Replacements ?? []).Where(r => string.Equals(r.ImportedBodyName, solid.Name, StringComparison.Ordinal)).ToArray();
        var replacedFaces = new HashSet<string>(StringComparer.Ordinal);
        if (replacementsVerified)
        {
            foreach (var replacement in replacements)
            {
                var region = regions.SingleOrDefault(r => string.Equals(r.RegionName, replacement.RecognizedRegionName, StringComparison.Ordinal));
                if (region is null) continue;
                foreach (var faceRef in region.FaceRefs)
                {
                    if (inlineStep.TopologyMap.TryResolveFaceEntity(faceRef, out _)) replacedFaces.Add(faceRef);
                }
            }
        }

        var plannedCount = replacements.Length;
        var verifiedCount = replacementsVerified ? plannedCount : 0;
        var emittedCount = replacementsEmitted ? verifiedCount : 0;
        var failedCount = plannedCount - verifiedCount;
        var residualFaceCount = Math.Max(0, originalFaceCount - replacedFaces.Count);
        var states = BuildStates(regions.Length, plannedCount, verifiedCount, emittedCount).ToArray();
        if (!residualSurgery && replacedFaces.Count > 0) diagnostics.Add("inline-step-migration-residual-surgery-not-performed");
        if (emissionStrategy.Contains("bounded-rebuild", StringComparison.Ordinal)) diagnostics.Add("inline-step-migration-emission-bounded-rebuild");

        return new InlineStepMigrationReport(
            solid.Name,
            inlineStep.SourcePath,
            inlineStep.ContentHash,
            new InlineStepOriginalTopologyAccounting(originalFaceCount, 0, 0),
            new InlineStepRecognizedAccounting(regions.Length, uniqueRecognized.Count, duplicateCount, unresolvedCount, regions.Count(r => r.Evidence is not null), regions.Count(r => r.Proposal is not null), 0, regions.Count(r => r.Proposal is not null), assist.ReadyCount, assist.BlockedCount),
            new InlineStepReplacementAccounting(plannedCount, verifiedCount, emittedCount, failedCount, replacedFaces.Count),
            new InlineStepResidualAccounting(residualFaceCount, residualFaceCount),
            new InlineStepCoverageAccounting(Ratio(uniqueRecognized.Count, originalFaceCount), Ratio(replacedFaces.Count, originalFaceCount)),
            emissionStrategy,
            residualSurgery,
            states,
            diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<string> BuildStates(int regionCount, int plannedCount, int verifiedCount, int emittedCount)
    {
        if (regionCount > 0) yield return "recognized";
        if (plannedCount > 0) yield return "replacement-planned";
        if (verifiedCount > 0) yield return "replacement-verified";
        yield return "residual-emitted";
        if (emittedCount > 0) yield return "hybrid-step-verified";
    }

    private static double Ratio(int value, int total) => total <= 0 ? 0d : value / (double)total;
}
