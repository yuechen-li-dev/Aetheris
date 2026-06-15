using System.Text;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep.Prismatic;
using Aetheris.Kernel.Firmament;

namespace Aetheris.CLI;

internal sealed record AirTraceReport(
    string Milestone,
    string Command,
    string TraceKind,
    string InputKind,
    string CaseName,
    bool Succeeded,
    string Recommendation,
    AirTraceAirSummary Air,
    AirTraceRouteDecisionSummary RouteDecision,
    [property: JsonPropertyName("brepPlan")] AirTraceBRepPlanSummary BRepPlan,
    AirTraceEmissionSummary Emission,
    AirTraceStepSmokeSummary StepSmoke,
    AirTraceCirMirrorSummary CirMirror,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> KnownLosses,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Guarantees,
    IReadOnlyList<string> UnchangedBehavior,
    AirTraceFixtureSummary? Fixture = null,
    string? FixturePath = null,
    string? FixtureExpectation = null,
    string? FixtureCaseName = null,
    string? ExpectedStage = null,
    string? ActualStageReached = null,
    string? ExpectedRoute = null,
    string? ExpectedReason = null,
    bool? ExpectationSatisfied = null,
    IReadOnlyList<string>? FixtureDiagnostics = null,
    AirTraceFrontendSummary? Frontend = null,
    AirTraceFeatureAirSummary? FeatureAir = null,
    AirTraceConstructiveAirSummary? ConstructiveAir = null,
    AirTraceProfileEmissionSummary? ProfileEmission = null);

internal sealed record AirTraceFixtureSummary(string Path, string Expectation, string CaseName, string? ExpectedStage, string ActualStageReached, string? ExpectedRoute, string? ExpectedReason, bool ExpectationSatisfied, bool ParserBacked, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceFrontendSummary(bool ParserBacked, string? ParserName, bool? ParseSucceeded, IReadOnlyList<string> ParseDiagnostics, string? FrontendStageReached, string? FrontendSummary);
internal sealed record AirTraceFeatureAirSummary(bool ParserBacked, string SourceOpKind, string NodeKind, AirTraceDimensionsSummary? Dimensions, string ConstructionIntent, string StageReached, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Guarantees);
internal sealed record AirTraceConstructiveAirSummary(string NodeKind, string CanonicalForm, string SourceFeatureAirNodeKind, string ProfileKind, double Width, double Depth, double Height, string ExtrusionAxis, string ConstructionIntent, string RouteKind, string StageReached, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Guarantees);
internal sealed record AirTraceProfileEmissionSummary(bool WrapperInvoked, string EmitterName, bool Succeeded, double Width, double Depth, double Height, string StageReached, AirTraceProfileEmissionTopologySummary? TopologySummary, AirTraceProfileEmissionStepSmokeSummary StepSmoke, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Guarantees);
internal sealed record AirTraceProfileEmissionTopologySummary(int Vertices, int Edges, int Faces, int PlanarFaces, int CylindricalFaces, int Loops, int Coedges, int? CapFaces, int? SideFaces, string? Bounds);
internal sealed record AirTraceProfileEmissionStepSmokeSummary(bool WasChecked, bool Succeeded, bool RequiredMarkersPresent, bool ForbiddenMarkersAbsent, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceDimensionsSummary(double Width, double Depth, double Height);
internal sealed record AirTraceAirSummary(string Node, string Route, string SelectionClass, string Rule, string ConstructionHistory, string FeatureName, string FeatureId, string ProvenanceMilestone);
internal sealed record AirTraceRouteDecisionSummary(string Mode, string? SelectedRoute, bool Succeeded, string Recommendation, string SelectionClass, string Rule, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceBRepPlanSummary(string PlanKind, int Vertices, int Curves, int Edges, int Faces, int Loops, int Coedges, int Surfaces, int CapFaces, int TransitionFaces, int ChamferFaces, int SideFaces, string SplitPolicy, string Bounds, string? RouteSelectionMode, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceEmissionSummary(string ExistingEmitterPath, bool Succeeded, string Recommendation);
internal sealed record AirTraceStepSmokeSummary(bool WasChecked, bool Succeeded, bool RequiredMarkersPresent, bool ForbiddenMarkersAbsent, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceCirMirrorSummary(string Status, string Backend, string SourceNode, string SourceKind, string SelectionClass, string Rule, string MirrorBuilderRoute, IReadOnlyList<string> Capabilities, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Provenance, IReadOnlyList<string> Diagnostics);

internal static class AirTraceReportBuilder
{
    public static readonly string[] SupportedCases = ["prismatic-section-transition", "top-face-loop-chamfer"];
    public static readonly string[] SupportedFixtureCases = ["arbitrary-graph-chamfer", "box", "loop-fillet-deferred", "non-uniform-loop-chamfer", "top-face-loop-chamfer"];

    public static AirTraceReport Build(string caseName)
    {
        caseName = Normalize(caseName);
        return caseName switch
        {
            "prismatic-section-transition" => BuildPrismatic(),
            "top-face-loop-chamfer" => BuildTopFaceLoopChamfer(),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unsupported trace case.")
        };
    }

    public static string Normalize(string value) => value switch
    {
        "prismatic" => "prismatic-section-transition",
        "loop-chamfer" => "top-face-loop-chamfer",
        _ => value
    };

    public static string FileStem(string caseName) => $"air-x6-{Normalize(caseName)}-trace";

    public static AirTraceReport BuildFixture(FirmFixture fixture)
    {
        if (fixture.ParserBacked) return BuildParserBackedFixture(fixture);

        var caseName = fixture.CaseName;
        var report = caseName switch
        {
            "top-face-loop-chamfer" => BuildTopFaceLoopChamfer(),
            "arbitrary-graph-chamfer" => BuildRejectedFixture(fixture, AirSelectionClass.ArbitraryGraph, AirRuleKind.UniformChamfer),
            "non-uniform-loop-chamfer" => BuildRejectedFixture(fixture, AirSelectionClass.FaceBoundaryLoop, AirRuleKind.Unsupported),
            "loop-fillet-deferred" => BuildRejectedFixture(fixture, AirSelectionClass.FaceBoundaryLoop, AirRuleKind.ConstantRadiusFillet),
            _ => throw new ArgumentOutOfRangeException(nameof(fixture), caseName, "Unsupported fixture case.")
        };

        var actualStage = caseName == "top-face-loop-chamfer" ? "cir-mirror" : (report.RouteDecision.Recommendation.Contains("deferred", StringComparison.Ordinal) ? "deferred" : "rejected");
        var expectedReasonSatisfied = string.IsNullOrWhiteSpace(fixture.ExpectedReason) || report.RouteDecision.Recommendation.Contains(fixture.ExpectedReason, StringComparison.Ordinal) || report.Diagnostics.Any(d => d.Contains(fixture.ExpectedReason, StringComparison.Ordinal));
        var expectedRouteSatisfied = string.IsNullOrWhiteSpace(fixture.ExpectedRoute) || string.Equals(report.RouteDecision.SelectedRoute, fixture.ExpectedRoute, StringComparison.Ordinal) || string.Equals(report.Air.Route, fixture.ExpectedRoute, StringComparison.Ordinal);
        var expectationSatisfied = fixture.Expectation == "valid" ? report.Succeeded && expectedRouteSatisfied : !report.RouteDecision.Succeeded && expectedReasonSatisfied;
        if (!string.IsNullOrWhiteSpace(fixture.ExpectedStage)) expectationSatisfied &= string.Equals(actualStage, fixture.ExpectedStage, StringComparison.Ordinal) || (fixture.ExpectedStage == "route-selection" && (actualStage == "rejected" || actualStage == "deferred"));
        var fxDiagnostics = Stable([.. fixture.Diagnostics, "air-x7-firmfixture-case-mapped", "air-x7-firmfixture-trace-created", "air-x7-lowering-stage-recorded", expectationSatisfied ? "air-x7-expectation-satisfied" : "air-x7-fixture-expectation-not-satisfied"]).ToArray();
        return report with
        {
            Milestone = "AIR-X7", InputKind = "firmfixture", FixturePath = fixture.Path, FixtureExpectation = fixture.Expectation, FixtureCaseName = caseName, ExpectedStage = fixture.ExpectedStage, ActualStageReached = actualStage, ExpectedRoute = fixture.ExpectedRoute, ExpectedReason = fixture.ExpectedReason, ExpectationSatisfied = expectationSatisfied, FixtureDiagnostics = fxDiagnostics, Fixture = new(fixture.Path, fixture.Expectation, caseName, fixture.ExpectedStage, actualStage, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, false, fxDiagnostics), Diagnostics = Stable([.. report.Diagnostics, .. fxDiagnostics]).ToArray()
        };
    }

    private static AirTraceReport BuildParserBackedFixture(FirmFixture fixture)
    {
        var frontend = FirmamentFrontendTraceProbe.ParseOnly(fixture.SourceBody);
        var profileEmissionProbe = frontend.ConstructiveAir is null ? null : BoxConstructiveAirToProfileEmissionTraceProbe.Invoke(frontend.ConstructiveAir);
        var actualStage = profileEmissionProbe?.StageReached ?? frontend.FrontendStageReached;
        var expectedStageSatisfied = string.IsNullOrWhiteSpace(fixture.ExpectedStage) || string.Equals(actualStage, fixture.ExpectedStage, StringComparison.Ordinal);
        var expectationSatisfied = fixture.Expectation == "valid"
            ? frontend.ParseSucceeded && expectedStageSatisfied && (profileEmissionProbe?.Succeeded ?? true)
            : !frontend.ParseSucceeded && expectedStageSatisfied;
        var fxDiagnostics = Stable([.. fixture.Diagnostics, .. frontend.Diagnostics, .. profileEmissionProbe?.Diagnostics ?? [], "air-x8-parser-backed-fixture-trace-created", "air-x11-parser-backed-fixture-loaded", "air-x11-firmament-parser-invoked", frontend.ParseSucceeded ? "air-x11-firmament-parse-succeeded" : "air-x11-firmament-parse-failed", expectationSatisfied ? "air-x11-parser-backed-expectation-satisfied" : "air-x11-profile-emission-expectation-not-satisfied"]).ToArray();

        var featureAir = frontend.FeatureAir is null
            ? null
            : new AirTraceFeatureAirSummary(
                frontend.FeatureAir.ParserBacked,
                frontend.FeatureAir.SourceOpKind,
                frontend.FeatureAir.FeatureAirNodeKind,
                frontend.FeatureAir.SourceDimensions is null ? null : new AirTraceDimensionsSummary(frontend.FeatureAir.SourceDimensions.Width, frontend.FeatureAir.SourceDimensions.Depth, frontend.FeatureAir.SourceDimensions.Height),
                frontend.FeatureAir.ConstructionIntent,
                frontend.FeatureAir.StageReached,
                frontend.FeatureAir.Diagnostics,
                frontend.FeatureAir.Guarantees);
        var constructiveAir = frontend.ConstructiveAir is null
            ? null
            : new AirTraceConstructiveAirSummary(frontend.ConstructiveAir.NodeKind, frontend.ConstructiveAir.CanonicalForm, frontend.ConstructiveAir.SourceFeatureAirNodeKind, frontend.ConstructiveAir.ProfileKind, frontend.ConstructiveAir.Dimensions.Width, frontend.ConstructiveAir.Dimensions.Depth, frontend.ConstructiveAir.Dimensions.Height, frontend.ConstructiveAir.ExtrusionAxis, frontend.ConstructiveAir.ConstructionIntent, frontend.ConstructiveAir.RouteKind, frontend.ConstructiveAir.StageReached, frontend.ConstructiveAir.Diagnostics, frontend.ConstructiveAir.Guarantees);

        var profileEmission = profileEmissionProbe is null ? null : new AirTraceProfileEmissionSummary(
            profileEmissionProbe.WrapperInvoked,
            profileEmissionProbe.EmitterName,
            profileEmissionProbe.Succeeded,
            profileEmissionProbe.Width,
            profileEmissionProbe.Depth,
            profileEmissionProbe.Height,
            profileEmissionProbe.StageReached,
            profileEmissionProbe.TopologySummary is null ? null : new AirTraceProfileEmissionTopologySummary(profileEmissionProbe.TopologySummary.Vertices, profileEmissionProbe.TopologySummary.Edges, profileEmissionProbe.TopologySummary.Faces, profileEmissionProbe.TopologySummary.PlanarFaces, profileEmissionProbe.TopologySummary.CylindricalFaces, profileEmissionProbe.TopologySummary.Loops, profileEmissionProbe.TopologySummary.Coedges, profileEmissionProbe.TopologySummary.CapFaces, profileEmissionProbe.TopologySummary.SideFaces, profileEmissionProbe.TopologySummary.Bounds),
            new AirTraceProfileEmissionStepSmokeSummary(profileEmissionProbe.StepSmoke.WasChecked, profileEmissionProbe.StepSmoke.Succeeded, profileEmissionProbe.StepSmoke.RequiredMarkersPresent, profileEmissionProbe.StepSmoke.ForbiddenMarkersAbsent, profileEmissionProbe.StepSmoke.Diagnostics),
            profileEmissionProbe.Diagnostics,
            profileEmissionProbe.Guarantees);

        return new("AIR-X11", "trace", "lowering", "firmfixture", fixture.CaseName, expectationSatisfied, frontend.FrontendSummary,
            new(constructiveAir?.NodeKind ?? featureAir?.NodeKind ?? "FirmamentPrimitive", constructiveAir?.RouteKind ?? "none", "none", "none", "parser-backed-source-fixture", fixture.CaseName, fixture.CaseName, "AIR-X11"),
            new("none", null, false, "Route selection and BRepPlan are deferred for parser-backed box fixtures in AIR-X11; profile emission is reported separately.", "none", "none", []),
            new("none", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none", "none", null, []),
            new(profileEmission?.EmitterName ?? "none", profileEmission?.Succeeded ?? false, profileEmission is null ? "parser-backed fixture stops before profile emission" : "parser-backed fixture invoked existing profile extrusion wrapper/emitter summary"),
            new(profileEmission?.StepSmoke.WasChecked ?? false, profileEmission?.StepSmoke.Succeeded ?? false, profileEmission?.StepSmoke.RequiredMarkersPresent ?? false, profileEmission?.StepSmoke.ForbiddenMarkersAbsent ?? false, profileEmission?.StepSmoke.Diagnostics ?? []),
            new("not-requested", "none", "none", "FirmamentPrimitive", "none", "none", "none", [], [], [], []),
            [], ["BRepPlan/CIR deferred for parser-backed box fixture", "STEP smoke unavailable for parser-backed box fixture"], fxDiagnostics,
            ["real Firmament parser invoked", "parser-backed fixture reaches Feature AIR summary", "parser-backed fixture reaches Constructive AIR summary", "ProfileExtrude wrapper invoked", "no production grammar expansion", "no production route replacement", "no new geometry", "no profile emitter rewrite"],
            ["no production Firmament grammar expansion", "no production route replacement", "no production analyzer behavior change", "no STEP exporter/importer change", "no BRep topology behavior change", "no route-selection/JudgmentUtility behavior change", "no CIR evaluator/tape behavior change", "no Boolean behavior change", "no AirEdgeSweep behavior change", "no BrepBoundedChamfer/BrepBoundedFillet behavior change", "no chamfer/fillet/shell geometry change", "no new geometry"],
            new(fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, true, fxDiagnostics),
            fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, fxDiagnostics,
            new(true, frontend.ParserName, frontend.ParseSucceeded, frontend.Diagnostics, actualStage, frontend.FrontendSummary),
            featureAir, constructiveAir, profileEmission);
    }

    public static string FixtureFileStem(FirmFixture fixture) => $"air-x7-{fixture.CaseName}-firmfixture-trace";

    private static AirTraceReport BuildPrismatic()
    {
        var lowering = AirPrismaticSectionTransitionWrapper.LowerCanonicalRectangleInset();
        var route = AirRouteSelector.Decide(AirRouteSelector.ForPrismaticSectionTransition());
        var brepPlan = AirPrismaticSectionTransitionBRepPlanner.Plan(new PrismaticSectionTransitionRequest(AirPrismaticSectionTransitionWrapper.CanonicalSections(), PrismaticCorrespondenceMap.Identity(4), new PrismaticSectionTransitionOptions(RunStepSmoke: true, TraceLabel: "air-x6-prismatic-section-transition-trace")));
        var mirror = AirCirMirrorAdapter.AdmitCanonicalPrismaticSectionTransition();
        return Compose("prismatic-section-transition", lowering, route, brepPlan, mirror, "PrismaticSectionTransitionEmitter", SpecificGuarantees([]));
    }

    private static AirTraceReport BuildTopFaceLoopChamfer()
    {
        var lowering = AirTopFaceLoopChamferWrapper.LowerCanonicalTopFaceLoopChamfer();
        var route = AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish("canonical-top-face-loop-chamfer", AirSelectionClass.FaceBoundaryLoop, AirRuleKind.UniformChamfer, "generated/history-known/top-face"));
        var request = new PrismaticTopFaceLoopChamferRequest(10, 8, 6, 1, ExportStep: true);
        var brepPlan = AirTopFaceLoopChamferBRepPlanner.Plan(request);
        var mirror = AirCirMirrorAdapter.AdmitCanonicalTopFaceLoopChamfer();
        return Compose("top-face-loop-chamfer", lowering, route, brepPlan, mirror, "PrismaticTopFaceLoopChamferPrototype / PrismaticSectionTransitionEmitter", SpecificGuarantees(["no AirEdgeSweep", "no BrepBoundedChamfer", "no topology graft", "no 3D Boolean", "no coplanar merge", "not four independent single-edge chamfers"]));
    }


    private static AirTraceReport BuildRejectedFixture(FirmFixture fixture, AirSelectionClass selectionClass, AirRuleKind ruleKind)
    {
        var route = AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish(fixture.CaseName, selectionClass, ruleKind, "fixture/firmament-contract"));
        var candidate = route.Candidates.First();
        var diagnostics = Stable(["air-x7-firmfixture-invalid-route-trace-created", .. route.Diagnostics.Select(d => d.Code)]).ToArray();
        return new("AIR-X7", "trace", "lowering", "firmfixture", fixture.CaseName, false, route.Recommendation,
            new("Unsupported", "Unsupported", selectionClass.ToString(), ruleKind.ToString(), "fixture/firmament-contract", fixture.CaseName, fixture.CaseName, "AIR-X7"),
            new(route.SelectionMode.ToString(), route.SelectedRouteKind?.ToString(), route.Succeeded, route.Recommendation, route.Summary.SelectionClass.ToString(), route.Summary.RuleKind.ToString(), route.Diagnostics.Select(d => d.Code).Order().ToArray()),
            new("none", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none", "none", null, []),
            new("none", false, "no geometry emitted for expected invalid fixture"),
            new(false, false, false, false, []),
            new("not-requested", "none", "none", "Unsupported", selectionClass.ToString(), ruleKind.ToString(), "none", [], [], [], []),
            [], [], diagnostics, ["invalid fixture stops before geometry emission", "no production route replacement", "no arbitrary graph support"], ["no production route replacement", "no production analyzer behavior change", "no STEP exporter/importer change", "no BRep topology behavior change", "no route-selection/JudgmentUtility behavior change", "no Firmament lowering behavior change", "no Boolean behavior change", "no CIR evaluator/tape behavior change", "no new geometry"]);
    }

    private static AirTraceReport Compose(string caseName, AirLoweringSummary lowering, AirRouteDecision route, AirBRepPlanResult planResult, AirCirMirrorAdapterResult mirror, string emitterPath, IReadOnlyList<string> guarantees)
    {
        var plan = planResult.Plan?.Summary ?? planResult.Validation.ExpectedTopologySummary;
        var capabilities = SplitFlags(mirror.Summary.Capabilities).ToArray();
        var losses = SplitFlags(mirror.Summary.KnownLosses).ToArray();
        var diagnostics = Stable([
            "air-x6-trace-command-started", $"air-x6-trace-case-selected:{caseName}", "air-x6-air-summary-created", "air-x6-route-decision-summary-created", "air-x6-brep-plan-summary-created", "air-x6-emission-summary-created", "air-x6-step-smoke-summary-created", "air-x6-cir-mirror-summary-created", "air-x6-lowering-report-created", "air-x6-trace-not-analyze", "air-x6-no-production-route-replacement", "air-x6-no-production-analyzer-change", "air-x6-no-step-exporter-change", "air-x6-no-brep-topology-change",
            .. lowering.Diagnostics.Select(d => d.Code), .. route.Diagnostics.Select(d => d.Code), .. plan.Diagnostics.Select(d => d.Code), .. mirror.Summary.Diagnostics
        ]).ToArray();
        return new("AIR-X6", "trace", "lowering", "built-in-case", caseName, lowering.Succeeded && route.Succeeded && planResult.Succeeded, lowering.Recommendation,
            new(lowering.NodeKind.ToString(), lowering.RouteKind.ToString(), lowering.Provenance.SelectionClass.ToString(), lowering.Provenance.RuleKind.ToString(), lowering.Provenance.ConstructionHistoryKind, lowering.Provenance.FeatureName, lowering.Provenance.FeatureId, lowering.Provenance.Milestone),
            new(route.SelectionMode.ToString(), route.SelectedRouteKind?.ToString(), route.Succeeded, route.Recommendation, route.Summary.SelectionClass.ToString(), route.Summary.RuleKind.ToString(), route.Diagnostics.Select(d => d.Code).Order().ToArray()),
            new(plan.PlanKind.ToString(), plan.VertexCount, plan.CurveCount, plan.EdgeCount, plan.FaceCount, plan.LoopCount, plan.CoedgeCount, plan.SurfaceCount, plan.CapFaceCount, plan.TransitionFaceCount, plan.ChamferFaceCount, plan.SideFaceCount, plan.SplitPolicy, plan.Bounds, plan.FeatureContext?.RouteSelectionMode, plan.Diagnostics.Select(d => d.Code).Order().ToArray()),
            new(emitterPath, lowering.Succeeded, lowering.Recommendation),
            new(lowering.StepSmokeSummary.WasChecked, lowering.StepSmokeSummary.Succeeded, lowering.StepSmokeSummary.RequiredMarkersPresent, lowering.StepSmokeSummary.ForbiddenMarkersAbsent, lowering.StepSmokeSummary.Diagnostics.Select(d => d.Code).Order().ToArray()),
            new(mirror.Summary.StatusText, mirror.Summary.MirrorBackend, mirror.Summary.SourceNodeId, mirror.Summary.SourceKind.ToString(), mirror.Summary.SelectionClass.ToString(), mirror.Summary.RuleKind.ToString(), mirror.Summary.MirrorBuilderRoute, capabilities, losses, mirror.Summary.Provenance, mirror.Summary.Diagnostics),
            capabilities, losses, diagnostics, guarantees, ["no production route replacement", "no production analyzer behavior change", "no STEP exporter/importer change", "no BRep topology behavior change", "no route-selection/JudgmentUtility behavior change", "no Firmament lowering behavior change", "no Boolean behavior change", "no CIR evaluator/tape behavior change", "no new geometry"]);
    }

    private static IReadOnlyList<string> SpecificGuarantees(IEnumerable<string> extra) => Stable(["no production route replacement", "no default production CLI behavior change outside adding trace command", "no production analyzer behavior change", "no STEP exporter/importer change", "no BRep topology behavior change", "no Boolean behavior change", "no Firmament lowering behavior change", "no CIR evaluator/tape behavior change", "no new geometry", "no import/recovery", "no arbitrary graph support", .. extra]).ToArray();
    private static IEnumerable<string> Stable(IEnumerable<string> x) => x.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal);
    private static IEnumerable<string> SplitFlags<T>(T value) where T : Enum => Enum.GetValues(typeof(T)).Cast<T>().Where(v => Convert.ToInt64(v) != 0 && value.HasFlag(v)).Select(v => Kebab(v.ToString())).OrderBy(x => x, StringComparer.Ordinal);
    private static string Kebab(string value) => string.Concat(value.Replace("BRep", "Brep", StringComparison.Ordinal).SelectMany((c, i) => char.IsUpper(c) && i > 0 ? new[] { '-', char.ToLowerInvariant(c) } : new[] { char.ToLowerInvariant(c) }));
}

internal static class AirTraceTextRenderer
{
    public static string Render(AirTraceReport r)
    {
        var b = new StringBuilder();
        b.AppendLine($"Aetheris trace — {r.Milestone} lowering report");
        b.AppendLine($"Case: {r.CaseName}"); b.AppendLine($"Trace kind: {r.TraceKind}"); b.AppendLine($"Input kind: {r.InputKind}"); b.AppendLine();
        if (r.Fixture is not null)
        {
            b.AppendLine("Fixture"); b.AppendLine($"  Path: {r.Fixture.Path}"); b.AppendLine($"  Expectation: {r.Fixture.Expectation}"); b.AppendLine($"  Expected stage: {r.Fixture.ExpectedStage}"); b.AppendLine($"  Actual stage: {r.Fixture.ActualStageReached}"); b.AppendLine($"  Expected route: {r.Fixture.ExpectedRoute}"); b.AppendLine($"  Expected reason: {r.Fixture.ExpectedReason}"); b.AppendLine($"  Parser-backed: {r.Fixture.ParserBacked.ToString().ToLowerInvariant()}"); b.AppendLine($"  Expectation satisfied: {r.Fixture.ExpectationSatisfied.ToString().ToLowerInvariant()}"); b.AppendLine();
        }
        if (r.Frontend is not null)
        {
            b.AppendLine("Frontend"); b.AppendLine($"  Parser-backed: {r.Frontend.ParserBacked.ToString().ToLowerInvariant()}"); b.AppendLine($"  Parser name: {r.Frontend.ParserName}"); b.AppendLine($"  Parse succeeded: {r.Frontend.ParseSucceeded?.ToString().ToLowerInvariant()}"); b.AppendLine($"  Frontend stage reached: {r.Frontend.FrontendStageReached}"); b.AppendLine("  Diagnostics:"); foreach (var d in r.Frontend.ParseDiagnostics) b.AppendLine($"    - {d}"); b.AppendLine();
        }
        if (r.FeatureAir is not null)
        {
            b.AppendLine("Feature AIR"); b.AppendLine($"  Source op: {r.FeatureAir.SourceOpKind}"); b.AppendLine($"  Node: {r.FeatureAir.NodeKind}");
            if (r.FeatureAir.Dimensions is not null) b.AppendLine($"  Dimensions: width={r.FeatureAir.Dimensions.Width:g}, depth={r.FeatureAir.Dimensions.Depth:g}, height={r.FeatureAir.Dimensions.Height:g}");
            b.AppendLine($"  Construction intent: {r.FeatureAir.ConstructionIntent}"); b.AppendLine($"  Stage reached: {r.FeatureAir.StageReached}"); b.AppendLine("  Diagnostics:"); foreach (var d in r.FeatureAir.Diagnostics) b.AppendLine($"    - {d}"); b.AppendLine();
        }
        if (r.ConstructiveAir is not null)
        {
            b.AppendLine("Constructive AIR"); b.AppendLine($"  Node: {r.ConstructiveAir.NodeKind}"); b.AppendLine($"  Canonical form: {r.ConstructiveAir.CanonicalForm}"); b.AppendLine($"  Profile: {r.ConstructiveAir.ProfileKind}(width={r.ConstructiveAir.Width:g}, depth={r.ConstructiveAir.Depth:g})"); b.AppendLine($"  Extrusion: height={r.ConstructiveAir.Height:g}"); b.AppendLine($"  Extrusion axis: {r.ConstructiveAir.ExtrusionAxis}"); b.AppendLine($"  Intent: {r.ConstructiveAir.ConstructionIntent}"); b.AppendLine($"  Route kind: {r.ConstructiveAir.RouteKind}"); b.AppendLine($"  Stage reached: {r.ConstructiveAir.StageReached}"); b.AppendLine("  Diagnostics:"); foreach (var d in r.ConstructiveAir.Diagnostics) b.AppendLine($"    - {d}"); b.AppendLine();
        }
        if (r.ProfileEmission is not null)
        {
            b.AppendLine("Profile extrusion emission"); b.AppendLine($"  Wrapper invoked: {r.ProfileEmission.WrapperInvoked.ToString().ToLowerInvariant()}"); b.AppendLine($"  Emitter: {r.ProfileEmission.EmitterName}"); b.AppendLine($"  Succeeded: {r.ProfileEmission.Succeeded.ToString().ToLowerInvariant()}"); b.AppendLine($"  Dimensions: width={r.ProfileEmission.Width:g}, depth={r.ProfileEmission.Depth:g}, height={r.ProfileEmission.Height:g}"); b.AppendLine($"  Stage reached: {r.ProfileEmission.StageReached}");
            if (r.ProfileEmission.TopologySummary is not null) b.AppendLine($"  Topology: vertices={r.ProfileEmission.TopologySummary.Vertices}, edges={r.ProfileEmission.TopologySummary.Edges}, faces={r.ProfileEmission.TopologySummary.Faces}, planarFaces={r.ProfileEmission.TopologySummary.PlanarFaces}, cylindricalFaces={r.ProfileEmission.TopologySummary.CylindricalFaces}, bounds={r.ProfileEmission.TopologySummary.Bounds}");
            b.AppendLine($"  STEP smoke: {(r.ProfileEmission.StepSmoke.Succeeded ? "succeeded" : r.ProfileEmission.StepSmoke.WasChecked ? "failed" : "unavailable")}"); b.AppendLine("  Diagnostics:"); foreach (var d in r.ProfileEmission.Diagnostics) b.AppendLine($"    - {d}"); b.AppendLine();
        }
        b.AppendLine("AIR"); b.AppendLine($"  Node: {r.Air.Node}"); b.AppendLine($"  Route: {r.Air.Route}"); b.AppendLine($"  Selection class: {r.Air.SelectionClass}"); b.AppendLine($"  Rule: {r.Air.Rule}"); b.AppendLine($"  Construction history: {r.Air.ConstructionHistory}"); b.AppendLine();
        b.AppendLine("Route decision"); b.AppendLine($"  Mode: {r.RouteDecision.Mode}"); b.AppendLine($"  Selected route: {r.RouteDecision.SelectedRoute}"); b.AppendLine($"  Succeeded: {r.RouteDecision.Succeeded.ToString().ToLowerInvariant()}"); b.AppendLine();
        b.AppendLine("BRepPlan"); b.AppendLine($"  Plan kind: {r.BRepPlan.PlanKind}"); b.AppendLine($"  Vertices: {r.BRepPlan.Vertices}"); b.AppendLine($"  Edges: {r.BRepPlan.Edges}"); b.AppendLine($"  Faces: {r.BRepPlan.Faces}"); b.AppendLine($"  Loops: {r.BRepPlan.Loops}"); b.AppendLine($"  Coedges: {r.BRepPlan.Coedges}"); b.AppendLine($"  Cap faces: {r.BRepPlan.CapFaces}"); b.AppendLine($"  Transition faces: {r.BRepPlan.TransitionFaces}"); b.AppendLine($"  Chamfer faces: {r.BRepPlan.ChamferFaces}"); b.AppendLine($"  Split policy: {r.BRepPlan.SplitPolicy}"); b.AppendLine($"  Bounds: {r.BRepPlan.Bounds}"); b.AppendLine();
        b.AppendLine("Emission / STEP"); b.AppendLine($"  Existing emitter path: {r.Emission.ExistingEmitterPath}"); b.AppendLine($"  STEP smoke: {(r.StepSmoke.Succeeded ? "succeeded" : "unavailable")}"); b.AppendLine();
        b.AppendLine("CIR mirror"); b.AppendLine($"  Status: {r.CirMirror.Status}"); b.AppendLine($"  Backend: {r.CirMirror.Backend}"); b.AppendLine($"  Capabilities: {string.Join(", ", r.CirMirror.Capabilities)}"); b.AppendLine($"  Known losses: {string.Join(", ", r.CirMirror.KnownLosses.Select(x => "no " + x.Replace('-', ' ')))}"); b.AppendLine();
        b.AppendLine("Guarantees"); foreach (var g in r.Guarantees) b.AppendLine($"  - {g}"); b.AppendLine();
        b.AppendLine("Diagnostics"); foreach (var d in r.Diagnostics) b.AppendLine($"  - {d}");
        return b.ToString();
    }
}
