using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed partial class NativeGeometryStateTests
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

    [Fact]
    public void ReplayPlacement_None_IsResolvedZero()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/box_basic.firmament"));
        var op = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState.ReplayLog.Operations.Single(o => o.FeatureId == "base");

        Assert.Equal(NativeGeometryPlacementKind.None, op.ResolvedPlacement.Kind);
        Assert.Equal(Vector3D.Zero, op.ResolvedPlacement.Translation);
        Assert.True(op.ResolvedPlacement.IsResolved);
    }

    [Fact]
    public void ReplayPlacement_Offset_CapturesTranslation()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/boolean_add_basic.firmament"));
        var op = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState.ReplayLog.Operations.Single(o => o.FeatureId == "shifted");

        Assert.Equal(NativeGeometryPlacementKind.Offset, op.ResolvedPlacement.Kind);
        Assert.Equal(new Vector3D(3d, 0d, 0d), op.ResolvedPlacement.Offset);
        Assert.Equal(new Vector3D(3d, 0d, 0d), op.ResolvedPlacement.Translation);
        Assert.True(op.ResolvedPlacement.IsResolved);
    }

    [Fact]
    public void ReplayPlacement_OnFace_CapturesAnchorAndTranslation()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/p1_blind_hole_on_face_semantic.firmament"));
        var op = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState.ReplayLog.Operations.Single(o => o.FeatureId == "blind_hole_tool");

        Assert.Equal(NativeGeometryPlacementKind.OnFace, op.ResolvedPlacement.Kind);
        Assert.Equal("base", op.ResolvedPlacement.AnchorFeatureId);
        Assert.Equal("top_face", op.ResolvedPlacement.AnchorPort);
        Assert.True(op.ResolvedPlacement.Translation.Z > 0d);
        Assert.True(op.ResolvedPlacement.IsResolved);
    }

    [Fact]
    public void ReplayPlacement_Unsupported_DoesNotFailProduction()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/p1_flange_radial_hole_semantic.firmament"));
        Assert.True(result.Compilation.IsSuccess);

        var op = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState.ReplayLog.Operations.Single(o => o.FeatureId == "radial_hole_tool");
        Assert.Equal(NativeGeometryPlacementKind.AroundAxis, op.ResolvedPlacement.Kind);
        Assert.False(op.ResolvedPlacement.IsResolved);
        Assert.False(string.IsNullOrWhiteSpace(op.ResolvedPlacement.Diagnostic));
    }

    [Fact]
    public void FallForward_EligibleUnsupportedBRep_TransitionsToCirOnly()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10l-unsupported-box-subtract-sphere-touching-boundary.firmament"));
        Assert.True(result.Compilation.IsSuccess);

        var state = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState;
        Assert.Equal(NativeGeometryExecutionMode.CirOnly, state.ExecutionMode);
        Assert.Equal(NativeGeometryMaterializationAuthority.CirIntentOnly, state.MaterializationAuthority);
        Assert.Null(state.MaterializedBody);
        Assert.Contains(state.TransitionEvents, e =>
            e.FromMode == NativeGeometryExecutionMode.BRepActive
            && e.ToMode == NativeGeometryExecutionMode.CirOnly
            && e.ReasonCategory == NativeGeometryTransitionReasonCategory.MaterializationUnsupported);
        Assert.NotNull(state.CirIntentRootReference);
    }

    [Fact]
    public void FallForward_CirOnly_CanAnalyze()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10l-unsupported-box-subtract-sphere-touching-boundary.firmament"));
        var state = result.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState;
        Assert.Equal(NativeGeometryExecutionMode.CirOnly, state.ExecutionMode);
        Assert.Equal(CirMirrorStatus.Available, state.CirMirror.Status);
        Assert.NotNull(state.CirMirror.Summary);
        Assert.True(state.CirMirror.Summary!.EstimatedVolume > 0d);
    }

    [Fact]
    public void FallForward_InvalidIntent_DoesNotTransition()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/p1-invalid-unsupported-placement-semantic.firmament"));
        Assert.False(result.Compilation.IsSuccess);
    }

    [Fact]
    public void FallForward_UnsupportedCIRLowering_DoesNotTransition()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10n-unsupported-box-subtract-torus.firmament"));
        Assert.False(result.Compilation.IsSuccess);
        Assert.DoesNotContain(result.Compilation.Diagnostics, d => d.Message.Contains("CIR fallback lowering failed", StringComparison.Ordinal));
    }
}
