namespace Aetheris.Kernel.Core.Air;

internal enum AirRouteSelectionMode { Direct, SwitchMatch, JudgmentUtility, Unsupported }
internal enum AirRouteCandidateStatus { Admitted, Rejected, Deferred, Unavailable, NotApplicable }

internal static class AirRouteRejectionReason
{
    public const string None = "none";
    public const string ArbitraryGraphUnsupported = "arbitrary-graph-unsupported";
    public const string LoopFilletDeferredUntilSingleEdgeEvidence = "loop-fillet-deferred-until-single-edge-fillet-evidence";
    public const string FilletX1Required = "fillet-x1-required";
    public const string NonUniformRuleUnsupported = "non-uniform-rule-unsupported";
    public const string JudgmentUtilityDeferred = "judgment-utility-deferred-until-air-x3-policy-work";
    public const string UnsupportedRequest = "unsupported-air-route-selection-request";
}

internal sealed record AirRouteCandidate(
    AirRouteKind RouteKind,
    AirRouteCandidateStatus Status,
    AirRouteSelectionMode SelectionMode,
    double? Score,
    string ReasonCode,
    string Reason,
    IReadOnlyList<AirDiagnostic> Diagnostics,
    IReadOnlyList<string> Guarantees,
    IReadOnlyList<string> KnownLosses);

internal sealed record AirRouteAdmissibility(
    AirRouteCandidateStatus Status,
    string ReasonCode,
    string Reason);

internal sealed record AirRouteSelectionSummary(
    AirRouteSelectionMode SelectionMode,
    AirSelectionClass SelectionClass,
    AirRuleKind RuleKind,
    string ConstructionHistoryKind,
    IReadOnlyList<AirDiagnostic> Diagnostics);

internal sealed record AirRouteSelectionRequest(
    string RequestName,
    AirNodeKind NodeKind,
    AirSelectionClass SelectionClass = AirSelectionClass.None,
    AirRuleKind RuleKind = AirRuleKind.None,
    string ConstructionHistoryKind = "unspecified");

internal sealed record AirRouteDecision(
    string RequestName,
    AirNodeKind NodeKind,
    AirRouteSelectionMode SelectionMode,
    AirRouteKind? SelectedRouteKind,
    bool Succeeded,
    string Recommendation,
    IReadOnlyList<AirRouteCandidate> Candidates,
    IReadOnlyList<AirDiagnostic> Diagnostics,
    IReadOnlyList<string> Guarantees,
    AirProvenance Provenance,
    AirRouteSelectionSummary Summary);

internal static class AirRouteSelector
{
    public static AirRouteSelectionRequest ForPrismaticSectionTransition(string requestName = "canonical-prismatic-section-transition") =>
        new(requestName, AirNodeKind.PrismaticSectionTransition, ConstructionHistoryKind: "generated/constructive");

    public static AirRouteSelectionRequest ForProfileExtrude(string requestName = "canonical-profile-extrude") =>
        new(requestName, AirNodeKind.ProfileExtrude, ConstructionHistoryKind: "generated/constructive");

    public static AirRouteSelectionRequest ForEdgeFinish(string requestName, AirSelectionClass selectionClass, AirRuleKind ruleKind, string constructionHistoryKind) =>
        new(requestName, AirNodeKind.TopFaceLoopChamfer, selectionClass, ruleKind, constructionHistoryKind);

    public static AirRouteSelectionRequest ForJudgmentUtilityProbe(string requestName = "contested-history-known-chamfer") =>
        new(requestName, AirNodeKind.Unsupported, AirSelectionClass.SingleEdge, AirRuleKind.UniformChamfer, "history-known/contested");

    public static AirRouteDecision Decide(AirRouteSelectionRequest request) => request.NodeKind switch
    {
        AirNodeKind.PrismaticSectionTransition when request.SelectionClass == AirSelectionClass.None => Direct(request, AirRouteKind.PrismaticSectionTransitionEmitter, "air-x2-prismatic-section-transition-direct-route"),
        AirNodeKind.ProfileExtrude when request.SelectionClass == AirSelectionClass.None => Direct(request, AirRouteKind.ProfileExtrudeEmitter, "air-x2-profile-extrude-direct-route"),
        _ when request.RequestName == "contested-history-known-chamfer" => JudgmentDeferred(request),
        _ => SwitchMatch(request),
    };

    // Direct selection is for already-canonical Constructive AIR nodes: no scoring, no route competition.
    private static AirRouteDecision Direct(AirRouteSelectionRequest request, AirRouteKind route, string routeDiagnostic)
    {
        var diagnostics = StableDiagnostics("air-x2-direct-selection", routeDiagnostic, "air-x2-no-judgment-engine-required", "air-x2-no-production-route-replacement");
        var guarantees = StableStrings("explicit direct route", "deterministic route decision", "no production route replacement");
        var candidate = new AirRouteCandidate(route, AirRouteCandidateStatus.Admitted, AirRouteSelectionMode.Direct, null, AirRouteRejectionReason.None, "already-canonical Constructive AIR node names the route", diagnostics, guarantees, []);
        return Decision(request, AirRouteSelectionMode.Direct, route, true, "air-x2-direct-route-admitted", [candidate], diagnostics, guarantees);
    }

    // Switch/match selection is for closed structural classifications: deterministic and deliberately unscored.
    private static AirRouteDecision SwitchMatch(AirRouteSelectionRequest request)
    {
        var codes = new List<string> { "air-x2-switch-match-selection", "air-x2-selection-class-classified", "air-x2-rule-kind-classified", "air-x2-no-production-route-replacement" };
        AirRouteKind route = AirRouteKind.Unsupported;
        var status = AirRouteCandidateStatus.Deferred;
        var reason = AirRouteRejectionReason.UnsupportedRequest;
        var text = "request is outside AIR-X2 route-selection scope";
        var guarantees = new List<string> { "deterministic switch/match classification", "no production route replacement" };

        if (request.SelectionClass == AirSelectionClass.FaceBoundaryLoop && request.RuleKind == AirRuleKind.UniformChamfer && request.ConstructionHistoryKind.Contains("top-face", StringComparison.OrdinalIgnoreCase))
        {
            route = AirRouteKind.TopFaceLoopChamferPrismatic; status = AirRouteCandidateStatus.Admitted; reason = AirRouteRejectionReason.None; text = "history-known top-face loop uniform chamfer admits the prismatic wrapper";
            codes.Add("air-x2-face-boundary-loop-uniform-chamfer-admitted"); guarantees.Add("Class B face-boundary loop"); guarantees.Add("not four independent single-edge chamfers");
        }
        else if (request.SelectionClass == AirSelectionClass.ArbitraryGraph && request.RuleKind == AirRuleKind.UniformChamfer) { status = AirRouteCandidateStatus.Rejected; reason = AirRouteRejectionReason.ArbitraryGraphUnsupported; text = "arbitrary graph edge finishes are unsupported in AIR-X2"; codes.Add("air-x2-arbitrary-graph-rejected"); }
        else if (request.SelectionClass == AirSelectionClass.FaceBoundaryLoop && request.RuleKind == AirRuleKind.ConstantRadiusFillet) { reason = AirRouteRejectionReason.LoopFilletDeferredUntilSingleEdgeEvidence; text = "loop fillet is deferred until single-edge fillet and corner evidence exist"; codes.Add("air-x2-fillet-deferred"); }
        else if (request.SelectionClass == AirSelectionClass.SingleEdge && request.RuleKind == AirRuleKind.ConstantRadiusFillet) { reason = AirRouteRejectionReason.FilletX1Required; text = "single-edge fillet requires FILLET-X1 evidence before AIR route admission"; codes.Add("air-x2-fillet-deferred"); }
        else if (request.SelectionClass == AirSelectionClass.FaceBoundaryLoop && request.RuleKind == AirRuleKind.Unsupported) { status = AirRouteCandidateStatus.Rejected; reason = AirRouteRejectionReason.NonUniformRuleUnsupported; text = "non-uniform or mixed loop rules are unsupported in AIR-X2"; codes.Add("air-x2-non-uniform-rule-rejected"); }

        var diagnostics = StableDiagnostics(codes.ToArray());
        var candidate = new AirRouteCandidate(route, status, AirRouteSelectionMode.SwitchMatch, null, reason, text, diagnostics, guarantees.Order(StringComparer.Ordinal).ToArray(), []);
        return Decision(request, status == AirRouteCandidateStatus.Admitted ? AirRouteSelectionMode.SwitchMatch : AirRouteSelectionMode.Unsupported, status == AirRouteCandidateStatus.Admitted ? route : null, status == AirRouteCandidateStatus.Admitted, status == AirRouteCandidateStatus.Admitted ? "air-x2-switch-match-route-admitted" : reason, [candidate], diagnostics, candidate.Guarantees);
    }

    // JudgmentUtility is reserved for competing admissible routes with policy tradeoffs; AIR-X2 represents it but defers wiring.
    private static AirRouteDecision JudgmentDeferred(AirRouteSelectionRequest request)
    {
        var diagnostics = StableDiagnostics("air-x2-judgment-utility-deferred", "air-x2-judgment-engine-not-required-for-direct-or-switch-selection", "air-x2-no-production-route-replacement");
        var candidate = new AirRouteCandidate(AirRouteKind.Unsupported, AirRouteCandidateStatus.Deferred, AirRouteSelectionMode.JudgmentUtility, null, AirRouteRejectionReason.JudgmentUtilityDeferred, "JudgmentUtility route policy is deferred to AIR-X3 because AIR-X2 direct and switch/match cases do not need scored tradeoffs.", diagnostics, ["no production route replacement"], []);
        return Decision(request, AirRouteSelectionMode.JudgmentUtility, null, false, AirRouteRejectionReason.JudgmentUtilityDeferred, [candidate], diagnostics, candidate.Guarantees);
    }

    private static AirRouteDecision Decision(AirRouteSelectionRequest request, AirRouteSelectionMode mode, AirRouteKind? selected, bool succeeded, string recommendation, IReadOnlyList<AirRouteCandidate> candidates, IReadOnlyList<AirDiagnostic> diagnostics, IReadOnlyList<string> guarantees) =>
        new(request.RequestName, request.NodeKind, mode, selected, succeeded, recommendation, candidates, diagnostics, guarantees, new AirProvenance("AIR-X2", "AIR route selector", request.RequestName, request.RequestName, selected?.ToString() ?? "none", request.SelectionClass, request.RuleKind, request.ConstructionHistoryKind, false, ["Route decision only; no production route replacement."]), new AirRouteSelectionSummary(mode, request.SelectionClass, request.RuleKind, request.ConstructionHistoryKind, diagnostics));

    private static IReadOnlyList<AirDiagnostic> StableDiagnostics(params string[] codes) => codes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(c => new AirDiagnostic(c, AirDiagnosticSeverity.Info, c)).ToArray();
    private static IReadOnlyList<string> StableStrings(params string[] values) => values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
