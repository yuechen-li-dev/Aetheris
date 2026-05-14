using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.FrictionLab;

public enum FrepBrepRecoveryCapability
{
    ExactBrepAndStep,
    BoundedBrepPreview,
    IntentOnlyFallback
}

public sealed record FrepBrepRecoveryContext(
    CirNode Root,
    NativeGeometryReplayLog? ReplayLog = null,
    IReadOnlyList<string>? UpstreamDiagnostics = null,
    bool TrimOracleAgreementAvailable = false,
    bool TrimOracleAgreement = false,
    bool VolumeAgreementAvailable = false,
    bool VolumeAgreement = false,
    bool ExportCapabilityAvailable = true,
    bool TopologyTemplateReady = false);

public sealed record FrepBrepRecoveryPolicyEvaluation(
    string PolicyName,
    bool Admissible,
    double Score,
    FrepBrepRecoveryCapability Capability,
    string RecommendedRoute,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<string> Diagnostics);

public sealed record FrepBrepRecoveryDecision(
    string? SelectedPolicy,
    string? SelectedRoute,
    IReadOnlyList<FrepBrepRecoveryPolicyEvaluation> Candidates,
    IReadOnlyList<string> Diagnostics);

public interface IFrepBrepRecoveryPolicy
{
    string Name { get; }
    int TieBreakerPriority { get; }
    FrepBrepRecoveryPolicyEvaluation Evaluate(FrepBrepRecoveryContext context);
}

public static class FrepBrepRecoveryPolicyLab
{
    public static readonly IFrepBrepRecoveryPolicy[] DefaultPolicies =
    [
        new BoxCylinderThroughHolePolicy(),
        new GenericNumericalContourPolicy(),
        new CirOnlyFallbackPolicy()
    ];

    public static FrepBrepRecoveryDecision SelectBestPolicy(FrepBrepRecoveryContext context, IReadOnlyList<IFrepBrepRecoveryPolicy>? policies = null)
    {
        var policySet = policies ?? DefaultPolicies;
        var evals = policySet.Select(p => p.Evaluate(context)).ToArray();
        var map = evals.ToDictionary(e => e.PolicyName, StringComparer.Ordinal);
        var engine = new JudgmentEngine<FrepBrepRecoveryContext>();
        var candidates = policySet.Select(p => new JudgmentCandidate<FrepBrepRecoveryContext>(
            p.Name,
            c => map[p.Name].Admissible,
            c => map[p.Name].Score,
            c => map[p.Name].RejectionReasons.Count == 0 ? "inadmissible" : string.Join(" | ", map[p.Name].RejectionReasons),
            p.TieBreakerPriority)).ToArray();
        var result = engine.Evaluate(context, candidates);
        var diagnostics = new List<string> { $"policy_count={policySet.Count}" };
        diagnostics.AddRange(evals.Select(e => $"candidate:{e.PolicyName}:admissible={e.Admissible}:score={e.Score:F3}"));
        diagnostics.Add(result.IsSuccess
            ? $"selected={result.Selection!.Value.Candidate.Name}"
            : "selected=<none>");
        diagnostics.AddRange(result.Rejections.Select(r => $"rejection:{r.CandidateName}:{r.Reason}"));

        var selectedName = result.IsSuccess ? result.Selection!.Value.Candidate.Name : null;
        return new(selectedName, selectedName is null ? null : map[selectedName].RecommendedRoute, evals, diagnostics);
    }

    private sealed class BoxCylinderThroughHolePolicy : IFrepBrepRecoveryPolicy
    {
        public string Name => "BoxCylinderThroughHolePolicy";
        public int TieBreakerPriority => 0;

        public FrepBrepRecoveryPolicyEvaluation Evaluate(FrepBrepRecoveryContext context)
        {
            var evidence = new List<string>();
            var rejects = new List<string>();
            var diagnostics = new List<string>();
            var recognition = CirBrepX4RecognizerSourceContractLab.Recognize(CirBoxCylinderRecognizerInput.FromNode(context.Root, context.ReplayLog));
            if (!recognition.Recognition.Success)
            {
                rejects.Add($"recognizer_failed:{recognition.Recognition.Reason}");
                diagnostics.Add(recognition.Recognition.Diagnostic);
            }
            else
            {
                evidence.Add("recognizer=canonical_box_cylinder");
                evidence.Add("transform=v1_translation_supported");
                evidence.Add("through_hole=passed");
                evidence.Add("strict_clearance=passed");
            }

            var score = 0d;
            if (recognition.Recognition.Success) score += 60;
            if (context.ReplayLog is not null) { evidence.Add("replay=available"); score += recognition.ReplayGeometryMismatch ? 0 : 8; }
            else diagnostics.Add("replay=missing");
            if (context.TrimOracleAgreementAvailable) { evidence.Add($"trim_oracle={context.TrimOracleAgreement}"); score += context.TrimOracleAgreement ? 10 : 0; } else diagnostics.Add("trim_oracle=deferred");
            if (context.VolumeAgreementAvailable) { evidence.Add($"volume_probe={context.VolumeAgreement}"); score += context.VolumeAgreement ? 8 : 0; } else diagnostics.Add("volume_probe=deferred");
            if (context.ExportCapabilityAvailable) { evidence.Add("export=elementary_supported"); score += 8; }
            if (context.TopologyTemplateReady) { evidence.Add("topology_template=ready"); score += 6; } else diagnostics.Add("topology_template=deferred");
            diagnostics.AddRange(recognition.Diagnostics);
            return new(Name, rejects.Count == 0, score, FrepBrepRecoveryCapability.ExactBrepAndStep, "Use existing BRep primitive + BrepBoolean.Subtract exact path", evidence, rejects, diagnostics);
        }
    }

    private sealed class GenericNumericalContourPolicy : IFrepBrepRecoveryPolicy
    {
        public string Name => "GenericNumericalContourPolicy";
        public int TieBreakerPriority => 1;
        public FrepBrepRecoveryPolicyEvaluation Evaluate(FrepBrepRecoveryContext context)
            => new(Name, true, 25d, FrepBrepRecoveryCapability.BoundedBrepPreview, "Use restricted-field trim oracle / numerical contour fallback", ["fallback=numerical_contour"], [], ["exact STEP not guaranteed"]);
    }

    private sealed class CirOnlyFallbackPolicy : IFrepBrepRecoveryPolicy
    {
        public string Name => "CirOnlyFallbackPolicy";
        public int TieBreakerPriority => 2;
        public FrepBrepRecoveryPolicyEvaluation Evaluate(FrepBrepRecoveryContext context)
            => new(Name, true, 5d, FrepBrepRecoveryCapability.IntentOnlyFallback, "Retain CIR intent only; defer BRep recovery", ["fallback=cir_only"], [], ["intent-preserving fallback"]);
    }
}
