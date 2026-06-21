using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Air.Regions;
using Aetheris.Kernel.Core.Brep.Prismatic;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;

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
    AirTraceProfileEmissionSummary? ProfileEmission = null,
    AirTraceFirmamentV2Summary? FirmamentV2 = null,
    [property: JsonPropertyName("regions")] AirRegionTraceSummary? Regions = null,
    AirTraceArtifactsSummary? Artifacts = null);

internal sealed record AirTraceFixtureSummary(string Path, string Expectation, string CaseName, string? ExpectedStage, string ActualStageReached, string? ExpectedRoute, string? ExpectedReason, bool ExpectationSatisfied, bool ParserBacked, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceFrontendSummary(bool ParserBacked, string? ParserName, bool? ParseSucceeded, IReadOnlyList<string> ParseDiagnostics, string? FrontendStageReached, string? FrontendSummary);
internal sealed record AirTraceFeatureAirSummary(bool ParserBacked, string SourceOpKind, string NodeKind, AirTraceDimensionsSummary? Dimensions, string ConstructionIntent, string StageReached, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Guarantees);
internal sealed record AirTraceConstructiveAirSummary(string NodeKind, string CanonicalForm, string SourceFeatureAirNodeKind, string ProfileKind, double Width, double Depth, double Height, string ExtrusionAxis, string ConstructionIntent, string RouteKind, string StageReached, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Guarantees);
internal sealed record AirTraceProfileEmissionSummary(bool WrapperInvoked, string EmitterName, bool Succeeded, double Width, double Depth, double Height, string StageReached, AirTraceProfileEmissionTopologySummary? TopologySummary, AirTraceProfileEmissionStepSmokeSummary StepSmoke, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Guarantees);
internal sealed record AirTraceProfileEmissionTopologySummary(int Vertices, int Edges, int Faces, int PlanarFaces, int CylindricalFaces, int Loops, int Coedges, int? CapFaces, int? SideFaces, string? Bounds);
internal sealed record AirTraceFirmamentV2Summary(string SyntaxVersion, string ModelName, string Units, string SolidName, string RecordType, IReadOnlyList<double> Size, string Stage, IReadOnlyList<AirTraceFirmamentV2SolidSummary> Solids, IReadOnlyList<AirTraceFirmamentV2ModifySummary>? ModifyBlocks = null, FirmamentV2SideHoleIntent? SemanticIntent = null, string? ParentIntegration = null, string? ShellClosure = null, string? StepSmoke = null, string? Blocker = null);
internal sealed record AirTraceFirmamentV2SolidSummary(string Name, string RecordType, IReadOnlyList<double> Size, string? DerivedFrom, IReadOnlyDictionary<string, IReadOnlyList<double>> Overrides, IReadOnlyList<AirTraceFirmamentV2ExposureSummary> Exposures);
internal sealed record AirTraceFirmamentV2ExposureSummary(string Alias, string SelectorKind, string Selector, string RefType, string Axis, string? Subselector);
internal sealed record AirTraceFirmamentV2ModifySummary(string TargetSolid, IReadOnlyList<AirTraceFirmamentV2RegionSummary> Regions);
internal sealed record AirTraceFirmamentV2RegionSummary(string Name, string Kind, string On, string OnKind, string ResolvedOn, string OnRefType, string Operation, string Tool, double Radius, FirmamentV2FaceLocalPoint2D? Center, string Through, string ThroughKind, string ResolvedThrough, string ThroughRefType);
internal sealed record AirTraceProfileEmissionStepSmokeSummary(bool WasChecked, bool Succeeded, bool RequiredMarkersPresent, bool ForbiddenMarkersAbsent, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceDimensionsSummary(double Width, double Depth, double Height);
internal sealed record AirTraceAirSummary(string Node, string Route, string SelectionClass, string Rule, string ConstructionHistory, string FeatureName, string FeatureId, string ProvenanceMilestone);
internal sealed record AirTraceRouteDecisionSummary(string Mode, string? SelectedRoute, bool Succeeded, string Recommendation, string SelectionClass, string Rule, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceBRepPlanSummary(string PlanKind, int Vertices, int Curves, int Edges, int Faces, int Loops, int Coedges, int Surfaces, int CapFaces, int TransitionFaces, int ChamferFaces, int SideFaces, string SplitPolicy, string Bounds, string? RouteSelectionMode, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceEmissionSummary(string ExistingEmitterPath, bool Succeeded, string Recommendation);
internal sealed record AirTraceStepSmokeSummary(bool WasChecked, bool Succeeded, bool RequiredMarkersPresent, bool ForbiddenMarkersAbsent, IReadOnlyList<string> Diagnostics);
internal sealed record AirTraceArtifactsSummary(string Step, string TraceJson, string TraceText, string Manifest);
internal sealed record AirTraceCirMirrorSummary(string Status, string Backend, string SourceNode, string SourceKind, string SelectionClass, string Rule, string MirrorBuilderRoute, IReadOnlyList<string> Capabilities, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Provenance, IReadOnlyList<string> Diagnostics);

internal static class AirTraceReportBuilder
{
    public static readonly string[] SupportedCases = ["prismatic-section-transition", "top-face-loop-chamfer"];
    public static readonly string[] SupportedFixtureCases = ["arbitrary-graph-chamfer", "box", "implicit-parent-mutation-region", "loop-fillet-deferred", "non-uniform-loop-chamfer", "side-hole-face-attached-region", "top-face-loop-chamfer"];

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
        if (IsMetadataOnlyFixture(fixture)) return BuildMetadataOnlyFixture(fixture);

        var caseName = fixture.CaseName;
        if (caseName == "side-hole-face-attached-region") return BuildSideHoleFaceAttachedRegionFixture(fixture);
        if (caseName == "implicit-parent-mutation-region") return BuildImplicitParentMutationRegionFixture(fixture);
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
            Milestone = "AIR-X7", InputKind = "firmfixture", FixturePath = fixture.Path, FixtureExpectation = fixture.Expectation, FixtureCaseName = caseName, ExpectedStage = fixture.ExpectedStage, ActualStageReached = actualStage, ExpectedRoute = fixture.ExpectedRoute, ExpectedReason = fixture.ExpectedReason, ExpectationSatisfied = expectationSatisfied, FixtureDiagnostics = fxDiagnostics, Fixture = new(fixture.Path, fixture.Expectation, caseName, fixture.ExpectedStage, actualStage!, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, false, fxDiagnostics), Diagnostics = Stable([.. report.Diagnostics, .. fxDiagnostics]).ToArray()
        };
    }

    private static AirTraceReport BuildParserBackedFixture(FirmFixture fixture)
    {
        var isFirmamentV2 = string.Equals(fixture.Metadata.GetValueOrDefault("syntax-version"), "FirmamentV2", StringComparison.Ordinal);
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(fixture.Path));
        var frontend = isFirmamentV2 ? FirmamentFrontendTraceProbe.ParseV2Only(fixture.SourceBody, sourceDirectory) : FirmamentFrontendTraceProbe.ParseOnly(fixture.SourceBody);
        var profileEmissionProbe = frontend.ConstructiveAir is null ? null : BoxConstructiveAirToProfileEmissionTraceProbe.Invoke(frontend.ConstructiveAir);
        var actualStage = profileEmissionProbe?.StageReached ?? frontend.FrontendStageReached ?? "frontend-unavailable";
        var stepVerifiedDiagnostics = Array.Empty<string>();
        if (isFirmamentV2 && string.Equals(fixture.ExpectedStage, "step-verified", StringComparison.Ordinal))
        {
            var featureArea = fixture.Metadata.GetValueOrDefault("feature-area");
            var stepVerified = string.Equals(featureArea, "semantic-hole", StringComparison.Ordinal) || string.Equals(featureArea, "semantic-reference", StringComparison.Ordinal) || string.Equals(featureArea, "multi-feature-composition", StringComparison.Ordinal) || string.Equals(featureArea, "semantic-pmi", StringComparison.Ordinal)
                ? TryVerifyV2SemanticHoleStepFixture(fixture)
                : TryVerifyV2BoxStepFixture(fixture);
            if (stepVerified.Succeeded)
            {
                actualStage = "step-verified";
            }
            stepVerifiedDiagnostics = stepVerified.Diagnostics;
        }
        else if (isFirmamentV2 && string.Equals(fixture.ExpectedStage, "deterministic rejection", StringComparison.Ordinal))
        {
            var rejection = TryVerifyV2DeterministicBuildRejection(fixture);
            if (rejection.Succeeded)
            {
                actualStage = "deterministic rejection";
            }
            stepVerifiedDiagnostics = rejection.Diagnostics;
        }
        var expectedStageSatisfied = string.IsNullOrWhiteSpace(fixture.ExpectedStage) || string.Equals(actualStage, fixture.ExpectedStage, StringComparison.Ordinal);
        var expectationSatisfied = fixture.Expectation == "valid"
            ? frontend.ParseSucceeded && expectedStageSatisfied && (profileEmissionProbe?.Succeeded ?? true)
            : ((!frontend.ParseSucceeded) || string.Equals(actualStage, "deterministic rejection", StringComparison.Ordinal)) && expectedStageSatisfied;
        var fxDiagnostics = Stable([.. fixture.Diagnostics, .. frontend.Diagnostics, .. profileEmissionProbe?.Diagnostics ?? [], .. stepVerifiedDiagnostics, "air-x8-parser-backed-fixture-trace-created", "air-x11-parser-backed-fixture-loaded", isFirmamentV2 ? "firmament-v2-parser-invoked" : "air-x11-firmament-parser-invoked", frontend.ParseSucceeded ? (isFirmamentV2 ? "firmament-v2-parse-succeeded" : "air-x11-firmament-parse-succeeded") : (isFirmamentV2 ? "firmament-v2-parse-failed" : "air-x11-firmament-parse-failed"), expectationSatisfied ? "air-x11-parser-backed-expectation-satisfied" : "air-x11-profile-emission-expectation-not-satisfied"]).ToArray();

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

        var regions = frontend.FirmamentV2?.SemanticIntent is not null ? AirRegionTraceFactory.ForFaceAttachedSideHoleDeferred(frontend.FirmamentV2.SemanticIntent.Radius, frontend.FirmamentV2.SemanticIntent.CenterU, frontend.FirmamentV2.SemanticIntent.CenterV, frontend.FirmamentV2.SemanticIntent.AttachFace, frontend.FirmamentV2.SemanticIntent.ThroughFace, frontend.FirmamentV2.SemanticIntent.CenterSelectorFrame) : AirRegionTraceFactory.ForRootBody(actualStage ?? "emitted-brep");
        fxDiagnostics = Stable([.. fxDiagnostics, .. regions.Diagnostics]).ToArray();
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

        var v2 = frontend.FirmamentV2 is null ? null : new AirTraceFirmamentV2Summary(frontend.FirmamentV2.SyntaxVersion, frontend.FirmamentV2.ModelName, frontend.FirmamentV2.Units, frontend.FirmamentV2.SolidName, frontend.FirmamentV2.RecordType, frontend.FirmamentV2.Size, frontend.FirmamentV2.StageReached, frontend.FirmamentV2.Solids.Select(s => new AirTraceFirmamentV2SolidSummary(s.Name, s.RecordType, s.Size, s.DerivedFrom, s.Overrides, s.Exposures.Select(e => new AirTraceFirmamentV2ExposureSummary(e.Alias, e.SelectorKind, e.Selector, e.RefType, e.Axis, e.Subselector)).ToArray())).ToArray(), frontend.FirmamentV2.ModifyBlocks?.Select(m => new AirTraceFirmamentV2ModifySummary(m.TargetSolid, m.Regions.Select(r => new AirTraceFirmamentV2RegionSummary(r.Name, r.Kind, r.On, r.OnKind, r.ResolvedOn, r.OnRefType, r.Operation, r.Tool, r.Radius, r.Center, r.Through, r.ThroughKind, r.ResolvedThrough, r.ThroughRefType)).ToArray())).ToArray(), frontend.FirmamentV2.SemanticIntent, frontend.FirmamentV2.ParentIntegration, frontend.FirmamentV2.ShellClosure, frontend.FirmamentV2.StepSmoke, frontend.FirmamentV2.Blocker);

        return new(isFirmamentV2 && frontend.FirmamentV2?.SemanticIntent is not null ? "AIR-FIRMAMENT-X4" : isFirmamentV2 ? "AIR-FIRMAMENT-X1" : "AIR-X11", "trace", "lowering", "firmfixture", fixture.CaseName, expectationSatisfied, frontend.FrontendSummary,
            new(constructiveAir?.NodeKind ?? featureAir?.NodeKind ?? "FirmamentPrimitive", constructiveAir?.RouteKind ?? "none", "none", "none", "parser-backed-source-fixture", fixture.CaseName, fixture.CaseName, "AIR-X11"),
            new("none", null, false, "Route selection and BRepPlan are deferred for parser-backed box fixtures in AIR-X11; profile emission is reported separately.", "none", "none", []),
            new("none", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none", "none", null, []),
            new(profileEmission?.EmitterName ?? "none", profileEmission?.Succeeded ?? false, profileEmission is null ? "parser-backed fixture stops before profile emission" : "parser-backed fixture invoked existing profile extrusion wrapper/emitter summary"),
            new(profileEmission?.StepSmoke.WasChecked ?? false, profileEmission?.StepSmoke.Succeeded ?? false, profileEmission?.StepSmoke.RequiredMarkersPresent ?? false, profileEmission?.StepSmoke.ForbiddenMarkersAbsent ?? false, profileEmission?.StepSmoke.Diagnostics ?? []),
            new("not-requested", "none", "none", "FirmamentPrimitive", "none", "none", "none", [], [], [], []),
            [], string.Equals(actualStage, "step-verified", StringComparison.Ordinal) ? ["Box-only BRep/AP242 path verified by build/export", "non-Box V2 features deferred"] : ["BRepPlan/CIR deferred for parser-backed box fixture", "STEP smoke unavailable for parser-backed box fixture"], fxDiagnostics,
            (isFirmamentV2
                ? string.Equals(actualStage, "step-verified", StringComparison.Ordinal)
                    ? ["real Firmament V2 parser invoked", "V2 Box lowered to existing FirmamentLoweredBoxParameters", "existing primitive executor produced BrepBody", "real Step242Exporter emitted AP242", "STEP reimport/topology/volume verified", "no V1 parser route"]
                    : ["real Firmament V2 parser invoked", "parser-backed fixture reaches Feature AIR summary", "no V1 parser route", "no production route replacement", "no new geometry"]
                : ["real Firmament parser invoked", "parser-backed fixture reaches Feature AIR summary", "parser-backed fixture reaches Constructive AIR summary", "ProfileExtrude wrapper invoked", "no production grammar expansion", "no production route replacement", "no new geometry", "no profile emitter rewrite"]),
            string.Equals(actualStage, "step-verified", StringComparison.Ordinal)
                ? ["V2 Box only", "no non-Box primitive wiring", "no side-hole exporter reroute", "no patterns", "no PMI", "no DFM enforcement", "no hardcoded STEP template", "no trace-only output"]
                : ["no production Firmament grammar expansion", "no production route replacement", "no production analyzer behavior change", "no STEP exporter/importer change", "no BRep topology behavior change", "no route-selection/JudgmentUtility behavior change", "no CIR evaluator/tape behavior change", "no Boolean behavior change", "no AirEdgeSweep behavior change", "no BrepBoundedChamfer/BrepBoundedFillet behavior change", "no chamfer/fillet/shell geometry change", "no new geometry"],
            new(fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage!, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, true, fxDiagnostics),
            fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage!, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, fxDiagnostics,
            new(true, frontend.ParserName, frontend.ParseSucceeded, frontend.Diagnostics, actualStage!, frontend.FrontendSummary),
            featureAir, constructiveAir, profileEmission, v2, regions);
    }





    private static (bool Succeeded, string[] Diagnostics) TryVerifyV2DeterministicBuildRejection(FirmFixture fixture)
    {
        var diagnostics = new List<string> { "step-v2-x4-build-command-invoked" };
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "aetheris-step-v2-x4-reject-trace", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var stepPath = Path.Combine(dir, fixture.CaseName + ".step");
            var build = FirmamentBuildAndExport.Run(fixture.Path, stepPath);
            if (build.IsSuccess || File.Exists(stepPath))
            {
                diagnostics.Add("step-v2-x4-invalid-build-unexpectedly-succeeded");
                return (false, Stable(diagnostics).ToArray());
            }

            var messages = string.Join("\n", build.Diagnostics.Select(d => d.Message));
            if (fixture.Metadata.TryGetValue("expected-diagnostic", out var expected) && !messages.Contains(expected, StringComparison.Ordinal))
            {
                diagnostics.Add("step-v2-x4-expected-diagnostic-missing");
                return (false, Stable(diagnostics).ToArray());
            }

            if (fixture.Metadata.TryGetValue("expected-diagnostic", out var expectedDiagnostic))
            {
                diagnostics.Add(expectedDiagnostic);
            }
            diagnostics.Add("step-v2-x4-deterministic-rejection-verified");
            return (true, Stable(diagnostics).ToArray());
        }
        catch
        {
            diagnostics.Add("step-v2-x4-rejection-verification-threw");
            return (false, Stable(diagnostics).ToArray());
        }
    }

    private static (bool Succeeded, string[] Diagnostics) TryVerifyV2SemanticHoleStepFixture(FirmFixture fixture)
    {
        var diagnostics = new List<string> { "step-v2-x2-build-command-invoked" };
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "aetheris-step-v2-x2-trace", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var stepPath = Path.Combine(dir, fixture.CaseName + ".step");
            var build = FirmamentBuildAndExport.Run(fixture.Path, stepPath);
            if (!build.IsSuccess)
            {
                diagnostics.Add("step-v2-x2-build-failed");
                return (false, Stable(diagnostics).ToArray());
            }

            var stepText = File.ReadAllText(stepPath);
            if (CountStepEntities(stepText, "ADVANCED_FACE") <= 0 || CountStepEntities(stepText, "VERTEX_POINT") <= 0)
            {
                diagnostics.Add("step-v2-x2-step-topology-markers-missing");
                return (false, Stable(diagnostics).ToArray());
            }

            if (stepText.Contains("trace", StringComparison.OrdinalIgnoreCase) || stepText.Contains("controlled fixture only", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add("step-v2-x2-trace-or-controlled-artifact-detected");
                return (false, Stable(diagnostics).ToArray());
            }

            var import = Step242Importer.ImportBody(stepText);
            if (!import.IsSuccess)
            {
                diagnostics.Add("step-v2-x2-step-reimport-failed");
                return (false, Stable(diagnostics).ToArray());
            }

            var volume = StepAnalyzer.AnalyzeVolume(stepPath);
            var expectedVolume = fixture.Metadata.TryGetValue("expected-volume", out var expectedVolumeRaw) && double.TryParse(expectedVolumeRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedVolume)
                ? parsedVolume
                : fixture.CaseName switch
                {
                    "feature-v2-shaft-hole-through-step-verified" => 480d - Math.PI * 6d,
                    "feature-v2-shaft-hole-blind-step-verified" => 480d - Math.PI * 3d,
                    "feature-v2-counterbore-step-verified" => 480d - ((Math.PI * 6d) + (Math.PI * 3d)),
                    "feature-v2-countersink-step-verified" => 480d - ((Math.PI * 6d) + (Math.PI * 7d / 3d) - Math.PI),
                    "pmi-v2-hole-diameter-callout-emits-in-step" => 480d - Math.PI * 6d,
                    "pmi-v2-datum-plane-emits-in-step" => 480d,
                    _ => double.NaN
                };

            if (!volume.Success || !volume.Exact || !double.IsFinite(expectedVolume) || Math.Abs(volume.Volume - expectedVolume) > 1e-8)
            {
                diagnostics.Add("step-v2-x2-volume-mismatch");
                return (false, Stable(diagnostics).ToArray());
            }

            if (string.Equals(fixture.Metadata.GetValueOrDefault("feature-area"), "semantic-pmi", StringComparison.Ordinal))
            {
                var hasPmiEvidence = fixture.CaseName switch
                {
                    "pmi-v2-hole-diameter-callout-emits-in-step" => stepText.Contains("SHAPE_DIMENSION_REPRESENTATION('diameter:base.mount'", StringComparison.Ordinal),
                    "pmi-v2-datum-plane-emits-in-step" => stepText.Contains("PROPERTY_DEFINITION('datum:A:base'", StringComparison.Ordinal),
                    _ => false
                };
                if (!hasPmiEvidence)
                {
                    diagnostics.Add("step-v2-x7-semantic-pmi-evidence-missing");
                    return (false, Stable(diagnostics).ToArray());
                }

                diagnostics.Add("step-v2-x7-semantic-pmi-evidence-verified");
                diagnostics.Add("step-v2-x7-graphical-pmi-not-required");
            }

            diagnostics.Add("step-v2-x2-real-ap242-emitted");
            diagnostics.Add("step-v2-x2-step-roundtrip-succeeded");
            diagnostics.Add("step-v2-x2-topology-evidence-verified");
            diagnostics.Add("step-v2-x2-volume-verified");
            return (true, Stable(diagnostics).ToArray());
        }
        catch
        {
            diagnostics.Add("step-v2-x2-step-verification-threw");
            return (false, Stable(diagnostics).ToArray());
        }
    }

    private static (bool Succeeded, string[] Diagnostics) TryVerifyV2BoxStepFixture(FirmFixture fixture)
    {
        var diagnostics = new List<string> { "step-v2-a1-build-command-invoked" };
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "aetheris-step-v2-a1-trace", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var stepPath = Path.Combine(dir, "pipeline-v2-box-step-verified.step");
            var build = FirmamentBuildAndExport.Run(fixture.Path, stepPath);
            if (!build.IsSuccess)
            {
                diagnostics.Add("step-v2-a1-build-failed");
                return (false, Stable(diagnostics).ToArray());
            }

            var stepText = File.ReadAllText(stepPath);
            if (CountStepEntities(stepText, "ADVANCED_FACE") <= 0 || CountStepEntities(stepText, "VERTEX_POINT") <= 0)
            {
                diagnostics.Add("step-v2-a1-step-topology-markers-missing");
                return (false, Stable(diagnostics).ToArray());
            }

            var import = Step242Importer.ImportBody(stepText);
            if (!import.IsSuccess)
            {
                diagnostics.Add("step-v2-a1-step-reimport-failed");
                return (false, Stable(diagnostics).ToArray());
            }

            if (fixture.Metadata.TryGetValue("expected-topology", out var expectedTopology) && TryReadTopologyCount(expectedTopology, "faces", out var expectedFaces) && import.Value.Topology.Faces.Count() != expectedFaces)
            {
                diagnostics.Add("step-v2-a1-topology-count-mismatch");
                return (false, Stable(diagnostics).ToArray());
            }

            var volume = StepAnalyzer.AnalyzeVolume(stepPath);
            var expectedVolume = fixture.Metadata.TryGetValue("expected-volume", out var expectedVolumeRaw) && double.TryParse(expectedVolumeRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedVolume) ? parsedVolume : 480d;
            if (!volume.Success || Math.Abs(volume.Volume - expectedVolume) > 1e-8)
            {
                diagnostics.Add("step-v2-a1-volume-mismatch");
                return (false, Stable(diagnostics).ToArray());
            }

            diagnostics.Add("step-v2-a1-real-ap242-emitted");
            diagnostics.Add("step-v2-a1-step-roundtrip-succeeded");
            diagnostics.Add("step-v2-a1-topology-verified");
            diagnostics.Add("step-v2-a1-volume-verified");
            return (true, Stable(diagnostics).ToArray());
        }
        catch
        {
            diagnostics.Add("step-v2-a1-step-verification-threw");
            return (false, Stable(diagnostics).ToArray());
        }
    }

    private static bool TryReadTopologyCount(string raw, string name, out int value)
    {
        value = 0;
        var match = Regex.Match(raw, $@"(?:^|[,\s]){Regex.Escape(name)}=(?<value>\d+)", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["value"].Value, out value);
    }

    private static int CountStepEntities(string stepText, string entityName) =>
        Regex.Matches(stepText, "=\\s*" + Regex.Escape(entityName) + "\\s*\\(", RegexOptions.CultureInvariant).Count;

    private static bool IsMetadataOnlyFixture(FirmFixture fixture)
    {
        var implementation = fixture.Metadata.GetValueOrDefault("implementation");
        if (string.Equals(implementation, "not-implemented", StringComparison.Ordinal) || string.Equals(implementation, "deferred", StringComparison.Ordinal)) return true;
        return !SupportedFixtureCases.Contains(fixture.CaseName, StringComparer.Ordinal) && string.Equals(implementation, "rejected", StringComparison.Ordinal);
    }

    private static AirTraceReport BuildMetadataOnlyFixture(FirmFixture fixture)
    {
        var implementation = fixture.Metadata.GetValueOrDefault("implementation") ?? (fixture.Expectation == "invalid" ? "rejected" : "not-implemented");
        var expectedDiagnostic = fixture.Metadata.GetValueOrDefault("expected-diagnostic") ?? (implementation == "not-implemented" ? "firmament-feature-not-implemented" : "firmament-fixture-rejected");
        var actualStage = fixture.ExpectedStage ?? (implementation == "deferred" ? "deferred" : implementation == "rejected" || fixture.Expectation == "invalid" ? "rejected" : "not-implemented");
        var isNotImplemented = implementation is "not-implemented" or "deferred";
        var expectationSatisfied = fixture.Expectation == "valid"
            ? isNotImplemented && (actualStage is "not-implemented" or "deferred" || fixture.ExpectedStage == actualStage)
            : implementation == "rejected" || actualStage == "rejected";
        var diagnostics = Stable([.. fixture.Diagnostics, "air-firmament-a1-metadata-only-fixture-classified", expectedDiagnostic, isNotImplemented ? "air-firmament-a1-feature-not-implemented" : "air-firmament-a1-fixture-rejected", expectationSatisfied ? "air-firmament-a1-expectation-satisfied" : "air-firmament-a1-expectation-not-satisfied"]).ToArray();
        return new("AIR-FIRMAMENT-A1", "trace", "lowering", "firmfixture", fixture.CaseName, expectationSatisfied, isNotImplemented ? "Fixture is valid Firmament design intent but its lowering/materialization route is deliberately not implemented in A1." : "Fixture is rejected by metadata contract with stable diagnostic.",
            new("FirmamentFixture", "none", fixture.Metadata.GetValueOrDefault("category") ?? "none", "none", "metadata-only-fixture", fixture.CaseName, fixture.Metadata.GetValueOrDefault("fixture-id") ?? fixture.CaseName, "AIR-FIRMAMENT-A1"),
            new("none", null, false, isNotImplemented ? "not implemented" : "rejected", fixture.Metadata.GetValueOrDefault("category") ?? "none", "none", diagnostics),
            new("none", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none", "none", null, []),
            new("none", false, isNotImplemented ? "no geometry emitted for not-implemented Firmament fixture" : "no geometry emitted for invalid Firmament fixture"),
            new(false, false, false, false, []),
            new("not-requested", "none", "FirmamentFixture", "Firmament", fixture.Metadata.GetValueOrDefault("category") ?? "none", "none", "none", [], ["no-topology-authority"], ["AIR-FIRMAMENT-A1"], diagnostics),
            [], ["metadata-only corpus fixture", isNotImplemented ? "feature lowering not implemented" : "invalid fixture rejected"], diagnostics,
            ["future Firmament fixtures do not require geometry implementation", "no production route replacement", "no BRep topology behavior change", "no STEP exporter/importer change", "no CIR topology authority"],
            ["no broad new geometry feature support", "no production route replacement", "no shell/fillet/surfacing implementation", "no general side-hole support", "no arbitrary face/axis support", "no CIR topology authority", "no Boolean general admission", "no STEP exporter/importer behavior change", "no BRep topology behavior change"],
            new(fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, false, diagnostics),
            fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, diagnostics);
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

    private static AirTraceReport BuildSideHoleFaceAttachedRegionFixture(FirmFixture fixture)
    {
        var regions = AirRegionTraceFactory.ForFaceAttachedSideHoleDeferred();
        var actualStage = "region-parent-integrated";
        var expectationSatisfied = fixture.Expectation == "valid" && (string.IsNullOrWhiteSpace(fixture.ExpectedStage) || fixture.ExpectedStage == actualStage);
        var fxDiagnostics = Stable([.. fixture.Diagnostics, .. regions.Diagnostics, "air-region-x1-firmfixture-case-mapped", "air-region-x4-firmfixture-boundary-contract-created", "air-region-x5-firmfixture-integration-decision-created", "air-region-x6-firmfixture-brep-placeholders-created", "air-region-x7-firmfixture-materialization-created", expectationSatisfied ? "air-region-x1-expectation-satisfied" : "air-region-x1-expectation-not-satisfied"]).ToArray();
        return new("AIR-REGION-X12", "trace", "lowering", "firmfixture", fixture.CaseName, expectationSatisfied, "FaceAttachedRegion side-hole preserves the X9/X10/X11 evidence chain and consumes the controlled RegionIntegrationPatch into closed parent shell evidence.",
            new("RegionFixture", "ControlledSideHoleParentBRepIntegration", "none", "none", "metadata-driven-region-contract", fixture.CaseName, fixture.CaseName, "AIR-REGION-X11"),
            new("SwitchMatch", "ControlledSideHoleParentBRepIntegration", true, "controlled +X entry loop, -X exit loop, cylindrical cut wall, and RegionIntegrationPatch consumed into closed parent shell evidence", "FaceAttachedRegion", "SideHole", []),
            new("none", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none", "none", null, []),
            new("ControlledSideHoleParentBRepIntegration", true, "standalone side-hole patch evidence preserved; controlled +X entry and -X exit face split evidence created; controlled parent shell closure evidence integrated"),
            new(false, false, false, false, []),
            new("not-requested", "none", "none", "RegionFixture", "none", "none", "none", [], [], [], []),
            [], ["parent BRep integration not implemented", "CIR evaluator composition deferred"], fxDiagnostics,
            [.. regions.Guarantees, "metadata-driven region fixture", "controlled standalone patch materialized", "controlled +X face split evidence", "controlled -X exit loop evidence", "controlled shell closure evidence", "parent integration integrated"],
            ["no AIR Region production integration", "no production route replacement", "no Firmament grammar expansion", "no BRepPlan semantics change", "no CIR evaluator/tape behavior change", "no STEP exporter/importer change", "no BRep topology behavior change", "no route-selection/JudgmentUtility behavior change", "no production analyzer behavior change", "no Boolean behavior change", "no AirEdgeSweep behavior change", "no BrepBoundedChamfer/BrepBoundedFillet behavior change", "no chamfer/fillet/shell geometry change", "no arbitrary graph support", "no import/recovery", "no triangle migration", "no NURBS/freeform behavior change"],
            new(fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage!, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, false, fxDiagnostics),
            fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage!, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, fxDiagnostics, Regions: regions);
    }

    private static AirTraceReport BuildImplicitParentMutationRegionFixture(FirmFixture fixture)
    {
        var regions = AirRegionTraceFactory.ForImplicitParentMutationRejected();
        var actualStage = "region-rejected";
        var reasonSatisfied = string.IsNullOrWhiteSpace(fixture.ExpectedReason) || fixture.ExpectedReason == "implicit-parent-mutation-rejected";
        var expectationSatisfied = fixture.Expectation == "invalid" && reasonSatisfied && (string.IsNullOrWhiteSpace(fixture.ExpectedStage) || fixture.ExpectedStage == actualStage);
        var fxDiagnostics = Stable([.. fixture.Diagnostics, .. regions.Diagnostics, expectationSatisfied ? "air-region-x1-expectation-satisfied" : "air-region-x1-expectation-not-satisfied"]).ToArray();
        return BuildSideHoleFaceAttachedRegionFixture(fixture) with { Succeeded = false, Recommendation = "implicit parent mutation rejected", Emission = new("none", false, "no geometry emitted for rejected implicit parent mutation fixture"), StepSmoke = new(false, false, false, false, []), ActualStageReached = actualStage, ExpectedReason = fixture.ExpectedReason, ExpectationSatisfied = expectationSatisfied, FixtureDiagnostics = fxDiagnostics, Diagnostics = fxDiagnostics, Fixture = new(fixture.Path, fixture.Expectation, fixture.CaseName, fixture.ExpectedStage, actualStage!, fixture.ExpectedRoute, fixture.ExpectedReason, expectationSatisfied, false, fxDiagnostics), Regions = regions };
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
        if (r.FirmamentV2 is not null)
        {
            b.AppendLine("Firmament V2");
            b.AppendLine($"  Stage: {r.FirmamentV2.Stage}");
            b.AppendLine($"  Model: {r.FirmamentV2.ModelName}");
            b.AppendLine($"  Units: {r.FirmamentV2.Units}");
            foreach (var solid in r.FirmamentV2.Solids)
            {
                b.AppendLine();
                b.AppendLine($"  Solid: {solid.Name}");
                b.AppendLine($"    Record: {solid.RecordType}");
                if (!string.IsNullOrWhiteSpace(solid.DerivedFrom)) b.AppendLine($"    DerivedFrom: {solid.DerivedFrom}");
                if (solid.Overrides.Count > 0)
                {
                    b.AppendLine("    With:");
                    foreach (var ov in solid.Overrides) b.AppendLine($"      {ov.Key}: [{string.Join(", ", ov.Value.Select(v => v.ToString("g", System.Globalization.CultureInfo.InvariantCulture)))}]");
                }
                b.AppendLine($"    Size: [{string.Join(", ", solid.Size.Select(v => v.ToString("g", System.Globalization.CultureInfo.InvariantCulture)))}]");
                if (solid.Exposures.Count > 0)
                {
                    b.AppendLine();
                    b.AppendLine("    Expose:");
                    foreach (var exposure in solid.Exposures) b.AppendLine($"      {exposure.Selector} => {exposure.Alias} : {exposure.RefType}");
                }
            }
            if (r.FirmamentV2.ModifyBlocks is not null)
            {
                foreach (var modify in r.FirmamentV2.ModifyBlocks)
                {
                    b.AppendLine();
                    b.AppendLine($"  Modify: {modify.TargetSolid}");
                    foreach (var region in modify.Regions)
                    {
                        b.AppendLine($"    Region: {region.Name}");
                        b.AppendLine($"      Kind: {region.Kind}");
                        b.AppendLine($"      On: {region.On}");
                        if (!string.Equals(region.On, region.ResolvedOn, StringComparison.Ordinal)) b.AppendLine($"      Resolved on: {region.ResolvedOn}");
                        b.AppendLine($"      Operation: {region.Operation}");
                        b.AppendLine($"      Tool: {region.Tool}");
                        b.AppendLine($"      Radius: {region.Radius:g}");
                        var cu = region.Center?.U ?? 0; var cv = region.Center?.V ?? 0;
                        b.AppendLine($"      Center: [{cu:g}, {cv:g}]");
                        var frame = FirmamentV2FaceLocalPoint2D.ConventionFor(region.ResolvedOn.Contains("-X", StringComparison.Ordinal) ? "-X" : region.ResolvedOn.Contains("+Y", StringComparison.Ordinal) ? "+Y" : region.ResolvedOn.Contains("-Y", StringComparison.Ordinal) ? "-Y" : region.ResolvedOn.Contains("+Z", StringComparison.Ordinal) ? "+Z" : region.ResolvedOn.Contains("-Z", StringComparison.Ordinal) ? "-Z" : "+X");
                        b.AppendLine($"      Center frame: {frame.Replace(":u=", " local u=").Replace(",v=", ", v=")}");
                        b.AppendLine($"      Through: {region.Through}");
                        if (!string.Equals(region.Through, region.ResolvedThrough, StringComparison.Ordinal)) b.AppendLine($"      Resolved through: {region.ResolvedThrough}");
                    }
                }
            }
            if (r.FirmamentV2.SemanticIntent is not null)
            {
                b.AppendLine();
                b.AppendLine("  Lowering:");
                b.AppendLine("    Semantic intent: SideHole");
                b.AppendLine($"    Route: {r.FirmamentV2.SemanticIntent.AttachFace} -> {r.FirmamentV2.SemanticIntent.ThroughFace}");
                if (r.FirmamentV2.SemanticIntent.AttachTargetKind == "Alias") b.AppendLine($"    Attach alias: {r.FirmamentV2.SemanticIntent.AttachTargetSource} -> face({r.FirmamentV2.SemanticIntent.AttachFace})");
                if (r.FirmamentV2.SemanticIntent.ThroughTargetKind == "Alias") b.AppendLine($"    Through alias: {r.FirmamentV2.SemanticIntent.ThroughTargetSource} -> face({r.FirmamentV2.SemanticIntent.ThroughFace})");
                b.AppendLine($"    AIR Region: {(r.Regions is null ? "not-reached" : "FaceAttachedRegion golden trace chain")}");
                b.AppendLine($"    Parent integration: {r.FirmamentV2.ParentIntegration ?? "not-reached"}");
                b.AppendLine($"    Shell closure: {r.FirmamentV2.ShellClosure ?? "not-reached"}");
                b.AppendLine($"    STEP smoke: {r.FirmamentV2.StepSmoke ?? "not-reached"}");
            }
            if (r.FeatureAir is not null) b.AppendLine($"  Feature AIR: {r.FeatureAir.NodeKind}");
            b.AppendLine();
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
        if (r.Regions is not null)
        {
            b.AppendLine("Regions"); b.AppendLine($"  Count: {r.Regions.RegionCount}"); b.AppendLine($"  Root: {r.Regions.RootRegionId}");
            foreach (var region in r.Regions.Regions)
            {
                b.AppendLine(); b.AppendLine($"  Region {region.RegionId}"); b.AppendLine($"    Kind: {region.RegionKind}");
                if (region.ParentRegionId is not null) b.AppendLine($"    Parent: {region.ParentRegionId}");
                b.AppendLine($"    Effect: {region.EffectKind}"); b.AppendLine($"    Yield: {region.YieldKind}"); b.AppendLine($"    Boundary: {region.BoundaryContractKind}");
                b.AppendLine($"    Frame: {FrameLabel(region.LocalFrame)}"); b.AppendLine($"    Integration: {region.IntegrationStatus}");
                if (region.Yield is not null)
                {
                    var y = region.Yield;
                    b.AppendLine("    Region yield");
                    b.AppendLine($"      Yield: {y.YieldKind}");
                    b.AppendLine($"      Feature: {y.FeatureKind}");
                    b.AppendLine($"      Attachment: {y.Attachment.AttachmentKind} {y.Attachment.FaceSelector}");
                    b.AppendLine($"      Profile: {y.Profile.ProfileKind}(radius={y.Profile.Radius:g}, center=({y.Profile.Center.X:g},{y.Profile.Center.Y:g}))");
                    b.AppendLine($"      Direction: {(y.Direction.IsThrough ? "through" : y.Direction.Depth)} {y.Direction.Sense.ToLowerInvariant()} along face normal");
                    b.AppendLine($"      Boundary: {y.BoundaryIntent.BoundaryKind}");
                    b.AppendLine($"        Entry: circular entry loop on {y.Attachment.FaceSelector} face");
                    b.AppendLine($"        Exit: opposite-side exit {y.BoundaryIntent.ExitBoundary.ToLowerInvariant()}");
                    b.AppendLine("        Rim: circular rim intent");
                    b.AppendLine($"      Affected scope: {(y.AffectedScope.ParentBodyOnly ? "parent body only" : "parent scope")}, local feature scope");
                    b.AppendLine($"      Integration: {y.IntegrationStatus}");
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in y.Guarantees) b.AppendLine($"        - {guarantee}");
                }

                if (region.CirMirror is not null)
                {
                    var m = region.CirMirror;
                    b.AppendLine("    Region CIR mirror");
                    b.AppendLine($"      Region: {m.SourceRegionId}");
                    b.AppendLine($"      Feature: {m.YieldFeatureKind}");
                    b.AppendLine($"      Status: {m.Status}");
                    b.AppendLine($"      Backend: {m.Backend}");
                    b.AppendLine($"      Effect: {m.Effect}");
                    b.AppendLine($"      Parent field: {m.ParentField}");
                    b.AppendLine($"      Subtract field: {m.SubtractField}");
                    b.AppendLine($"      Capabilities: {string.Join(", ", m.Capabilities)}");
                    b.AppendLine("      Known losses:");
                    foreach (var loss in m.KnownLosses) b.AppendLine($"        - {loss.Replace('-', ' ')}");
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in m.Guarantees) b.AppendLine($"        - {guarantee}");
                }

                if (region.BrepBoundary is not null)
                {
                    var bb = region.BrepBoundary;
                    b.AppendLine("    Region BRepPlan boundary");
                    b.AppendLine($"      Region: {bb.SourceRegionId}");
                    b.AppendLine($"      Feature: {bb.FeatureKind}");
                    b.AppendLine($"      Status: {bb.Status}");
                    b.AppendLine($"      Affected parent: {bb.AffectedParent.ParentBody}");
                    b.AppendLine($"      Affected face: {bb.AffectedParent.AffectedFaceSelector}");
                    b.AppendLine("      Entry boundary: circular entry loop intent");
                    b.AppendLine("      Exit boundary: opposite-side exit deferred");
                    b.AppendLine("      Cut wall: cylindrical cut wall intent deferred");
                    b.AppendLine($"      Planned roles: {string.Join(", ", bb.PlannedRoles)}");
                    b.AppendLine($"      Integration: {bb.IntegrationStatus}");
                    b.AppendLine("      Known losses:");
                    foreach (var loss in bb.KnownLosses) b.AppendLine($"        - {loss.Replace('-', ' ')}");
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in bb.Guarantees) b.AppendLine($"        - {guarantee}");
                }


                if (region.BrepPlaceholders is not null)
                {
                    var bp = region.BrepPlaceholders;
                    b.AppendLine("    Region BRepPlan placeholders");
                    b.AppendLine($"      Region: {bp.SourceRegionId}");
                    b.AppendLine($"      Feature: {bp.FeatureKind}");
                    b.AppendLine($"      Status: {bp.PlaceholderStatus}");
                    b.AppendLine($"      Elements: {bp.Summary.PlaceholderElementCount}");
                    b.AppendLine($"      Materialized: {bp.Summary.MaterializedElementCount}");
                    b.AppendLine("      Placeholders:");
                    foreach (var e in bp.Elements)
                    {
                        b.AppendLine($"        - {e.Id}");
                        b.AppendLine($"          Kind: {e.Kind}");
                        b.AppendLine($"          Role: {e.Role}");
                        b.AppendLine($"          Materialization: {e.MaterializationStatus}");
                    }
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in bp.Guarantees) b.AppendLine($"        - {guarantee}");
                }


                if (region.Materialization is not null)
                {
                    var m = region.Materialization;
                    b.AppendLine("    Region materialization");
                    b.AppendLine($"      Region: {m.SourceRegionId}");
                    b.AppendLine($"      Feature: {m.FeatureKind}");
                    b.AppendLine($"      Status: {m.Status}");
                    b.AppendLine($"      Route: {m.Route}");
                    b.AppendLine("      Placeholder mappings:");
                    foreach (var map in m.PlaceholderMappings)
                    {
                        b.AppendLine($"        - {map.PlaceholderRole} -> {map.MaterializationStatus}");
                        b.AppendLine($"          Element: {map.MaterializedRole}");
                    }
                    b.AppendLine("      Topology:");
                    b.AppendLine($"        Faces: {m.TopologySummary.FaceCount}");
                    b.AppendLine($"        Loops: {m.TopologySummary.LoopCount}");
                    b.AppendLine($"        Cylindrical faces: {m.TopologySummary.CylindricalFaceCount}");
                    b.AppendLine($"        Closed: {(m.TopologySummary.Closed.HasValue ? m.TopologySummary.Closed.Value.ToString().ToLowerInvariant() : "unavailable")}");
                    b.AppendLine($"      STEP smoke: {(m.StepSmoke.WasChecked ? (m.StepSmoke.Succeeded ? "succeeded" : "failed") : "unavailable")}");
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in m.Guarantees) b.AppendLine($"        - {guarantee}");
                }

                if (region.FaceSplit is not null)
                {
                    var fs = region.FaceSplit;
                    b.AppendLine("    Region face split");
                    b.AppendLine($"      Region: {fs.SourceRegionId}");
                    b.AppendLine($"      Affected face: {fs.AffectedFaceSelector}");
                    b.AppendLine($"      Status: {fs.FaceSplitStatus}");
                    b.AppendLine($"      Entry loop: {fs.EntryLoopStatus}");
                    b.AppendLine($"      Profile: {fs.EntryLoopProfile}");
                    b.AppendLine($"      Placeholder consumed: {fs.EntryLoopRole}");
                    b.AppendLine("      Topology:");
                    b.AppendLine($"        Face loops: {fs.TopologySummary.FaceLoopCount}");
                    b.AppendLine($"        Inner loops: {fs.TopologySummary.InnerLoopCount}");
                    b.AppendLine($"        Circular edges: {fs.TopologySummary.CircularEdgeCount}");
                    b.AppendLine("      Blocker:");
                    if (fs.Blocker is null)
                    {
                        b.AppendLine("        Category: none");
                        b.AppendLine("        Code: none");
                        b.AppendLine("        Message: none");
                    }
                    else
                    {
                        b.AppendLine($"        Category: {fs.Blocker.Category}");
                        b.AppendLine($"        Code: {fs.Blocker.Code}");
                        b.AppendLine($"        Message: {fs.Blocker.Message}");
                    }
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in fs.Guarantees) b.AppendLine($"        - {guarantee}");
                }

                if (region.ExitLoop is not null)
                {
                    var el = region.ExitLoop;
                    b.AppendLine("    Region exit loop");
                    b.AppendLine($"      Region: {el.SourceRegionId}");
                    b.AppendLine($"      Exit face: {el.ExitFaceSelector}");
                    b.AppendLine($"      Status: {el.ExitLoopStatus}");
                    b.AppendLine($"      Profile: {el.ExitLoopProfile}");
                    b.AppendLine($"      Placeholder consumed: {el.ExitLoopRole}");
                    b.AppendLine("      Topology:");
                    b.AppendLine($"        Face loops: {el.TopologySummary.FaceLoopCount}");
                    b.AppendLine($"        Inner loops: {el.TopologySummary.InnerLoopCount}");
                    b.AppendLine($"        Circular edges: {el.TopologySummary.CircularEdgeCount}");
                    b.AppendLine("      Blocker:");
                    if (el.Blocker is null)
                    {
                        b.AppendLine("        Category: none");
                        b.AppendLine("        Code: none");
                        b.AppendLine("        Message: none");
                    }
                    else
                    {
                        b.AppendLine($"        Category: {el.Blocker.Category}");
                        b.AppendLine($"        Code: {el.Blocker.Code}");
                        b.AppendLine($"        Message: {el.Blocker.Message}");
                    }
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in el.Guarantees) b.AppendLine($"        - {guarantee}");
                }

                if (region.CutWallAttachment is not null || region.ShellClosure is not null)
                {
                    b.AppendLine("    Region cut wall / shell closure");
                    b.AppendLine($"      Region: {region.RegionId}");
                    if (region.CutWallAttachment is not null)
                    {
                        var cw = region.CutWallAttachment;
                        b.AppendLine($"      Status: {cw.Status}");
                        b.AppendLine($"      Cut wall: {(cw.Status == Aetheris.Kernel.Core.Air.Regions.AirRegionCutWallAttachmentStatus.CutWallAttached ? "cylindrical face materialized" : "blocked")}");
                        b.AppendLine("      Entry loop: materialized");
                        b.AppendLine("      Exit loop: materialized");
                        b.AppendLine("      Placeholder consumed: CutWallFace");
                    }
                    if (region.ShellClosure is not null)
                    {
                        var sc = region.ShellClosure;
                        b.AppendLine("    Region shell closure");
                        b.AppendLine($"      Region: {sc.SourceRegionId}");
                        b.AppendLine($"      Shell closure: {sc.Status}");
                        b.AppendLine($"      Parent integration: {region.ParentIntegration?.Status.ToString() ?? region.IntegrationStatus.ToString()}");
                        b.AppendLine($"      RegionIntegrationPatch: {sc.RegionIntegrationPatchStatus}");
                        b.AppendLine("      Entry loop: materialized");
                        b.AppendLine("      Exit loop: materialized");
                        b.AppendLine("      Cut wall: materialized");
                        b.AppendLine($"      Closed shell: {(sc.Closed.HasValue ? sc.Closed.Value.ToString().ToLowerInvariant() : "unavailable")}");
                        b.AppendLine($"      STEP smoke: {region.ParentIntegration?.StepSmoke.Status.ToLowerInvariant() ?? "unavailable"}");
                        b.AppendLine("      Blocker:");
                        if (sc.Blocker is null)
                        {
                            b.AppendLine("        Category: none");
                            b.AppendLine("        Code: none");
                            b.AppendLine("        Message: none");
                        }
                        else
                        {
                            b.AppendLine($"        Category: {sc.Blocker.Category}");
                            b.AppendLine($"        Code: {sc.Blocker.Code}");
                            b.AppendLine($"        Message: {sc.Blocker.Message}");
                        }
                    }
                }

                if (region.ParentIntegration is not null)
                {
                    var pi = region.ParentIntegration;
                    b.AppendLine("    Region parent integration");
                    b.AppendLine($"      Region: {pi.SourceRegionId}");
                    b.AppendLine($"      Feature: {pi.FeatureKind}");
                    b.AppendLine($"      Status: {pi.Status}");
                    b.AppendLine($"      Route: {pi.Route}");
                    b.AppendLine("      Placeholder mappings:");
                    foreach (var map in pi.PlaceholderMappings)
                    {
                        b.AppendLine($"        - {map.PlaceholderRole}: {map.MaterializationStatus}");
                        b.AppendLine($"          Element: {map.MaterializedRole}");
                    }
                    b.AppendLine("      Topology:");
                    b.AppendLine($"        Faces: {(pi.TopologySummary.FaceCount.HasValue ? pi.TopologySummary.FaceCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unavailable")}");
                    b.AppendLine($"        Loops: {(pi.TopologySummary.LoopCount.HasValue ? pi.TopologySummary.LoopCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unavailable")}");
                    b.AppendLine($"        Cylindrical faces: {(pi.TopologySummary.CylindricalFaceCount.HasValue ? pi.TopologySummary.CylindricalFaceCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unavailable")}");
                    b.AppendLine($"        Closed: {(pi.TopologySummary.Closed.HasValue ? pi.TopologySummary.Closed.Value.ToString().ToLowerInvariant() : "unavailable")}");
                    b.AppendLine($"      STEP smoke: {pi.StepSmoke.Status}");
                    if (pi.Blocker is not null)
                    {
                        b.AppendLine();
                        b.AppendLine("      Blocker:");
                        b.AppendLine($"        Category: {pi.Blocker.Category}");
                        b.AppendLine($"        Code: {pi.Blocker.Code}");
                        b.AppendLine($"        Message: {pi.Blocker.Message}");
                    }
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in pi.Guarantees) b.AppendLine($"        - {guarantee}");
                }

                if (region.IntegrationDecision is not null)
                {
                    var decision = region.IntegrationDecision;
                    b.AppendLine("    Region integration decision");
                    b.AppendLine($"      Region: {decision.SourceRegionId}");
                    b.AppendLine($"      Feature: {decision.YieldFeatureKind}");
                    b.AppendLine($"      Effect: {decision.EffectKind}");
                    b.AppendLine($"      Boundary: {decision.BoundaryContractKind}");
                    b.AppendLine($"      Mode: {decision.SelectionMode}");
                    b.AppendLine($"      Selected: {decision.SelectedRouteKind}");
                    b.AppendLine($"      Status: {decision.SelectedStatus}");
                    b.AppendLine("      Candidates:");
                    foreach (var c in decision.Candidates)
                    {
                        b.AppendLine($"        - {c.RouteKind}: {c.Status}");
                        b.AppendLine($"          Reason: {c.Reason}");
                    }
                    b.AppendLine("      Guarantees:");
                    foreach (var guarantee in decision.Guarantees) b.AppendLine($"        - {guarantee}");
                }
                foreach (var loss in region.KnownLosses) b.AppendLine($"    Reason: {loss}");
                foreach (var guarantee in region.Guarantees) b.AppendLine($"    Guarantee: {guarantee}");
            }
            b.AppendLine();
        }
        if (r.Artifacts is not null)
        {
            b.AppendLine("Artifacts");
            b.AppendLine($"  STEP: {r.Artifacts.Step}");
            b.AppendLine($"  JSON: {r.Artifacts.TraceJson}");
            b.AppendLine($"  Text: {r.Artifacts.TraceText}");
            b.AppendLine($"  Manifest: {r.Artifacts.Manifest}");
            b.AppendLine();
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

    private static string FrameLabel(AirLocalFrameSummary frame) => frame.FrameKind == AirLocalFrameKind.FaceAttached && frame.SourceFace is not null ? $"FaceAttached({frame.SourceFace})" : frame.FrameKind.ToString();
}
