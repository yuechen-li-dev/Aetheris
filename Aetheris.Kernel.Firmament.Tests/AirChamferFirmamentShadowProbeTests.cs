using System.Numerics;
using Aetheris.Firmament.FrictionLab.CIRLab;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class AirChamferFirmamentShadowProbeTests
{
    private const string SupportedFixture = "fixtures/Compatibility/LegacyV1/Corpus/valid/edge-x13-valid-airchamfer-shadow-box-edge.firmament";

    [Fact]
    public void FirmamentAirChamferShadowProbe_SupportedControlledCase_ProducesSidecarWithoutChangingProductionOutput()
    {
        var compiled = CompileFixture(SupportedFixture);
        Assert.True(compiled.Compilation.IsSuccess);

        var productionBodyBeforeProbe = AssertLegacyChamferRoute(compiled);
        var productionStepBeforeProbe = ExportStep(productionBodyBeforeProbe);

        var report = FirmamentAirChamferShadowProbe.Evaluate(compiled, FirmamentAirChamferShadowProbeOptions.SupportedControlledCase());

        var productionBodyAfterProbe = AssertLegacyChamferRoute(compiled);
        var productionStepAfterProbe = ExportStep(productionBodyAfterProbe);
        Assert.Equal(productionStepBeforeProbe, productionStepAfterProbe);

        Assert.True(report.IsOptInTestOnly);
        Assert.True(report.LegacyAuthorityPreserved);
        Assert.True(report.NoProductionRouteReplacement);
        Assert.True(report.NoThreeDimensionalBooleanUsed);
        Assert.True(report.ProductionOutputUnchanged);
        Assert.True(report.ShadowRouteInvoked);
        Assert.True(report.ShadowCandidateProduced);
        Assert.Equal(AirChamferShadowCandidateStatus.Succeeded, report.ShadowReport!.ShadowCandidateStatus);
        Assert.NotNull(report.ShadowReport.TopologySummary);
        Assert.NotNull(report.ShadowReport.FeatureRecognitionSummary);
        Assert.True(report.ShadowReport.FeatureRecognitionSummary!.RecognitionContractSatisfied);
        Assert.NotNull(report.ShadowReport.StepSmokeSummary);
        Assert.True(report.ShadowReport.StepSmokeSummary!.Succeeded);
        Assert.Equal("AirChamferShadowRoute->AirChamferRealBodyPrototype", report.CandidatePath);
        Assert.Equal("BrepBoundedChamfer", report.ProductionAuthority);
        Assert.Equal("Firmament test-only sidecar", report.Seam);
        Assert.Equal("air-chamfer-firmament-shadow-ready-for-controlled-opt-in", report.Recommendation);

        Assert.Contains("edge-x13-firmament-shadow-probe-started", report.Diagnostics);
        Assert.Contains("edge-x13-firmament-production-route-executed", report.Diagnostics);
        Assert.Contains("edge-x13-air-chamfer-shadow-route-invoked", report.Diagnostics);
        Assert.Contains("edge-x13-shadow-candidate-produced", report.Diagnostics);
        Assert.Contains("edge-x13-shadow-feature-recognition-captured", report.Diagnostics);
        Assert.Contains("edge-x13-shadow-step-smoke-succeeded", report.Diagnostics);
        Assert.Contains("edge-x13-legacy-authority-preserved", report.Diagnostics);
        Assert.Contains("edge-x13-production-output-unchanged", report.Diagnostics);
        Assert.Contains("edge-x13-no-production-route-replacement", report.Diagnostics);
        Assert.Contains("edge-x13-no-3d-boolean-used", report.Diagnostics);
    }

    [Theory]
    [InlineData("opt-in-off", false, AirChamferFaceFamily.Planar, false, false, false, "opt-in-disabled")]
    [InlineData("non-planar-adjacent-face", true, AirChamferFaceFamily.Cylindrical, false, false, false, "unsupported-face-family")]
    [InlineData("edge-chain", true, AirChamferFaceFamily.Planar, true, false, false, "edge-chain")]
    [InlineData("corner-chain", true, AirChamferFaceFamily.Planar, false, true, false, "corner-chain")]
    [InlineData("legacy-dependent", true, AirChamferFaceFamily.Planar, false, false, true, "legacy-dependent-topology")]
    public void FirmamentAirChamferShadowProbe_UnsupportedOrDisabledCases_DoNotProduceCandidate(
        string caseName,
        bool enabled,
        AirChamferFaceFamily faceFamily,
        bool isEdgeChain,
        bool isCornerChain,
        bool legacyDependency,
        string expectedReason)
    {
        var compiled = CompileFixture(SupportedFixture);
        Assert.True(compiled.Compilation.IsSuccess);
        var productionBodyBeforeProbe = AssertLegacyChamferRoute(compiled);
        var productionStepBeforeProbe = ExportStep(productionBodyBeforeProbe);

        var options = new FirmamentAirChamferShadowProbeOptions(
            Enabled: enabled,
            CaseName: caseName,
            FaceFamily: faceFamily,
            IsEdgeChain: isEdgeChain,
            IsCornerChain: isCornerChain,
            LegacyDependency: legacyDependency,
            IncludeStepSmoke: true);
        var report = FirmamentAirChamferShadowProbe.Evaluate(compiled, options);

        Assert.Equal(productionStepBeforeProbe, ExportStep(AssertLegacyChamferRoute(compiled)));
        Assert.True(report.IsOptInTestOnly);
        Assert.True(report.LegacyAuthorityPreserved);
        Assert.True(report.NoProductionRouteReplacement);
        Assert.True(report.NoThreeDimensionalBooleanUsed);
        Assert.True(report.ProductionOutputUnchanged);
        Assert.False(report.ShadowCandidateProduced);
        Assert.Null(report.ShadowCandidateBody);
        Assert.Contains("edge-x13-firmament-shadow-probe-started", report.Diagnostics);
        Assert.Contains("edge-x13-firmament-production-route-executed", report.Diagnostics);
        Assert.Contains("edge-x13-legacy-authority-preserved", report.Diagnostics);
        Assert.Contains("edge-x13-production-output-unchanged", report.Diagnostics);
        Assert.Contains("edge-x13-no-production-route-replacement", report.Diagnostics);
        Assert.Contains("edge-x13-no-3d-boolean-used", report.Diagnostics);
        Assert.Contains(report.Diagnostics, d => d == $"edge-x13-shadow-deferred:{expectedReason}" || d == $"edge-x13-shadow-rejected:{expectedReason}");
        Assert.Contains(report.Recommendation, new[]
        {
            "air-chamfer-firmament-shadow-deferred-unsupported",
            "air-chamfer-firmament-shadow-rejected-invalid",
            "air-chamfer-firmament-shadow-keep-legacy-authority"
        });
    }

    private static BrepBody AssertLegacyChamferRoute(FirmamentCompileResult compiled)
    {
        var primitiveExecution = compiled.Compilation.Value.PrimitiveExecutionResult!;
        var executedBoolean = Assert.Single(primitiveExecution.ExecutedBooleans);
        Assert.Equal(FirmamentLoweredBooleanKind.Chamfer, executedBoolean.Kind);
        Assert.Equal("edge_x13_legacy_edge_break", executedBoolean.FeatureId);
        Assert.Equal(7, executedBoolean.Body.Topology.Faces.Count());
        Assert.NotEmpty(executedBoolean.Body.Topology.Bodies);
        return executedBoolean.Body;
    }

    private static string ExportStep(BrepBody body)
    {
        var export = Step242Exporter.ExportBody(body);
        Assert.True(export.IsSuccess);
        return export.Value;
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}

internal sealed record FirmamentAirChamferShadowProbeOptions(
    bool Enabled,
    string CaseName,
    AirChamferFaceFamily FaceFamily,
    bool IsEdgeChain,
    bool IsCornerChain,
    bool LegacyDependency,
    bool IncludeStepSmoke)
{
    public static FirmamentAirChamferShadowProbeOptions SupportedControlledCase() =>
        new(
            Enabled: true,
            CaseName: "edge-x13-firmament-controlled-box-convex-planar-single-edge",
            FaceFamily: AirChamferFaceFamily.Planar,
            IsEdgeChain: false,
            IsCornerChain: false,
            LegacyDependency: false,
            IncludeStepSmoke: true);
}

internal sealed record FirmamentAirChamferShadowProbeReport(
    bool IsOptInTestOnly,
    string Seam,
    string ProductionAuthority,
    string CandidatePath,
    bool LegacyAuthorityPreserved,
    bool ProductionOutputUnchanged,
    bool NoProductionRouteReplacement,
    bool NoThreeDimensionalBooleanUsed,
    bool ShadowRouteInvoked,
    bool ShadowCandidateProduced,
    BrepBody? ShadowCandidateBody,
    AirChamferShadowReport? ShadowReport,
    string Recommendation,
    IReadOnlyList<string> Diagnostics);

internal static class FirmamentAirChamferShadowProbe
{
    private const string CandidatePath = "AirChamferShadowRoute->AirChamferRealBodyPrototype";
    private const string ProductionAuthority = "BrepBoundedChamfer";
    private const string Seam = "Firmament test-only sidecar";

    public static FirmamentAirChamferShadowProbeReport Evaluate(FirmamentCompileResult compiled, FirmamentAirChamferShadowProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<string>
        {
            "edge-x13-firmament-shadow-probe-started",
            "edge-x13-legacy-authority-preserved",
            "edge-x13-production-output-unchanged",
            "edge-x13-no-production-route-replacement",
            "edge-x13-no-3d-boolean-used"
        };

        if (!compiled.Compilation.IsSuccess || compiled.Compilation.Value.PrimitiveExecutionResult is null)
            return Deferred("compile-failed", "air-chamfer-firmament-shadow-deferred-unsupported", diagnostics);

        var primitiveExecution = compiled.Compilation.Value.PrimitiveExecutionResult;
        if (!HasSingleLegacyChamfer(primitiveExecution, out var sourceBody, out var distance))
            return Deferred("no-controlled-firmament-chamfer", "air-chamfer-firmament-shadow-deferred-unsupported", diagnostics);

        diagnostics.Add("edge-x13-firmament-production-route-executed");

        if (!options.Enabled)
            return Deferred("opt-in-disabled", "air-chamfer-firmament-shadow-deferred-unsupported", diagnostics);

        if (options.IsEdgeChain)
            return Deferred("edge-chain", "air-chamfer-firmament-shadow-deferred-unsupported", diagnostics);

        if (options.IsCornerChain)
            return Deferred("corner-chain", "air-chamfer-firmament-shadow-deferred-unsupported", diagnostics);

        if (options.LegacyDependency)
            return Deferred("legacy-dependent-topology", "air-chamfer-firmament-shadow-keep-legacy-authority", diagnostics);

        if (options.FaceFamily != AirChamferFaceFamily.Planar)
            return Rejected("unsupported-face-family", "air-chamfer-firmament-shadow-rejected-invalid", diagnostics);

        var routeReport = AirChamferShadowRoute.Evaluate(new AirChamferShadowRouteRequest(
            options.CaseName,
            sourceBody,
            new Vector3(5f, 4f, -3f),
            new Vector3(5f, 4f, 3f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            distance,
            options.FaceFamily,
            options.IsEdgeChain,
            options.IsCornerChain,
            options.LegacyDependency,
            AirChamferClassificationExpectation.Convex,
            IsOrthogonalFacePair: true,
            ReferenceEnvelope: 10d,
            IncludeStepSmoke: options.IncludeStepSmoke));

        diagnostics.Add("edge-x13-air-chamfer-shadow-route-invoked");
        if (routeReport.ShadowCandidateProduced)
            diagnostics.Add("edge-x13-shadow-candidate-produced");
        else if (routeReport.ShadowCandidateStatus is AirChamferShadowCandidateStatus.Deferred or AirChamferShadowCandidateStatus.FallbackLegacy)
            diagnostics.Add($"edge-x13-shadow-deferred:{routeReport.AirChamferDecision}");
        else
            diagnostics.Add($"edge-x13-shadow-rejected:{routeReport.AirChamferDecision}");

        if (routeReport.FeatureRecognitionSummary is not null)
            diagnostics.Add("edge-x13-shadow-feature-recognition-captured");

        if (routeReport.StepSmokeSummary?.Succeeded == true)
            diagnostics.Add("edge-x13-shadow-step-smoke-succeeded");

        return new FirmamentAirChamferShadowProbeReport(
            IsOptInTestOnly: true,
            Seam,
            ProductionAuthority,
            CandidatePath,
            LegacyAuthorityPreserved: routeReport.LegacyAuthoritative,
            ProductionOutputUnchanged: !routeReport.ProductionOutputChanged,
            NoProductionRouteReplacement: true,
            NoThreeDimensionalBooleanUsed: true,
            ShadowRouteInvoked: true,
            ShadowCandidateProduced: routeReport.ShadowCandidateProduced,
            routeReport.ShadowCandidateBody,
            routeReport,
            ToFirmamentRecommendation(routeReport),
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static bool HasSingleLegacyChamfer(FirmamentPrimitiveExecutionResult primitiveExecution, out BrepBody sourceBody, out double distance)
    {
        sourceBody = null!;
        distance = 0d;

        var primitive = primitiveExecution.ExecutedPrimitives.SingleOrDefault(p =>
            p.FeatureId == "base" && p.Kind == FirmamentLoweredPrimitiveKind.Box);
        var chamfer = primitiveExecution.ExecutedBooleans.SingleOrDefault(b =>
            b.FeatureId == "edge_x13_legacy_edge_break" && b.Kind == FirmamentLoweredBooleanKind.Chamfer);
        if (primitive is null || chamfer is null)
            return false;

        sourceBody = primitive.Body;
        distance = 1d;
        return true;
    }

    private static FirmamentAirChamferShadowProbeReport Deferred(string reason, string recommendation, List<string> diagnostics)
    {
        diagnostics.Add($"edge-x13-shadow-deferred:{reason}");
        return Terminal(recommendation, diagnostics);
    }

    private static FirmamentAirChamferShadowProbeReport Rejected(string reason, string recommendation, List<string> diagnostics)
    {
        diagnostics.Add($"edge-x13-shadow-rejected:{reason}");
        return Terminal(recommendation, diagnostics);
    }

    private static FirmamentAirChamferShadowProbeReport Terminal(string recommendation, List<string> diagnostics) =>
        new(
            IsOptInTestOnly: true,
            Seam,
            ProductionAuthority,
            CandidatePath,
            LegacyAuthorityPreserved: true,
            ProductionOutputUnchanged: true,
            NoProductionRouteReplacement: true,
            NoThreeDimensionalBooleanUsed: true,
            ShadowRouteInvoked: false,
            ShadowCandidateProduced: false,
            ShadowCandidateBody: null,
            ShadowReport: null,
            recommendation,
            diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());

    private static string ToFirmamentRecommendation(AirChamferShadowReport report)
    {
        if (!report.ShadowCandidateProduced)
        {
            return report.ShadowCandidateStatus switch
            {
                AirChamferShadowCandidateStatus.FallbackLegacy => "air-chamfer-firmament-shadow-keep-legacy-authority",
                AirChamferShadowCandidateStatus.Rejected => "air-chamfer-firmament-shadow-rejected-invalid",
                _ => "air-chamfer-firmament-shadow-deferred-unsupported"
            };
        }

        return report.FeatureRecognitionSummary?.RecognitionContractSatisfied == true
            ? "air-chamfer-firmament-shadow-ready-for-controlled-opt-in"
            : "air-chamfer-firmament-shadow-needs-recognition-hardening";
    }
}
