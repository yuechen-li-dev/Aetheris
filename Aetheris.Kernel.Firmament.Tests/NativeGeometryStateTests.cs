using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class NativeGeometryStateTests
{
    [Fact]
    public void NativeGeometryState_BoxBasic_IsBRepActive()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/box_basic.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var state = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState;
        Assert.Equal(NativeGeometryExecutionMode.BRepActive, state.ExecutionMode);
        Assert.Equal(NativeGeometryMaterializationAuthority.BRepAuthoritative, state.MaterializationAuthority);
        Assert.NotNull(state.MaterializedBody);
        Assert.Empty(state.TransitionEvents);
    }

    [Fact]
    public void ReplayLog_BoxMinusCylinder_RecordsOps()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/boolean_box_cylinder_hole.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var log = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState.ReplayLog;

        Assert.True(log.Operations.Count >= 2);
        Assert.Contains(log.Operations, op => op.OperationKind.StartsWith("primitive:", StringComparison.Ordinal));
        Assert.Contains(log.Operations, op => op.OperationKind.StartsWith("boolean:", StringComparison.Ordinal));
        Assert.Equal(log.Operations.OrderBy(op => op.OpIndex).Select(op => op.OpIndex), log.Operations.Select(op => op.OpIndex));
        Assert.All(log.Operations, op => Assert.False(string.IsNullOrWhiteSpace(op.FeatureId)));
    }

    [Fact]
    public void TransitionDiagnostic_Categories_AreDistinct()
    {
        var unsupported = new NativeGeometryTransitionEvent(NativeGeometryExecutionMode.BRepActive, NativeGeometryExecutionMode.CirOnly, "f1", 3, NativeGeometryTransitionReasonCategory.MaterializationUnsupported, "unsupported materializer");
        var invalid = new NativeGeometryTransitionEvent(NativeGeometryExecutionMode.BRepActive, NativeGeometryExecutionMode.Failed, "f2", 4, NativeGeometryTransitionReasonCategory.InvalidIntent, "invalid selector");
        var uncertain = new NativeGeometryTransitionEvent(NativeGeometryExecutionMode.BRepActive, NativeGeometryExecutionMode.BRepActive, "f3", 5, NativeGeometryTransitionReasonCategory.AnalyzerUncertainty, "approximate result");

        Assert.NotEqual(unsupported.ReasonCategory, invalid.ReasonCategory);
        Assert.NotEqual(invalid.ReasonCategory, uncertain.ReasonCategory);
        Assert.NotEqual(unsupported.ReasonCategory, uncertain.ReasonCategory);
    }

    [Fact]
    public void ProductionPath_Unchanged()
    {
        var export = FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/box_basic.firmament"))));

        Assert.True(export.IsSuccess);
        Assert.Equal("base", export.Value.ExportedFeatureId);
        Assert.NotEmpty(export.Value.StepText);
    }

    [Fact]
    public void UnsupportedFutureState_CanBeRepresented()
    {
        var cirOnly = new NativeGeometryState(
            NativeGeometryExecutionMode.CirOnly,
            NativeGeometryMaterializationAuthority.CirIntentOnly,
            null,
            "cir-root:test",
            new NativeGeometryReplayLog([]),
            [new NativeGeometryTransitionEvent(
                NativeGeometryExecutionMode.BRepActive,
                NativeGeometryExecutionMode.CirOnly,
                "cut1",
                7,
                NativeGeometryTransitionReasonCategory.MaterializationUnsupported,
                "future fallback path")],
            new NativeGeometryCirMirrorState(CirMirrorStatus.NotAttempted, null, []));

        var failed = cirOnly with { ExecutionMode = NativeGeometryExecutionMode.Failed, MaterializationAuthority = NativeGeometryMaterializationAuthority.PendingRematerialization };

        Assert.Equal(NativeGeometryExecutionMode.CirOnly, cirOnly.ExecutionMode);
        Assert.Equal(NativeGeometryExecutionMode.Failed, failed.ExecutionMode);
        Assert.Null(cirOnly.MaterializedBody);
    }


    [Fact]
    public void NativeGeometryState_CirMirror_BoxBasic_Available()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/box_basic.firmament"));
        var state = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState;

        Assert.Equal(NativeGeometryExecutionMode.BRepActive, state.ExecutionMode);
        Assert.Equal(NativeGeometryMaterializationAuthority.BRepAuthoritative, state.MaterializationAuthority);
        Assert.Equal(CirMirrorStatus.Available, state.CirMirror.Status);
        Assert.NotNull(state.CirMirror.Summary);
        Assert.True(state.CirMirror.Summary!.EstimatedVolume > 0d);
    }

    [Fact]
    public void NativeGeometryState_CirMirror_BoxMinusCylinder_Available()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/boolean_box_cylinder_hole.firmament"));
        var state = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState;

        Assert.True(result.Compilation.IsSuccess);
        Assert.Equal(CirMirrorStatus.Available, state.CirMirror.Status);
        Assert.NotNull(state.CirMirror.Summary);
    }

    [Fact]
    public void NativeGeometryState_CirMirror_Unsupported_DoesNotFailProduction()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/rounded_corner_box_basic.firmament"));
        var state = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState;

        Assert.True(result.Compilation.IsSuccess);
        Assert.Equal(CirMirrorStatus.Unsupported, state.CirMirror.Status);
        Assert.NotEmpty(state.CirMirror.Diagnostics);
    }


    [Fact]
    public void StateBackedDifferential_UsesNativeGeometryState()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/box_basic.firmament"));
        var state = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState;

        var differential = NativeGeometryStateDifferentialHelper.CompareBoundsAndVolumeFromState(state);

        Assert.True(differential.Success);
        Assert.True(differential.CirEstimatedVolume > 0d);
    }

}