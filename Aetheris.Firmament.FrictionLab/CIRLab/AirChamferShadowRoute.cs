using System.Numerics;
using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum AirChamferShadowCandidateStatus { Succeeded, Rejected, Deferred, Failed, FallbackLegacy }

public sealed record AirChamferShadowRouteRequest(
    string CaseName,
    BrepBody SourceBody,
    Vector3 TargetEdgeStart,
    Vector3 TargetEdgeEnd,
    Vector3? AdjacentFaceNormalA,
    Vector3? AdjacentFaceNormalB,
    double ChamferDistance,
    AirChamferFaceFamily FaceFamily,
    bool IsEdgeChain,
    bool IsCornerChain,
    bool LegacyDependency,
    AirChamferClassificationExpectation ClassificationExpectation,
    bool IsOrthogonalFacePair,
    double ReferenceEnvelope,
    bool IncludeStepSmoke = true);

public sealed record AirChamferShadowReport(
    bool LegacyAuthoritative,
    bool ProductionOutputChanged,
    bool ShadowCandidateProduced,
    AirChamferShadowCandidateStatus ShadowCandidateStatus,
    string AirChamferDecision,
    AirChamferRealBodyTopologySummary? TopologySummary,
    AirChamferClosedWitnessStepSummary? StepSmokeSummary,
    AirChamferFeatureRecognitionParityRow? FeatureRecognitionSummary,
    string? FirstDivergence,
    string Recommendation,
    BrepBody? ShadowCandidateBody,
    IReadOnlyList<string> Diagnostics);

public static class AirChamferShadowRoute
{
    public static readonly IReadOnlySet<string> AllowedRecommendations = new HashSet<string>(StringComparer.Ordinal)
    {
        "air-chamfer-shadow-ready-for-controlled-opt-in-route",
        "air-chamfer-shadow-needs-recognition-hardening",
        "air-chamfer-shadow-needs-body-prototype-hardening",
        "air-chamfer-shadow-rejected-invalid",
        "air-chamfer-shadow-deferred-unsupported",
        "air-chamfer-shadow-keep-legacy-authority"
    };

    public static AirChamferShadowReport Evaluate(AirChamferShadowRouteRequest request)
    {
        var diagnostics = new List<string>
        {
            "edge-v3-shadow-route-started",
            "edge-v3-shadow-route-internal-only",
            "edge-v3-legacy-authority-preserved",
            "edge-v3-production-output-unchanged",
            "edge-v3-no-production-route-replacement",
            "edge-v3-no-3d-boolean-used"
        };

        var prototypeRequest = new AirChamferRealBodyPrototypeRequest(
            request.CaseName,
            request.SourceBody,
            request.TargetEdgeStart,
            request.TargetEdgeEnd,
            request.AdjacentFaceNormalA,
            request.AdjacentFaceNormalB,
            request.ChamferDistance,
            request.FaceFamily,
            request.IsEdgeChain,
            request.IsCornerChain,
            request.LegacyDependency,
            request.ClassificationExpectation,
            request.IsOrthogonalFacePair,
            request.ReferenceEnvelope,
            request.IncludeStepSmoke);

        var prototype = AirChamferRealBodyPrototype.Evaluate(prototypeRequest);
        diagnostics.Add("edge-v3-air-chamfer-real-body-prototype-invoked");
        diagnostics.Add("edge-v3-judgment-engine-used");

        var status = ToShadowStatus(prototype.Status);
        if (prototype.Status is not AirChamferRealBodyPrototypeStatus.Succeeded || prototype.CandidateBody is null || prototype.TopologySummary is null)
        {
            var terminal = status is AirChamferShadowCandidateStatus.Deferred or AirChamferShadowCandidateStatus.FallbackLegacy
                ? "edge-v3-shadow-route-deferred"
                : "edge-v3-shadow-route-rejected";
            diagnostics.Add($"{terminal}:{prototype.Decision}");

            var terminalRecommendation = status switch
            {
                AirChamferShadowCandidateStatus.FallbackLegacy => "air-chamfer-shadow-keep-legacy-authority",
                AirChamferShadowCandidateStatus.Deferred => "air-chamfer-shadow-deferred-unsupported",
                AirChamferShadowCandidateStatus.Rejected => "air-chamfer-shadow-rejected-invalid",
                _ => "air-chamfer-shadow-needs-body-prototype-hardening"
            };

            return Report(
                candidateProduced: false,
                status,
                prototype.Decision,
                topology: null,
                step: null,
                recognition: null,
                firstDivergence: $"prototype-status-{prototype.Status}",
                terminalRecommendation,
                candidateBody: null,
                diagnostics);
        }

        diagnostics.Add("edge-v3-shadow-candidate-produced");
        if (prototype.StepSmoke?.Succeeded == true)
            diagnostics.Add("edge-v3-shadow-candidate-step-smoke-succeeded");

        var recognition = AirChamferFeatureRecognitionParityLab.EvaluateCandidateEvidence(request.CaseName, request.SourceBody, prototype);
        diagnostics.Add("edge-v3-shadow-feature-recognition-captured");

        var firstDivergence = recognition.FirstDivergence;
        string recommendation;
        if (recognition.RecognitionContractSatisfied)
        {
            diagnostics.Add("edge-v3-shadow-feature-recognition-parity-succeeded");
            recommendation = "air-chamfer-shadow-ready-for-controlled-opt-in-route";
        }
        else
        {
            diagnostics.Add($"edge-v3-shadow-feature-recognition-parity-mismatch:{firstDivergence ?? "unknown"}");
            recommendation = "air-chamfer-shadow-needs-recognition-hardening";
        }

        return Report(
            candidateProduced: true,
            status,
            prototype.Decision,
            prototype.TopologySummary,
            prototype.StepSmoke,
            recognition,
            firstDivergence,
            recommendation,
            prototype.CandidateBody,
            diagnostics);
    }

    private static AirChamferShadowReport Report(
        bool candidateProduced,
        AirChamferShadowCandidateStatus status,
        string decision,
        AirChamferRealBodyTopologySummary? topology,
        AirChamferClosedWitnessStepSummary? step,
        AirChamferFeatureRecognitionParityRow? recognition,
        string? firstDivergence,
        string recommendation,
        BrepBody? candidateBody,
        IEnumerable<string> diagnostics)
    {
        if (!AllowedRecommendations.Contains(recommendation))
            throw new InvalidOperationException($"Unexpected AirChamfer shadow recommendation '{recommendation}'.");

        return new AirChamferShadowReport(
            LegacyAuthoritative: true,
            ProductionOutputChanged: false,
            ShadowCandidateProduced: candidateProduced,
            status,
            decision,
            topology,
            step,
            recognition,
            firstDivergence,
            recommendation,
            candidateBody,
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static AirChamferShadowCandidateStatus ToShadowStatus(AirChamferRealBodyPrototypeStatus status) => status switch
    {
        AirChamferRealBodyPrototypeStatus.Succeeded => AirChamferShadowCandidateStatus.Succeeded,
        AirChamferRealBodyPrototypeStatus.Rejected => AirChamferShadowCandidateStatus.Rejected,
        AirChamferRealBodyPrototypeStatus.Deferred => AirChamferShadowCandidateStatus.Deferred,
        AirChamferRealBodyPrototypeStatus.FallbackLegacy => AirChamferShadowCandidateStatus.FallbackLegacy,
        _ => AirChamferShadowCandidateStatus.Failed
    };
}
