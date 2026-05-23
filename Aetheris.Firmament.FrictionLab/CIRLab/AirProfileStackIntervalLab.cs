using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirIntervalLabRow(
    string ScenarioName,
    string RepresentationName,
    string Status,
    bool BrepProduced,
    bool StepSmokeAttempted,
    bool StepSmokeSucceeded,
    string FailureStage,
    string FailureCode,
    IReadOnlyList<string> Diagnostics,
    string TopologySummary,
    IReadOnlyList<string> StepMarkers,
    string RecommendationNotes);

public sealed record AirIntervalLabResult(
    IReadOnlyList<AirIntervalLabRow> Rows,
    string BlindRecommendation,
    string CounterboreRecommendation);

public static class AirProfileStackIntervalLab
{
    public static readonly HashSet<string> AllowedRecommendations =
    [
        "use contiguous layers",
        "use direct SafeBooleanComposition descriptor",
        "extend profile-stack layer model with explicit no-hole regions",
        "keep legacy for now",
        "split AIR atoms",
        "another evidence-backed route"
    ];

    public static AirIntervalLabResult Run()
    {
        var rows = new List<AirIntervalLabRow>
        {
            RunProfileStack(
                "Blind-hole",
                "B1-NullInnerLoopSolidLayer",
                [
                    new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(-10, 2, null, "blind-solid", Array.Empty<string>()),
                    new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(2, 10, 2, "blind-cut", Array.Empty<string>())
                ]),

            RunProfileStack(
                "Blind-hole",
                "B2-ZeroRadiusInnerLoop",
                [
                    new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(-10, 2, 0, "blind-zero", Array.Empty<string>()),
                    new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(2, 10, 2, "blind-cut", Array.Empty<string>())
                ]),

            Skip(
                "Blind-hole",
                "B3-ExplicitBlindPocketDescriptor",
                "lowering",
                "lab-not-implemented",
                "Descriptor bypass path intentionally deferred in EVT X2 scope."),

            Skip(
                "Blind-hole",
                "B4-SplitCapTransitionModel",
                "lowering",
                "lab-not-implemented",
                "Cap/floor transition metadata has no executable emitter in current lab APIs."),

            RunLegacyBlindBaseline("Blind-hole", "B5-LegacyBooleanBaseline"),

            RunProfileStack(
                "Counterbore",
                "C1-ContiguousLayerRadii",
                [
                    new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(-10, 6, 2, "small-through", Array.Empty<string>()),
                    new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(6, 10, 4, "large-entry", Array.Empty<string>())
                ]),

            Skip(
                "Counterbore",
                "C2-OverlappingToolIntervals",
                "lowering",
                "lab-not-implemented",
                "Overlapping tool interval lowering requires dedicated AIR/tool interval adapter."),

            RunProfileStack(
                "Counterbore",
                "C3-NormalizedSteppedStack",
                [
                    new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(-10, 6, 2, "small-through", Array.Empty<string>()),
                    new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(6, 10, 4, "large-entry", Array.Empty<string>())
                ]),

            Skip(
                "Counterbore",
                "C4-DirectSafeCompositionDescriptor",
                "lowering",
                "lab-not-implemented",
                "Direct descriptor API not exposed at AIR lab surface yet."),

            RunLegacyCounterboreBaseline("Counterbore", "C5-LegacyBooleanBaseline")
        };

        return new AirIntervalLabResult(
            rows,
            "extend profile-stack layer model with explicit no-hole regions",
            "use contiguous layers");
    }

    private static AirIntervalLabRow RunProfileStack(
        string scenario,
        string representation,
        IReadOnlyList<Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer> layers)
    {
        var spec = new Aetheris.Kernel.Firmament.Materializer.ProfileStackExtrudeSpec(30, 30, -10, 10, layers, Array.Empty<string>());
        var execution = ProfileStackExtrudeExecutor.Execute(spec);

        if (execution.Status != ProfileStackExtrudeExecutionStatus.Succeeded || execution.Body is null)
        {
            return new AirIntervalLabRow(
                scenario,
                representation,
                "failed",
                false,
                false,
                false,
                "emitter",
                execution.Status.ToString(),
                execution.Diagnostics,
                "no-body",
                Array.Empty<string>(),
                "Emitter blocked.");
        }

        return BuildStepRow(scenario, representation, execution.Body, execution.Diagnostics, "ProfileStack succeeded.");
    }

    private static AirIntervalLabRow RunLegacyBlindBaseline(string scenario, string representation)
    {
        var plan = new HoleRecoveryPlan(
            HoleHostKind.RectangularBox,
            HoleAxisKind.Z,
            HoleKind.Blind,
            HoleDepthKind.Blind,
            HoleEntryFeatureKind.Plain,
            HoleExitFeatureKind.ClosedBottom,
            8,
            30,
            30,
            20,
            new Vector3D(0, 0, 0),
            new Vector3D(0, 0, 0),
            [new HoleProfileSegment(HoleProfileSegmentKind.Cylindrical, 2, 2, 0, 8, HoleTierAnchorSide.Top, 8, 2, 10, false, ["lab"])],
            Array.Empty<HoleSurfacePatchExpectation>(),
            Array.Empty<HoleTrimCurveExpectation>(),
            FrepMaterializerCapability.ExactBRep,
            ["air-x2-lab"]);

        var execution = HoleRecoveryExecutor.Execute(plan);
        if (execution.Status != HoleRecoveryExecutionStatus.Succeeded || execution.Body is null)
        {
            return new AirIntervalLabRow(
                scenario,
                representation,
                "failed",
                false,
                false,
                false,
                "legacy-executor",
                execution.Status.ToString(),
                execution.Diagnostics,
                "no-body",
                Array.Empty<string>(),
                "Legacy blind failed.");
        }

        return BuildStepRow(scenario, representation, execution.Body, execution.Diagnostics, "Legacy baseline succeeded.");
    }

    private static AirIntervalLabRow RunLegacyCounterboreBaseline(string scenario, string representation)
    {
        var plan = new HoleRecoveryPlan(
            HoleHostKind.RectangularBox,
            HoleAxisKind.Z,
            HoleKind.Counterbore,
            HoleDepthKind.ThroughWithEntryRelief,
            HoleEntryFeatureKind.Counterbore,
            HoleExitFeatureKind.Plain,
            20,
            30,
            30,
            20,
            new Vector3D(0, 0, 0),
            new Vector3D(0, 0, 0),
            [
                new HoleProfileSegment(HoleProfileSegmentKind.Cylindrical, 4, 4, 0, 4, HoleTierAnchorSide.Top, 4, 6, 10, false, ["lab"]),
                new HoleProfileSegment(HoleProfileSegmentKind.Cylindrical, 2, 2, 0, 20, HoleTierAnchorSide.Through, 20, -10, 10, true, ["lab"])
            ],
            Array.Empty<HoleSurfacePatchExpectation>(),
            Array.Empty<HoleTrimCurveExpectation>(),
            FrepMaterializerCapability.ExactBRep,
            ["air-x2-lab"]);

        var execution = HoleRecoveryExecutor.Execute(plan);
        if (execution.Status != HoleRecoveryExecutionStatus.Succeeded || execution.Body is null)
        {
            return new AirIntervalLabRow(
                scenario,
                representation,
                "failed",
                false,
                false,
                false,
                "legacy-executor",
                execution.Status.ToString(),
                execution.Diagnostics,
                "no-body",
                Array.Empty<string>(),
                "Legacy counterbore failed.");
        }

        return BuildStepRow(scenario, representation, execution.Body, execution.Diagnostics, "Legacy baseline succeeded.");
    }

    private static AirIntervalLabRow BuildStepRow(
        string scenario,
        string representation,
        BrepBody body,
        IReadOnlyList<string> diagnostics,
        string note)
    {
        var step = Step242Exporter.ExportBody(body);
        if (!step.IsSuccess)
        {
            return new AirIntervalLabRow(
                scenario,
                representation,
                "failed",
                true,
                true,
                false,
                "step-export",
                "step-failed",
                diagnostics,
                "brep-only",
                Array.Empty<string>(),
                note);
        }

        var text = step.Value;
        var markers = new[] { "ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "CYLINDRICAL_SURFACE" }
            .Where(marker => text.Contains(marker, StringComparison.Ordinal))
            .ToArray();

        var success = markers.Length == 4
            && !text.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal);

        return new AirIntervalLabRow(
            scenario,
            representation,
            "succeeded",
            true,
            true,
            success,
            "none",
            "none",
            diagnostics,
            "brep-produced",
            markers,
            note);
    }

    private static AirIntervalLabRow Skip(
        string scenario,
        string representation,
        string stage,
        string code,
        string why)
    {
        return new AirIntervalLabRow(
            scenario,
            representation,
            "skipped",
            false,
            false,
            false,
            stage,
            code,
            [why],
            "not-attempted",
            Array.Empty<string>(),
            why);
    }
}
