using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CirBrepMaterializerTests
{
    [Fact]
    public void MaterializerRegistry_Selects_BoxMinusCylinder()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(20, 20, 10), Transform3D.CreateTranslation(new Vector3D(0, 0, 5))),
            new CirTransformNode(new CirCylinderNode(3, 20), Transform3D.CreateTranslation(new Vector3D(0, 0, 5))));
        var replay = BuildReplay("cylinder");

        var result = CirBrepMaterializer.TryMaterialize(new CirBrepMaterializer.CirBrepMaterializerContext(root, replay));

        Assert.True(result.IsSuccess);
        Assert.Equal("subtract_box_cylinder", result.SelectedStrategy);
        Assert.Equal(CirBrepMaterializer.BoxMinusCylinderPattern, result.PatternName);
    }

    [Fact]
    public void MaterializerRegistry_Selects_BoxMinusBox()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(20, 20, 10), Transform3D.CreateTranslation(new Vector3D(0, 0, 5))),
            new CirTransformNode(new CirBoxNode(6, 20, 10), Transform3D.CreateTranslation(new Vector3D(7, 0, 5))));

        var result = CirBrepMaterializer.TryMaterialize(new CirBrepMaterializer.CirBrepMaterializerContext(root, BuildReplay("box")));

        Assert.True(result.IsSuccess);
        Assert.Equal("subtract_box_box", result.SelectedStrategy);
        Assert.Equal(CirBrepMaterializer.BoxMinusBoxPattern, result.PatternName);
    }

    [Fact]
    public void MaterializerRegistry_RejectionDiagnostics_ListRejectedStrategies()
    {
        var result = CirBrepMaterializer.TryMaterialize(new CirBrepMaterializer.CirBrepMaterializerContext(new CirUnionNode(new CirBoxNode(1, 1, 1), new CirBoxNode(1, 1, 1)), null));

        Assert.False(result.IsSuccess);
        Assert.Equal("no-strategy-matched", result.UnsupportedReason);
        Assert.Contains(result.StrategyRejections, r => r.CandidateName == "subtract_box_cylinder");
        Assert.Contains(result.StrategyRejections, r => r.CandidateName == "subtract_box_box");
    }

    [Fact]
    public void ReplayGuided_Mismatch_IsDiagnosed()
    {
        var root = new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirBoxNode(6, 20, 10));
        var mismatchReplay = BuildReplay("cylinder");

        var result = CirBrepMaterializer.TryMaterialize(new CirBrepMaterializer.CirBrepMaterializerContext(root, mismatchReplay));

        Assert.False(result.IsSuccess);
        Assert.Contains("Replay/CIR mismatch", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CirMaterializer_BoxMinusTorus_FailsWithPreciseUnsupported()
    {
        var root = new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTorusNode(8, 2));
        var replay = BuildReplay("torus");
        var result = CirBrepMaterializer.TryMaterialize(new CirBrepMaterializer.CirBrepMaterializerContext(root, replay));
        Assert.False(result.IsSuccess);
        Assert.Equal("subtract_box_torus", result.SelectedStrategy);
        Assert.Equal("materialization-unsupported", result.UnsupportedReason);
        Assert.Contains("recognized", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CirOnly_BoxMinusCylinder_RematerializesToBRepActive()
    {
        var plan = BuildBoxMinusCylinderPlan();
        var state = new NativeGeometryState(NativeGeometryExecutionMode.CirOnly, NativeGeometryMaterializationAuthority.PendingRematerialization, null, "hole", BuildReplay("cylinder"), [], new NativeGeometryCirMirrorState(CirMirrorStatus.NotAttempted, null, []));

        var remat = NativeGeometryRematerializer.TryRematerialize(plan, state);

        Assert.True(remat.IsSuccess, string.Join(" | ", remat.Diagnostics.Select(d => d.Message)));
        Assert.Equal(NativeGeometryExecutionMode.BRepActive, remat.Value.ExecutionMode);
    }

    [Fact]
    public void CirOnly_BoxMinusBox_RematerializesToBRepActive()
    {
        var plan = BuildBoxMinusBoxPlan();
        var state = new NativeGeometryState(NativeGeometryExecutionMode.CirOnly, NativeGeometryMaterializationAuthority.PendingRematerialization, null, "notch", BuildReplay("box"), [], new NativeGeometryCirMirrorState(CirMirrorStatus.NotAttempted, null, []));

        var remat = NativeGeometryRematerializer.TryRematerialize(plan, state);

        Assert.True(remat.IsSuccess, string.Join(" | ", remat.Diagnostics.Select(d => d.Message)));
        Assert.Equal(NativeGeometryExecutionMode.BRepActive, remat.Value.ExecutionMode);
    }

    private static NativeGeometryReplayLog BuildReplay(string toolKind)
        => new([
            new NativeGeometryReplayOperation(0, "base", "primitive:box", null, null, null, null, new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.None, null, null, Vector3D.Zero, Vector3D.Zero, true, null), null),
            new NativeGeometryReplayOperation(1, "cut", "boolean:subtract", "base", toolKind, "tool", null, new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.None, null, null, Vector3D.Zero, Vector3D.Zero, true, null), null)
        ]);

    private static FirmamentPrimitiveLoweringPlan BuildBoxMinusCylinderPlan()
    {
        var primitives = new[] { new FirmamentLoweredPrimitive(0, "base", FirmamentLoweredPrimitiveKind.Box, new FirmamentLoweredBoxParameters(20,20,10), null) };
        var booleans = new[] { new FirmamentLoweredBoolean(1, "hole", FirmamentLoweredBooleanKind.Subtract, "from", "base", new FirmamentLoweredToolOp("cylinder", new Dictionary<string,string>{{"op","cylinder"},{"radius","3"},{"height","20"}}, "op cylinder radius=3 height=20"), null) };
        return new FirmamentPrimitiveLoweringPlan(primitives, booleans, []);
    }

    private static FirmamentPrimitiveLoweringPlan BuildBoxMinusBoxPlan()
    {
        var primitives = new[] { new FirmamentLoweredPrimitive(0, "base", FirmamentLoweredPrimitiveKind.Box, new FirmamentLoweredBoxParameters(20,20,10), null) };
        var booleans = new[] { new FirmamentLoweredBoolean(1, "notch", FirmamentLoweredBooleanKind.Subtract, "from", "base", new FirmamentLoweredToolOp("box", new Dictionary<string,string>{{"op","box"},{"size","[6,20,10]"}}, "op box size=[6,20,10]"), new FirmamentLoweredPlacement(new FirmamentLoweredPlacementOriginAnchor(), true, new double[] { 7, 0, 0 }, null, null, null, [])) };
        return new FirmamentPrimitiveLoweringPlan(primitives, booleans, []);
    }
}
