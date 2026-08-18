using System.Numerics;
using Aetheris.Firmament.FrictionLab.CIRLab;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class AirChamferFirmamentOptInRouteTests
{
    private const string SupportedFixture = "fixtures/LegacyV1/Corpus/valid/edge-x13-valid-airchamfer-shadow-box-edge.firmament";

    [Fact]
    public void FirmamentAirChamferOptInRoute_DisabledByDefault_UsesLegacyResult()
    {
        var compiled = CompileFixture(SupportedFixture);
        var legacy = AssertLegacyChamferRoute(compiled.Compilation.Value.PrimitiveExecutionResult!);
        var legacyStep = ExportStep(legacy.Body);

        var executed = ExecuteWith(FirmamentAirChamferExperimentalOptions.Disabled, out var diagnostics);
        var chamfer = AssertLegacyChamferRoute(executed);

        Assert.Equal(legacyStep, ExportStep(chamfer.Body));
        Assert.DoesNotContain("edge-v4-air-chamfer-candidate-selected", diagnostics);
        Assert.Contains("edge-v4-air-chamfer-opt-in-disabled", diagnostics);
        Assert.Contains("edge-v4-legacy-default-route-used", diagnostics);
        Assert.Contains("edge-v4-production-default-unchanged", diagnostics);
        Assert.Contains("edge-v4-no-3d-boolean-used", diagnostics);
    }

    [Fact]
    public void FirmamentAirChamferOptInRoute_SupportedControlledCase_SelectsAirChamferCandidate()
    {
        var executed = ExecuteWith(SupportedOptions(), out var diagnostics);
        var chamfer = Assert.Single(executed.ExecutedBooleans);

        Assert.Equal(FirmamentLoweredBooleanKind.Chamfer, chamfer.Kind);
        Assert.Equal("edge_x13_legacy_edge_break", chamfer.FeatureId);
        Assert.Contains("edge-v4-air-chamfer-opt-in-enabled", diagnostics);
        Assert.Contains("edge-v4-supported-case-accepted", diagnostics);
        Assert.Contains("edge-v4-air-chamfer-shadow-route-invoked", diagnostics);
        Assert.Contains("edge-v4-air-chamfer-candidate-selected", diagnostics);
        Assert.Contains("edge-v4-no-3d-boolean-used", diagnostics);
        Assert.Contains("edge-v3-shadow-candidate-step-smoke-succeeded", diagnostics);
        Assert.Contains("edge-v3-shadow-feature-recognition-parity-succeeded", diagnostics);
        Assert.DoesNotContain("edge-v4-legacy-fallback-used", diagnostics);

        Assert.NotEmpty(chamfer.Body.Topology.Bodies);
        Assert.NotEmpty(chamfer.Body.Topology.Faces);
        var export = Step242Exporter.ExportBody(chamfer.Body);
        Assert.True(export.IsSuccess);
        Assert.Contains("MANIFOLD_SOLID_BREP", export.Value);
    }

    [Theory]
    [InlineData("non-planar-face-family", "cylindrical", false, false, false, "unsupported-face-family")]
    [InlineData("edge-chain", "planar", true, false, false, "edge-chain")]
    [InlineData("corner-chain", "planar", false, true, false, "corner-chain")]
    [InlineData("legacy-dependent-triangle", "planar", false, false, true, "legacy-dependent-topology")]
    public void FirmamentAirChamferOptInRoute_UnsupportedCases_FallbackToLegacy(
        string caseName,
        string faceFamily,
        bool edgeChain,
        bool cornerChain,
        bool legacyDependency,
        string expectedReason)
    {
        var compiled = CompileFixture(SupportedFixture);
        var legacyStep = ExportStep(AssertLegacyChamferRoute(compiled.Compilation.Value.PrimitiveExecutionResult!).Body);

        var executed = ExecuteWith(SupportedOptions(caseName, faceFamily == "planar" ? FirmamentAirChamferExperimentalFaceFamily.Planar : FirmamentAirChamferExperimentalFaceFamily.Cylindrical, edgeChain, cornerChain, legacyDependency), out var diagnostics);
        var chamfer = AssertLegacyChamferRoute(executed);

        Assert.Equal(legacyStep, ExportStep(chamfer.Body));
        Assert.DoesNotContain("edge-v4-air-chamfer-candidate-selected", diagnostics);
        Assert.Contains($"edge-v4-supported-case-rejected:{expectedReason}", diagnostics);
        Assert.Contains("edge-v4-legacy-fallback-used", diagnostics);
        Assert.Contains("edge-v4-no-3d-boolean-used", diagnostics);
    }

    [Fact]
    public void FirmamentAirChamferOptInRoute_CandidateFailure_FallbackToLegacy()
    {
        var compiled = CompileFixture(SupportedFixture);
        var legacyStep = ExportStep(AssertLegacyChamferRoute(compiled.Compilation.Value.PrimitiveExecutionResult!).Body);
        var options = SupportedOptions() with
        {
            CandidateProvider = _ => new FirmamentAirChamferExperimentalCandidateReport(
                CandidateProduced: false,
                FirmamentAirChamferExperimentalCandidateStatus.Failed,
                "injected-failure",
                CandidateBody: null,
                TopologyContractSatisfied: false,
                StepSmokeSucceeded: false,
                RecognitionContractSatisfied: false,
                NoThreeDimensionalBooleanUsed: true,
                Diagnostics: ["edge-v4-test-injected-candidate-failure"])
        };

        var executed = ExecuteWith(options, out var diagnostics);
        var chamfer = AssertLegacyChamferRoute(executed);

        Assert.Equal(legacyStep, ExportStep(chamfer.Body));
        Assert.Contains("edge-v4-test-injected-candidate-failure", diagnostics);
        Assert.Contains("edge-v4-air-chamfer-candidate-rejected:failed", diagnostics);
        Assert.Contains("edge-v4-air-chamfer-candidate-failed-fallback", diagnostics);
        Assert.Contains("edge-v4-legacy-fallback-used", diagnostics);
    }

    private static FirmamentPrimitiveExecutionResult ExecuteWith(FirmamentAirChamferExperimentalOptions options, out IReadOnlyList<string> diagnostics)
    {
        IReadOnlyList<string> capturedDiagnostics = [];
        options = options with { DiagnosticSink = d => capturedDiagnostics = d };
        var compiled = CompileFixture(SupportedFixture);
        Assert.True(compiled.Compilation.IsSuccess);
        var result = FirmamentPrimitiveExecutor.Execute(compiled.Compilation.Value.PrimitiveLoweringPlan!, options);
        Assert.True(result.IsSuccess);
        diagnostics = capturedDiagnostics;
        Assert.NotEmpty(diagnostics);
        return result.Value;
    }

    private static FirmamentAirChamferExperimentalOptions SupportedOptions(
        string caseName = "edge-v4-firmament-controlled-box-convex-planar-single-edge",
        FirmamentAirChamferExperimentalFaceFamily faceFamily = FirmamentAirChamferExperimentalFaceFamily.Planar,
        bool edgeChain = false,
        bool cornerChain = false,
        bool legacyDependency = false) =>
        new(
            EnableAirChamferExperimentalRoute: true,
            CaseName: caseName,
            FaceFamily: faceFamily,
            IsEdgeChain: edgeChain,
            IsCornerChain: cornerChain,
            LegacyDependency: legacyDependency,
            IncludeStepSmoke: true,
            CandidateProvider: InvokeShadowRoute);

    private static FirmamentAirChamferExperimentalCandidateReport InvokeShadowRoute(FirmamentAirChamferExperimentalCandidateRequest request)
    {
        var route = AirChamferShadowRoute.Evaluate(new AirChamferShadowRouteRequest(
            request.CaseName,
            request.SourceBody,
            new Vector3(5f, 4f, -3f),
            new Vector3(5f, 4f, 3f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            request.ChamferDistance,
            request.FaceFamily == FirmamentAirChamferExperimentalFaceFamily.Planar ? AirChamferFaceFamily.Planar : AirChamferFaceFamily.Cylindrical,
            request.IsEdgeChain,
            request.IsCornerChain,
            request.LegacyDependency,
            AirChamferClassificationExpectation.Convex,
            IsOrthogonalFacePair: true,
            ReferenceEnvelope: 10d,
            IncludeStepSmoke: request.IncludeStepSmoke));

        return new FirmamentAirChamferExperimentalCandidateReport(
            route.ShadowCandidateProduced,
            route.ShadowCandidateStatus switch
            {
                AirChamferShadowCandidateStatus.Succeeded => FirmamentAirChamferExperimentalCandidateStatus.Succeeded,
                AirChamferShadowCandidateStatus.Rejected => FirmamentAirChamferExperimentalCandidateStatus.Rejected,
                AirChamferShadowCandidateStatus.Deferred => FirmamentAirChamferExperimentalCandidateStatus.Deferred,
                AirChamferShadowCandidateStatus.FallbackLegacy => FirmamentAirChamferExperimentalCandidateStatus.FallbackLegacy,
                _ => FirmamentAirChamferExperimentalCandidateStatus.Failed
            },
            route.AirChamferDecision,
            route.ShadowCandidateBody,
            TopologyContractSatisfied: route.TopologySummary is not null,
            StepSmokeSucceeded: route.StepSmokeSummary?.Succeeded == true,
            RecognitionContractSatisfied: route.FeatureRecognitionSummary?.RecognitionContractSatisfied == true,
            NoThreeDimensionalBooleanUsed: route.Diagnostics.Contains("edge-v3-no-3d-boolean-used"),
            route.Diagnostics);
    }

    private static FirmamentExecutedBoolean AssertLegacyChamferRoute(FirmamentPrimitiveExecutionResult execution)
    {
        var executedBoolean = Assert.Single(execution.ExecutedBooleans);
        Assert.Equal(FirmamentLoweredBooleanKind.Chamfer, executedBoolean.Kind);
        Assert.Equal("edge_x13_legacy_edge_break", executedBoolean.FeatureId);
        Assert.Equal(7, executedBoolean.Body.Topology.Faces.Count());
        Assert.NotEmpty(executedBoolean.Body.Topology.Bodies);
        return executedBoolean;
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
